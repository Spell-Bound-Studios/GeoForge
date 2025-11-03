// Copyright 2025 Spellbound Studio Inc.

using System;
using Spellbound.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Recursively Subdividing OctreeNode to subdivide a chunk at varying LODs.
    /// Either it has 8 children, or it has an Octree leaf (representing actual terrain).
    /// </summary>
    public class OctreeNode : IDisposable {
        private OctreeNode[] _children;
        private GameObject _leafGo;
        private GameObject _transitionGo;
        private Mesh _mesh;
        private Mesh _transitionMesh;
        private int _transitionMask;
        private bool _transitionDirtyFlag;
        private NativeList<int> _allTransitionTriangles;
        private NativeList<int> _filteredTransitionTriangles;
        private NativeArray<int2> _transitionRanges;
        private Vector3Int _localPosition;
        private readonly int _lod;
        private Bounds _bounds;
        private readonly IVoxelTerrainChunk _chunk;
        private readonly MarchingCubesManager _mcManager;

        private bool IsLeaf => _children == null;
        private NativeArray<VoxelData> VoxelData => _chunk.GetVoxelData();

        private Vector3Int WorldPosition => _chunk.GetChunkCoord() * _mcManager.McConfigBlob.Value.ChunkSize;

        public OctreeNode(Vector3Int localPosition, int lod, IVoxelTerrainChunk chunk) {
            _localPosition = localPosition;
            _lod = lod;
            _chunk = chunk;
            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();

            var octreeSize = _mcManager.McConfigBlob.Value.CubesMarchedPerOctreeLeaf * math.pow(2, _lod) + 2;

            _bounds = new Bounds(WorldPosition + _localPosition + Vector3.one * octreeSize / 2,
                Vector3.one * octreeSize);
        }

        public void Dispose() {
            if (_children != null) {
                for (var i = 0; i < 8; i++) {
                    _children[i].Dispose();
                    _children[i] = null;
                }

                _children = null;
            }

            if (_leafGo != null) {
                if (_transitionGo.TryGetComponent<MeshCollider>(out var transitionMeshCollider))
                    transitionMeshCollider.sharedMesh = null;
                _mcManager.ReleasePooledObject(_transitionGo);
                _transitionGo = null;
                if (_leafGo.TryGetComponent<MeshCollider>(out var meshCollider)) meshCollider.sharedMesh = null;
                _mcManager.ReleasePooledObject(_leafGo);
                _leafGo = null;
            }

            if (_allTransitionTriangles.IsCreated)
                _allTransitionTriangles.Dispose();

            if (_filteredTransitionTriangles.IsCreated)
                _filteredTransitionTriangles.Dispose();

            if (_transitionRanges.IsCreated)
                _transitionRanges.Dispose();

            _mcManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
        }

        private void Subdivide() {
            if (_children != null)
                return;

            if (_leafGo != null) {
                Object.Destroy(_leafGo);
                _leafGo = null;
            }

            _children = new OctreeNode[8];
            var childLod = _lod - 1;
            var childSize = _mcManager.McConfigBlob.Value.CubesMarchedPerOctreeLeaf << childLod;

            for (var i = 0; i < 8; i++) {
                var offset = new Vector3Int(
                    (i & 1) == 0 ? 0 : childSize,
                    (i & 2) == 0 ? 0 : childSize,
                    (i & 4) == 0 ? 0 : childSize
                );

                _children[i] = new OctreeNode(_localPosition + offset, childLod, _chunk);
            }
        }

        public void ValidateOctreeLods(Vector3 playerPosition) {
            var octreePos = WorldPosition
                            + _localPosition
                            + Vector3.one * (_mcManager.McConfigBlob.Value.CubesMarchedPerOctreeLeaf << (_lod - 1));
            var targetLod = GetLodRange(octreePos, playerPosition);

            if (_chunk.GetDensityRange().IsSkippable())
                return;

            if (targetLod == -1) {
                _leafGo?.SetActive(false);

                return;
            }

            if (_lod <= targetLod) {
                if (_leafGo == null) MakeLeaf();
                _leafGo?.SetActive(true);

                return;
            }

            if (_lod > targetLod)
                Subdivide();

            foreach (var child in _children)
                child.ValidateOctreeLods(playerPosition);
        }

        public void ValidateOctreeEdits(Bounds bounds) {
            if (!bounds.Intersects(_bounds)) return;

            if (IsLeaf) {
                UpdateLeaf();

                return;
            }

            foreach (var child in _children)
                child.ValidateOctreeEdits(bounds);
        }

        public void ValidateTransition(
            OctreeNode neighbor, Vector3 facePos, McStaticHelper.TransitionFaceMask faceMask) {
            if (!_bounds.Contains(facePos))
                return;

            if (!IsLeaf) {
                foreach (var child in _children)
                    child.ValidateTransition(neighbor, facePos, faceMask);

                return;
            }

            if (_lod > neighbor._lod) {
                UpdateTransitionMask(GetOppositeTransition(faceMask), true);
                neighbor.UpdateTransitionMask(faceMask, false);

                return;
            }

            if (_lod == neighbor._lod) {
                UpdateTransitionMask(GetOppositeTransition(faceMask), false);
                neighbor.UpdateTransitionMask(faceMask, false);

                return;
            }

            UpdateTransitionMask(GetOppositeTransition(faceMask), false);
            neighbor.UpdateTransitionMask(faceMask, true);
        }

        private int GetLodRange(Vector3 octreePos, Vector3 playerPos) {
            var distance = Vector3.Distance(octreePos, playerPos);
            var targetLod = McStaticHelper.GetLod(distance, _mcManager.McConfigBlob.Value.LodRanges.ToArray());

            return targetLod;
        }

        private void MarchAndMesh() {
            var marchingCubeJob = new MarchingCubeJob {
                TablesBlob = _mcManager.McTablesBlob,
                ConfigBlob = _mcManager.McConfigBlob,
                VoxelArray = VoxelData,

                // New Allocation - Ensure this is disposed of after the job.
                Vertices = new NativeList<MeshingVertexData>(Allocator.Persistent),
                // New Allocation - Ensure this is disposed of after the job.
                Triangles = new NativeList<int>(Allocator.Persistent),
                Lod = _lod,
                Start = new int3(_localPosition.x, _localPosition.y, _localPosition.z)
            };
            var jobHandle = marchingCubeJob.Schedule();
            _mcManager.RegisterMarchJob(this, jobHandle, marchingCubeJob.Vertices, marchingCubeJob.Triangles);

            if (_lod != 0) {
                var transitionMarchingCubeJob = new TransitionMarchingCubeJob {
                    TablesBlob = _mcManager.McTablesBlob,
                    ConfigBlob = _mcManager.McConfigBlob,
                    VoxelArray = VoxelData,

                    // New Allocation - Ensure this is disposed of after the job.
                    TransitionMeshingVertexData = new NativeList<MeshingVertexData>(Allocator.Persistent),

                    TransitionTriangles = new NativeList<int>(Allocator.Persistent),
                    TransitionRanges = new NativeArray<int2>(6, Allocator.Persistent),

                    Lod = _lod,
                    Start = new int3(_localPosition.x, _localPosition.y, _localPosition.z)
                };

                var transitionJobHandle = transitionMarchingCubeJob.Schedule();

                _mcManager.RegisterTransitionJob(this,
                    transitionJobHandle,
                    transitionMarchingCubeJob.TransitionMeshingVertexData,
                    transitionMarchingCubeJob.TransitionTriangles,
                    transitionMarchingCubeJob.TransitionRanges);
            }
        }

        private void MakeLeaf() {
            if (_leafGo != null) return;

            if (!IsLeaf) {
                for (var i = 0; i < 8; i++) _children[i]?.Dispose();
                _children = null;
            }

            BuildLeaf();
            BuildTransitions();
            MarchAndMesh();
            BroadcastNewLeaf();
        }

        private void BuildLeaf() {
            _leafGo = _mcManager.GetPooledObject(_chunk.GetChunkTransform());
            _leafGo.transform.position = WorldPosition;

            _mesh = new Mesh();
            _leafGo.GetComponent<MeshFilter>().mesh = _mesh;

            _leafGo.name = $"LeafSize {_mcManager.McConfigBlob.Value.CubesMarchedPerOctreeLeaf << _lod} " +
                           $"at {_localPosition.x}, {_localPosition.y}, {_localPosition.z}";
        }

        private void UpdateLeaf() {
            if (_leafGo == null) return;

            MarchAndMesh();
        }

        private void UpdateLeafMesh(NativeList<MeshingVertexData> vertices, NativeList<int> triangles) {
            _mesh.SetVertexBufferParams(vertices.Length, MeshingVertexData.VertexBufferMemoryLayout);

            _mesh.SetVertexBufferData(
                vertices.AsArray(),
                0,
                0,
                vertices.Length,
                0,
                MeshUpdateFlags.DontValidateIndices
            );

            _mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);

            _mesh.SetIndexBufferData(
                triangles.AsArray(),
                0,
                0,
                triangles.Length,
                MeshUpdateFlags.DontValidateIndices
            );

            var subMesh = new SubMeshDescriptor(0, triangles.Length);
            _mesh.subMeshCount = 1;

            _mesh.SetSubMesh(0, subMesh);
            _mesh.RecalculateBounds();

            if (triangles.Length < 3 || vertices.Length < 3)
                return;

            if (_mcManager.UseColliders && _leafGo.TryGetComponent<MeshCollider>(out var meshCollider))
                meshCollider.sharedMesh = _mesh;
        }

        private void BuildTransitions() {
            _transitionGo = _mcManager.GetPooledObject(_leafGo.transform);
            _transitionGo.transform.position = WorldPosition;

            _transitionMesh = new Mesh();
            _transitionGo.GetComponent<MeshFilter>().mesh = _transitionMesh;
            //_transitionGo.GetComponent<MeshCollider>().sharedMesh = _transitionMesh;

            _transitionGo.name = $"Transition " +
                                 $"at {_localPosition.x}, {_localPosition.y}, {_localPosition.z}";
            _transitionGo.transform.parent = _leafGo.transform;

            _transitionMask = 0;

            if (!_allTransitionTriangles.IsCreated) _allTransitionTriangles = new NativeList<int>(Allocator.Persistent);

            if (!_filteredTransitionTriangles.IsCreated)
                _filteredTransitionTriangles = new NativeList<int>(Allocator.Persistent);

            if (!_transitionRanges.IsCreated) _transitionRanges = new NativeArray<int2>(6, Allocator.Persistent);
        }

        private void UpdateTransitionVertexBuffer(NativeList<MeshingVertexData> vertices) {
            if (!vertices.IsCreated)
                return;

            _transitionMesh.SetVertexBufferParams(vertices.Length, MeshingVertexData.VertexBufferMemoryLayout);

            _transitionMesh.SetVertexBufferData(
                vertices.AsArray(),
                0, 0, vertices.Length, 0,
                MeshUpdateFlags.DontValidateIndices
            );
        }

        private void UpdateTransitionMask(McStaticHelper.TransitionFaceMask mask, bool isSetter) {
            var newTransitionMask = _transitionMask;

            if (isSetter)
                newTransitionMask |= (int)mask;
            else
                newTransitionMask &= ~(int)mask;

            if (_transitionMask == newTransitionMask)
                return;

            _transitionMask = newTransitionMask;

            if (_transitionDirtyFlag) return;

            _transitionDirtyFlag = true;
            _mcManager.OctreeBatchTransitionUpdate += HandleTransitionUpdate;
        }

        private void HandleTransitionUpdate() {
            if (_transitionDirtyFlag) {
                _mcManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
                _transitionDirtyFlag = false;
            }

            if (!_transitionRanges.IsCreated)
                return;

            var triangles =
                    GetFilteredTransitionTriangles(_allTransitionTriangles, _transitionRanges, _transitionMask);

            _transitionMesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);

            _transitionMesh.SetIndexBufferData(
                triangles.AsArray(),
                0,
                0,
                triangles.Length,
                MeshUpdateFlags.DontValidateIndices
            );

            var subMesh = new SubMeshDescriptor(0, triangles.Length);
            _transitionMesh.subMeshCount = 1;

            _transitionMesh.SetSubMesh(0, subMesh);
            _transitionMesh.RecalculateBounds();
        }

        private NativeList<int> GetFilteredTransitionTriangles(
            NativeList<int> allTriangles, NativeArray<int2> triangleRanges,
            int transitionMask) {
            _filteredTransitionTriangles.Clear();

            for (var i = 0; i < 6; i++) {
                if ((transitionMask & (1 << i)) == 0) continue;

                var range = triangleRanges[i];

                if (range.x < 0 || range.y > allTriangles.Length || range.x > range.y) continue;

                for (var j = range.x; j < range.y; j++) _filteredTransitionTriangles.Add(allTriangles[j]);
            }

            return _filteredTransitionTriangles;
        }

        private void BroadcastNewLeaf() {
            var neighborPositions = GetFaceCenters();
            for (var i = 0; i < 6; i++) _chunk.BroadcastNewLeafAcrossChunks(this, neighborPositions[i], i);
        }

        private Vector3[] GetFaceCenters() =>
                new[] {
                    new Vector3(_bounds.min.x - 1, _bounds.center.y, _bounds.center.z),
                    new Vector3(_bounds.center.x, _bounds.min.y - 1, _bounds.center.z),
                    new Vector3(_bounds.center.x, _bounds.center.y, _bounds.min.z - 1),
                    new Vector3(_bounds.max.x + 1, _bounds.center.y, _bounds.center.z),
                    new Vector3(_bounds.center.x, _bounds.max.y + 1, _bounds.center.z),
                    new Vector3(_bounds.center.x, _bounds.center.y, _bounds.max.z + 1)
                };

        private McStaticHelper.TransitionFaceMask GetOppositeTransition(
            McStaticHelper.TransitionFaceMask transitionMask) =>
                transitionMask switch {
                    McStaticHelper.TransitionFaceMask.XMin => McStaticHelper.TransitionFaceMask.XMax,
                    McStaticHelper.TransitionFaceMask.YMin => McStaticHelper.TransitionFaceMask.YMax,
                    McStaticHelper.TransitionFaceMask.ZMin => McStaticHelper.TransitionFaceMask.ZMax,
                    McStaticHelper.TransitionFaceMask.XMax => McStaticHelper.TransitionFaceMask.XMin,
                    McStaticHelper.TransitionFaceMask.YMax => McStaticHelper.TransitionFaceMask.YMin,
                    McStaticHelper.TransitionFaceMask.ZMax => McStaticHelper.TransitionFaceMask.ZMin,
                    _ => McStaticHelper.TransitionFaceMask.XMin
                };

        public void ApplyMarchResults(NativeList<MeshingVertexData> vertices, NativeList<int> triangles) {
            UpdateLeafMesh(vertices, triangles);

            if (_lod != 0)
                HandleTransitionUpdate();
        }

        public void ApplyTransitionMarchResults(
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeArray<int2> triangleRanges) {
            _allTransitionTriangles.CopyFrom(triangles);
            _transitionRanges.CopyFrom(triangleRanges);
            UpdateTransitionVertexBuffer(vertices);
        }
    }
}
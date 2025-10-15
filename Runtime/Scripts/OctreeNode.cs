// Copyright 2025 Spellbound Studio Inc.

using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Helper = SpellBound.Core.SpellBoundStaticHelper;
using Object = UnityEngine.Object;
using McHelper = SpellBound.MarchingCubes.McStaticHelper;

namespace SpellBound.MarchingCubes {
    /// <summary>
    /// Recursively Subdividing OctreeNode to subdivide a chunk at varying LODs.
    /// Either it has 8 children, or it has an Octree leaf (representing actual terrain).
    /// </summary>
    public class OctreeNode : IDisposable {
        private OctreeNode[] _children = null;
        public bool IsLeaf => _children == null;
        private GameObject _leafGO = null;
        private GameObject _transitionGO;
        private int _activeTransitionMask;
        private NativeList<MeshingVertexData> _transitionVertices;
        private NativeList<int> _transitionTriangles;
        private NativeArray<int2> _transitionRanges;
        private Vector3Int _localPosition;
        private int _lod;
        private Bounds _bounds;
        private IVoxelTerrainChunk _chunk;
        private NativeArray<VoxelData> _voxelData => _chunk.GetVoxelArray();

        private Vector3Int _worldPosition => _chunk.GetChunkCoord() * Helper.ChunkSize;

        public OctreeNode(Vector3Int localPosition, int lod, IVoxelTerrainChunk chunk) {
            _localPosition = localPosition;
            _lod = lod;
            _chunk = chunk;

            var octreeSize = McHelper.CubesMarchedPerOctreeLeaf * math.pow(2, _lod) + 2;

            _bounds = new Bounds(_worldPosition + _localPosition + Vector3.one * octreeSize / 2,
                Vector3.one * octreeSize);
        }

        public void ValidateOctreeEdits(Bounds bounds) {
            if (!bounds.Intersects(_bounds)) return;

            if (IsLeaf) {
                UpdateLeaf();

                return;
            }

            for (var i = 0; i < _children.Length; ++i) _children[i].ValidateOctreeEdits(bounds);
        }

        private (int, int) GetLodRange(Vector3 octreePos, Vector3 playerPos) {
            var distance = Vector3.Distance(octreePos, playerPos);
            var coarsestLod = McHelper.GetCoarsestLod(distance, MarchingCubesManager.Instance.lodRanges);
            var finestLod = McHelper.GetFinestLod(distance, MarchingCubesManager.Instance.lodRanges);

            return (coarsestLod, finestLod);
        }

        public void ValidateOctreeLods(Vector3 playerPosition) {
            var octreePos = _worldPosition
                            + _localPosition
                            + Vector3.one * (McHelper.CubesMarchedPerOctreeLeaf << (_lod - 1));
            var (coarsestLod, finestLod) = GetLodRange(octreePos, playerPosition);

            if (_chunk.IsChunkAllOneSideOfThreshold()) return;

            if (_lod <= finestLod) {
                MakeLeaf();

                return;
            }

            if (_lod == coarsestLod && IsLeaf && _leafGO == null) {
                MakeLeaf();

                return;
            }

            if (_lod > coarsestLod) Subdivide();

            if (IsLeaf)
                return;

            for (var i = 0; i < _children.Length; ++i) _children[i].ValidateOctreeLods(playerPosition);
        }

        private void MarchAndUpdateLeaf() {
            var marchingCubeJob = new MarchingCubeJob {
                Tables = MarchingCubesManager.Instance.McTablesBlob,
                VoxelArray = _voxelData,

                // New Allocation - Ensure this is disposed of after the job.
                Vertices = new NativeList<MeshingVertexData>(Allocator.TempJob),
                // New Allocation - Ensure this is disposed of after the job.
                Triangles = new NativeList<int>(Allocator.TempJob),
                Lod = _lod,
                Start = new int3(_localPosition.x, _localPosition.y, _localPosition.z)
            };
            var jobHandle = marchingCubeJob.Schedule();
            jobHandle.Complete();
            UpdateLeaf(marchingCubeJob.Vertices, marchingCubeJob.Triangles);

            marchingCubeJob.Vertices.Dispose();
            marchingCubeJob.Triangles.Dispose();

            if (_lod != 0) {
                var transitionMarchingCubeJob = new TransitionMarchingCubeJob {
                    Tables = MarchingCubesManager.Instance.McTablesBlob,
                    VoxelArray = _voxelData,

                    // New Allocation - Ensure this is disposed of after the job.
                    TransitionMeshingVertexData = new NativeList<MeshingVertexData>(Allocator.TempJob),

                    TransitionTriangles = new NativeList<int>(Allocator.TempJob),
                    TransitionRanges = new NativeArray<int2>(6, Allocator.TempJob),

                    Lod = _lod,
                    Start = new int3(_localPosition.x, _localPosition.y, _localPosition.z)
                };

                var transitionJobHandle = transitionMarchingCubeJob.Schedule();
                transitionJobHandle.Complete();
                _activeTransitionMask = 0;
                _transitionVertices.CopyFrom(transitionMarchingCubeJob.TransitionMeshingVertexData);
                _transitionTriangles.CopyFrom(transitionMarchingCubeJob.TransitionTriangles);
                _transitionRanges.CopyFrom(transitionMarchingCubeJob.TransitionRanges);
                UpdateTransition(_activeTransitionMask);
                transitionMarchingCubeJob.TransitionMeshingVertexData.Dispose();
                transitionMarchingCubeJob.TransitionTriangles.Dispose();
                ;
                transitionMarchingCubeJob.TransitionRanges.Dispose();
                ;
            }
        }

        private void BroadcastNewLeaf() {
            var neighborPositions = GetFaceCenters();
            for (var i = 0; i < 6; i++) _chunk.BroadcastNewLeaf(this, neighborPositions[i], i);
        }

        public void ValidateTransition(OctreeNode neighbor, Vector3 facePos, McHelper.TransitionFaceMask faceMask) {
            if (!_bounds.Contains(facePos))
                return;

            if (!IsLeaf) {
                for (var i = 0; i < _children.Length; ++i) _children[i].ValidateTransition(neighbor, facePos, faceMask);

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

        public void UpdateTransitionMask(McHelper.TransitionFaceMask mask, bool isSetter) {
            var newTransitionMask = _activeTransitionMask;

            if (isSetter)
                newTransitionMask |= (int)mask;
            else
                newTransitionMask &= ~(int)mask;

            if (newTransitionMask == _activeTransitionMask)
                return;

            _activeTransitionMask = newTransitionMask;
            UpdateTransition(_activeTransitionMask);
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

        private McHelper.TransitionFaceMask GetOppositeTransition(McHelper.TransitionFaceMask transitionMask) =>
                transitionMask switch {
                    McHelper.TransitionFaceMask.XMin => McHelper.TransitionFaceMask.XMax,
                    McHelper.TransitionFaceMask.YMin => McHelper.TransitionFaceMask.YMax,
                    McHelper.TransitionFaceMask.ZMin => McHelper.TransitionFaceMask.ZMax,
                    McHelper.TransitionFaceMask.XMax => McHelper.TransitionFaceMask.XMin,
                    McHelper.TransitionFaceMask.YMax => McHelper.TransitionFaceMask.YMin,
                    McHelper.TransitionFaceMask.ZMax => McHelper.TransitionFaceMask.ZMin,
                    _ => McHelper.TransitionFaceMask.XMin
                };

        private void Subdivide() {
            if (!IsLeaf || _children != null)
                return;

            if (_leafGO != null) {
                GameObject.Destroy(_leafGO);
                _leafGO = null;
                GameObject.Destroy(_transitionGO);
                _transitionGO = null;
            }

            _children = new OctreeNode[8];
            var childLod = _lod - 1;
            var childSize = McHelper.CubesMarchedPerOctreeLeaf << childLod;

            for (var i = 0; i < 8; i++) {
                var offset = new Vector3Int(
                    (i & 1) == 0 ? 0 : childSize,
                    (i & 2) == 0 ? 0 : childSize,
                    (i & 4) == 0 ? 0 : childSize
                );

                _children[i] = new OctreeNode(_localPosition + offset, childLod, _chunk);
            }
        }

        private void MakeLeaf() {
            if (_leafGO != null) return;

            if (!IsLeaf) {
                for (var i = 0; i < 8; i++) _children[i]?.Dispose();
                _children = null;
            }

            BuildLeaf();
            BuildTransitions();
            MarchAndUpdateLeaf();
            BroadcastNewLeaf();
        }

        private void UpdateLeaf() {
            if (_leafGO == null) return;

            MarchAndUpdateLeaf();
        }

        public void Dispose() {
            if (_children != null) {
                for (var i = 0; i < 8; i++) _children[i].Dispose();
                _children = null;
            }

            if (_leafGO != null) {
                GameObject.Destroy(_leafGO);
                _leafGO = null;
                GameObject.Destroy(_transitionGO);
                _transitionGO = null;
            }

            if (_transitionVertices.IsCreated)
                _transitionVertices.Dispose();

            if (_transitionTriangles.IsCreated)
                _transitionTriangles.Dispose();

            if (_transitionRanges.IsCreated)
                _transitionRanges.Dispose();
        }

        private void UpdateLeaf(NativeList<MeshingVertexData> vertices, NativeList<int> triangles) {
            if (triangles.Length < 3 || vertices.Length < 3)
                return;

            var meshFilter = _leafGO.GetComponent<MeshFilter>();
            var meshCollider = _leafGO.GetComponent<MeshCollider>();

            var mesh = new Mesh();

            mesh.SetVertexBufferParams(vertices.Length, MeshingVertexData.VertexBufferMemoryLayout);

            mesh.SetVertexBufferData(
                vertices.AsArray(),
                0,
                0,
                vertices.Length,
                0,
                MeshUpdateFlags.DontValidateIndices
            );

            mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);

            mesh.SetIndexBufferData(
                triangles.AsArray(),
                0,
                0,
                triangles.Length,
                MeshUpdateFlags.DontValidateIndices
            );

            var subMesh = new SubMeshDescriptor(0, triangles.Length);
            mesh.subMeshCount = 1;

            mesh.SetSubMesh(0, subMesh);
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;
        }

        private void BuildLeaf() {
            _leafGO = Object.Instantiate(
                MarchingCubesManager.Instance.octreePrefab,
                _worldPosition,
                Quaternion.identity,
                _chunk.GetChunkTransform()
            );

            _leafGO.name = $"LeafSize {McHelper.CubesMarchedPerOctreeLeaf << _lod} " +
                           $"at {_localPosition.x}, {_localPosition.y}, {_localPosition.z}";

            if (!_transitionVertices.IsCreated)
                _transitionVertices = new NativeList<MeshingVertexData>(Allocator.Persistent);

            if (!_transitionTriangles.IsCreated) _transitionTriangles = new NativeList<int>(Allocator.Persistent);

            if (!_transitionRanges.IsCreated) _transitionRanges = new NativeArray<int2>(6, Allocator.Persistent);
        }

        private void BuildTransitions() {
            _transitionGO = Object.Instantiate(
                MarchingCubesManager.Instance.octreePrefab,
                _worldPosition,
                Quaternion.identity,
                _chunk.GetChunkTransform()
            );

            _transitionGO.name = "transition";
            _transitionGO.transform.parent = _leafGO.transform;
        }

        private void UpdateTransition(int transitionMask) {
            if (!_transitionVertices.IsCreated)
                return;

            var meshFilter = _transitionGO.GetComponent<MeshFilter>();

            if (transitionMask == 0) {
                // Clear the mesh to hide transitions

                meshFilter.mesh = null; // clear mesh
            }

            var triangles =
                    GetFilteredTransitionTriangles(_transitionTriangles, _transitionRanges, transitionMask);

            if (triangles.Length < 3 || _transitionVertices.Length < 3)
                return;

            meshFilter = _transitionGO.GetComponent<MeshFilter>();
            //var meshCollider = _transitionGO.GetComponent<MeshCollider>();

            var mesh = new Mesh();

            mesh.SetVertexBufferParams(_transitionVertices.Length, MeshingVertexData.VertexBufferMemoryLayout);

            mesh.SetVertexBufferData(
                _transitionVertices.AsArray(),
                0,
                0,
                _transitionVertices.Length,
                0,
                MeshUpdateFlags.DontValidateIndices
            );

            mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);

            mesh.SetIndexBufferData(
                triangles.AsArray(),
                0,
                0,
                triangles.Length,
                MeshUpdateFlags.DontValidateIndices
            );

            var subMesh = new SubMeshDescriptor(0, triangles.Length);
            mesh.subMeshCount = 1;

            mesh.SetSubMesh(0, subMesh);
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
            //meshCollider.sharedMesh = mesh;
        }

        private NativeList<int> GetFilteredTransitionTriangles(
            NativeList<int> allTriangles, NativeArray<int2> triangleRanges,
            int transitionMask) {
            var filteredTriangles = new NativeList<int>(Allocator.Temp);

            for (var i = 0; i < 6; i++) {
                if ((transitionMask & (1 << i)) == 0) continue;

                var range = triangleRanges[i];

                if (range.x < 0 || range.y > allTriangles.Length || range.x > range.y) continue;

                for (var j = range.x; j < range.y; j++) filteredTriangles.Add(allTriangles[j]);
            }

            return filteredTriangles;
        }
    }
}
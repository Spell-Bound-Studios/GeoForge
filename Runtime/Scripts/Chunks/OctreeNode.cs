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
        private BoundsInt _boundsVoxel;
        private readonly IChunk _chunk;
        private readonly MarchingCubesManager _mcManager;
        private readonly IVolume _parentVolume;
        private Vector3Int[] _cachedNeighborPositions;
        private MaterialPropertyBlock _materialPropertyBlock;

        private Vector3 Center => (_boundsVoxel.min + _boundsVoxel.max - Vector3.one) * 0.5f;

        private bool IsLeaf => _children == null;

        public OctreeNode(Vector3Int localPosition, int lod, IChunk chunk, IVolume parentVolume) {
            _parentVolume = parentVolume;
            _localPosition = localPosition;
            _lod = lod;
            _chunk = chunk;

            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
            var octreeSizeVoxels = 3 + (_parentVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << _lod);
            _boundsVoxel = new BoundsInt(_localPosition, Vector3Int.one * octreeSizeVoxels);
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
            var childSize = _parentVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << childLod;

            for (var i = 0; i < 8; i++) {
                var offset = new Vector3Int(
                    (i & 1) == 0 ? 0 : childSize,
                    (i & 2) == 0 ? 0 : childSize,
                    (i & 4) == 0 ? 0 : childSize
                );

                _children[i] = new OctreeNode(_localPosition + offset, childLod, _chunk, _parentVolume);
            }
        }

        public void ValidateMaterial() {
            if (_leafGo != null) {
                SetMaterialOrigin();

                return;
            }

            if (_children == null) return;

            foreach (var child in _children) child.ValidateMaterial();
        }

        private void SetMaterialOrigin() {
            var meshRenderer = _leafGo.GetComponent<MeshRenderer>();
            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetMatrix("_WorldToLocal", _parentVolume.VolumeTransform.worldToLocalMatrix);
            meshRenderer.SetPropertyBlock(materialPropertyBlock);

            if (_transitionGo != null) {
                meshRenderer = _transitionGo.GetComponent<MeshRenderer>();
                meshRenderer.SetPropertyBlock(materialPropertyBlock);
            }
        }

        public void ValidateOctreeLods(Vector3 playerPosition, NativeArray<VoxelData> voxelArray) {
            var targetLod = GetLodRange(Center, playerPosition, _parentVolume.ConfigBlob.Value.Resolution);

            if (_chunk.DensityRange.IsSkippable()) return;

            if (_lod <= targetLod) {
                if (_leafGo == null)
                    MakeLeaf(voxelArray);

                _leafGo?.SetActive(true);

                return;
            }

            if (_lod > targetLod)
                Subdivide();

            foreach (var child in _children)
                child.ValidateOctreeLods(playerPosition, voxelArray);
        }

        public void ValidateOctreeEdits(BoundsInt boundsVoxel, NativeArray<VoxelData> voxelArray) {
            if (!BoundsIntersect(_boundsVoxel, boundsVoxel)) return;

            if (IsLeaf) {
                UpdateLeaf(voxelArray);

                return;
            }

            foreach (var child in _children)
                child.ValidateOctreeEdits(boundsVoxel, voxelArray);
        }

        private bool BoundsIntersect(BoundsInt a, BoundsInt b) {
            if (a.max.x < b.min.x || a.min.x > b.max.x) return false;
            if (a.max.y < b.min.y || a.min.y > b.max.y) return false;
            if (a.max.z < b.min.z || a.min.z > b.max.z) return false;

            return true;
        }

        public void ValidateTransition(
            OctreeNode neighbor, Vector3Int voxelPos, McStaticHelper.TransitionFaceMask faceMask) {
            if (!_boundsVoxel.Contains(voxelPos))
                return;

            if (!IsLeaf) {
                foreach (var child in _children)
                    child.ValidateTransition(neighbor, voxelPos, faceMask);

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

        private int GetLodRange(Vector3 octreePos, Vector3 playerPos, float resolution) {
            var distance = Vector3.Distance(octreePos, playerPos) * resolution;

            for (var i = 0; i < _parentVolume.ViewDistanceLodRanges.Length; i++) {
                if (distance <= _parentVolume.ViewDistanceLodRanges[i].y)
                    return i;
            }

            // If distance is beyond all ranges, return -1
            // return - 1;
            return _parentVolume.ViewDistanceLodRanges.Length - 1;
        }

        private void MarchAndMesh(NativeArray<VoxelData> voxelArray) {
            var marchingCubeJob = new MarchingCubeJob {
                TablesBlob = _mcManager.McTablesBlob,
                ConfigBlob = _parentVolume.ConfigBlob,
                VoxelArray = voxelArray,

                Vertices = new NativeList<MeshingVertexData>(Allocator.Persistent),
                Triangles = new NativeList<int>(Allocator.Persistent),
                Lod = _lod,
                Start = new int3(_localPosition.x, _localPosition.y, _localPosition.z)
            };
            var jobHandle = marchingCubeJob.Schedule();

            _mcManager.RegisterMarchJob(this, jobHandle, marchingCubeJob.Vertices, marchingCubeJob.Triangles,
                _chunk.ChunkCoord);

            if (_lod != 0) {
                var transitionMarchingCubeJob = new TransitionMarchingCubeJob {
                    TablesBlob = _mcManager.McTablesBlob,
                    ConfigBlob = _parentVolume.ConfigBlob,
                    VoxelArray = voxelArray,

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
                    transitionMarchingCubeJob.TransitionRanges,
                    _chunk.ChunkCoord);
            }
        }

        private void MakeLeaf(NativeArray<VoxelData> voxelArray) {
            if (!IsLeaf) {
                for (var i = 0; i < 8; i++) _children[i]?.Dispose();
                _children = null;
            }

            BuildLeaf();
            BuildTransitions();
            SetMaterialOrigin();
            MarchAndMesh(voxelArray);
            BroadcastNewLeaf();
        }

        private void BuildLeaf() {
            _leafGo = _mcManager.GetPooledObject(_chunk.Transform);
            _leafGo.transform.localPosition = Vector3.zero;
            _leafGo.transform.localRotation = Quaternion.identity;

            _mesh = new Mesh();
            _mesh.MarkDynamic();
            _leafGo.GetComponent<MeshFilter>().mesh = _mesh;

            _leafGo.name = $"LeafSize {_parentVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << _lod} " +
                           $"at {_localPosition.x}, {_localPosition.y}, {_localPosition.z}";
        }

        private void UpdateLeaf(NativeArray<VoxelData> voxelArray) {
            if (_leafGo == null) return;

            MarchAndMesh(voxelArray);
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
            _transitionGo.transform.localPosition = Vector3.zero;
            _transitionGo.transform.localRotation = Quaternion.identity;

            _transitionMesh = new Mesh();
            _mesh.MarkDynamic();
            _transitionGo.GetComponent<MeshFilter>().mesh = _transitionMesh;

            _transitionGo.name = $"Transition " +
                                 $"at {_localPosition.x}, {_localPosition.y}, {_localPosition.z}";
            _transitionGo.transform.parent = _leafGo.transform;

            _transitionMask = 0;

            if (!_allTransitionTriangles.IsCreated)
                _allTransitionTriangles = new NativeList<int>(Allocator.Persistent);

            if (!_filteredTransitionTriangles.IsCreated)
                _filteredTransitionTriangles = new NativeList<int>(Allocator.Persistent);

            if (!_transitionRanges.IsCreated)
                _transitionRanges = new NativeArray<int2>(6, Allocator.Persistent);
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
            var neighborVoxelPositions = GetNeighborPositions();

            for (var i = 0; i < 6; i++)
                _chunk.BroadcastNewLeafAcrossChunks(this, neighborVoxelPositions[i], i);
        }

        private Vector3Int[] GetNeighborPositions() {
            if (_cachedNeighborPositions == null) _cachedNeighborPositions = SetNeighborPositions(_boundsVoxel);

            return _cachedNeighborPositions;
        }

        private Vector3Int[] SetNeighborPositions(BoundsInt boundsVoxel) {
            var center = (boundsVoxel.min + boundsVoxel.max) / 2;

            return new[] {
                new Vector3Int(boundsVoxel.min.x - 1, center.y, center.z), // XMin face (outside left)
                new Vector3Int(center.x, boundsVoxel.min.y - 1, center.z), // YMin face (outside bottom)
                new Vector3Int(center.x, center.y, boundsVoxel.min.z - 1), // ZMin face (outside back)
                new Vector3Int(boundsVoxel.max.x + 1, center.y, center.z), // XMax face (at boundary right)
                new Vector3Int(center.x, boundsVoxel.max.y + 1, center.z), // YMax face (at boundary top)
                new Vector3Int(center.x, center.y, boundsVoxel.max.z + 1)  // ZMax face (at boundary front)
            };
        }

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
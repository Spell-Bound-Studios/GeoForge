// Copyright 2026 Spellbound Studio Inc.

using System;
using Spellbound.Core.Tooling;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Recursively Subdividing OctreeNode to subdivide a geoChunk at varying LODs.
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
        private readonly IGeoChunk _geoChunk;
        private readonly GeoForgeManager _gfManager;
        private readonly IGeoVolume _parentGeoVolume;
        private Vector3Int[] _cachedNeighborPositions;
        private MaterialPropertyBlock _materialPropertyBlock;

        private Vector3 Center => (_boundsVoxel.min + _boundsVoxel.max - Vector3.one) * 0.5f;

        private bool IsLeaf => _children == null;

        public OctreeNode(Vector3Int localPosition, int lod, IGeoChunk geoChunk, IGeoVolume parentGeoVolume) {
            _parentGeoVolume = parentGeoVolume;
            _localPosition = localPosition;
            _lod = lod;
            _geoChunk = geoChunk;

            _gfManager = SingletonManager.GetSingletonInstance<GeoForgeManager>();
            var octreeSizeVoxels = 3 + (_parentGeoVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << _lod);
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
                _gfManager.ReleasePooledObject(_transitionGo);
                _transitionGo = null;

                if (_transitionMesh != null) {
                    Object.Destroy(_transitionMesh);
                    _transitionMesh = null;
                }

                if (_leafGo.TryGetComponent<MeshCollider>(out var meshCollider)) meshCollider.sharedMesh = null;
                _gfManager.ReleasePooledObject(_leafGo);
                _leafGo = null;

                if (_mesh != null) {
                    Object.Destroy(_mesh);
                    _mesh = null;
                }
            }

            if (_allTransitionTriangles.IsCreated)
                _allTransitionTriangles.Dispose();

            if (_filteredTransitionTriangles.IsCreated)
                _filteredTransitionTriangles.Dispose();

            if (_transitionRanges.IsCreated)
                _transitionRanges.Dispose();

            _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
        }

        private void Subdivide() {
            if (_children != null)
                return;

            // Release both pooled objects back through the pool, mirroring Dispose() exactly -
            // Object.Destroy would permanently destroy them (the pool loses an object every LOD
            // cycle) and, since _transitionGo is parented under _leafGo, destroying _leafGo would
            // also destroy _transitionGo as a side effect while leaving the _transitionGo field
            // pointing at a dead object until the next BuildTransitions() call overwrites it.
            if (_leafGo != null) {
                if (_transitionGo.TryGetComponent<MeshCollider>(out var transitionMeshCollider))
                    transitionMeshCollider.sharedMesh = null;
                _gfManager.ReleasePooledObject(_transitionGo);
                _transitionGo = null;

                if (_transitionMesh != null) {
                    Object.Destroy(_transitionMesh);
                    _transitionMesh = null;
                }

                if (_leafGo.TryGetComponent<MeshCollider>(out var meshCollider)) meshCollider.sharedMesh = null;
                _gfManager.ReleasePooledObject(_leafGo);
                _leafGo = null;

                if (_mesh != null) {
                    Object.Destroy(_mesh);
                    _mesh = null;
                }

                // A transition update might still be queued for this node (subscribed via
                // UpdateTransitionMask) from just before it stopped being a leaf. Unsubscribe and
                // clear the dirty flag now - otherwise the next OctreeBatchTransitionUpdate
                // invocation would run HandleTransitionUpdate() against a now-null _transitionMesh
                // (or a released _transitionGo). _transitionRanges.IsCreated - the guard
                // HandleTransitionUpdate itself checks - doesn't catch this, since
                // _transitionRanges is only ever disposed in Dispose(), not here.
                _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
                _transitionDirtyFlag = false;
            }

            _children = new OctreeNode[8];
            var childLod = _lod - 1;
            var childSize = _parentGeoVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << childLod;

            for (var i = 0; i < 8; i++) {
                var offset = new Vector3Int(
                    (i & 1) == 0 ? 0 : childSize,
                    (i & 2) == 0 ? 0 : childSize,
                    (i & 4) == 0 ? 0 : childSize
                );

                _children[i] = new OctreeNode(_localPosition + offset, childLod, _geoChunk, _parentGeoVolume);
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
            materialPropertyBlock.SetMatrix("_WorldToLocal", _parentGeoVolume.VolumeTransform.worldToLocalMatrix);
            meshRenderer.SetPropertyBlock(materialPropertyBlock);

            if (_transitionGo != null) {
                meshRenderer = _transitionGo.GetComponent<MeshRenderer>();
                meshRenderer.SetPropertyBlock(materialPropertyBlock);
            }
        }

        public void ValidateOctreeLods(Vector3 playerPosition, NativeArray<VoxelData> voxelArray) {
            var targetLod = GetLodRange(Center, playerPosition, _parentGeoVolume.ConfigBlob.Value.Resolution);

            if (_geoChunk.DensityRange.IsSkippable()) return;

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
            OctreeNode neighbor, Vector3Int voxelPos, GfStaticHelper.TransitionFaceMask faceMask) {
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

            for (var i = 0; i < _parentGeoVolume.ViewDistanceLodRanges.Length; i++) {
                if (distance <= _parentGeoVolume.ViewDistanceLodRanges[i].y)
                    return i;
            }

            // If distance is beyond all ranges, return -1
            // return - 1;
            return _parentGeoVolume.ViewDistanceLodRanges.Length - 1;
        }

        private void MarchAndMesh(NativeArray<VoxelData> voxelArray) {
            var profile = _gfManager.jobAndRenderProfile;
            var start = new int3(_localPosition.x, _localPosition.y, _localPosition.z);

            var vertices = new NativeList<MeshingVertexData>(Allocator.Persistent);
            var triangles = new NativeList<int>(Allocator.Persistent);

            var jobHandle = profile.ScheduleMarchingCubes(
                _gfManager.McTablesBlob,
                _parentGeoVolume.ConfigBlob,
                _gfManager.FlatShadedLookUp,
                voxelArray,
                vertices,
                triangles,
                _lod,
                start);

            _gfManager.RegisterMarchJob(this, jobHandle, vertices, triangles, _geoChunk.ChunkCoord);

            if (_lod != 0) {
                var transitionMeshingVertexData = new NativeList<MeshingVertexData>(Allocator.Persistent);
                var transitionTriangles = new NativeList<int>(Allocator.Persistent);
                var transitionRanges = new NativeArray<int2>(6, Allocator.Persistent);

                var transitionJobHandle = profile.ScheduleTransitionMarchingCubes(
                    _gfManager.McTablesBlob,
                    _parentGeoVolume.ConfigBlob,
                    voxelArray,
                    transitionMeshingVertexData,
                    transitionTriangles,
                    transitionRanges,
                    _lod,
                    start);

                _gfManager.RegisterTransitionJob(this,
                    transitionJobHandle,
                    transitionMeshingVertexData,
                    transitionTriangles,
                    transitionRanges,
                    _geoChunk.ChunkCoord);
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
            _leafGo = _gfManager.GetPooledObject(_geoChunk.GeoChunk.Transform);
            _leafGo.transform.localPosition = Vector3.zero;
            _leafGo.transform.localRotation = Quaternion.identity;

            if (_mesh != null)
                Object.Destroy(_mesh);

            _mesh = new Mesh();
            _mesh.MarkDynamic();
            _leafGo.GetComponent<MeshFilter>().mesh = _mesh;

            _leafGo.name = $"LeafSize {_parentGeoVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << _lod} " +
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

            if (!_leafGo.TryGetComponent<MeshCollider>(out var meshCollider))
                return;

            // If the leaf's geometry has emptied out (fully dug away), clear the collider instead
            // of leaving PhysX's stale bake data behind - MeshCollider only re-bakes when
            // sharedMesh is reassigned, so an early-return here without touching it (as before)
            // left the OLD, solid collision shape standing as an invisible wall after all visible
            // terrain was removed.
            meshCollider.sharedMesh = triangles.Length < 3 || vertices.Length < 3 ? null : _mesh;
        }

        private void BuildTransitions() {
            _transitionGo = _gfManager.GetPooledObject(_leafGo.transform);
            _transitionGo.transform.localPosition = Vector3.zero;
            _transitionGo.transform.localRotation = Quaternion.identity;

            if (_transitionMesh != null)
                Object.Destroy(_transitionMesh);

            _transitionMesh = new Mesh();
            _transitionMesh.MarkDynamic();
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

        private void UpdateTransitionMask(GfStaticHelper.TransitionFaceMask mask, bool isSetter) {
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
            _gfManager.OctreeBatchTransitionUpdate += HandleTransitionUpdate;
        }

        private void HandleTransitionUpdate() {
            if (_transitionDirtyFlag) {
                _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
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
                _geoChunk.BroadcastNewLeafAcrossChunks(this, neighborVoxelPositions[i], i);
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

        private GfStaticHelper.TransitionFaceMask GetOppositeTransition(
            GfStaticHelper.TransitionFaceMask transitionMask) =>
                transitionMask switch {
                    GfStaticHelper.TransitionFaceMask.XMin => GfStaticHelper.TransitionFaceMask.XMax,
                    GfStaticHelper.TransitionFaceMask.YMin => GfStaticHelper.TransitionFaceMask.YMax,
                    GfStaticHelper.TransitionFaceMask.ZMin => GfStaticHelper.TransitionFaceMask.ZMax,
                    GfStaticHelper.TransitionFaceMask.XMax => GfStaticHelper.TransitionFaceMask.XMin,
                    GfStaticHelper.TransitionFaceMask.YMax => GfStaticHelper.TransitionFaceMask.YMin,
                    GfStaticHelper.TransitionFaceMask.ZMax => GfStaticHelper.TransitionFaceMask.ZMin,
                    _ => GfStaticHelper.TransitionFaceMask.XMin
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
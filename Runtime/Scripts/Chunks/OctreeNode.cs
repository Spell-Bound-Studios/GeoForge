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
    public sealed class OctreeNode : IDisposable {
        private OctreeNode[] _children;
        private GameObject _leafGo;
        private GameObject _transitionGo;
        private Mesh _mesh;
        private Mesh _transitionMesh;
        private int _transitionMask;
        private bool _transitionDirtyFlag;

        // True once this node has been evaluated as a leaf (MakeLeaf has run at least once since
        // the last Subdivide) - independent of whether _leafGo actually exists. A leaf whose
        // geometry marches to zero triangles never gets a GameObject at all (see ApplyMarchResults),
        // but still needs to be recognized as "already handled" so ValidateOctreeLods doesn't
        // re-schedule a march job for it every single validation pass.
        private bool _leafInitialized;

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

            ReleaseLeafObjects();

            if (_allTransitionTriangles.IsCreated)
                _allTransitionTriangles.Dispose();

            if (_filteredTransitionTriangles.IsCreated)
                _filteredTransitionTriangles.Dispose();

            if (_transitionRanges.IsCreated)
                _transitionRanges.Dispose();

            // Unconditional: covers non-leaf/parent nodes too, where ReleaseLeafObjects above is a
            // no-op (it only unsubscribes when _leafGo != null). Safe to call twice - event -= is
            // always a no-op if not currently subscribed.
            _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
        }

        private void Subdivide() {
            if (_children != null)
                return;

            ReleaseLeafObjects();
            _leafInitialized = false;

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

        /// <summary>
        /// Releases the pooled leaf/transition GameObjects (if any exist) and destroys their
        /// Meshes, mirroring what used to be duplicated inline in both Dispose() and Subdivide().
        /// Safe to call when _leafGo is already null (no-op). Also unsubscribes from
        /// OctreeBatchTransitionUpdate and clears the dirty flag: a transition update might still
        /// be queued for this node from just before it lost its leaf objects (either via Subdivide
        /// or via ApplyMarchResults finding zero triangles) - otherwise the next batched invocation
        /// would run HandleTransitionUpdate() against a now-null _transitionMesh. _transitionRanges
        /// stays created deliberately (see class-level NativeCollections) - HandleTransitionUpdate's
        /// own IsCreated guard doesn't catch this case, which is exactly why this needs its own
        /// explicit unsubscribe rather than relying on that guard.
        /// </summary>
        private void ReleaseLeafObjects() {
            if (_leafGo == null)
                return;

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

            _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
            _transitionDirtyFlag = false;
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
            // Reuse the existing _materialPropertyBlock field instead of allocating a fresh
            // MaterialPropertyBlock every call - this runs once per leaf, every frame, for any
            // volume that's currently moving (via ValidateMaterial -> OnVolumeMovement), so a
            // per-call allocation here was real, avoidable GC pressure. Clear() resets it for
            // reuse rather than risking stale state leaking in from whatever it held last.
            _materialPropertyBlock ??= new MaterialPropertyBlock();
            _materialPropertyBlock.Clear();
            _materialPropertyBlock.SetMatrix("_WorldToLocal", _parentGeoVolume.VolumeTransform.worldToLocalMatrix);

            var meshRenderer = _leafGo.GetComponent<MeshRenderer>();
            meshRenderer.SetPropertyBlock(_materialPropertyBlock);

            if (_transitionGo != null) {
                meshRenderer = _transitionGo.GetComponent<MeshRenderer>();
                meshRenderer.SetPropertyBlock(_materialPropertyBlock);
            }
        }

        public void ValidateOctreeLods(Vector3 playerPosition, NativeArray<VoxelData> voxelArray) {
            var targetLod = GetLodRange(Center, playerPosition, _parentGeoVolume.ConfigBlob.Value.Resolution);

            if (_geoChunk.DensityRange.IsSkippable()) return;

            if (_lod <= targetLod) {
                if (!_leafInitialized)
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

            // Rented from GeoForgeManager's buffer pool instead of freshly allocated - avoids the
            // per-march Allocator.Persistent churn (up to 6 fresh NativeCollections per call
            // previously). Returned to the pool (not disposed) once results are applied - see
            // GeoForgeManager.CompleteAndApplyMarchingCubesJobs / ReturnMarchBuffers.
            var vertices = _gfManager.RentVertexBuffer();
            var triangles = _gfManager.RentTriangleBuffer();
            var computedBounds = _gfManager.RentBoundsBuffer();

            var jobHandle = profile.ScheduleMarchingCubes(
                _gfManager.McTablesBlob,
                _parentGeoVolume.ConfigBlob,
                _gfManager.FlatShadedLookUp,
                voxelArray,
                vertices,
                triangles,
                computedBounds,
                _lod,
                start);

            _gfManager.RegisterMarchJob(this, jobHandle, vertices, triangles, computedBounds, _geoChunk.ChunkCoord);

            if (_lod != 0) {
                var transitionMeshingVertexData = _gfManager.RentVertexBuffer();
                var transitionTriangles = _gfManager.RentTriangleBuffer();
                var transitionRanges = _gfManager.RentRangesBuffer();

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

            _leafInitialized = true;

            // GameObject/Mesh creation, material setup, and the new-leaf broadcast are all
            // deferred to ApplyMarchResults now - only once we actually know the march produced
            // triangles does any of that need to happen at all.
            MarchAndMesh(voxelArray);
        }

        private void UpdateLeaf(NativeArray<VoxelData> voxelArray) {
            if (!_leafInitialized) return;

            MarchAndMesh(voxelArray);
        }

        private void UpdateLeafMesh(
            NativeList<MeshingVertexData> vertices, NativeList<int> triangles, Bounds computedBounds) {
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

            // Computed by the march job itself (see MarchAndMesh/MarchingCubeJob.ComputedBounds)
            // instead of scanning every vertex again here via RecalculateBounds().
            _mesh.bounds = computedBounds;

            // ApplyMarchResults only calls this once triangles.Length/vertices.Length are already
            // confirmed >= 3, so there's no "empty mesh" case left to guard against here - that's
            // handled upstream by releasing the leaf entirely instead of calling this at all.
            if (_leafGo.TryGetComponent<MeshCollider>(out var meshCollider))
                meshCollider.sharedMesh = _mesh;
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
            // No RecalculateBounds() here - bounds are set once in ApplyTransitionMarchResults,
            // when the vertex buffer actually changes. This method also runs on mask-only changes
            // where the vertex buffer is untouched, so recomputing here would be redundant.
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

        /// <summary>
        /// Called once the march job(s) for this node have completed. If there's nothing to show,
        /// releases any existing leaf objects (an edit may have just emptied out a previously
        /// visible leaf) and stops - no GameObject/Mesh gets created for a leaf with no geometry,
        /// and no BroadcastNewLeaf, since neighbors don't need a transition seam against a leaf
        /// with nothing to show. Otherwise, builds the leaf objects on first use only (isFirstBuild),
        /// updates the mesh, and broadcasts only on that same first transition from empty to
        /// non-empty - matching the original one-time MakeLeaf behavior, just re-anchored to
        /// "first time this leaf actually has geometry" instead of "first time MakeLeaf ran."
        /// </summary>
        public void ApplyMarchResults(
            NativeList<MeshingVertexData> vertices, NativeList<int> triangles, Bounds computedBounds) {
            if (triangles.Length < 3 || vertices.Length < 3) {
                ReleaseLeafObjects();

                return;
            }

            var isFirstBuild = _leafGo == null;

            if (isFirstBuild) {
                BuildLeaf();
                BuildTransitions();
                SetMaterialOrigin();
            }

            UpdateLeafMesh(vertices, triangles, computedBounds);

            if (isFirstBuild)
                BroadcastNewLeaf();

            // HandleTransitionUpdate is NOT called here anymore - it needs _allTransitionTriangles/
            // _transitionRanges to already hold this pass's actual transition triangle data, which
            // only ApplyTransitionMarchResults provides. GeoForgeManager.CompleteAndApplyMarchingCubesJobs
            // applies main march results (this method) before transition results specifically so
            // that BuildTransitions above has already run - and run first - by the time
            // ApplyTransitionMarchResults needs _transitionMesh/_allTransitionTriangles to exist.
        }

        public void ApplyTransitionMarchResults(
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeArray<int2> triangleRanges) {
            // _transitionMesh (and _allTransitionTriangles/_transitionRanges alongside it) only
            // exist once this leaf actually has geometry - see ApplyMarchResults/BuildTransitions.
            // If this leaf turned out empty this pass, ApplyMarchResults already released
            // everything (possibly just now, in the same CompleteAndApplyMarchingCubesJobs call,
            // since that always applies march results before transition results) - there's nothing
            // to stitch a transition seam against, so just discard these results instead of writing
            // into collections that no longer exist.
            if (_transitionMesh == null)
                return;

            _allTransitionTriangles.CopyFrom(triangles);
            _transitionRanges.CopyFrom(triangleRanges);
            UpdateTransitionVertexBuffer(vertices);

            // Reuse the main mesh's bounds instead of computing separately in the transition job:
            // transition geometry re-samples the same boundary the main mesh already covers, just
            // at higher density to match a neighbor's finer LOD, so it should never extend
            // meaningfully past the main mesh's own extent. ApplyMarchResults always runs before
            // this method in the same CompleteAndApplyMarchingCubesJobs pass, so _mesh.bounds is
            // guaranteed current here. The margin guards against the T-junction correction pass
            // (bIsLowResFace) nudging a vertex a hair outside that extent.
            var expandedBounds = _mesh.bounds;
            expandedBounds.Expand(_parentGeoVolume.ConfigBlob.Value.Resolution);
            _transitionMesh.bounds = expandedBounds;

            HandleTransitionUpdate();
        }
    }
}
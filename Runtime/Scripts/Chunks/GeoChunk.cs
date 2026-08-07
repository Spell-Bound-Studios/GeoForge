// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Tooling;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Sealed GeoForge Chunk Logic.
    /// This class should only be touched externally by an IGeoChunk implementer.
    /// </summary>
    public sealed class GeoChunk : IDisposable {
        private Vector3Int _chunkCoord;
        public IGeoEditStore IGeoEditStore { get; private set; }

        private BoundsInt _bounds;
        private NativeList<SparseVoxelData> _sparseVoxels;
        private OctreeNode _rootNode;
        private DensityRange _densityRange;

        // Addresses (lod, localPosition) of octree nodes that produced zero triangles the last
        // time they were marched, and haven't been invalidated since. Presence in the set means
        // "known empty, safe to skip re-marching"; absence means "unknown" (never marched at this
        // address since the last edit, or it produced real geometry) - not "known non-empty" -
        // so the correct default on a miss is to march it, never to assume it has mesh.
        //
        // A HashSet rather than a Dictionary<_, bool> because there's never a reason to store a
        // false/non-empty entry - the "not present" state already means exactly that, for free.
        //
        // Keyed by exact (lod, localPosition) address, not by lod alone or by voxel region:
        // marching the same address again would always reproduce the same result off unchanged
        // data, which is what makes this safe to reuse - it says nothing about a *different*
        // address (a finer or coarser subdivision covering the same physical space), which needs
        // its own march and its own cache entry.
        //
        // Wiped wholesale on any edit to this chunk (see ApplyVoxelEdits) rather than only
        // invalidating addresses that overlap the edited bounds - simplest correct thing to do,
        // and a chunk that was just edited will have most of its cache invalidated by the edit's
        // meshing pass anyway, so partial invalidation would save little for real complexity.
        private HashSet<(int Lod, Vector3Int LocalPosition)> _knownEmptyOctreeAddresses = new();

        private readonly GeoForgeManager _mcManager;
        private IGeoVolume _parentGeoVolume;
        private Transform _transform;
        private readonly IGeoChunk _implementer;
        private VoxelOverrides _voxelOverrides;
        private bool _isDisposed;

        public Vector3Int ChunkCoord => _chunkCoord;
        public DensityRange DensityRange => _densityRange;
        public BoundsInt Bounds => _bounds;

        public Transform Transform => _transform;
        public OctreeNode RootNode => _rootNode;

        public IGeoVolume ParentGeoVolume => _parentGeoVolume;

        public GeoChunk(
            IGeoChunk implementer, Transform transform, IGeoEditStore iGeoEditStore, Vector3Int chunkCoord) {
            _implementer = implementer;
            _transform = transform;
            IGeoEditStore = iGeoEditStore;
            IGeoEditStore.OnGeoEditChanged += HandleResolvedVoxelEdits;
            _chunkCoord = chunkCoord;
            _parentGeoVolume = _transform.GetComponentInParent<IGeoVolume>();
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;
            _chunkCoord = chunkCoord;
            var voxelMin = chunkCoord * config.ChunkSize;
            _bounds = new BoundsInt(voxelMin, config.ChunkSize * Vector3Int.one);
            _transform.gameObject.name = chunkCoord.ToString();
            _mcManager = SingletonManager.GetSingletonInstance<GeoForgeManager>();
            _voxelOverrides = new VoxelOverrides();
        }

        public void SetOverrides(VoxelOverrides overrides) => _voxelOverrides = overrides;

        public bool HasOverrides() {
            if (_voxelOverrides == null || !_voxelOverrides.HasAnyOverrides)
                return false;

            return true;
        }

        private bool ApplyOverrides(NativeArray<VoxelData> voxels) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            _voxelOverrides.CopyToNativeHashMaps(
                out var xOverrides,
                out var yOverrides,
                out var zOverrides,
                out var pointOverrides
            );

            var hasOverridesArray = new NativeArray<bool>(1, Allocator.TempJob);
            hasOverridesArray[0] = false;

            var job = new ApplyBoundaryOverridesJob {
                voxelArray = voxels,
                xOverrides = xOverrides,
                yOverrides = yOverrides,
                zOverrides = zOverrides,
                pointOverrides = pointOverrides,
                chunkDataAreaSize = config.ChunkDataAreaSize,
                chunkDataWidthSize = config.ChunkDataWidthSize,
                hasOverrides = hasOverridesArray
            };

            var jobHandle = job.Schedule(voxels.Length, 64);
            jobHandle.Complete();

            var hasOverriddenVoxels = hasOverridesArray[0];

            xOverrides.Dispose();
            yOverrides.Dispose();
            zOverrides.Dispose();
            pointOverrides.Dispose();
            hasOverridesArray.Dispose();

            return hasOverriddenVoxels;
        }

        // NOTE (David): this now requires isEdit since GetVoxelDataArray does. Nothing in
        // GeoChunk.cs or GeoForgeManager.*.cs calls ValidateVoxels, so I can't tell which pool it
        // should check out from - the compiler will point you at the real call site to fix this.
        private bool ValidateVoxels(bool isEdit, NativeArray<VoxelData> voxels = default) {
            if (_voxelOverrides == null || !_voxelOverrides.HasAnyOverrides)
                return false;

            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            var hasCheckedOutDenseArray = false;

            if (voxels == default) {
                voxels = GetVoxelDataArray(isEdit: isEdit);
                hasCheckedOutDenseArray = true;
            }

            _voxelOverrides.CopyToNativeHashMaps(
                out var xOverrides,
                out var yOverrides,
                out var zOverrides,
                out var pointOverrides
            );

            var hasOverridesArray = new NativeArray<bool>(1, Allocator.TempJob);
            hasOverridesArray[0] = false;

            var job = new ApplyBoundaryOverridesJob {
                voxelArray = voxels,
                xOverrides = xOverrides,
                yOverrides = yOverrides,
                zOverrides = zOverrides,
                pointOverrides = pointOverrides,
                chunkDataAreaSize = config.ChunkDataAreaSize,
                chunkDataWidthSize = config.ChunkDataWidthSize,
                hasOverrides = hasOverridesArray
            };

            var jobHandle = job.Schedule(voxels.Length, 64);
            jobHandle.Complete();

            var hasOverriddenVoxels = hasOverridesArray[0];

            xOverrides.Dispose();
            yOverrides.Dispose();
            zOverrides.Dispose();
            pointOverrides.Dispose();
            hasOverridesArray.Dispose();

            if (hasCheckedOutDenseArray)
                _mcManager.ReleaseVoxelArray(config.ChunkSize, this, isEdit: isEdit);

            return hasOverriddenVoxels;
        }

        // Applies the edit and schedules its octree/march validation. If GeoForgeManager is
        // currently batching (see BeginEditBatch/EndEditBatch), this chunk registers itself for a
        // shared Complete()+release later instead of completing/releasing right here - lets
        // DistributeVoxelEdits schedule every affected chunk's march jobs before any of them block,
        // so they can actually run concurrently on worker threads. Outside a batch (any other
        // caller of IGeoChunk.PassVoxelEdits), this stays fully synchronous, same as before.
        public void HandleResolvedVoxelEdits(List<(int, VoxelData)> newVoxelChanges) {
            if (!ApplyVoxelEdits(newVoxelChanges, out var editBounds))
                return;

            ScheduleOctreeEditValidation(editBounds);

            if (_mcManager.IsBatchingEdits) {
                _mcManager.RegisterPendingEditRelease(this);

                return;
            }

            _mcManager.CompleteAndApplyMarchingCubesJobs();
            _mcManager.ReleaseVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this, isEdit: true);
        }

        public void ActivateGeoChunk(NativeArray<VoxelData> voxels = default) {
            ParentGeoVolume.GeoVolume.RegisterChunk(ChunkCoord, _implementer);

            if (voxels == default) {
                voxels = new NativeArray<VoxelData>(
                    ParentGeoVolume.ConfigBlob.Value.ChunkDataVolumeSize,
                    Allocator.Persistent);
            }

            SetVoxels(voxels);

            if (voxels.IsCreated)
                voxels.Dispose();
        }

        public void SetVoxels(NativeArray<VoxelData> voxels) {
            if (!voxels.IsCreated) {
                Debug.LogError(
                    $"_sparseVoxels being initialized with native array that has not been created for chunkCoord {_chunkCoord}.");

                return;
            }

            if (_sparseVoxels.IsCreated) _sparseVoxels.Dispose();
            _sparseVoxels = new NativeList<SparseVoxelData>(Allocator.Persistent);

            if (HasOverrides())
                ApplyOverrides(voxels);

            var densityRangeArray = new NativeArray<DensityRange>(1, Allocator.TempJob);

            new DenseToSparseVoxelDataJob {
                Voxels = voxels,
                SparseVoxels = _sparseVoxels,
                DensityRange = densityRangeArray
            }.Schedule().Complete();

            // Use the range DenseToSparseVoxelDataJob actually computed from this chunk's real
            // data, instead of always forcing "never skip" - a freshly-generated, fully-buried
            // solid (or fully-empty) chunk now correctly starts out skippable on first load,
            // rather than only becoming skippable after its first edit-triggered pack.
            _densityRange = densityRangeArray[0];
            densityRangeArray.Dispose();

            _rootNode = new OctreeNode(Vector3Int.zero, _parentGeoVolume.ConfigBlob.Value.LevelsOfDetail, _implementer,
                _parentGeoVolume);
        }

        public bool ApplyVoxelEdits(
            List<(int, VoxelData)> voxelChanges, out BoundsInt editBounds, BoundsInt existingEditBounds = default) {
            if (!_sparseVoxels.IsCreated) {
                editBounds = existingEditBounds;

                return false;
            }

            ref var config = ref ParentGeoVolume.ConfigBlob.Value;
            var voxelArray = GetVoxelDataArray(isEdit: true);

            var hasEdits = false;
            editBounds = existingEditBounds;

            foreach (var voxelChange in voxelChanges) {
                var index = voxelChange.Item1;

                GfStaticHelper.IndexToInt3(index, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x,
                    out var y, out var z);
                var voxelPos = new Vector3Int(x, y, z);

                if (_voxelOverrides.HasOverride(voxelPos))
                    continue;

                var existingVoxel = voxelArray[index];

                if (voxelChange.Item2.Density == existingVoxel.Density &&
                    voxelChange.Item2.MaterialIndex == existingVoxel.MaterialIndex)
                    continue;

                voxelArray[index] = VoxelData.CreateImmature(voxelChange.Item2.Density, voxelChange.Item2.MaterialIndex);

                if (!hasEdits) {
                    editBounds = new BoundsInt(voxelPos, Vector3Int.one);
                    hasEdits = true;
                }
                else {
                    var min = Vector3Int.Min(editBounds.min, voxelPos);
                    var max = Vector3Int.Max(editBounds.max, voxelPos + Vector3Int.one);
                    editBounds = new BoundsInt(min, max - min);
                }

                // DensityRange is a struct - the public DensityRange property only has a getter, so
                // calling Encapsulate through it (as this used to) mutates a throwaway copy and never
                // reaches _densityRange. Mutate the field directly since we're inside GeoChunk itself.
                _densityRange.Encapsulate(voxelChange.Item2.Density);
            }

            if (hasEdits) {
                _mcManager.PackVoxelArray(config.ChunkSize, this, isEdit: true, editBounds);

                // Any cached "empty" result may no longer hold once the underlying voxel data has
                // changed - wipe the whole cache rather than figuring out which addresses actually
                // overlap editBounds. See the field's own comment for why whole-chunk invalidation
                // is the right tradeoff here.
                _knownEmptyOctreeAddresses.Clear();
            }

            _mcManager.ReleaseVoxelArray(config.ChunkSize, this, isEdit: true);

            return hasEdits;
        }

        public void OnVolumeMovement() => RootNode?.ValidateMaterial();

        // NOTE (David): public signature change - now requires isEdit. I've only verified the call
        // sites inside GeoChunk.cs/GeoForgeManager.*.cs; if anything outside this file calls
        // GetVoxelDataArray(), it'll need a grep-and-fix pass.
        public NativeArray<VoxelData> GetVoxelDataArray(bool isEdit) =>
                _mcManager.GetOrUnpackVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this,
                    _sparseVoxels, isEdit);

        internal void UpdateVoxelData(NativeList<SparseVoxelData> voxels, DensityRange densityRange) {
            if (!_sparseVoxels.IsCreated)
                return;

            _sparseVoxels.Clear();
            _sparseVoxels.CopyFrom(voxels);
            _densityRange = densityRange;
        }

        public void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            var worldVoxelPos = pos + _chunkCoord * config.ChunkSize;

            if (_bounds.Contains(worldVoxelPos)) {
                _rootNode?.ValidateTransition(newLeaf, pos, GfStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = GfStaticHelper.GetNeighborCoord(index, _chunkCoord);
            var neighborChunk = _parentGeoVolume.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;

            var neighborLocalPos = worldVoxelPos - neighborCoord * config.ChunkSize;
            neighborChunk.BroadcastNewLeafAcrossChunks(newLeaf, neighborLocalPos, index);
        }

        public VoxelData GetVoxelData(int index) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            if (_mcManager.TryGetResidentVoxelArray(config.ChunkSize, this, out var denseVoxels))
                return denseVoxels[index];

            var sparseIndex = GfStaticHelper.BinarySearchVoxelData(index, config.ChunkDataVolumeSize, _sparseVoxels);

            return _sparseVoxels[sparseIndex].Voxel;
        }

        public VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;
            var chunkSpacePosition = position - _chunkCoord * config.ChunkSize;

            var index = GfStaticHelper.Coord3DToIndex(
                chunkSpacePosition.x,
                chunkSpacePosition.y,
                chunkSpacePosition.z,
                config.ChunkDataAreaSize,
                config.ChunkDataWidthSize
            );

            return GetVoxelData(index);
        }

        public bool HasVoxelData() => _sparseVoxels.IsCreated;

        // Whether the octree node at this exact address was found empty (zero triangles) the last
        // time it was marched, and hasn't been invalidated by an edit since. A miss (false) means
        // "unknown" - never marched at this address since the last edit, or it had real geometry -
        // never "known non-empty", so callers must treat a miss as "go ahead and march it", not as
        // proof of anything.
        public bool IsKnownEmpty(int lod, Vector3Int localPosition) =>
                _knownEmptyOctreeAddresses.Contains((lod, localPosition));

        // Records that the octree node at this address marched to zero triangles. Overwriting an
        // existing entry is a no-op (HashSet.Add just returns false) - fine, since re-marching the
        // same address always reproduces the same result off unchanged data.
        public void MarkKnownEmpty(int lod, Vector3Int localPosition) =>
                _knownEmptyOctreeAddresses.Add((lod, localPosition));

        // Explicit clear for callers that need to invalidate outside of an edit (none exist yet -
        // ApplyVoxelEdits above handles the normal case directly). Kept public in case that
        // changes; safe to call even when the cache is already empty.
        public void ClearKnownEmptyOctreeAddresses() => _knownEmptyOctreeAddresses.Clear();

        // Schedule-only half of octree edit validation: cascades the edit through the octree,
        // scheduling any resulting march/transition jobs via GeoForgeManager.RegisterMarchJob/
        // RegisterTransitionJob - but does NOT complete or release anything. See
        // HandleResolvedVoxelEdits for the caller and the batching/non-batching split.
        public void ScheduleOctreeEditValidation(BoundsInt bounds) {
            if (!_sparseVoxels.IsCreated)
                return;

            _rootNode?.ValidateOctreeEdits(bounds, GetVoxelDataArray(isEdit: true));
        }

        // Schedule-only half: checks out the Validation-pool slot and cascades the LOD check
        // through the octree, scheduling any resulting march/transition jobs - but does NOT
        // complete those jobs or release the checkout. Used by GeoVolume.ValidateChunkLodsAsync to
        // batch up to ValidatesPerFrame chunks' worth of scheduling before completing once, so
        // their march jobs can actually run concurrently instead of one Complete() serializing
        // each chunk before the next one is even scheduled.
        //
        // Caller MUST NOT call ReleaseLodValidation for this chunk until AFTER
        // CompleteAndApplyMarchingCubesJobs() has run for the whole batch - releasing any earlier
        // would let another chunk's LRU claim on the Validation pool overwrite this array while a
        // still-pending march job on a worker thread is reading it.
        public void ScheduleOctreeLodValidation(Vector3 playerPosition) {
            if (!_sparseVoxels.IsCreated)
                return;

            var playerPositionChunkSpace = playerPosition - _bounds.min;
            _rootNode.ValidateOctreeLods(playerPositionChunkSpace, GetVoxelDataArray(isEdit: false));
        }

        // Releases this chunk's Validation-pool checkout. Only valid to call after
        // CompleteAndApplyMarchingCubesJobs() has run for whatever batch this chunk's
        // ScheduleOctreeLodValidation call was part of.
        public void ReleaseLodValidation() =>
                _mcManager.ReleaseVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this, isEdit: false);

        public void ValidateOctreeLods(Vector3 playerPosition, NativeArray<VoxelData> voxels) {
            if (!_sparseVoxels.IsCreated)
                return;

            var playerPositionChunkSpace = playerPosition - _bounds.min;
            _rootNode.ValidateOctreeLods(playerPositionChunkSpace, voxels);
            _mcManager.CompleteAndApplyMarchingCubesJobs();
        }

        public void Dispose() {
            // Idempotent by design, not by accident: everything this touches downstream (event
            // -=, Dictionary.Remove, OctreeNode.Dispose's own IsCreated guards) happens to also
            // be safe to call twice today, but that's a chain of coincidences a future change
            // could break. GeoVolume.Dispose() calls this explicitly and then destroys the chunk
            // GameObject, which can re-enter here via SimpleGeoChunk.OnDestroy() - this guard is
            // what actually makes that safe, rather than relying on every downstream piece to
            // keep guarding itself correctly forever.
            if (_isDisposed)
                return;

            _isDisposed = true;

            IGeoEditStore.OnGeoEditChanged -= HandleResolvedVoxelEdits;
            _parentGeoVolume.GeoVolume.ChunkDict.Remove(_chunkCoord);
            _rootNode?.Dispose();

            if (_sparseVoxels.IsCreated)
                _sparseVoxels.Dispose();
        }
    }
}
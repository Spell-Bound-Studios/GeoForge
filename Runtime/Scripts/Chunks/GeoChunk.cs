// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Tooling;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge {
    public class GeoChunk : IDisposable {
        private Vector3Int _chunkCoord;
        public IGeoEditStore IGeoEditStore { get; private set; }

        private BoundsInt _bounds;
        private NativeList<SparseVoxelData> _sparseVoxels;
        private OctreeNode _rootNode;
        private DensityRange _densityRange;
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
            IGeoEditStore.OnGeoEditChanged += PassVoxelEdits;
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

        private bool ValidateVoxels(NativeArray<VoxelData> voxels = default) {
            if (_voxelOverrides == null || !_voxelOverrides.HasAnyOverrides)
                return false;

            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            var hasCheckedOutDenseArray = false;

            if (voxels == default) {
                voxels = GetVoxelDataArray();
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
                _mcManager.ReleaseVoxelArray(config.ChunkSize);

            return hasOverriddenVoxels;
        }

        public virtual void PassVoxelEdits(List<(int, VoxelData)> newVoxelChanges) {
            if (ApplyVoxelEdits(newVoxelChanges, out var editBounds))
                ValidateOctreeEdits(editBounds);
        }

        public void InitializeChunk(NativeArray<VoxelData> voxels = default) {
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
            var voxelArray = GetVoxelDataArray();

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

            if (hasEdits)
                _mcManager.PackVoxelArray(config.ChunkSize);

            _mcManager.ReleaseVoxelArray(config.ChunkSize);

            return hasEdits;
        }

        public void OnVolumeMovement() => RootNode?.ValidateMaterial();

        public NativeArray<VoxelData> GetVoxelDataArray() =>
                _mcManager.GetOrUnpackVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this,
                    _sparseVoxels);

        public void UpdateVoxelData(NativeList<SparseVoxelData> voxels, DensityRange densityRange) {
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

        public void ValidateOctreeEdits(BoundsInt bounds) {
            if (!_sparseVoxels.IsCreated)
                return;

            _rootNode?.ValidateOctreeEdits(bounds, GetVoxelDataArray());
            _mcManager.CompleteAndApplyMarchingCubesJobs();
            _mcManager.ReleaseVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize);
        }

        public void ValidateOctreeLods(Vector3 playerPosition) {
            if (!_sparseVoxels.IsCreated)
                return;

            var playerPositionChunkSpace = playerPosition - _bounds.min;
            _rootNode.ValidateOctreeLods(playerPositionChunkSpace, GetVoxelDataArray());
            _mcManager.CompleteAndApplyMarchingCubesJobs();
            _mcManager.ReleaseVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize);
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

            IGeoEditStore.OnGeoEditChanged -= PassVoxelEdits;
            _parentGeoVolume.GeoVolume.ChunkDict.Remove(_chunkCoord);
            _rootNode?.Dispose();

            if (_sparseVoxels.IsCreated)
                _sparseVoxels.Dispose();
        }
    }
}
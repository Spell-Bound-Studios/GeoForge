// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class BaseChunk : IDisposable {
        private Vector3Int _chunkCoord;
        private BoundsInt _bounds;
        private NativeList<SparseVoxelData> _sparseVoxels;
        private Dictionary<int, VoxelEdit> _voxelEdits;
        private OctreeNode _rootNode;
        private DensityRange _densityRange;
        private readonly MarchingCubesManager _mcManager;
        private IVolume _parentVolume;
        private readonly MonoBehaviour _owner;
        private readonly IChunk _ownerAsIChunk;
        private VoxelOverrides _voxelOverrides;

        public Vector3Int ChunkCoord => _chunkCoord;
        public DensityRange DensityRange => _densityRange;
        public BoundsInt Bounds => _bounds;
        public OctreeNode RootNode => _rootNode;
        public Transform Transform => _owner.transform;

        public IVolume ParentVolume => _parentVolume;

        public BaseChunk(MonoBehaviour owner, IChunk ownerAsIChunk) {
            _owner = owner;
            _ownerAsIChunk = ownerAsIChunk;
            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
            _voxelOverrides = new VoxelOverrides();
        }

        public void SetCoordAndFields(Vector3Int coord) {
            _parentVolume = _owner.GetComponentInParent<IVolume>();
            ref var config = ref ParentVolume.ConfigBlob.Value;
            _chunkCoord = coord;
            var voxelMin = coord * config.ChunkSize;
            _bounds = new BoundsInt(voxelMin, config.ChunkSize * Vector3Int.one);
            _owner.gameObject.name = coord.ToString();
        }

        public void SetOverrides(VoxelOverrides overrides) => _voxelOverrides = overrides;

        public bool HasOverrides() {
            if (_voxelOverrides == null || !_voxelOverrides.HasAnyOverrides)
                return false;

            return true;
        }

        private bool ApplyOverrides(NativeArray<VoxelData> voxels) {
            ref var config = ref ParentVolume.ConfigBlob.Value;

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

            ref var config = ref ParentVolume.ConfigBlob.Value;

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

        public void InitializeVoxels(NativeArray<VoxelData> voxels) {
            if (_sparseVoxels.IsCreated) {
                Debug.LogError($"_sparseVoxels is already created for this chunkCoord {_chunkCoord}.");

                return;
            }

            if (!voxels.IsCreated) {
                Debug.LogError(
                    $"_sparseVoxels being initialized with native array that has not been created for chunkCoord {_chunkCoord}.");

                return;
            }

            if (HasOverrides())
                ApplyOverrides(voxels);

            _sparseVoxels = new NativeList<SparseVoxelData>(Allocator.Persistent);

            new DenseToSparseVoxelDataJob {
                Voxels = voxels,
                SparseVoxels = _sparseVoxels
            }.Schedule().Complete();

            _densityRange = new DensityRange(byte.MinValue, byte.MaxValue,
                _parentVolume.ConfigBlob.Value.DensityThreshold);

            _rootNode = new OctreeNode(Vector3Int.zero, _parentVolume.ConfigBlob.Value.LevelsOfDetail, _ownerAsIChunk,
                _parentVolume);
        }

        public bool ApplyVoxelEdits(
            List<VoxelEdit> voxelEdits, out BoundsInt editBounds, BoundsInt existingEditBounds = default) {
            if (!_sparseVoxels.IsCreated) {
                editBounds = existingEditBounds;

                return false;
            }

            ref var config = ref ParentVolume.ConfigBlob.Value;
            var voxelArray = GetVoxelDataArray();

            var hasEdits = false;
            editBounds = existingEditBounds;

            foreach (var voxelEdit in voxelEdits) {
                var index = voxelEdit.index;

                McStaticHelper.IndexToInt3(index, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x,
                    out var y, out var z);
                var voxelPos = new Vector3Int(x, y, z);

                if (_voxelOverrides.HasOverride(voxelPos))
                    continue;

                var existingVoxel = voxelArray[index];

                if (voxelEdit.density == existingVoxel.Density &&
                    voxelEdit.MaterialType == existingVoxel.MaterialIndex)
                    continue;

                voxelArray[index] = new VoxelData(voxelEdit.density, voxelEdit.MaterialType);

                if (!hasEdits) {
                    editBounds = new BoundsInt(voxelPos, Vector3Int.one);
                    hasEdits = true;
                }
                else {
                    var min = Vector3Int.Min(editBounds.min, voxelPos);
                    var max = Vector3Int.Max(editBounds.max, voxelPos + Vector3Int.one);
                    editBounds = new BoundsInt(min, max - min);
                }

                DensityRange.Encapsulate(voxelEdit.density);
            }

            if (hasEdits)
                _mcManager.PackVoxelArray(config.ChunkSize);

            _mcManager.ReleaseVoxelArray(config.ChunkSize);

            return hasEdits;
        }

        public void OnVolumeMovement() => RootNode?.ValidateMaterial();

        public NativeArray<VoxelData> GetVoxelDataArray() =>
                _mcManager.GetOrUnpackVoxelArray(ParentVolume.ConfigBlob.Value.ChunkSize, this,
                    _sparseVoxels);

        public void UpdateVoxelData(NativeList<SparseVoxelData> voxels, DensityRange densityRange) {
            if (!_sparseVoxels.IsCreated)
                return;

            _sparseVoxels.Clear();
            _sparseVoxels.CopyFrom(voxels);
            _densityRange = densityRange;
        }

        public void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) {
            ref var config = ref ParentVolume.ConfigBlob.Value;

            var worldVoxelPos = pos + _chunkCoord * config.ChunkSize;

            if (_bounds.Contains(worldVoxelPos)) {
                _rootNode?.ValidateTransition(newLeaf, pos, McStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = McStaticHelper.GetNeighborCoord(index, _chunkCoord);
            var neighborChunk = _parentVolume.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;

            var neighborLocalPos = worldVoxelPos - neighborCoord * config.ChunkSize;
            neighborChunk.BroadcastNewLeafAcrossChunks(newLeaf, neighborLocalPos, index);
        }

        public VoxelData GetVoxelData(int index) {
            ref var config = ref ParentVolume.ConfigBlob.Value;
            var sparseIndex = McStaticHelper.BinarySearchVoxelData(index, config.ChunkDataVolumeSize, _sparseVoxels);

            return _sparseVoxels[sparseIndex].Voxel;
        }

        public VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) {
            ref var config = ref ParentVolume.ConfigBlob.Value;
            var chunkSpacePosition = position - _chunkCoord * config.ChunkSize;

            var index = McStaticHelper.Coord3DToIndex(
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
            _mcManager.ReleaseVoxelArray(ParentVolume.ConfigBlob.Value.ChunkSize);
        }

        public void ValidateOctreeLods(Vector3 playerPosition) {
            if (!_sparseVoxels.IsCreated)
                return;

            var playerPositionChunkSpace = playerPosition - _bounds.min;
            _rootNode.ValidateOctreeLods(playerPositionChunkSpace, GetVoxelDataArray());
            _mcManager.CompleteAndApplyMarchingCubesJobs();
            _mcManager.ReleaseVoxelArray(ParentVolume.ConfigBlob.Value.ChunkSize);
        }

        public void Dispose() {
            _rootNode?.Dispose();

            if (_sparseVoxels.IsCreated)
                _sparseVoxels.Dispose();
        }
    }
}
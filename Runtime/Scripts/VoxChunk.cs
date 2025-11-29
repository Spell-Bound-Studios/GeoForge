// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class VoxChunk : IDisposable {
        private Vector3Int _chunkCoord;
        private BoundsInt _bounds;
        private NativeList<SparseVoxelData> _sparseVoxels;
        private Dictionary<int, VoxelEdit> _voxelEdits;
        private OctreeNode _rootNode;
        private DensityRange _densityRange;
        private MarchingCubesManager _mcManager;
        private IVolume _parentVolume;
        private MonoBehaviour _owner;
        private VoxelOverrides _voxelOverrides;

        public Vector3Int ChunkCoord => _chunkCoord;
        public DensityRange DensityRange => _densityRange;
        public BoundsInt Bounds => _bounds;
        public OctreeNode RootNode => _rootNode;
        public Transform Transform => _owner.transform;

        public IVolume ParentVolume => _parentVolume;

        public VoxChunk(MonoBehaviour owner) {
            _owner = owner;
            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
            _voxelOverrides = new VoxelOverrides();
        }

        public void SetCoordAndFields(Vector3Int coord) {
            _parentVolume = _owner.GetComponentInParent<IVolume>();
            ref var config = ref ParentVolume.VoxelVolume.ConfigBlob.Value;
            _chunkCoord = coord;
            var voxelMin = coord * config.ChunkSize;
            _bounds = new BoundsInt(voxelMin, config.ChunkSize * Vector3Int.one);
            _owner.gameObject.name = coord.ToString();
        }

        public void SetOverrides(IEnumerable<(Axis axis, int slice, VoxelData voxel)> overrides) {
            foreach (var (axis, slice, voxel) in overrides) _voxelOverrides.AddOverride(axis, slice, voxel);
        }

        private void ValidateVoxels() {
            if (_voxelOverrides == null || !_voxelOverrides.HasAnyOverrides) return;

            ref var config = ref ParentVolume.VoxelVolume.ConfigBlob.Value;
            var voxelArray = GetVoxelDataArray();

            _voxelOverrides.CopyToNativeHashMaps(
                out var xOverrides,
                out var yOverrides,
                out var zOverrides,
                Allocator.TempJob
            );

            var hasOverridesArray = new NativeArray<int>(1, Allocator.TempJob);
            hasOverridesArray[0] = 0;

            var job = new ApplyBoundaryOverridesJob {
                voxelArray = voxelArray,
                xOverrides = xOverrides,
                yOverrides = yOverrides,
                zOverrides = zOverrides,
                chunkDataAreaSize = config.ChunkDataAreaSize,
                chunkDataWidthSize = config.ChunkDataWidthSize,
                hasOverrides = hasOverridesArray
            };

            var jobHandle = job.Schedule(voxelArray.Length, 64);
            jobHandle.Complete();

            var hasOverriddenVoxels = hasOverridesArray[0] == 1;

            xOverrides.Dispose();
            yOverrides.Dispose();
            zOverrides.Dispose();
            hasOverridesArray.Dispose();

            if (hasOverriddenVoxels) _mcManager.PackVoxelArray(config.ChunkSize);

            _mcManager.ReleaseVoxelArray(config.ChunkSize);
        }

        public void InitializeVoxels(NativeList<SparseVoxelData> voxels) {
            if (_sparseVoxels.IsCreated) {
                Debug.LogError($"_sparseVoxels is already created for this chunkCoord {_chunkCoord}.");

                return;
            }

            if (!voxels.IsCreated) {
                Debug.LogError(
                    $"_sparseVoxels being initialized with a List is already created for this chunkCoord {_chunkCoord}.");

                return;
            }

            _sparseVoxels = new NativeList<SparseVoxelData>(voxels.Length, Allocator.Persistent);
            _sparseVoxels.AddRange(voxels.AsArray());
            ValidateVoxels();

            _densityRange = new DensityRange(byte.MinValue, byte.MaxValue,
                _parentVolume.VoxelVolume.ConfigBlob.Value.DensityThreshold);

            _rootNode = new OctreeNode(Vector3Int.zero, _parentVolume.VoxelVolume.ConfigBlob.Value.LevelsOfDetail, this,
                _parentVolume);
        }

        public bool ApplyVoxelEdits(
            List<VoxelEdit> voxelEdits, out BoundsInt editBounds, BoundsInt existingEditBounds = default) {
            ref var config = ref ParentVolume.VoxelVolume.ConfigBlob.Value;
            var voxelArray = GetVoxelDataArray();

            var hasEdits = false;
            editBounds = existingEditBounds;

            foreach (var voxelEdit in voxelEdits) {
                var index = voxelEdit.index;

                McStaticHelper.IndexToInt3(index, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x,
                    out var y, out var z);

                if (_voxelOverrides.HasOverride(x, y, z)) continue;

                var voxelPos = new Vector3Int(x, y, z);
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
                _mcManager.GetOrUnpackVoxelArray(ParentVolume.VoxelVolume.ConfigBlob.Value.ChunkSize, this,
                    _sparseVoxels);

        public void UpdateVoxelData(NativeList<SparseVoxelData> voxels, DensityRange densityRange) {
            if (!_sparseVoxels.IsCreated)
                return;

            _sparseVoxels.Clear();
            _sparseVoxels.CopyFrom(voxels);
            _densityRange = densityRange;
        }

        public void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) {
            ref var config = ref ParentVolume.VoxelVolume.ConfigBlob.Value;

            var worldVoxelPos = pos + _chunkCoord * config.ChunkSize;

            if (_bounds.Contains(worldVoxelPos)) {
                _rootNode?.ValidateTransition(newLeaf, pos, McStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = McStaticHelper.GetNeighborCoord(index, _chunkCoord);
            var neighborChunk = _parentVolume.VoxelVolume.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;

            var neighborLocalPos = worldVoxelPos - neighborCoord * config.ChunkSize;
            neighborChunk.VoxelChunk.BroadcastNewLeafAcrossChunks(newLeaf, neighborLocalPos, index);
        }

        public VoxelData GetVoxelData(int index) {
            ref var config = ref ParentVolume.VoxelVolume.ConfigBlob.Value;
            var sparseIndex = McStaticHelper.BinarySearchVoxelData(index, config.ChunkDataVolumeSize, _sparseVoxels);

            return _sparseVoxels[sparseIndex].Voxel;
        }

        public VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) {
            ref var config = ref ParentVolume.VoxelVolume.ConfigBlob.Value;
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
            _mcManager.ReleaseVoxelArray(ParentVolume.VoxelVolume.ConfigBlob.Value.ChunkSize);
        }

        public void ValidateOctreeLods(Vector3 playerPosition) {
            if (!_sparseVoxels.IsCreated)
                return;

            var playerPositionChunkSpace = playerPosition - _bounds.min;
            _rootNode.ValidateOctreeLods(playerPositionChunkSpace, GetVoxelDataArray());
            _mcManager.CompleteAndApplyMarchingCubesJobs();
            _mcManager.ReleaseVoxelArray(ParentVolume.VoxelVolume.ConfigBlob.Value.ChunkSize);
        }

        public void Dispose() {
            _rootNode?.Dispose();

            if (_sparseVoxels.IsCreated)
                _sparseVoxels.Dispose();
        }
    }
}
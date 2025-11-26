// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Unity.Collections;
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
        private IVolume _chunkManager;
        private MonoBehaviour _owner;
        private Dictionary<int, VoxelData> _xOverrides = new();
        private Dictionary<int, VoxelData> _yOverrides = new();
        private Dictionary<int, VoxelData> _zOverrides = new ();

        public Vector3Int ChunkCoord => _chunkCoord;
        public DensityRange DensityRange => _densityRange;
        public BoundsInt Bounds => _bounds;
        public OctreeNode RootNode => _rootNode;
        public Transform Transform => _owner.transform;

        public VoxChunk(MonoBehaviour owner) {
            _owner = owner;
            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
        }

        public void SetCoordAndFields(Vector3Int coord) {
            ref var config = ref _mcManager.McConfigBlob.Value;
            _chunkManager = _owner.GetComponentInParent<IVolume>();
            _chunkCoord = coord;
            var voxelMin = coord * config.ChunkSize;
            _bounds = new BoundsInt(voxelMin, config.ChunkSize * Vector3Int.one);
            _owner.gameObject.name = coord.ToString();
        }

        public void SetOverrides(Dictionary<(BoundaryOverrides.Axis, int), VoxelData> voxelOverrides) {
            foreach (var kvp in voxelOverrides) {
                switch (kvp.Key.Item1) {
                    case BoundaryOverrides.Axis.X:
                        _xOverrides.Add(kvp.Key.Item2, kvp.Value);
                        break;
                    case BoundaryOverrides.Axis.Y:
                        _yOverrides.Add(kvp.Key.Item2, kvp.Value);
                        break;
                    case BoundaryOverrides.Axis.Z:
                        _zOverrides.Add(kvp.Key.Item2, kvp.Value);
                        break;
                }
            }
        }

        private void ValidateVoxels() {
            ref var config = ref _mcManager.McConfigBlob.Value;
            var voxelArray = GetVoxelDataArray();
            var hasOverridenVoxels = false;
            for (var i = 0; i < voxelArray.Length; i++) {
                McStaticHelper.IndexToInt3(i, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x,
                    out var y, out var z);
                VoxelData overrideVoxel;
                if (_yOverrides.TryGetValue(y, out overrideVoxel)) {
                    voxelArray[i] = overrideVoxel;
                    hasOverridenVoxels = true;
                }
                else if (_xOverrides.TryGetValue(x, out overrideVoxel)) {
                    voxelArray[i] = overrideVoxel;
                    hasOverridenVoxels = true;
                }
                else if (_zOverrides.TryGetValue(z, out overrideVoxel)) {
                    voxelArray[i] = overrideVoxel;
                    hasOverridenVoxels = true;
                }
                
            }
            if (hasOverridenVoxels)
                _mcManager.PackVoxelArray();
                
            _mcManager.ReleaseVoxelArray();
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
                _mcManager.McConfigBlob.Value.DensityThreshold);

            _rootNode = new OctreeNode(Vector3Int.zero, _mcManager.McConfigBlob.Value.LevelsOfDetail, this,
                _chunkManager.VoxelVolume.Transform);
        }

        public bool ApplyVoxelEdits(
            List<VoxelEdit> voxelEdits, out BoundsInt editBounds, BoundsInt existingEditBounds = default) {
            ref var config = ref _mcManager.McConfigBlob.Value;
            var voxelArray = GetVoxelDataArray();

            var hasEdits = false;
            editBounds = existingEditBounds;

            foreach (var voxelEdit in voxelEdits) {
                var index = voxelEdit.index;
                McStaticHelper.IndexToInt3(index, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x,
                    out var y, out var z);
                if (_xOverrides.ContainsKey(x) || _yOverrides.ContainsKey(y) || _zOverrides.ContainsKey(z)) 
                    continue;
                
                var voxelPos = new Vector3Int(x, y, z);
                var existingVoxel = voxelArray[index];

                if (voxelEdit.density == existingVoxel.Density &&
                    voxelEdit.MaterialType == existingVoxel.MaterialType)
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

            if (hasEdits) _mcManager.PackVoxelArray();

            return hasEdits;
        }

        public void OnVolumeMovement() => RootNode?.ValidateMaterial();

        public NativeArray<VoxelData> GetVoxelDataArray() =>
                _mcManager.GetOrUnpackVoxelArray(_chunkCoord, this, _sparseVoxels);

        public void UpdateVoxelData(NativeList<SparseVoxelData> voxels, DensityRange densityRange) {
            if (!_sparseVoxels.IsCreated)
                return;

            _sparseVoxels.Clear();
            _sparseVoxels.CopyFrom(voxels);
            _densityRange = densityRange;
        }

        public void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) {
            ref var config = ref _mcManager.McConfigBlob.Value;

            var worldVoxelPos = pos + _chunkCoord * config.ChunkSize;

            if (_bounds.Contains(worldVoxelPos)) {
                _rootNode?.ValidateTransition(newLeaf, pos, McStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = McStaticHelper.GetNeighborCoord(index, _chunkCoord);
            var neighborChunk = _chunkManager.VoxelVolume.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;

            var neighborLocalPos = worldVoxelPos - neighborCoord * config.ChunkSize;
            neighborChunk.VoxelChunk.BroadcastNewLeafAcrossChunks(newLeaf, neighborLocalPos, index);
        }

        public VoxelData GetVoxelData(int index) {
            ref var config = ref _mcManager.McConfigBlob.Value;
            var sparseIndex = McStaticHelper.BinarySearchVoxelData(index, config.ChunkDataVolumeSize, _sparseVoxels);

            return _sparseVoxels[sparseIndex].Voxel;
        }

        public VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) {
            ref var config = ref _mcManager.McConfigBlob.Value;
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
            _mcManager.ReleaseVoxelArray();
        }

        public void ValidateOctreeLods(Vector3 playerPosition) {
            if (!_sparseVoxels.IsCreated)
                return;

            var playerPositionChunkSpace = playerPosition - _bounds.min;
            _rootNode.ValidateOctreeLods(playerPositionChunkSpace, GetVoxelDataArray());
            _mcManager.CompleteAndApplyMarchingCubesJobs();
            _mcManager.ReleaseVoxelArray();
        }

        public void Dispose() {
            _rootNode?.Dispose();

            if (_sparseVoxels.IsCreated)
                _sparseVoxels.Dispose();
        }
    }
}
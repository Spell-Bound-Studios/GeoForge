// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Spellbound.Core;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class TerrainChunk : MonoBehaviour, IVoxelTerrainChunk {
        private Vector3Int _chunkCoord;
        private Bounds _bounds;
        private NativeList<SparseVoxelData> _sparseVoxels;
        private Dictionary<int, VoxelEdit> _voxelEdits;
        private OctreeNode _rootNode;
        private DensityRange _densityRange;
        private MarchingCubesManager _mcManager;
        private IVoxelTerrainChunkManager _chunkManager;

        public NativeArray<VoxelData> GetVoxelDataArray() => _mcManager.GetOrUnpackVoxelArray(_chunkCoord, this, _sparseVoxels);

        public DensityRange GetDensityRange() => _densityRange;

        public void SetDensityRange(DensityRange densityRange) => _densityRange = densityRange;
        public Vector3Int GetChunkCoord() => _chunkCoord;

        public Transform GetChunkTransform() => transform;

        public void InitializeVoxelData(NativeList<SparseVoxelData> voxels) {
            if (_sparseVoxels.IsCreated)
                Debug.LogError($"_sparseVoxels is already created for this chunkCoord {_chunkCoord}.");

            if (!voxels.IsCreated) {
                Debug.LogError(
                    $"_sparseVoxels being initialized with a List is already created for this chunkCoord {_chunkCoord}.");
            }

            _sparseVoxels = new NativeList<SparseVoxelData>(voxels.Length, Allocator.Persistent);
            _sparseVoxels.AddRange(voxels.AsArray());
            _rootNode = new OctreeNode(Vector3Int.zero, _mcManager.McConfigBlob.Value.LevelsOfDetail, this);
            ValidateOctreeLods(Camera.main.transform.position);
        }

        public void UpdateVoxelData(NativeList<SparseVoxelData> voxels) {
            if (!_sparseVoxels.IsCreated)
                return;

            _sparseVoxels.Clear();
            _sparseVoxels.CopyFrom(voxels);
        }
        
        public void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3 pos, int index) {
            if (_bounds.Contains(pos)) {
                _rootNode?.ValidateTransition(newLeaf, pos, McStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = McStaticHelper.GetNeighborCoord(index, _chunkCoord);

            var neighborChunk = _chunkManager.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;

            neighborChunk.BroadcastNewLeafAcrossChunks(newLeaf, pos, index);
        }

        public void AddToVoxelEdits(List<VoxelEdit> newVoxelEdits) {
            if (newVoxelEdits.Count == 0)
                return;

            var voxelArray = GetVoxelDataArray();
            var hasAnyEdits = false;
            Bounds editBounds = default;

            ref var config = ref _mcManager.McConfigBlob.Value;

            foreach (var voxelEdit in newVoxelEdits) {
                var index = voxelEdit.index;
                var existingVoxel = voxelArray[index];

                // Skip if no change
                if (voxelEdit.density == existingVoxel.Density &&
                    voxelEdit.MaterialType == existingVoxel.MaterialType)
                    continue;

                voxelArray[index] = new VoxelData(voxelEdit.density, voxelEdit.MaterialType);

                McStaticHelper.IndexToInt3(index, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x,
                    out var y, out var z);
                var localPos = new Vector3Int(x, y, z);

                if (!hasAnyEdits) {
                    editBounds = new Bounds(localPos, Vector3.zero);
                    hasAnyEdits = true;
                }
                else
                    editBounds.Encapsulate(localPos);

                _densityRange.Encapsulate(voxelEdit.density); // Use voxelEdit.density directly
            }

            if (hasAnyEdits) {
                ValidateOctreeEdits(editBounds);
            }
            
            _mcManager.PackVoxelArray();
            _mcManager.CompleteAndApplyMarchingCubesJobs();
        }

        public VoxelData GetVoxelData(int index) {
            ref var config = ref _mcManager.McConfigBlob.Value;
            var sparseIndex = McStaticHelper.BinarySearchVoxelData(index, config.ChunkDataVolumeSize, _sparseVoxels);

            return _sparseVoxels[sparseIndex].Voxel;
        }

        public VoxelData GetVoxelData(Vector3Int position) {
            ref var config = ref _mcManager.McConfigBlob.Value;
            var localPos = position - _chunkCoord * config.ChunkSize;

            var index = McStaticHelper.Coord3DToIndex(localPos.x, localPos.y, localPos.z, config.ChunkDataAreaSize,
                config.ChunkDataWidthSize);

            return GetVoxelData(index);
        }

        public bool HasVoxelData() => _sparseVoxels.IsCreated;

        // TODO: Null checking twice is weird.
        public void ValidateOctreeEdits(Bounds bounds) {
            ref var config = ref _mcManager.McConfigBlob.Value;

            if (_rootNode == null)
                _rootNode = new OctreeNode(Vector3Int.zero, config.LevelsOfDetail, this);

            var worldBounds = new Bounds(bounds.center + _chunkCoord * config.ChunkSize, bounds.size);
            _rootNode.ValidateOctreeEdits(worldBounds);
        }

        public void ValidateOctreeLods(Vector3 playerPosition) {
            if (!_sparseVoxels.IsCreated)
                return;

            _rootNode.ValidateOctreeLods(playerPosition);
            _mcManager.CompleteAndApplyMarchingCubesJobs();
        }

        private void OnDrawGizmos() => Gizmos.DrawWireCube(_bounds.center, _bounds.size);

        public void SetChunkFields(Vector3Int coord) {
            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
            ref var config = ref _mcManager.McConfigBlob.Value;
            _chunkManager = SingletonManager.GetSingletonInstance<IVoxelTerrainChunkManager>();
            _chunkCoord = coord;

            _bounds = new Bounds(
                coord * config.ChunkSize + config.ChunkCenter,
                config.ChunkExtents);
            gameObject.name = coord.ToString();
        }

        private void OnDestroy() {
            _rootNode?.Dispose();

            if (_sparseVoxels.IsCreated)
                _sparseVoxels.Dispose();
        }
    }
}
// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Defines the contract that a chunk must fulfill to integrate with the Marching Cubes Voxel System.
    /// </summary>
    public interface IChunk {
        BaseChunk BaseChunk { get; }

        DensityRange DensityRange => BaseChunk.DensityRange;

        Vector3Int ChunkCoord => BaseChunk.ChunkCoord;

        Transform Transform => BaseChunk.Transform;

        void InitializeChunk(NativeArray<VoxelData> voxels); //polymorphic

        void PassVoxelEdits(List<VoxelEdit> newVoxelEdits); //polymorphic

        // Default Implementations
        VoxelData GetVoxelData(int index) => BaseChunk.GetVoxelData(index);

        VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) =>
                BaseChunk.GetVoxelDataFromVoxelPosition(position);

        bool HasVoxelData() => BaseChunk.HasVoxelData();

        void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) =>
                BaseChunk.BroadcastNewLeafAcrossChunks(newLeaf, pos, index);

        void ValidateOctreeLods(Vector3 playerPosition) => BaseChunk.ValidateOctreeLods(playerPosition);

        void SetCoordAndFields(Vector3Int coord) => BaseChunk.SetCoordAndFields(coord);

        void OnVolumeMovement() => BaseChunk.OnVolumeMovement();

        void SetOverrides(VoxelOverrides overrides) => BaseChunk.SetOverrides(overrides);
    }
}
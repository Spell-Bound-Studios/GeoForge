// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Defines the contract that a chunk must fulfill to integrate with the Marching Cubes Voxel System.
    /// </summary>
    public interface IChunk {
        #region Abstract Methods and Properties

        /// <summary>
        /// Getter Property for Base Chunk, which holds the core functionality of a Chunk.
        /// </summary>
        BaseChunk BaseChunk { get; }

        /// <summary>
        /// Contains the smallest and largest Density. Used as a shortcut to read whether a chunk has any mesh at all.
        /// </summary>
        DensityRange DensityRange => BaseChunk.DensityRange;

        /// <summary>
        /// Method to kick-off the Chunk being an actively managed Marching Cubes Chunk.
        /// </summary>
        /// <param name="voxels"></param> Can be called with voxels, or can generate voxels in the implementation.
        void InitializeChunk(NativeArray<VoxelData> voxels = default);

        /// <summary>
        /// Method for Chunk to receive voxel edits.
        /// </summary>
        /// <param name="newVoxelEdits"></param>
        void PassVoxelEdits(List<VoxelEdit> newVoxelEdits);

        #endregion

        #region Default Implementations

        Vector3Int ChunkCoord => BaseChunk.ChunkCoord;

        Transform Transform => BaseChunk.Transform;

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

        #endregion
    }
}
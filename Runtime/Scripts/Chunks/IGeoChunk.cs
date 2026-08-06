// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Defines the contract that a geoChunk must fulfill to integrate with the GeoForge Marching Cubes Voxel System.
    /// </summary>
    public interface IGeoChunk {
        #region Abstract Methods and Properties

        /// <summary>
        /// Getter Property for Base Chunk, which holds the core functionality of a Chunk.
        /// </summary>
        GeoChunk GeoChunk { get; }

        #endregion

        #region Default Implementations

        Vector3Int ChunkCoord => GeoChunk.ChunkCoord;

        /// <summary>
        /// Contains the smallest and largest Density. Used as a shortcut to read whether a geoChunk has any mesh at all.
        /// </summary>
        DensityRange DensityRange => GeoChunk.DensityRange;

        /// <summary>
        /// Method for Chunk to receive a voxel edit operation.
        /// </summary>
        void PassVoxelEdits(VoxelEditOperation operation) => GeoChunk.IGeoEditStore.Delta(operation);

        /// <summary>
        /// Method to kick-off the Chunk being an actively managed Marching Cubes Chunk.
        /// </summary>
        /// <param name="voxels"></param> Can be called with voxels, or can generate voxels in the implementation.
        void ActivateGeoChunk(NativeArray<VoxelData> voxels = default) => GeoChunk.InitializeChunk(voxels);

        void InitializeGeoChunk(Vector3Int coord);

        VoxelData GetVoxelData(int index) => GeoChunk.GetVoxelData(index);

        VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) =>
                GeoChunk.GetVoxelDataFromVoxelPosition(position);

        bool HasVoxelData() => GeoChunk.HasVoxelData();

        void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) =>
                GeoChunk.BroadcastNewLeafAcrossChunks(newLeaf, pos, index);

        // Schedule-only half of LOD validation, used by GeoVolume.ValidateChunkLodsAsync to batch
        // up to ValidatesPerFrame chunks before completing march jobs once for the whole batch.
        // See GeoChunk.ScheduleOctreeLodValidation for the ordering contract with ReleaseLodValidation.
        void ScheduleOctreeLodValidation(Vector3 playerPosition) => GeoChunk.ScheduleOctreeLodValidation(playerPosition);

        void ReleaseLodValidation() => GeoChunk.ReleaseLodValidation();

        void OnVolumeMovement() => GeoChunk.OnVolumeMovement();

        void SetOverrides(VoxelOverrides overrides) => GeoChunk.SetOverrides(overrides);

        #endregion
    }
}
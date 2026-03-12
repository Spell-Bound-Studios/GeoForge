// Copyright 2025 Spellbound Studio Inc.

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

        Transform Transform => GeoChunk.Transform;
        
        /// <summary>
        /// Contains the smallest and largest Density. Used as a shortcut to read whether a geoChunk has any mesh at all.
        /// </summary>
        DensityRange DensityRange => GeoChunk.DensityRange;
        
        /// <summary>
        /// Method for Chunk to receive voxel edits.
        /// </summary>
        /// <param name="newVoxelEdits"></param>
        void PassVoxelEdits(List<VoxelEdit> newVoxelEdits) => GeoChunk.PassVoxelEdits(newVoxelEdits);
        
        /// <summary>
        /// Method to kick-off the Chunk being an actively managed Marching Cubes Chunk.
        /// </summary>
        /// <param name="voxels"></param> Can be called with voxels, or can generate voxels in the implementation.
        void InitializeChunk(NativeArray<VoxelData> voxels = default) => GeoChunk.InitializeChunk(voxels);

        VoxelData GetVoxelData(int index) => GeoChunk.GetVoxelData(index);

        VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) =>
                GeoChunk.GetVoxelDataFromVoxelPosition(position);

        bool HasVoxelData() => GeoChunk.HasVoxelData();

        void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) =>
                GeoChunk.BroadcastNewLeafAcrossChunks(newLeaf, pos, index);

        void ValidateOctreeLods(Vector3 playerPosition) => GeoChunk.ValidateOctreeLods(playerPosition);

        void SetCoordAndFields(Vector3Int coord) => GeoChunk.SetCoordAndFields(coord);

        void OnVolumeMovement() => GeoChunk.OnVolumeMovement();

        void SetOverrides(VoxelOverrides overrides) => GeoChunk.SetOverrides(overrides);

        #endregion
    }
}
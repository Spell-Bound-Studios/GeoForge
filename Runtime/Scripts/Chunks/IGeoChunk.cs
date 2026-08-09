// Copyright 2026 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Defines the contract for being a GeoForge Chunk.
    /// Contains Default Implementations that simply wrap GeoChunk's public methods.
    /// </summary>
    public interface IGeoChunk {
        #region Abstract Methods and Properties

        /// <summary>
        /// Getter Property for Base Chunk, which holds the core functionality of a Chunk.
        /// </summary>
        GeoChunkEngine GeoChunkEngine { get; }

        void InitializeGeoChunk(Vector3Int coord, IGeoEditStore geoEditStore);

        /// <summary>
        /// Called by GeoChunkEngine the first time this chunk's mesh becomes ready - either
        /// after a real march completes (see GeoChunkEngine.ReleaseLodValidation), or immediately
        /// during SetVoxels if DensityRange turned out to be skippable, meaning no mesh will ever
        /// be produced. Fires at most once per SetVoxels call - see GeoChunkEngine.SetMeshReady.
        /// Implementers should use this as their notification hook (e.g. GeoChunk forwards it to
        /// its own protected OnMeshReady) rather than tracking readiness themselves.
        /// </summary>
        void HandleMeshReady();

        #endregion

        #region Default Implementations

        Vector3Int ChunkCoord => GeoChunkEngine.ChunkCoord;

        /// <summary>
        /// Contains the smallest and largest Density. Used as a shortcut to read whether a geoChunk has any mesh at all.
        /// </summary>
        DensityRange DensityRange => GeoChunkEngine.DensityRange;

        /// <summary>
        /// Whether this chunk's voxel data has been set at least once (via SetVoxels).
        /// </summary>
        bool VoxelsReady => GeoChunkEngine.VoxelsReady;

        /// <summary>
        /// Whether this chunk's mesh is ready - see HandleMeshReady for exactly when this flips.
        /// </summary>
        bool MeshReady => GeoChunkEngine.MeshReady;

        // Known-empty march cache - see GeoChunk.IsKnownEmpty/MarkKnownEmpty for the full
        // reasoning. IsKnownEmpty is consulted before marching/subdividing at an octree address;
        // MarkKnownEmpty is recorded after a march at an address produces zero triangles. Wiped
        // wholesale on any edit to the chunk.
        bool IsKnownEmpty(int lod, Vector3Int localPosition) => GeoChunkEngine.IsKnownEmpty(lod, localPosition);

        void MarkKnownEmpty(int lod, Vector3Int localPosition) => GeoChunkEngine.MarkKnownEmpty(lod, localPosition);

        /// <summary>
        /// Method for Chunk to receive a voxel edit operation.
        /// </summary>
        void PassVoxelEditOperation(VoxelEditOperation operation) => GeoChunkEngine.IGeoEditStore.PassVoxelEditOperation(operation);

        /// <summary>
        /// Method to kick-off the Chunk being an actively managed Marching Cubes Chunk.
        /// </summary>

        void SetVoxels(Vector3Int chunkCoord, 
            BlobAssetReference<VolumeConfigBlobAsset> configBlobAsset, 
            GeoForgeDataGenerator geoDataGenerator) 
            => GeoChunkEngine.SetVoxels(geoDataGenerator.GenerateProceduralVoxels(chunkCoord, configBlobAsset));

        void SetBoundaryOverrides(Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlobAsset,
            BoundaryOverrides boundaryOverrides) 
            => GeoChunkEngine.SetOverrides(boundaryOverrides.BuildChunkOverrides(chunkCoord, configBlobAsset));

        VoxelData GetVoxelData(int index) => GeoChunkEngine.GetVoxelData(index);

        VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) =>
                GeoChunkEngine.GetVoxelDataFromVoxelPosition(position);

        bool HasVoxelData() => GeoChunkEngine.HasVoxelData();

        void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) =>
                GeoChunkEngine.BroadcastNewLeafAcrossChunks(newLeaf, pos, index);

        // Schedule-only half of LOD validation, used by GeoVolume.ValidateChunkLodsAsync to batch
        // up to ValidatesPerFrame chunks before completing march jobs once for the whole batch.
        // See GeoChunk.ScheduleOctreeLodValidation for the ordering contract with ReleaseLodValidation.
        void ScheduleOctreeLodValidation(Vector3 playerPosition) => GeoChunkEngine.ScheduleOctreeLodValidation(playerPosition);

        void ReleaseLodValidation() => GeoChunkEngine.ReleaseLodValidation();

        void OnVolumeMovement() => GeoChunkEngine.OnVolumeMovement();

        void SetOverrides(VoxelOverrides overrides) => GeoChunkEngine.SetOverrides(overrides);

        #endregion
    }
}
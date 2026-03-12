// Copyright 2025 Spellbound Studio Inc.

using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IGeoVolume {
        #region Abstract Methods and Properties

        /// <summary>
        /// Getter Property for Base Volume, which holds the core functionality of a Volume.
        /// </summary>
        GeoVolume GeoVolume { get; }

        /// <summary>
        /// Ranges for each Level of Detail and the view distance range where you see it.
        /// </summary>
        Vector2[] ViewDistanceLodRanges { get; }

        /// <summary>
        /// Target that the view distances are calculated from. Camera.Main is a good candidate for this.
        /// </summary>
        Transform LodTarget { get; }

        /// <summary>
        /// Indication of if the Volume is moving or is capable of moving, or if not. 
        /// </summary>
        bool IsMoving { get; set; }

        /// <summary>
        /// Indication of if the Volume is the primary Terrain, making it the default Volume to Query.
        /// </summary>
        bool IsPrimaryTerrain { get; set; }
        
        #endregion

        #region Default Implmentations

        Transform VolumeTransform => GeoVolume.Transform;

        (Vector3, Quaternion) SnapToGrid(Vector3 pos) => GeoVolume.SnapToGrid(pos);

        BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob => GeoVolume.ConfigBlob;

        bool IntersectsVolume(Bounds voxelBounds) => GeoVolume.IntersectsVolume(voxelBounds);

        async Awaitable ValidateChunkLods() => await GeoVolume.ValidateChunkLodsAsync();

        Vector3Int WorldToVoxelSpace(Vector3 worldPosition) => GeoVolume.WorldToVoxelSpace(worldPosition);

        IGeoChunk GetChunkByCoord(Vector3Int coord) => GeoVolume.GetChunkByCoord(coord);

        IGeoChunk GetChunkByWorldPosition(Vector3 worldPos) => GeoVolume.GetChunkByWorldPosition(worldPos);

        IGeoChunk GetChunkByVoxelPosition(Vector3Int voxelPos) => GeoVolume.GetChunkByVoxelPosition(voxelPos);

        Vector3Int GetCoordByVoxelPosition(Vector3Int voxelPos) => GeoVolume.GetCoordByVoxelPosition(voxelPos);

        #endregion
    }
}
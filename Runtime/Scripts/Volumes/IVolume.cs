// Copyright 2025 Spellbound Studio Inc.

using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IVolume {
        #region Abstract Methods and Properties

        /// <summary>
        /// Getter Property for Base Volume, which holds the core functionality of a Volume.
        /// </summary>
        BaseVolume BaseVolume { get; }

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

        /// <summary>
        /// Method to kick-off the Volume being an actively managed Marching Cubes Volume.
        /// </summary>
        void InitializeVolume();

        #endregion

        #region Default Implmentations

        Transform VolumeTransform => BaseVolume.Transform;

        (Vector3, Quaternion) SnapToGrid(Vector3 pos) => BaseVolume.SnapToGrid(pos);

        BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob => BaseVolume.ConfigBlob;

        bool IntersectsVolume(Bounds voxelBounds) => BaseVolume.IntersectsVolume(voxelBounds);

        async Awaitable ValidateChunkLods() => await BaseVolume.ValidateChunkLodsAsync();

        Vector3Int WorldToVoxelSpace(Vector3 worldPosition) => BaseVolume.WorldToVoxelSpace(worldPosition);

        IChunk GetChunkByCoord(Vector3Int coord) => BaseVolume.GetChunkByCoord(coord);

        IChunk GetChunkByWorldPosition(Vector3 worldPos) => BaseVolume.GetChunkByWorldPosition(worldPos);

        IChunk GetChunkByVoxelPosition(Vector3Int voxelPos) => BaseVolume.GetChunkByVoxelPosition(voxelPos);

        Vector3Int GetCoordByVoxelPosition(Vector3Int voxelPos) => BaseVolume.GetCoordByVoxelPosition(voxelPos);

        #endregion
    }
}
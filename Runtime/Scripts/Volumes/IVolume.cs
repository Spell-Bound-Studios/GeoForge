// Copyright 2025 Spellbound Studio Inc.

using Unity.Entities;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public interface IVolume {
        BaseVolume BaseVolume { get; }
        Vector2[] ViewDistanceLodRanges { get; }

        Transform LodTarget { get; }

        bool IsMoving { get; set; }

        bool IsPrimaryTerrain { get; set; }

        // Default Implementations

        void InitializeVolume();
        
        Transform VolumeTransform => BaseVolume.Transform;

        BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob => BaseVolume.ConfigBlob;

        bool IntersectsVolume(Bounds voxelBounds) => BaseVolume.IntersectsVolume(voxelBounds);

        async Awaitable ValidateChunkLods() => await BaseVolume.ValidateChunkLodsAsync();

        Vector3Int WorldToVoxelSpace(Vector3 worldPosition) => BaseVolume.WorldToVoxelSpace(worldPosition);

        IChunk GetChunkByCoord(Vector3Int coord) => BaseVolume.GetChunkByCoord(coord);

        IChunk GetChunkByWorldPosition(Vector3 worldPos) => BaseVolume.GetChunkByWorldPosition(worldPos);

        IChunk GetChunkByVoxelPosition(Vector3Int voxelPos) => BaseVolume.GetChunkByVoxelPosition(voxelPos);

        Vector3Int GetCoordByVoxelPosition(Vector3Int voxelPos) => BaseVolume.GetCoordByVoxelPosition(voxelPos);
    }
}
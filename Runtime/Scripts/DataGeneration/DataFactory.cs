// Copyright 2025 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public abstract class DataFactory : ScriptableObject {
        protected Vector3Int GetChunkOrigin(
            Vector3Int chunkCoord, in VolumeConfigBlobAsset config) =>
                new(
                    chunkCoord.x * config.ChunkSize + config.Offset.x,
                    chunkCoord.y * config.ChunkSize + config.Offset.y,
                    chunkCoord.z * config.ChunkSize + config.Offset.z
                );

        protected Vector3Int GetVoxelPosition(
            int index, Vector3Int chunkOrigin, in VolumeConfigBlobAsset config) {
            McStaticHelper.IndexToInt3(
                index,
                config.ChunkDataAreaSize,
                config.ChunkDataWidthSize,
                out var x, out var y, out var z
            );

            return new Vector3Int(
                chunkOrigin.x + x,
                chunkOrigin.y + y,
                chunkOrigin.z + z
            );
        }

        protected byte SignedDistanceToDensity(float signedDistance, float gradient, in VolumeConfigBlobAsset config) {
            var density = config.DensityThreshold - signedDistance * gradient;

            return (byte)Mathf.Clamp(density, byte.MinValue, byte.MaxValue);
        }

        public abstract void FillDataArray(
            Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> data);
    }
}
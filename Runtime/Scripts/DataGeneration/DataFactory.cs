// Copyright 2026 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Abstract for crude data generation. This is managed C# on the main thread which means it's very slow.
    /// </summary>
    public abstract class DataFactory : GeoForgeDataGenerator {
        protected Vector3Int GetChunkOrigin(
            Vector3Int chunkCoord, in VolumeConfigBlobAsset config) =>
                new(
                    chunkCoord.x * config.ChunkSize + config.Offset.x,
                    chunkCoord.y * config.ChunkSize + config.Offset.y,
                    chunkCoord.z * config.ChunkSize + config.Offset.z
                );

        protected Vector3Int GetVoxelPosition(
            int index, Vector3Int chunkOrigin, in VolumeConfigBlobAsset config) {
            GfStaticHelper.IndexToInt3(
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

        protected sbyte SignedDistanceToDensity(float signedDistance, float gradient) {
            var density = -signedDistance * gradient;

            return (sbyte)Mathf.Clamp(density, sbyte.MinValue, sbyte.MaxValue);
        }

        public abstract void FillDataArray(
            Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> data);

        public override NativeArray<VoxelData> GenerateProceduralVoxels(Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob, uint seed = uint.MinValue) {
            var voxels = new NativeArray<VoxelData>(
                configBlob.Value.ChunkDataVolumeSize,
                Allocator.Persistent);
            FillDataArray(chunkCoord, configBlob, voxels);
            return voxels;
        }
    }
}
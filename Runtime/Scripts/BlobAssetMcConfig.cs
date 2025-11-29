// Copyright 2025 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Blob Asset to hold Marching Cubes Settings.
    /// </summary>
    public struct McConfigBlobAsset {
        public float Resolution;
        public int ChunkSize;
        public int ChunkSizeResolution;
        public int LevelsOfDetail;
        public int CubesMarchedPerOctreeLeaf;
        public int ChunkDataWidthSize;
        public int ChunkDataAreaSize;
        public int ChunkDataVolumeSize;
        public Vector3Int ChunkCenter;
        public Vector3Int ChunkExtents;
        public byte DensityThreshold;
        public BlobArray<Vector2> LodRanges;
    }

    public static class McConfigBlobCreator {
        public static BlobAssetReference<McConfigBlobAsset>
                CreateMcConfigBlobAsset(TerrainConfig terrainConfig) {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var config = ref builder.ConstructRoot<McConfigBlobAsset>();

            // TODO: max may not be used if they clash with eachother or with this max view distance
            config.Resolution = terrainConfig.resolution;
            config.ChunkSize = terrainConfig.chunkSize;
            config.ChunkSizeResolution = Mathf.RoundToInt(terrainConfig.resolution * terrainConfig.chunkSize);
            config.LevelsOfDetail = terrainConfig.lods - 1;

            config.CubesMarchedPerOctreeLeaf = terrainConfig.cubesPerMarch;
            config.ChunkDataWidthSize = config.ChunkSize + 3;
            config.ChunkDataAreaSize = config.ChunkDataWidthSize * config.ChunkDataWidthSize;
            config.ChunkDataVolumeSize = config.ChunkDataAreaSize * config.ChunkDataWidthSize;
            config.ChunkCenter = Vector3Int.one * (1 + config.ChunkSizeResolution / 2);
            config.ChunkExtents = Vector3Int.one * config.ChunkSizeResolution;
            config.DensityThreshold = (byte)(terrainConfig.marchingCubeDensityThreshold * byte.MaxValue);

            var lodRangesBuilder =
                    builder.Allocate(ref config.LodRanges, terrainConfig.lodRanges.Length);

            for (var i = 0; i < terrainConfig.lodRanges.Length; i++) lodRangesBuilder[i] = terrainConfig.lodRanges[i];

            var result = builder.CreateBlobAssetReference<McConfigBlobAsset>(Allocator.Persistent);
            builder.Dispose();

            return result;
        }
    }
}
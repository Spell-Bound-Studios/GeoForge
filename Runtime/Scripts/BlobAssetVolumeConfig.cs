// Copyright 2025 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public struct VolumeConfigBlobAsset {
        public byte DensityThreshold;
        public int CubesMarchedPerOctreeLeaf;
        public int LevelsOfDetail;
        public int ChunkSize;
        public int ChunkDataWidthSize;
        public int ChunkDataAreaSize;
        public int ChunkDataVolumeSize;
        public float Resolution;
        public Vector3Int SizeInChunks;
    }

    public static class VolumeConfigBlobCreator {
        public static BlobAssetReference<VolumeConfigBlobAsset>
                CreateVolumeConfigBlobAsset(VoxelVolumeConfig voxelVolumeConfig) {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var config = ref builder.ConstructRoot<VolumeConfigBlobAsset>();
            config.DensityThreshold = (byte)(voxelVolumeConfig.threshold * byte.MaxValue);
            config.CubesMarchedPerOctreeLeaf = voxelVolumeConfig.cubesPerMarch;
            config.LevelsOfDetail = voxelVolumeConfig.levelsOfDetail - 1;
            config.ChunkSize = voxelVolumeConfig.ChunkSize;
            config.ChunkDataWidthSize = config.ChunkSize + 3;
            config.ChunkDataAreaSize = config.ChunkDataWidthSize * config.ChunkDataWidthSize;
            config.ChunkDataVolumeSize = config.ChunkDataAreaSize * config.ChunkDataWidthSize;
            config.Resolution = voxelVolumeConfig.resolution;
            config.SizeInChunks = voxelVolumeConfig.sizeInChunks;
            var result = builder.CreateBlobAssetReference<VolumeConfigBlobAsset>(Allocator.Persistent);
            builder.Dispose();

            return result;
        }
    }
}
// Copyright 2026 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IGeoForgeDataGenerator {
        NativeArray<VoxelData> GenerateProceduralVoxels(Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob);
    }
}
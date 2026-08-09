// Copyright 2026 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    public abstract class GeoForgeDataGenerator : ScriptableObject {
        public abstract NativeArray<VoxelData> GenerateProceduralVoxels(Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob, uint seed = uint.MinValue);
    }
}
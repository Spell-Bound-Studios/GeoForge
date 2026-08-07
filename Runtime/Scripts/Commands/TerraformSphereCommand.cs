// Copyright 2026 Spellbound Studio Inc.

using Spellbound.Core.Tooling;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Per-voxel core of TerraformSphereCommand: for every voxel in a cube bounding the sphere
    /// (side = 2*Radius+1), computes distance from VoxelCenter, applies the same falloff formula
    /// TerraformCommands.TerraformSphere used, and scatters a VoxelDensityDelta for each non-zero
    /// result. See TerraformSphereCommand for the pre-validation this depends on.
    /// </summary>
    [BurstCompile]
    internal struct TerraformSphereJob : IJobParallelFor {
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;
        public int3 VoxelCenter;
        public int Radius; // ceil(HalfSizeVoxels) - the bounding cube's half-extent
        public float HalfSizeVoxels;
        public short Delta;
        public NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>.ParallelWriter Writer;

        public void Execute(int index) {
            var side = Radius * 2 + 1;

            var dz = index / (side * side);
            var remainder = index - dz * side * side;
            var dy = remainder / side;
            var dx = remainder - dy * side;

            var offset = new int3(dx, dy, dz) - Radius;
            var dist = math.sqrt(offset.x * offset.x + offset.y * offset.y + offset.z * offset.z);

            var normalizedDist = dist - (HalfSizeVoxels - 1f);
            var falloff = 1f - math.saturate(normalizedDist);
            var scaledDelta = (int)math.round(Delta * falloff);

            if (scaledDelta == 0)
                return;

            var voxelPos = VoxelCenter + offset;

            ref var config = ref ConfigBlob.Value;

            TerraformCommandUtility.ScatterVoxelDelta(
                voxelPos, (short)scaledDelta, config.ChunkSize, config.ChunkDataAreaSize,
                config.ChunkDataWidthSize, Writer);
        }
    }

    /// <summary>
    /// Standalone, job-based terraform command: the sphere-with-falloff shape from
    /// TerraformCommands.TerraformSphere, reimplemented as a fused shape-generation-plus-chunk-fanout
    /// job. Internal - reached only through GeoForgeCommands.
    /// </summary>
    internal static class TerraformSphereCommand {
        internal static bool Execute(
            IGeoVolume geoVolume,
            Vector3 worldPosition,
            float size,
            short delta,
            byte materialIndex,
            uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError(
                    "TerraformSphereCommand: GeoForgeManager not found. Ensure it's in the current scene.");

                return false;
            }

            ref var config = ref geoVolume.ConfigBlob.Value;
            var isFiniteVolume = config.IsFiniteSize;

            var voxelCenterInt = geoVolume.WorldToVoxelSpace(worldPosition);
            var voxelCenter = new int3(voxelCenterInt.x, voxelCenterInt.y, voxelCenterInt.z);

            var halfSizeVoxels = size * 0.5f / config.Resolution;
            var radius = Mathf.CeilToInt(halfSizeVoxels);

            var paddedRadius = radius + TerraformCommandUtility.PaddingMargin;
            var minVoxel = voxelCenterInt - Vector3Int.one * paddedRadius;
            var maxVoxel = voxelCenterInt + Vector3Int.one * paddedRadius;

            if (!TerraformCommandUtility.TryValidateChunkRange(
                    geoVolume, gfManager, minVoxel, maxVoxel, nameof(TerraformSphereCommand), worldPosition,
                    out _, out _)) {
                return false;
            }

            var side = radius * 2 + 1;
            var voxelCount = side * side * side;

            var resultMap = new NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>(
                voxelCount * 8, Allocator.TempJob);

            var job = new TerraformSphereJob {
                ConfigBlob = geoVolume.ConfigBlob,
                VoxelCenter = voxelCenter,
                Radius = radius,
                HalfSizeVoxels = halfSizeVoxels,
                Delta = delta,
                Writer = resultMap.AsParallelWriter()
            };

            job.Schedule(voxelCount, 32).Complete();

            TerraformCommandUtility.DispatchEdits(
                gfManager, geoVolume, resultMap, materialIndex, allowedMaterialsMask, isFiniteVolume,
                nameof(TerraformSphereCommand));

            return true;
        }
    }
}
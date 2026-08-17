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
    /// Per-voxel core of TerraformCubeCommand: for every voxel in a uniform cube centered on
    /// VoxelCenter, scatters a VoxelDensityDelta via TerraformJobUtility.ScatterVoxelDelta -
    /// every voxel shares the same Delta, no falloff. See TerraformCubeCommand for the
    /// pre-validation this depends on.
    /// </summary>
    [BurstCompile]
    internal struct TerraformCubeJob : IJobParallelFor {
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;
        public int3 VoxelCenter;
        public int HalfExtent;
        public short Delta;
        public NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>.ParallelWriter Writer;

        public void Execute(int index) {
            var side = HalfExtent * 2 + 1;

            var dz = index / (side * side);
            var remainder = index - dz * side * side;
            var dy = remainder / side;
            var dx = remainder - dy * side;

            var offset = new int3(dx, dy, dz) - HalfExtent;
            var voxelPos = VoxelCenter + offset;

            ref var config = ref ConfigBlob.Value;

            TerraformCommandUtility.ScatterVoxelDelta(
                voxelPos, Delta, config.ChunkSize, config.ChunkDataAreaSize, config.ChunkDataWidthSize, Writer);
        }
    }

    /// <summary>
    /// Standalone, job-based terraform command: carves a uniform cube (no falloff, hard edges)
    /// centered on the nearest voxel to worldPosition. Internal - reached only through
    /// GeoForgeCommands, the public entry point for this whole job-based command family.
    /// </summary>
    internal static class TerraformCubeCommand {
        internal static bool Execute(
            IGeoVolume geoVolume,
            Vector3 worldPosition,
            int halfExtent,
            short delta,
            byte materialIndex,
            uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("TerraformCubeCommand: GeoForgeManager not found. Ensure it's in the current scene.");

                return false;
            }

            ref var config = ref geoVolume.ConfigBlob.Value;
            var isFiniteVolume = config.IsFiniteSize;

            var voxelCenterInt = geoVolume.WorldToVoxelSpace(worldPosition);
            var voxelCenter = new int3(voxelCenterInt.x, voxelCenterInt.y, voxelCenterInt.z);

            var paddedHalfExtent = halfExtent + TerraformCommandUtility.PaddingMargin;
            var minVoxel = voxelCenterInt - Vector3Int.one * paddedHalfExtent;
            var maxVoxel = voxelCenterInt + Vector3Int.one * paddedHalfExtent;

            if (!TerraformCommandUtility.TryValidateChunkRange(
                    geoVolume, gfManager, minVoxel, maxVoxel, nameof(TerraformCubeCommand), worldPosition,
                    out _, out _)) {
                return false;
            }

            var side = halfExtent * 2 + 1;
            var voxelCount = side * side * side;
            
            geoVolume.OnTerraform(worldPosition, voxelCount);

            var resultMap = new NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>(
                voxelCount * 8, Allocator.TempJob);

            var job = new TerraformCubeJob {
                ConfigBlob = geoVolume.ConfigBlob,
                VoxelCenter = voxelCenter,
                HalfExtent = halfExtent,
                Delta = delta,
                Writer = resultMap.AsParallelWriter()
            };

            job.Schedule(voxelCount, 32).Complete();

            TerraformCommandUtility.DispatchEdits(
                gfManager, geoVolume, resultMap, materialIndex, allowedMaterialsMask, isFiniteVolume,
                nameof(TerraformCubeCommand));

            return true;
        }
    }
}
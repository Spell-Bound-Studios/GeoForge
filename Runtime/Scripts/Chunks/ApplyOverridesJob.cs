// Copyright 2026 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    /// <summary>
    /// This is like a final proc gen "pass" to force whatever voxel array has been generated  will obey
    /// the Boundary Overrides.
    /// </summary>
    [BurstCompile]
    internal struct ApplyBoundaryOverridesJob : IJobParallelFor {
        internal NativeArray<VoxelData> voxelArray;

        [ReadOnly] internal NativeHashMap<int, VoxelData> xOverrides;
        [ReadOnly] internal NativeHashMap<int, VoxelData> yOverrides;
        [ReadOnly] internal NativeHashMap<int, VoxelData> zOverrides;
        [ReadOnly] internal NativeHashMap<int3, VoxelData> pointOverrides;

        [ReadOnly] internal int chunkDataAreaSize;
        [ReadOnly] internal int chunkDataWidthSize;

        [NativeDisableParallelForRestriction, WriteOnly]
        internal NativeArray<bool> hasOverrides;

        public void Execute(int i) {
            GfStaticHelper.IndexToInt3(i, chunkDataAreaSize, chunkDataWidthSize,
                out var x, out var y, out var z);

            VoxelData overrideVoxel;
            var hasOverride = false;

            if (pointOverrides.TryGetValue(new int3(x, y, z), out overrideVoxel))
                hasOverride = true;
            else if (yOverrides.TryGetValue(y, out overrideVoxel))
                hasOverride = true;
            else if (xOverrides.TryGetValue(x, out overrideVoxel))
                hasOverride = true;
            else if (zOverrides.TryGetValue(z, out overrideVoxel)) hasOverride = true;

            if (hasOverride) {
                voxelArray[i] = overrideVoxel;
                hasOverrides[0] = true;
            }
        }
    }
}
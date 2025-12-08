// Copyright 2025 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Spellbound.MarchingCubes {
    [BurstCompile]
    public struct ApplyBoundaryOverridesJob : IJobParallelFor {
        public NativeArray<VoxelData> voxelArray;

        [ReadOnly] public NativeHashMap<int, VoxelData> xOverrides;
        [ReadOnly] public NativeHashMap<int, VoxelData> yOverrides;
        [ReadOnly] public NativeHashMap<int, VoxelData> zOverrides;
        [ReadOnly] public NativeHashMap<int3, VoxelData> pointOverrides;

        [ReadOnly] public int chunkDataAreaSize;
        [ReadOnly] public int chunkDataWidthSize;

        [NativeDisableParallelForRestriction, WriteOnly]
        public NativeArray<bool> hasOverrides;

        public void Execute(int i) {
            McStaticHelper.IndexToInt3(i, chunkDataAreaSize, chunkDataWidthSize,
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
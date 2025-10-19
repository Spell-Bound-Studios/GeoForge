// Copyright 2025 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.MarchingCubes {
    [BurstCompile]
    public struct SparseToDenseVoxelDataJob : IJob {
        [ReadOnly] public NativeArray<VoxelData> Voxels;
        public NativeList<SparseVoxelData> SparseVoxels;

        public void Execute() {
            for (var i = 0; i < SparseVoxels.Length; i++) {
                var current = SparseVoxels[i];
                var isLast = i == SparseVoxels.Length - 1;
                var next = isLast ? default : SparseVoxels[i + 1];
                var runLength = GetRunLength(current, isLast, next, Voxels.Length);

                for (var j = 0; j < runLength; j++) {
                    Voxels[current.StartIndex + j] = current.Voxel;
                }
            }
        }

        private int GetRunLength(SparseVoxelData current, bool isLast, SparseVoxelData next, int totalLength) {
            if (isLast) {
                return totalLength - current.StartIndex;
            }
            return next.StartIndex - current.StartIndex;
        }
    }
}
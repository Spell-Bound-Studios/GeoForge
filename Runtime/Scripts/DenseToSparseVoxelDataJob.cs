// Copyright 2025 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.MarchingCubes {
    [BurstCompile]
    public struct DenseToSparseVoxelDataJob : IJob {
        public NativeArray<VoxelData> Voxels;
        public NativeList<SparseVoxelData> SparseVoxels;

        public void Execute() {
            SparseVoxels.Clear();
            
            var currentSparseRange = new SparseVoxelData(Voxels[0], 0);
            
            for (var i = 1; i < Voxels.Length; i++) {
                if (currentSparseRange.Voxel.Density == Voxels[i].Density
                    && currentSparseRange.Voxel.MatIndex == Voxels[i].MatIndex) {
                    continue;
                }

                SparseVoxels.Add(currentSparseRange);
                currentSparseRange = new SparseVoxelData(Voxels[i], i);
            }

            SparseVoxels.Add(currentSparseRange);
        }
    }
}
// Copyright 2025 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace SpellBound.MarchingCubes {
    [BurstCompile]
    public struct SparseDataJob : IJob {
        public NativeArray<VoxelData> Voxels;
        public NativeList<VoxelRLE> SparseVoxels;

        public void Execute() {
            SparseVoxels.Clear();
            var currentSparseRange = new VoxelRLE(Voxels[0], 0);

            for (var i = 0; i < Voxels.Length; i++) {
                if (currentSparseRange.Voxel.Density == Voxels[i].Density
                    && currentSparseRange.Voxel.MatIndex == Voxels[i].MatIndex
                    && currentSparseRange.RunLength != ushort.MaxValue) {
                    currentSparseRange.RunLength++;

                    continue;
                }

                SparseVoxels.Add(currentSparseRange);
                currentSparseRange = new VoxelRLE(Voxels[i], 1);
            }

            SparseVoxels.Add(currentSparseRange);
        }
    }

    public struct VoxelRLE {
        public VoxelData Voxel;
        public ushort RunLength;

        public VoxelRLE(VoxelData voxel, ushort runLength) {
            Voxel = voxel;
            RunLength = runLength;
        }
    }
}
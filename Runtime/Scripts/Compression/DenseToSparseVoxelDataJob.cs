// Copyright 2026 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Packs Dense VoxelData to Sparse Run-Length-Encoded. Also computes the chunk's DensityRange as a byproduct of
    /// the same single walk over Voxels - this is the only place DensityRange is computed. This
    /// job is IJob (single-threaded, not IJobParallelFor), so there's no race to guard against,
    /// and it naturally reflects the freshest data since any edits are already written into
    /// Voxels before this job runs.
    /// </summary>
    [BurstCompile]
    internal struct DenseToSparseVoxelDataJob : IJob {
        [ReadOnly] public NativeArray<VoxelData> Voxels;
        public NativeList<SparseVoxelData> SparseVoxels;
        public NativeArray<DensityRange> DensityRange; // single-element output slot

        public void Execute() {
            SparseVoxels.Clear();

            var currentSparseRange = new SparseVoxelData(Voxels[0], 0);
            var range = new DensityRange(Voxels[0].Density, Voxels[0].Density);

            for (var i = 1; i < Voxels.Length; i++) {
                if (currentSparseRange.Voxel == Voxels[i])
                    continue;

                SparseVoxels.Add(currentSparseRange);
                currentSparseRange = new SparseVoxelData(Voxels[i], i);
                range.Encapsulate(Voxels[i].Density);
            }

            SparseVoxels.Add(currentSparseRange);
            DensityRange[0] = range;
        }
    }
}
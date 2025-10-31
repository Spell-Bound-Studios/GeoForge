// Copyright 2025 Spellbound Studio Inc.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Unpacks Sparse Voxel Data to Dense.
    /// Vibecoded with chatgpt because Binary Searches and RLEs are well-known and replicable. 
    /// </summary>
    [BurstCompile]
    public struct SparseToDenseVoxelDataJob : IJobParallelFor {
        [ReadOnly] public BlobAssetReference<McConfigBlobAsset> ConfigBlob;
        [NativeDisableParallelForRestriction] public NativeArray<VoxelData> Voxels;

        [ReadOnly] public NativeList<SparseVoxelData> SparseVoxels;

        [NativeDisableParallelForRestriction] public NativeArray<DensityRange> DensityRange;

        public void Execute(int deckIndex) {
            ref var config = ref ConfigBlob.Value;
            var voxelsPerDeck = config.ChunkDataAreaSize;
            var start = deckIndex * voxelsPerDeck;
            var end = start + voxelsPerDeck;

            var rleIndex = BinarySearchForStart(start);

            while (rleIndex < SparseVoxels.Length) {
                var rle = SparseVoxels[rleIndex];
                var range = DensityRange[0];          // COPY the struct
                range.Encapsulate(rle.Voxel.Density); // Modify the COPY
                DensityRange[0] = range;
                var runStart = rle.StartIndex;

                var runEnd = rleIndex == SparseVoxels.Length - 1
                        ? Voxels.Length
                        : SparseVoxels[rleIndex + 1].StartIndex;

                if (runStart >= end) break;

                var copyStart = math.max(runStart, start);
                var copyEnd = math.min(runEnd, end);

                for (var i = copyStart; i < copyEnd; i++) Voxels[i] = rle.Voxel;

                rleIndex++;
            }
        }

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BinarySearchForStart(int targetIndex) {
            int left = 0, right = SparseVoxels.Length - 1;
            var result = 0;

            while (left <= right) {
                var mid = (left + right) / 2;
                var startIndex = SparseVoxels[mid].StartIndex;

                var nextStart = mid == SparseVoxels.Length - 1
                        ? Voxels.Length
                        : SparseVoxels[mid + 1].StartIndex;

                if (targetIndex >= startIndex && targetIndex < nextStart) return mid;

                if (targetIndex < startIndex)
                    right = mid - 1;
                else {
                    left = mid + 1;
                    result = left;
                }
            }

            return result;
        }
    }
}
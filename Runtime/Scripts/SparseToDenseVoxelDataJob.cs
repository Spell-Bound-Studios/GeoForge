// Copyright 2025 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Helper = Spellbound.Core.SpellboundStaticHelper;

namespace Spellbound.MarchingCubes {
    [BurstCompile]
    public struct SparseToDenseVoxelDataJob : IJobParallelFor {
        [NativeDisableParallelForRestriction]
        public NativeArray<VoxelData> Voxels;
        
        [ReadOnly] public NativeList<SparseVoxelData> SparseVoxels;

        public void Execute(int deckIndex) {

            var voxelsPerDeck = (Helper.ChunkSize + 3) * (Helper.ChunkSize + 3);
            var start = deckIndex * voxelsPerDeck;
            var end = start + voxelsPerDeck;

            var rleIndex = BinarySearchForStart(start);
            
            while (rleIndex < SparseVoxels.Length) {
                var rle = SparseVoxels[rleIndex];
                int runStart = rle.StartIndex;
                int runEnd = (rleIndex == SparseVoxels.Length - 1)
                    ? Voxels.Length
                    : SparseVoxels[rleIndex + 1].StartIndex;

                if (runStart >= end) break; 

                int copyStart = math.max(runStart, start);
                int copyEnd = math.min(runEnd, end);
                
                for (int i = copyStart; i < copyEnd; i++) {
                    Voxels[i] = rle.Voxel;
                }

                rleIndex++;
                
            }
        }

        private int BinarySearchForStart(int targetIndex) {
            int left = 0, right = SparseVoxels.Length - 1;
            int result = 0;

            while (left <= right) {
                int mid = (left + right) / 2;
                int startIndex = SparseVoxels[mid].StartIndex;
                int nextStart = (mid == SparseVoxels.Length - 1)
                    ? Voxels.Length
                    : SparseVoxels[mid + 1].StartIndex;

                if (targetIndex >= startIndex && targetIndex < nextStart) {
                    return mid;
                }

                if (targetIndex < startIndex) {
                    right = mid - 1;
                } else {
                    left = mid + 1;
                    result = left;
                }
            }

            return result;
        }
    }
}
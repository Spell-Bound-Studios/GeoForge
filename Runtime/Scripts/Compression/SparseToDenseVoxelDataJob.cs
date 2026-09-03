// Copyright 2026 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Unpacks Sparse Run-Length-Encoded Voxel Data to Dense. This
    /// job runs as IJobParallelFor across decks. A deck is a slice of all x and z voxels with the same y.
    /// The execution of a Deck begins with a Binary Search to find its start point on the Sparse Run-Length-Encoded
    /// data.
    ///
    /// Sized by a single ChunkDataWidthSize rather than a full VolumeConfigBlobAsset - every block this
    /// job ever decodes (a real streamed chunk, or a captured POI) is cubic, so a deck's own voxel count
    /// and the whole block's voxel count are just width^2 and width^3 - nothing else a real config asset
    /// carries (Resolution, etc.) means anything to decoding. This also makes the job correct by
    /// construction when the block being decoded ISN'T the same size as the world's own streaming chunk
    /// width - the two were never required to match. Pass whichever width the SparseVoxelData was
    /// actually encoded against: a real chunk's own configBlob.Value.ChunkDataWidthSize, or a captured
    /// POI entry's own (cubic) dimensions.x - never someone else's config just because one happens to be
    /// at hand.
    /// </summary>
    [BurstCompile]
    public struct SparseToDenseVoxelDataJob : IJobParallelFor {
        public int ChunkDataWidthSize;
        [NativeDisableParallelForRestriction] public NativeArray<VoxelData> Voxels;

        [ReadOnly] public NativeList<SparseVoxelData> SparseVoxels;

        public void Execute(int deckIndex) {
            var voxelsPerDeck = ChunkDataWidthSize * ChunkDataWidthSize;
            var chunkDataVolumeSize = voxelsPerDeck * ChunkDataWidthSize;
            var start = deckIndex * voxelsPerDeck;
            var end = start + voxelsPerDeck;

            var rleIndex = GfStaticHelper.BinarySearchVoxelData(start, chunkDataVolumeSize, SparseVoxels);

            while (rleIndex < SparseVoxels.Length) {
                var rle = SparseVoxels[rleIndex];
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
    }
}
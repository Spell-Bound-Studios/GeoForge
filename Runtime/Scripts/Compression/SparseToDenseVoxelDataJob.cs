// Copyright 2026 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Unpacks Sparse Run-Length-Encoded Voxel Data to Dense. This
    /// job runs as IJobParallelFor across decks. A deck is a slice of all x and z voxels with the same y.
    /// The execution of a Deck begins with a Binary Search to find its start point on the Sparse Run-Length-Encoded
    /// data.
    /// </summary>
    [BurstCompile]
    internal struct SparseToDenseVoxelDataJob : IJobParallelFor {
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;
        [NativeDisableParallelForRestriction] public NativeArray<VoxelData> Voxels;

        [ReadOnly] public NativeList<SparseVoxelData> SparseVoxels;

        public void Execute(int deckIndex) {
            ref var config = ref ConfigBlob.Value;
            var voxelsPerDeck = config.ChunkDataAreaSize;
            var start = deckIndex * voxelsPerDeck;
            var end = start + voxelsPerDeck;

            var rleIndex =
                    GfStaticHelper.BinarySearchVoxelData(start, ConfigBlob.Value.ChunkDataVolumeSize, SparseVoxels);

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
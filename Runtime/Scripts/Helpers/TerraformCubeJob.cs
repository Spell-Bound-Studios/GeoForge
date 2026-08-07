// Copyright 2026 Spellbound Studio Inc.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    [BurstCompile]
    internal struct TerraformCubeJob : IJobParallelFor {
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;
        public int3 VoxelCenter;
        public int HalfExtent;
        public short Delta;
        public NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>.ParallelWriter Writer;

        public void Execute(int index) {
            var side = HalfExtent * 2 + 1;

            var dz = index / (side * side);
            var remainder = index - dz * side * side;
            var dy = remainder / side;
            var dx = remainder - dy * side;

            var offset = new int3(dx, dy, dz) - HalfExtent;
            var voxelPos = VoxelCenter + offset;

            ref var config = ref ConfigBlob.Value;
            var chunkSize = config.ChunkSize;
            var chunkDataAreaSize = config.ChunkDataAreaSize;
            var chunkDataWidthSize = config.ChunkDataWidthSize;

            var centralCoord = GetChunkCoord(voxelPos, chunkSize);
            var centralLocalPos = voxelPos - centralCoord * chunkSize;

            var centralIndex = GfStaticHelper.Coord3DToIndex(
                centralLocalPos.x, centralLocalPos.y, centralLocalPos.z, chunkDataAreaSize, chunkDataWidthSize);

            Writer.Add(new ChunkCoordKey(centralCoord), new VoxelDensityDelta(centralIndex, Delta));

            for (var ndx = -1; ndx <= 1; ndx++) {
                if (!IsAxisDeltaValid(centralLocalPos.x, chunkSize, ndx)) continue;

                for (var ndy = -1; ndy <= 1; ndy++) {
                    if (!IsAxisDeltaValid(centralLocalPos.y, chunkSize, ndy)) continue;

                    for (var ndz = -1; ndz <= 1; ndz++) {
                        if (ndx == 0 && ndy == 0 && ndz == 0) continue;
                        if (!IsAxisDeltaValid(centralLocalPos.z, chunkSize, ndz)) continue;

                        var neighborCoord = centralCoord + new int3(ndx, ndy, ndz);
                        var neighborLocalPos = voxelPos - neighborCoord * chunkSize;

                        var neighborIndex = GfStaticHelper.Coord3DToIndex(
                            neighborLocalPos.x, neighborLocalPos.y, neighborLocalPos.z,
                            chunkDataAreaSize, chunkDataWidthSize);

                        Writer.Add(new ChunkCoordKey(neighborCoord), new VoxelDensityDelta(neighborIndex, Delta));
                    }
                }
            }
        }

        // No [BurstCompile] here - that attribute marks a job's Execute entry point, not an
        // arbitrary helper. Attaching it to a plain instance/static method makes Burst treat it as
        // a separate external function with its own ABI boundary, and Burst's external-call
        // convention can't pass a struct like int3 by value across that boundary - hence the
        // "structs cannot be passed to or returned from external functions" error. AggressiveInlining
        // alone is correct: it folds this method's body directly into Execute at compile time, so
        // there's no function-call boundary for that restriction to apply to.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetChunkCoord(int3 voxelPos, int chunkSize) =>
                new(
                    (int)math.floor((voxelPos.x - 1f) / chunkSize),
                    (int)math.floor((voxelPos.y - 1f) / chunkSize),
                    (int)math.floor((voxelPos.z - 1f) / chunkSize)
                );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAxisDeltaValid(int value, int chunkSize, int delta) {
            if (delta == 0) return true;
            if (delta == -1) return value < 3;

            return value >= chunkSize;
        }
    }
}
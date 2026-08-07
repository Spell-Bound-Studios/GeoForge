// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using Spellbound.Core.Tooling;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class TerraformCubeCommand {
        private static readonly uint4 AllMaterialsMask = new(uint.MaxValue);

        public static bool Execute(
            IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, short delta, byte materialIndex) =>
                Execute(geoVolume, worldPosition, halfExtent, delta, materialIndex, AllMaterialsMask);

        public static bool Execute(
            IGeoVolume geoVolume,
            Vector3 worldPosition,
            int halfExtent,
            short delta,
            byte materialIndex,
            uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("TerraformCubeCommand: GeoForgeManager not found. Ensure it's in the current scene.");

                return false;
            }

            ref var config = ref geoVolume.ConfigBlob.Value;
            var chunkSize = config.ChunkSize;
            var isFiniteVolume = config.IsFiniteSize;

            var voxelCenterInt = geoVolume.WorldToVoxelSpace(worldPosition);
            var voxelCenter = new int3(voxelCenterInt.x, voxelCenterInt.y, voxelCenterInt.z);

            const int paddingMargin = 3;
            var paddedHalfExtent = halfExtent + paddingMargin;

            var minVoxel = voxelCenterInt - Vector3Int.one * paddedHalfExtent;
            var maxVoxel = voxelCenterInt + Vector3Int.one * paddedHalfExtent;

            var minChunkCoord = geoVolume.GetCoordByVoxelPosition(minVoxel);
            var maxChunkCoord = geoVolume.GetCoordByVoxelPosition(maxVoxel);

            var chunkCountX = maxChunkCoord.x - minChunkCoord.x + 1;
            var chunkCountY = maxChunkCoord.y - minChunkCoord.y + 1;
            var chunkCountZ = maxChunkCoord.z - minChunkCoord.z + 1;
            var candidateChunkCount = chunkCountX * chunkCountY * chunkCountZ;

            var editPoolCapacity = gfManager.GetEditPoolCapacity(chunkSize);

            if (candidateChunkCount > editPoolCapacity) {
                Debug.LogWarning(
                    $"TerraformCubeCommand: rejected - action at {worldPosition} (halfExtent {halfExtent}) " +
                    $"would touch up to {candidateChunkCount} chunks, exceeding the Edit pool's capacity " +
                    $"of {editPoolCapacity} for chunk size {chunkSize}.");

                return false;
            }

            if (!isFiniteVolume) {
                for (var cz = minChunkCoord.z; cz <= maxChunkCoord.z; cz++) {
                    for (var cy = minChunkCoord.y; cy <= maxChunkCoord.y; cy++) {
                        for (var cx = minChunkCoord.x; cx <= maxChunkCoord.x; cx++) {
                            var candidateCoord = new Vector3Int(cx, cy, cz);

                            if (geoVolume.GetChunkByCoord(candidateCoord) != null)
                                continue;

                            Debug.LogWarning(
                                $"TerraformCubeCommand: rejected - action at {worldPosition} (halfExtent " +
                                $"{halfExtent}) would touch chunk {candidateCoord}, which does not exist.");

                            return false;
                        }
                    }
                }
            }

            var side = halfExtent * 2 + 1;
            var voxelCount = side * side * side;

            var resultMap = new NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>(
                voxelCount * 8, Allocator.TempJob);
            var uniqueKeysAllocated = false;
            NativeArray<ChunkCoordKey> uniqueKeys = default;

            try {
                var job = new TerraformCubeJob {
                    ConfigBlob = geoVolume.ConfigBlob,
                    VoxelCenter = voxelCenter,
                    HalfExtent = halfExtent,
                    Delta = delta,
                    Writer = resultMap.AsParallelWriter()
                };

                job.Schedule(voxelCount, 32).Complete();

                gfManager.BeginEditBatch();

                try {
                    int uniqueKeyCount;
                    (uniqueKeys, uniqueKeyCount) = resultMap.GetUniqueKeyArray(Allocator.Temp);
                    uniqueKeysAllocated = true;

                    for (var i = 0; i < uniqueKeyCount; i++) {
                        var key = uniqueKeys[i];
                        var chunkCoord = key.ToVector3Int();
                        var chunk = geoVolume.GetChunkByCoord(chunkCoord);

                        if (chunk == null) {
                            if (!isFiniteVolume) {
                                Debug.LogError(
                                    $"TerraformCubeCommand: chunk {chunkCoord} passed validation but is " +
                                    "missing at dispatch time - skipping this chunk's edits.");
                            }

                            continue;
                        }

                        var deltas = new List<VoxelDensityDelta>();

                        if (resultMap.TryGetFirstValue(key, out var delta_, out var iterator)) {
                            do {
                                deltas.Add(delta_);
                            } while (resultMap.TryGetNextValue(out delta_, ref iterator));
                        }

                        chunk.PassVoxelEditOperation(new VoxelEditOperation(materialIndex, deltas, allowedMaterialsMask));
                    }
                }
                finally {
                    gfManager.EndEditBatch();
                }
            }
            finally {
                if (uniqueKeysAllocated)
                    uniqueKeys.Dispose();

                resultMap.Dispose();
            }

            return true;
        }
    }
}
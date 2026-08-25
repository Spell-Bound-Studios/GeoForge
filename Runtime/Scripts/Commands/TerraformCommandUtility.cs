// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Shared plumbing for every job-based terraform command (TerraformCubeCommand,
    /// TerraformSphereCommand, TerraformArcCommand). Split into two regions by where the code
    /// runs: JobHelpers are Burst-safe, called from inside each command's Execute job; the rest
    /// runs on the main thread, either side of the job (pre-validation before scheduling, dispatch
    /// after completion).
    /// </summary>
    internal static class TerraformCommandUtility {
        // Apron margin matching the established ChunkDataWidthSize = ChunkSize + 3 convention - a
        // voxel near a shape's own boundary can still fan out into a neighbor chunk up to this
        // far away, so the candidate range validated against has to be at least this much wider
        // than the raw shape on every side.
        internal const int PaddingMargin = 3;

        #region Job Helpers (Burst-safe)

        // Not marked [BurstCompile] - that attribute is for job Execute entry points, not
        // arbitrary helpers. Attaching it to a method that takes/returns a struct (int3 here)
        // makes Burst treat it as an external function with its own ABI boundary, which can't
        // pass structs by value ("structs cannot be passed to or returned from external functions
        // in burst"). AggressiveInlining alone is correct: each calling job's Burst compilation
        // inlines these bodies directly, so there's no function-call boundary for that
        // restriction to hit.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 GetChunkCoord(int3 voxelPos, int chunkSize) =>
                new(
                    (int)math.floor((voxelPos.x - 1f) / chunkSize),
                    (int)math.floor((voxelPos.y - 1f) / chunkSize),
                    (int)math.floor((voxelPos.z - 1f) / chunkSize)
                );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAxisDeltaValid(int value, int chunkSize, int delta) {
            if (delta == 0) return true;
            if (delta == -1) return value < 3;

            return value >= chunkSize; // delta == 1
        }

        /// <summary>
        /// Writes one voxel's delta into its owning chunk, plus up to 7 shared-boundary neighbor
        /// chunks (mirrors GfStaticHelper.GetSharedNeighborDirections/DistributeVoxelEdits's
        /// existing fan-out exactly - same nested axis-validity checks, same enumeration order).
        /// Called once per voxel from each job's Execute - this is the entire "does this voxel
        /// belong to more than one chunk's apron" concern, isolated in one place.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ScatterVoxelDelta(
            int3 voxelPos,
            short delta,
            int chunkSize,
            int chunkDataAreaSize,
            int chunkDataWidthSize,
            NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>.ParallelWriter writer) {
            var centralCoord = GetChunkCoord(voxelPos, chunkSize);
            var centralLocalPos = voxelPos - centralCoord * chunkSize;

            var centralIndex = GfStaticHelper.Coord3DToIndex(
                centralLocalPos.x, centralLocalPos.y, centralLocalPos.z, chunkDataAreaSize, chunkDataWidthSize);

            writer.Add(new ChunkCoordKey(centralCoord), new VoxelDensityDelta(centralIndex, delta));

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

                        writer.Add(new ChunkCoordKey(neighborCoord), new VoxelDensityDelta(neighborIndex, delta));
                    }
                }
            }
        }

        #endregion

        #region Main-Thread Helpers (managed types, run either side of the job)

        /// <summary>
        /// Computes the chunk-coordinate range covering [minVoxel, maxVoxel] (which should already
        /// include the apron padding), then validates it: rejects if the range would touch more
        /// chunks than the Edit pool has capacity for, and - only for non-finite (streaming)
        /// volumes - rejects if any candidate chunk in range doesn't exist. A finite volume is
        /// fully loaded across its whole fixed extent, so a missing candidate there just means the
        /// action is digging near the edge of the map, which is expected and shouldn't reject
        /// anything - see DispatchEdits for how that's handled at dispatch time instead. Logs a
        /// warning and returns false on rejection.
        /// </summary>
        internal static bool TryValidateChunkRange(
            IGeoVolume geoVolume,
            GeoForgeManager gfManager,
            Vector3Int minVoxel,
            Vector3Int maxVoxel,
            string commandName,
            Vector3 worldPosition,
            out Vector3Int minChunkCoord,
            out Vector3Int maxChunkCoord) {
            ref var config = ref geoVolume.ConfigBlob.Value;
            var chunkSize = config.ChunkSize;
            var isFiniteVolume = config.IsFiniteSize;

            minChunkCoord = geoVolume.GetCoordByVoxelPosition(minVoxel);
            maxChunkCoord = geoVolume.GetCoordByVoxelPosition(maxVoxel);

            var chunkCountX = maxChunkCoord.x - minChunkCoord.x + 1;
            var chunkCountY = maxChunkCoord.y - minChunkCoord.y + 1;
            var chunkCountZ = maxChunkCoord.z - minChunkCoord.z + 1;
            var candidateChunkCount = chunkCountX * chunkCountY * chunkCountZ;

            var editPoolCapacity = gfManager.GetEditPoolCapacity(chunkSize);

            if (candidateChunkCount > editPoolCapacity) {
                Debug.LogWarning(
                    $"{commandName}: rejected - action at {worldPosition} would touch up to " +
                    $"{candidateChunkCount} chunks, exceeding the Edit pool's capacity of " +
                    $"{editPoolCapacity} for chunk size {chunkSize}.");

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
                                $"{commandName}: rejected - action at {worldPosition} would touch " +
                                $"chunk {candidateCoord}, which does not exist.");

                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Drains a filled multi-hashmap into one PassVoxelEditOperation call per unique chunk,
        /// wrapped in GeoForgeManager.BeginEditBatch/EndEditBatch so every affected chunk's march
        /// jobs get scheduled before any of them complete/release. Disposes resultMap and its
        /// unique-key array itself - callers must not touch resultMap again after calling this.
        /// </summary>
        internal static void DispatchEdits(
            GeoForgeManager gfManager,
            IGeoVolume geoVolume,
            Vector3 worldPosition,
            NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta> resultMap,
            byte materialIndex,
            uint4 allowedMaterialsMask,
            bool isFiniteVolume,
            string commandName) {
            var uniqueKeysAllocated = false;
            NativeArray<ChunkCoordKey> uniqueKeys = default;

            try {
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
                                // Should be unreachable for a non-finite volume - existence was
                                // already confirmed for every candidate coordinate during
                                // validation, and nothing between then and here can remove a
                                // chunk. Guarded rather than trusted blindly.
                                Debug.LogError(
                                    $"{commandName}: chunk {chunkCoord} passed validation but is " +
                                    "missing at dispatch time - skipping this chunk's edits.");
                            }

                            // For a finite volume, this is the expected/normal case whenever the
                            // action's apron-expanded range spills past the volume's boundary -
                            // not an error, just silently skip this chunk's edits.
                            continue;
                        }

                        // Presized from the exact known count, rather than growing via repeated
                        // reallocate-and-copy as entries are appended.
                        var deltas = new List<VoxelDensityDelta>(resultMap.CountValuesForKey(key));

                        if (resultMap.TryGetFirstValue(key, out var delta, out var iterator)) {
                            do {
                                deltas.Add(delta);
                            } while (resultMap.TryGetNextValue(out delta, ref iterator));
                        }

                        chunk.PassVoxelEditOperation(
                            new VoxelEditOperation(materialIndex, deltas, allowedMaterialsMask, worldPosition));
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
        }

        #endregion
    }
}
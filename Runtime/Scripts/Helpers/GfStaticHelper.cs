// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Contains constants and static methods for the GeoForge library.
    /// </summary>
    [BurstCompile]
    public static class GfStaticHelper {
        //public const int MaxLevelOfDetail = 3;
        //public const int CubesMarchedPerOctreeLeaf = 16; // must be ChunkSize >> MaxLevelOfDetail, eg: 32 /2 /2 = 8

        //public const int ChunkDataWidthSize = SpellboundStaticHelper.ChunkSize + 3;
        //public const int ChunkDataAreaSize = ChunkDataWidthSize * ChunkDataWidthSize;
        //public const int ChunkDataVolumeSize = ChunkDataWidthSize * ChunkDataWidthSize * ChunkDataWidthSize;

        //public static readonly Vector3Int ChunkCenter = Vector3Int.one * (1 + SpellboundStaticHelper.ChunkSize / 2);
        //public static readonly Vector3Int ChunkExtents = Vector3Int.one * SpellboundStaticHelper.ChunkSize;

        //public const byte DensityThreshold = 128;

        [Flags]
        public enum TransitionFaceMask {
            None = 0,
            XMin = 1 << 0,
            YMin = 1 << 1,
            ZMin = 1 << 2,
            XMax = 1 << 3,
            YMax = 1 << 4,
            ZMax = 1 << 5,
            All = ~0
        }

        public static TransitionFaceMask GetTransitionFaceMask(int index) =>
                index switch {
                    0 => TransitionFaceMask.XMin,
                    1 => TransitionFaceMask.YMin,
                    2 => TransitionFaceMask.ZMin,
                    3 => TransitionFaceMask.XMax,
                    4 => TransitionFaceMask.YMax,
                    5 => TransitionFaceMask.ZMax,
                    _ => TransitionFaceMask.XMin
                };

        public static Vector3Int GetNeighborCoord(int index, Vector3Int chunkCoord) =>
                index switch {
                    0 => chunkCoord + Vector3Int.left,
                    1 => chunkCoord + Vector3Int.down,
                    2 => chunkCoord + Vector3Int.back,
                    3 => chunkCoord + Vector3Int.right,
                    4 => chunkCoord + Vector3Int.up,
                    // 5 => chunkCoord + Vector3Int.forward, handled by the default case
                    _ => chunkCoord + Vector3Int.forward
                };

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndexToInt3(
            int index, int chunkDataAreaSize, int chunkDataWidthSize,
            out int x, out int y, out int z) {
            y = index / chunkDataAreaSize;
            z = index / chunkDataWidthSize % chunkDataWidthSize;
            x = index % chunkDataWidthSize;
        }

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Coord3DToIndex(int x, int y, int z, int chunkDataAreaSize, int chunkDataWidthSize) =>
                x + z * chunkDataWidthSize + y * chunkDataAreaSize;

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndexToInt2(int index, int chunkDataWidthSize, out int x, out int z) {
            z = index / chunkDataWidthSize;
            x = index % chunkDataWidthSize;
        }

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Coord2DToIndex(int x, int z, int chunkDataWidthSize) => x + z * chunkDataWidthSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 GetChunkOrigin(int3 chunkCoord, in VolumeConfigBlobAsset config) =>
                new(
                    chunkCoord.x * config.ChunkSize + config.Offset.x,
                    chunkCoord.y * config.ChunkSize + config.Offset.y,
                    chunkCoord.z * config.ChunkSize + config.Offset.z
                );

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BinarySearchVoxelData(
            int targetIndex, int chunkDataVolumeSize, in NativeList<SparseVoxelData> sparseVoxels) {
            int left = 0, right = sparseVoxels.Length - 1;
            var result = 0;

            while (left <= right) {
                var mid = (left + right) / 2;
                var startIndex = sparseVoxels[mid].StartIndex;

                var nextStart = mid == sparseVoxels.Length - 1
                        ? chunkDataVolumeSize
                        : sparseVoxels[mid + 1].StartIndex;

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

        /// <summary>
        /// For a local voxel position within one chunk's padded data window (size chunkSize + 3
        /// per axis, 3-voxel padding on each side), computes which of the 26 neighbor directions
        /// (dx,dy,dz each in {-1,0,1}, excluding (0,0,0)) also contain that same local position in
        /// their own padded window - i.e. which neighbor chunks an edit at this position needs to
        /// fan out to. Replaces the old SharedIndicesAcrossChunks precomputed dictionary (built
        /// once per registered chunk size, ~300k entries for chunkSize 128) with direct arithmetic:
        /// whether a given axis allows a non-zero delta is independent per axis and purely a
        /// function of how close that axis's coordinate is to a boundary - value &lt; 3 allows -1,
        /// value &gt;= chunkSize allows +1, and 0 is always allowed on every axis. A direction is
        /// valid only when every axis's chosen delta is independently valid (mirroring the original
        /// BoundsInt.Contains check, which tests all three axes simultaneously). Enumeration order
        /// matches the original's nested dx -&gt; dy -&gt; dz loop exactly.
        /// results is cleared and refilled each call - pass a reusable list to avoid repeated
        /// per-edit allocation in a hot loop.
        /// </summary>
        public static void GetSharedNeighborDirections(Vector3Int localPos, int chunkSize, List<Vector3Int> results) {
            results.Clear();

            for (var dx = -1; dx <= 1; dx++) {
                if (!IsAxisDeltaValid(localPos.x, chunkSize, dx)) continue;

                for (var dy = -1; dy <= 1; dy++) {
                    if (!IsAxisDeltaValid(localPos.y, chunkSize, dy)) continue;

                    for (var dz = -1; dz <= 1; dz++) {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        if (!IsAxisDeltaValid(localPos.z, chunkSize, dz)) continue;

                        results.Add(new Vector3Int(dx, dy, dz));
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAxisDeltaValid(int value, int chunkSize, int delta) =>
                delta switch {
                    0 => true,
                    -1 => value < 3,
                    1 => value >= chunkSize,
                    _ => false
                };
    }
}
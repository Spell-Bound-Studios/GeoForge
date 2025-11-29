// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Contains constants and static methods for the Marching Cubes library.
    /// </summary>
    [BurstCompile]
    public static class McStaticHelper {
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

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearchVoxelData(
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
    }
}
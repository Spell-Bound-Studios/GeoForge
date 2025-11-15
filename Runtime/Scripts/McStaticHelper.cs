// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Linq;
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

        public static int GetLod(float distance, Vector2[] lodRanges) {
            for (var i = 0; i < lodRanges.Length; i++) {
                if (distance <= lodRanges[i].y)
                    return i;
            }

            // If distance is beyond all ranges, return -1
            return -1;
        }

        public static Vector3Int WorldToChunk(Vector3 pos, int chunkSize) =>
                new(
                    Mathf.FloorToInt((pos.x - 1) / chunkSize),
                    Mathf.FloorToInt((pos.y - 1) / chunkSize),
                    Mathf.FloorToInt((pos.z - 1) / chunkSize)
                );

        /*
        private static readonly int2[] CornerPositions = {
            new(1, 1),
            new(1, ChunkDataWidthSize - 2),
            new(ChunkDataWidthSize - 2, 1),
            new(ChunkDataWidthSize - 2, ChunkDataWidthSize - 2)
        };

        public static readonly int[] CornerIndices = {
            Coord2DToIndex(CornerPositions[0].x, CornerPositions[0].y),
            Coord2DToIndex(CornerPositions[1].x, CornerPositions[1].y),
            Coord2DToIndex(CornerPositions[2].x, CornerPositions[2].y),
            Coord2DToIndex(CornerPositions[3].x, CornerPositions[3].y)
        };
        */

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

        public static List<MaterialType> GetAllMaterialTypes() =>
                Enum.GetValues(typeof(MaterialType)).Cast<MaterialType>().ToList();
        
        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearchVoxelData(int targetIndex, int chunkDataVolumeSize, in NativeList<SparseVoxelData> sparseVoxels) {
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
        /// This is meant to be called externally so that a reference to a chunk can be retrieved from game logic.
        /// </summary>
        public static List<Vector3Int> GetChunksTouchingPosition(Vector3Int position, int chunksize) {
            //Should be able to create a map to do this efficiently instead of bruteforce
            var chunkKeys = new HashSet<Vector3Int>();

            // Compute base chunk coord (floor division handles negatives correctly)
            var baseCoord = WorldToChunk(position, chunksize);

            for (var dx = -1; dx <= 1; dx++) {
                for (var dy = -1; dy <= 1; dy++) {
                    for (var dz = -1; dz <= 1; dz++) {
                        var neighborCoord = baseCoord + new Vector3Int(dx, dy, dz);
                        chunkKeys.Add(neighborCoord);
                    }
                }
            }

            return chunkKeys.ToList();
        }
    }
}
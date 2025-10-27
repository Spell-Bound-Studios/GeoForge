// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Spellbound.Core;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Contains constants and static methods for the Marching Cubes library.
    /// </summary>
    [BurstCompile]
    public static class McStaticHelper {
        public const int MaxLevelOfDetail = 3;
        public const int CubesMarchedPerOctreeLeaf = 16; // must be ChunkSize >> MaxLevelOfDetail, eg: 32 /2 /2 = 8

        public const int ChunkDataWidthSize = SpellboundStaticHelper.ChunkSize + 3;
        public const int ChunkDataAreaSize = ChunkDataWidthSize * ChunkDataWidthSize;
        public const int ChunkDataVolumeSize = ChunkDataWidthSize * ChunkDataWidthSize * ChunkDataWidthSize;

        public const byte DensityThreshold = 128;

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


        public static int GetCoarsestLod(float distance, Vector2[] lodRanges) {
            for (var i = lodRanges.Length - 1; i >= 0; i--) {
                var range = lodRanges[i];

                if (distance >= range.x && distance <= range.y) return i;
            }

            return lodRanges.Length - 1;
        }

        public static int GetFinestLod(float distance, Vector2[] lodRanges) {
            for (var i = 0; i < lodRanges.Length; i++) {
                var range = lodRanges[i];

                if (distance <= range.y) return i;
            }

            return 0;
        }

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

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndexToInt3(int index, out int x, out int y, out int z) {
            y = index / ChunkDataAreaSize;
            z = index / ChunkDataWidthSize % ChunkDataWidthSize;
            x = index % ChunkDataWidthSize;
        }

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Coord3DToIndex(int x, int y, int z) => x + z * ChunkDataWidthSize + y * ChunkDataAreaSize;

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndexToInt2(int index, out int x, out int z) {
            z = index / ChunkDataWidthSize;
            x = index % ChunkDataWidthSize;
        }

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Coord2DToIndex(int x, int z) => x + z * ChunkDataWidthSize;

        public static List<MaterialType> GetAllMaterialTypes() =>
                Enum.GetValues(typeof(MaterialType)).Cast<MaterialType>().ToList();
        
        /// <summary>
        /// This is meant to be called externally so that a reference to a chunk can be retrieved from game logic.
        /// </summary>
        public static List<Vector3Int> GetChunksTouchingPosition(Vector3Int position) {
            //Should be able to create a map to do this efficiently instead of bruteforce
            var chunkKeys = new HashSet<Vector3Int>();

            // Compute base chunk coord (floor division handles negatives correctly)
            var baseCoord = SpellboundStaticHelper.WorldToChunk(position);

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
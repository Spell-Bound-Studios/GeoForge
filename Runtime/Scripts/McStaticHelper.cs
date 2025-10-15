// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Helper = SpellBound.Core.SpellBoundStaticHelper;

namespace SpellBound.MarchingCubes {
    [BurstCompile]
    public static class McStaticHelper {
        public const int MaxLevelOfDetail = 3;
        public const int CubesMarchedPerOctreeLeaf = 16; // must be Chunksize >> MaxLevelOfDetail, eg: 32 /2 /2 = 8

        public const int ChunkDataWidthSize = Helper.ChunkSize + 3;
        public const int ChunkDataAreaSize = ChunkDataWidthSize * ChunkDataWidthSize;
        public const int ChunkDataVolumeSize = ChunkDataWidthSize * ChunkDataWidthSize * ChunkDataWidthSize;
        public const float LowFrequencyPass = 0.006f;
        public const float DensityFrequencyPass = 0.55f;
        public const float ContinentalAmplitude = 10f;

        public const int PoiSize = 40;
        public const int PoiChanceInv = 10;

        public const float marchingCubesTransitionThickness = 0;

        public static readonly byte[] NegX = { 0, 2, 4, 6 };
        public static readonly byte[] PosX = { 1, 3, 5, 7 };
        public static readonly byte[] NegY = { 0, 1, 2, 3 };
        public static readonly byte[] PosY = { 4, 5, 6, 7 };
        public static readonly byte[] NegZ = { 0, 1, 4, 5 };
        public static readonly byte[] PosZ = { 2, 3, 6, 7 };

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

        public const float SmallNumber = 0.25f;
        public static readonly int3 Padding = new(1, 1, 1);
        public static readonly int3 ChunkMinima = Padding;
        public static readonly int3 ChunkMaxima = new int3(1, 1, 1) * (1 + Helper.ChunkSize);
        public static readonly Vector3Int ChunkMinimaAsV3 = new(ChunkMinima.x, ChunkMinima.y, ChunkMinima.z);
        public static readonly Vector3Int ChunkMaximaAsV3 = new(ChunkMaxima.x, ChunkMaxima.y, ChunkMaxima.z);
        public static readonly float3 ChunkStringentMinima = (float3)Padding * 1.01f;
        public static readonly float3 ChunkStringentMaxima = (float3)Padding * (0.99f + Helper.ChunkSize);
        public static readonly Vector3Int ChunkCenter = Vector3Int.one * (1 + Helper.ChunkSize / 2);
        public static readonly Vector3Int ChunkExtents = Vector3Int.one * Helper.ChunkSize;

        public static readonly int2[] CornerPositions = {
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

        public const byte DensityThreshold = 128;

        public const int UnderdarkThreshold = -150;
        public const int SeaLevel = 0;
        public const int MountainThreshold = 150;

        public static long HashStringToLong(string str) {
            unchecked {
                long hash = 5381;

                foreach (var c in str)
                        // hash * 33 + c
                    hash = (hash << 5) + hash + c;

                return hash;
            }
        }

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
    }
}
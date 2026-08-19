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
    /// Grab bag of public static helper methods. 
    /// </summary>
    [BurstCompile]
    public static class GfStaticHelper {
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

        public static bool GeoRaycast(Vector3 origin, Vector3 direction, out IGeoVolume geoVolume, float maxDistance,
            LayerMask layerMask) {
            geoVolume = null;
            if (!Physics.Raycast(origin, direction, out var hit, maxDistance, layerMask))
                return false;
            geoVolume = hit.collider.GetComponentInParent<IGeoVolume>();
            return geoVolume != null;
        }
    }
}
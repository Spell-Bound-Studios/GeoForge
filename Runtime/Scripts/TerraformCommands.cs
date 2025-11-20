// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public static class TerraformCommands {
        public static void RemoveSphere(
            Vector3 position, List<MaterialType> diggableMaterialTypes, float radius, int delta) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var marchingCubesManager))
                return;

            ref var config = ref marchingCubesManager.McConfigBlob.Value;

            var rawVoxelEdits = new List<RawVoxelEdit>();

            // Convert everything to normalized voxel space (multiply by 1/resolution)
            var normalizedPosition = position / config.Resolution;
            var normalizedRadius = radius / config.Resolution;

            // Now work entirely in normalized space
            var center = normalizedPosition;
            var r = Mathf.CeilToInt(normalizedRadius);
            var radiusSq = normalizedRadius * normalizedRadius;

            for (var x = -r; x <= r; x++) {
                for (var y = -r; y <= r; y++) {
                    for (var z = -r; z <= r; z++) {
                        // Voxel position in normalized space
                        var voxelPos = new Vector3Int(
                            Mathf.RoundToInt(center.x) + x,
                            Mathf.RoundToInt(center.y) + y,
                            Mathf.RoundToInt(center.z) + z
                        );

                        // Distance from exact center (not rounded)
                        var offset = new Vector3(
                            voxelPos.x - center.x,
                            voxelPos.y - center.y,
                            voxelPos.z - center.z
                        );

                        var distSq = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                        if (distSq > radiusSq)
                            continue;

                        var dist = Mathf.Sqrt(distSq);
                        var falloff = 1f - dist / normalizedRadius;
                        var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                        if (adjustedDelta != 0)
                            rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, -adjustedDelta, 0));
                    }
                }
            }

            marchingCubesManager.DistributeVoxelEdits(rawVoxelEdits, diggableMaterialTypes.ToHashSet());
        }

        public static void AddSphere(
            Vector3 position, MaterialType addedMaterial, float radius, int delta) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var marchingCubesManager))
                return;

            ref var config = ref marchingCubesManager.McConfigBlob.Value;

            var rawVoxelEdits = new List<RawVoxelEdit>();

            // Convert everything to normalized voxel space (multiply by 1/resolution)
            var normalizedPosition = position / config.Resolution;
            var normalizedRadius = radius / config.Resolution;

            // Now work entirely in normalized space
            var center = normalizedPosition;
            var r = Mathf.CeilToInt(normalizedRadius);
            var radiusSq = normalizedRadius * normalizedRadius;

            for (var x = -r; x <= r; x++) {
                for (var y = -r; y <= r; y++) {
                    for (var z = -r; z <= r; z++) {
                        // Voxel position in normalized space
                        var voxelPos = new Vector3Int(
                            Mathf.RoundToInt(center.x) + x,
                            Mathf.RoundToInt(center.y) + y,
                            Mathf.RoundToInt(center.z) + z
                        );

                        // Distance from exact center (not rounded)
                        var offset = new Vector3(
                            voxelPos.x - center.x,
                            voxelPos.y - center.y,
                            voxelPos.z - center.z
                        );

                        var distSq = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                        if (distSq > radiusSq)
                            continue;

                        var dist = Mathf.Sqrt(distSq);
                        var falloff = 1f - dist / normalizedRadius;
                        var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                        if (adjustedDelta != 0)
                            rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, adjustedDelta, addedMaterial));
                    }
                }
            }

            marchingCubesManager.DistributeVoxelEdits(rawVoxelEdits);
        }
    }
}
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

            var rawVoxelEdits = new List<RawVoxelEdit>();
            var center = Vector3Int.RoundToInt(position);
            var r = Mathf.CeilToInt(radius);
            var radiusSq = radius * radius; // Use squared distance to avoid sqrt

            for (var x = -r; x <= r; x++) {
                for (var y = -r; y <= r; y++) {
                    for (var z = -r; z <= r; z++) {
                        var offset = new Vector3Int(x, y, z);
                        var distSq = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                        if (distSq > radiusSq)
                            continue;

                        var dist = Mathf.Sqrt(distSq); // Only calculate sqrt when needed
                        var falloff = 1f - dist / radius;
                        var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                        if (adjustedDelta != 0) rawVoxelEdits.Add(new RawVoxelEdit(center + offset, -adjustedDelta, 0));
                    }
                }
            }

            marchingCubesManager.DistributeVoxelEdits(rawVoxelEdits, diggableMaterialTypes.ToHashSet());
        }

        public static void AddSphere(
            Vector3 position, MaterialType addedMaterial, float radius, int delta) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var marchingCubesManager))
                return;

            var rawVoxelEdits = new List<RawVoxelEdit>();
            var center = Vector3Int.RoundToInt(position);
            var r = Mathf.CeilToInt(radius);
            var radiusSq = radius * radius; // Use squared distance to avoid sqrt

            for (var x = -r; x <= r; x++) {
                for (var y = -r; y <= r; y++) {
                    for (var z = -r; z <= r; z++) {
                        var offset = new Vector3Int(x, y, z);
                        var distSq = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                        if (distSq > radiusSq)
                            continue;

                        var dist = Mathf.Sqrt(distSq); // Only calculate sqrt when needed
                        var falloff = 1f - dist / radius;
                        var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                        if (adjustedDelta != 0)
                            rawVoxelEdits.Add(new RawVoxelEdit(center + offset, adjustedDelta, addedMaterial));
                    }
                }
            }

            marchingCubesManager.DistributeVoxelEdits(rawVoxelEdits);
        }
    }
}
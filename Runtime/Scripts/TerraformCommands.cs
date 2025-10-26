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

            for (var x = -r; x <= r; x++) {
                for (var y = -r; y <= r; y++) {
                    for (var z = -r; z <= r; z++) {
                        var offset = new Vector3Int(x, y, z);
                        var voxelPos = center + offset;
                        var dist = Vector3.Distance(position, voxelPos);

                        if (!(dist <= radius))
                            continue;

                        var falloff = 1f - dist / radius;
                        var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                        if (adjustedDelta != 0)
                            rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, -adjustedDelta, 0));
                    }
                }
            }

            marchingCubesManager.DistributeVoxelEdits(rawVoxelEdits, diggableMaterialTypes.ToHashSet());
        }
    }
}
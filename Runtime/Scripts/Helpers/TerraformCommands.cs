// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Terraform commands. Internal and "DRY". Should be accessed through the public GeoForgeStatic class.
    /// </summary>
    internal static class TerraformCommands {
        internal static (List<RawVoxelEdit> edits, Bounds bounds) TerraformSphere(
            IGeoVolume iGeoVoxelVolume,
            Vector3 worldPosition,
            float size,
            short delta,
            HashSet<byte> materials) {
            var voxelCenter = iGeoVoxelVolume.WorldToVoxelSpace(worldPosition);
            var halfSizeVoxels = size * 0.5f / iGeoVoxelVolume.ConfigBlob.Value.Resolution;
            var r = Mathf.CeilToInt(halfSizeVoxels);

            var diameter = 2 * r + 1;
            var rawVoxelEdits = new List<RawVoxelEdit>(diameter * diameter * diameter);

            for (var x = -r; x <= r; x++)
            for (var y = -r; y <= r; y++)
            for (var z = -r; z <= r; z++) {
                var dist = Mathf.Sqrt(x * x + y * y + z * z);
                var voxelPos = voxelCenter + new Vector3Int(x, y, z);

                var normalizedDist = dist - (halfSizeVoxels - 1f); 
                var falloff = 1f - Mathf.Clamp01(normalizedDist);
                var scaledDelta = Mathf.RoundToInt(delta * falloff);
                
                rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, (short)scaledDelta, materials.FirstOrDefault()));
            }

            var voxelBounds = new Bounds(voxelCenter, Vector3.one * halfSizeVoxels * 2f);

            return (rawVoxelEdits, voxelBounds);
        }
    }
}
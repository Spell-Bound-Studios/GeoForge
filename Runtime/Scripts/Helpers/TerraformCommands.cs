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
            IVolume iVoxelVolume,
            Vector3 worldPosition,
            float size,
            int delta,
            HashSet<byte> materials) {
            var voxelCenter = iVoxelVolume.WorldToVoxelSpace(worldPosition);
            var halfSizeVoxels = size * 0.5f / iVoxelVolume.ConfigBlob.Value.Resolution;
            var r = Mathf.CeilToInt(halfSizeVoxels);

            var diameter = 2 * r + 1;
            var rawVoxelEdits = new List<RawVoxelEdit>(diameter * diameter * diameter);

            for (var x = -r; x <= r; x++)
            for (var y = -r; y <= r; y++)
            for (var z = -r; z <= r; z++) {
                var dist = Mathf.Sqrt(x * x + y * y + z * z);
                var voxelPos = voxelCenter + new Vector3Int(x, y, z);
                var chunk = iVoxelVolume.GetChunkByVoxelPosition(voxelPos);

                if (chunk == null)
                    continue;

                var voxelData = chunk.GetVoxelDataFromVoxelPosition(voxelPos);

                if (delta < 0 && !materials.Contains(voxelData.MaterialIndex))
                    continue;

                var normalizedDist = dist - (halfSizeVoxels - 1f); 
                var falloff = 1f - Mathf.Clamp01(normalizedDist);
                var scaledDelta = Mathf.RoundToInt(delta * falloff);
                var newDensity = (byte)Mathf.Clamp(voxelData.Density + scaledDelta, byte.MinValue, byte.MaxValue);

                var newMaterial = byte.MaxValue - voxelData.Density  > scaledDelta * 3
                        ? voxelData.MaterialIndex
                        : materials.First();
                
                rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, newDensity, newMaterial));
            }

            var voxelBounds = new Bounds(voxelCenter, Vector3.one * halfSizeVoxels * 2f);

            return (rawVoxelEdits, voxelBounds);
        }
    }
}
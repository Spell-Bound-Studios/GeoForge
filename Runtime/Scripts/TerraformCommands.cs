// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public static class TerraformCommands {
        public static Func<IVolume, (List<RawVoxelEdit> edits, Bounds bounds)> RemoveSphere(
            Vector3 worldPosition,
            float radius,
            int delta) =>
                (iVoxelVolume) => {
                    // Convert world position to volume-local space
                    var voxelCenter = iVoxelVolume.VoxelVolume.WorldToVoxelSpace(worldPosition);

                    var rawVoxelEdits = new List<RawVoxelEdit>();
                    var radiusVoxels = radius / iVoxelVolume.VoxelVolume.ConfigBlob.Value.Resolution;

                    var r = Mathf.CeilToInt(radiusVoxels);
                    var radiusSq = radiusVoxels * radiusVoxels;

                    for (var x = -r; x <= r; x++) {
                        for (var y = -r; y <= r; y++) {
                            for (var z = -r; z <= r; z++) {
                                // Voxel position in volume-relative voxel space
                                var voxelPos = new Vector3Int(
                                    Mathf.RoundToInt(voxelCenter.x) + x,
                                    Mathf.RoundToInt(voxelCenter.y) + y,
                                    Mathf.RoundToInt(voxelCenter.z) + z
                                );

                                // Distance from exact center (not rounded)
                                var offset = new Vector3(
                                    voxelPos.x - voxelCenter.x,
                                    voxelPos.y - voxelCenter.y,
                                    voxelPos.z - voxelCenter.z
                                );

                                var distSq = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                                if (distSq > radiusSq)
                                    continue;

                                var dist = Mathf.Sqrt(distSq);
                                var falloff = 1f - dist / radiusVoxels;
                                var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                                if (adjustedDelta != 0)
                                    rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, -adjustedDelta, 0));
                            }
                        }
                    }

                    // Create bounds in world space centered on the world position
                    var worldBounds = new Bounds(worldPosition, Vector3.one * radius * 2f);

                    return (rawVoxelEdits, worldBounds);
                };

        public static Func<IVolume, (List<RawVoxelEdit> edits, Bounds bounds)> AddSphere(
            Vector3 worldPosition,
            byte addedMaterial,
            float radius,
            int delta) =>
                (iVoxelVolume) => {
                    // Convert world position to volume-local space
                    var voxelCenter = iVoxelVolume.VoxelVolume.WorldToVoxelSpace(worldPosition);

                    var rawVoxelEdits = new List<RawVoxelEdit>();
                    var radiusVoxels = radius / iVoxelVolume.VoxelVolume.ConfigBlob.Value.Resolution;

                    var r = Mathf.CeilToInt(radiusVoxels);
                    var radiusSq = radiusVoxels * radiusVoxels;

                    for (var x = -r; x <= r; x++) {
                        for (var y = -r; y <= r; y++) {
                            for (var z = -r; z <= r; z++) {
                                var voxelPos = new Vector3Int(
                                    Mathf.RoundToInt(voxelCenter.x) + x,
                                    Mathf.RoundToInt(voxelCenter.y) + y,
                                    Mathf.RoundToInt(voxelCenter.z) + z
                                );

                                var offset = new Vector3(
                                    voxelPos.x - voxelCenter.x,
                                    voxelPos.y - voxelCenter.y,
                                    voxelPos.z - voxelCenter.z
                                );

                                var distSq = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                                if (distSq > radiusSq)
                                    continue;

                                var dist = Mathf.Sqrt(distSq);
                                var falloff = 1f - dist / radiusVoxels;
                                var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                                if (adjustedDelta != 0)
                                    rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, adjustedDelta, addedMaterial));
                            }
                        }
                    }

                    // Create bounds in world space centered on the world position
                    var worldBounds = new Bounds(worldPosition, Vector3.one * radius * 2f);

                    return (rawVoxelEdits, worldBounds);
                };
    }
}
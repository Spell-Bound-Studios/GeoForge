// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public static class TerraformCommands {
        public static (List<RawVoxelEdit> edits, Bounds bounds) RemoveSphere(
            IVolume iVoxelVolume,
            Vector3 worldPosition,
            float radius,
            int delta) {
            var voxelCenter = iVoxelVolume.WorldToVoxelSpace(worldPosition);

            var rawVoxelEdits = new List<RawVoxelEdit>();
            var radiusVoxels = radius / iVoxelVolume.ConfigBlob.Value.Resolution;

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
                            rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, -adjustedDelta, 0));
                    }
                }
            }

            var voxelBounds = new Bounds(voxelCenter, Vector3.one * radiusVoxels * 2f);

            return (rawVoxelEdits, voxelBounds);
        }

        public static (List<RawVoxelEdit> edits, Bounds bounds) AddSphere(
            IVolume iVoxelVolume,
            Vector3 worldPosition,
            byte addedMaterial,
            float radius,
            int delta) {
            var voxelCenter = iVoxelVolume.WorldToVoxelSpace(worldPosition);

            var rawVoxelEdits = new List<RawVoxelEdit>();
            var radiusVoxels = radius / iVoxelVolume.ConfigBlob.Value.Resolution;

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

            var voxelBounds = new Bounds(voxelCenter, Vector3.one * radiusVoxels * 2f);

            return (rawVoxelEdits, voxelBounds);
        }
    }
}
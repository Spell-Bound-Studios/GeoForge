// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public static class TerraformCommands {
        public static (List<RawVoxelEdit> edits, Bounds bounds) RemoveCube(
    IVolume iVoxelVolume,
    Vector3 worldPosition,
    Vector3 direction,
    float size,
    int delta) {
    var voxelCenter = iVoxelVolume.WorldToVoxelSpace(worldPosition);

    var rawVoxelEdits = new List<RawVoxelEdit>();
    var halfSizeVoxels = (size * 0.5f) / iVoxelVolume.ConfigBlob.Value.Resolution;

    var r = Mathf.CeilToInt(halfSizeVoxels);
    
    // Create rotation from direction
    var rotation = Quaternion.LookRotation(direction.normalized);
    
    // Define how many voxels from the edge should have falloff (1 or 2)
    var falloffVoxels = 1f; // Adjust this value (1.0f for 1 voxel, 2.0f for 2 voxels)

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

                // Rotate offset to cube's local space
                var localOffset = Quaternion.Inverse(rotation) * offset;

                // Check if voxel is within cube bounds
                if (Mathf.Abs(localOffset.x) > halfSizeVoxels ||
                    Mathf.Abs(localOffset.y) > halfSizeVoxels ||
                    Mathf.Abs(localOffset.z) > halfSizeVoxels)
                    continue;

                // Calculate falloff only for edge voxels
                var maxOffset = Mathf.Max(Mathf.Abs(localOffset.x), Mathf.Abs(localOffset.y), Mathf.Abs(localOffset.z));
                var distanceFromEdge = halfSizeVoxels - maxOffset;
                
                float falloff;
                if (distanceFromEdge >= falloffVoxels) {
                    // Full strength for interior voxels
                    falloff = 1f;
                } else {
                    // Falloff only in the outer layer(s)
                    falloff = distanceFromEdge / falloffVoxels;
                }
                
                var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                if (adjustedDelta != 0)
                    rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, -adjustedDelta, 0));
            }
        }
    }

    var voxelBounds = new Bounds(voxelCenter, Vector3.one * halfSizeVoxels * 2f);

    return (rawVoxelEdits, voxelBounds);
}

public static (List<RawVoxelEdit> edits, Bounds bounds) AddCube(
    IVolume iVoxelVolume,
    Vector3 worldPosition,
    Vector3 direction,
    byte addedMaterial,
    float size,
    int delta) {
    var voxelCenter = iVoxelVolume.WorldToVoxelSpace(worldPosition);

    var rawVoxelEdits = new List<RawVoxelEdit>();
    var halfSizeVoxels = (size * 0.5f) / iVoxelVolume.ConfigBlob.Value.Resolution;

    var r = Mathf.CeilToInt(halfSizeVoxels);
    
    // Create rotation from direction
    var rotation = Quaternion.LookRotation(direction.normalized);
    
    // Define how many voxels from the edge should have falloff (1 or 2)
    var falloffVoxels = 1f; // Adjust this value (1.0f for 1 voxel, 2.0f for 2 voxels)

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

                // Rotate offset to cube's local space
                var localOffset = Quaternion.Inverse(rotation) * offset;

                // Check if voxel is within cube bounds
                if (Mathf.Abs(localOffset.x) > halfSizeVoxels ||
                    Mathf.Abs(localOffset.y) > halfSizeVoxels ||
                    Mathf.Abs(localOffset.z) > halfSizeVoxels)
                    continue;

                // Calculate falloff only for edge voxels
                var maxOffset = Mathf.Max(Mathf.Abs(localOffset.x), Mathf.Abs(localOffset.y), Mathf.Abs(localOffset.z));
                var distanceFromEdge = halfSizeVoxels - maxOffset;
                
                float falloff;
                if (distanceFromEdge >= falloffVoxels) {
                    // Full strength for interior voxels
                    falloff = 1f;
                } else {
                    // Falloff only in the outer layer(s)
                    falloff = distanceFromEdge / falloffVoxels;
                }
                
                var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                if (adjustedDelta != 0)
                    rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, adjustedDelta, addedMaterial));
            }
        }
    }

    var voxelBounds = new Bounds(voxelCenter, Vector3.one * halfSizeVoxels * 2f);

    return (rawVoxelEdits, voxelBounds);
}
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
// Copyright 2026 Spellbound Studio Inc.

using Spellbound.Core.Tooling;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// DX Library for GeoForge Usage
    /// </summary>
    public static class GeoForgeStatic {
        // uint4 can't be used as a C# default parameter value (not a compile-time constant), so
        // the unrestricted-mask overloads below forward to the full method with this value
        // explicitly, rather than declaring it as a default argument.
        private static readonly uint4 AllMaterialsMask = new(uint.MaxValue);

        /// <summary>
        /// Check for GeoForgeManager being in the scene.
        /// </summary>
        public static bool IsInitialized() => SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out _);

        /// <summary>
        /// Check to facilitate not falling thru the terrain if your collider slips under the terrain collider.
        /// Returns false whenever nothing is actually queryable (no manager, no primary volume, or
        /// no loaded chunk at this position) - never treats "couldn't query" as "must be air."
        /// </summary>
        public static bool IsInsideTerrain(Vector3 position) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager))
                return false;

            if (!gfManager.TryQueryVoxel(position, out var voxelData, out _))
                return false;

            return voxelData.Density >= 0;
        }
    }
}
// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// DX Library for GeoForge Usage
    /// </summary>
    public static class GeoForgeStatic {
        /// <summary>
        /// Check for GeoForgeManager being in the scene.
        /// </summary>
        public static bool IsInitialized() => SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out _);

        /// <summary>
        /// Check for GeoForgeManager being active in the scene
        /// </summary>
        public static bool IsActive() {
            var gfManager = SingletonManager.GetSingletonInstance<GeoForgeManager>();

            return gfManager.IsActive();
        }

        /// <summary>
        /// Check to facilitate not falling thru the terrain if your collider slips under the terrain collider.
        /// </summary>
        public static bool IsInsideTerrain(Vector3 position) {
            var gfManager = SingletonManager.GetSingletonInstance<GeoForgeManager>();
            var voxelData = gfManager.QueryVoxel(position, out var volume);

            return voxelData.Density >= volume.ConfigBlob.Value.DensityThreshold;
        }

        /// <summary>
        /// Public method to Remove or "Dig-into" a spherical region in one specific GeoForge Volume.
        /// </summary>
        public static void RemoveSphere(
            RaycastHit hit, float radius, int delta, List<byte> materials = null) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            var iVolume = hit.collider.transform.GetComponentInParent<IVolume>();

            if (iVolume == null)
                return;

            var matHashSet = materials == null ? gfManager.GetAllMaterials() : materials.ToHashSet();
            var results = TerraformCommands.TerraformSphere(iVolume, hit.point, radius, -delta, matHashSet);
            gfManager.DistributeVoxelEdits(iVolume, results.edits);
        }

        /// <summary>
        /// Public method to Remove or "Dig-into" a spherical region for ALL GeoForge volumes in the region.
        /// </summary>
        public static void RemoveSphereAll(
            RaycastHit hit, float radius, int delta, List<byte> materials = null) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            var iVolume = hit.collider.transform.GetComponentInParent<IVolume>();

            if (iVolume == null)
                return;

            var matHashSet = materials == null ? gfManager.GetAllMaterials() : materials.ToHashSet();
            var results = TerraformCommands.TerraformSphere(iVolume, hit.point, radius, -delta, matHashSet);

            gfManager.ExecuteTerraformAll(volume =>
                    TerraformCommands.TerraformSphere(volume, hit.point, radius, -delta, matHashSet)
            );
        }
        
        /// <summary>
        /// Public method to Add or "Deposit-onto" a spherical region for one specific GeoForge volume. 
        /// </summary>
        public static void AddSphere(
            RaycastHit hit, float radius, int delta, byte material) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            var iVolume = hit.collider.transform.GetComponentInParent<IVolume>();

            if (iVolume == null)
                return;

            var matHashSet = new HashSet<byte> { material };
            var results = TerraformCommands.TerraformSphere(iVolume, hit.point, radius, delta, matHashSet);
            gfManager.DistributeVoxelEdits(iVolume, results.edits);
        }
    }
}
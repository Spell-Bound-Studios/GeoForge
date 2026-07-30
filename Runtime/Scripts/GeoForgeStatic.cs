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
        /// Public method to Remove or "Dig-into" a spherical region in one specific GeoForge Volume,
        /// with no restriction on which existing materials can be affected.
        /// </summary>
        public static void RemoveSphere(RaycastHit hit, float radius, int delta) =>
                RemoveSphere(hit, radius, delta, AllMaterialsMask);

        /// <summary>
        /// Public method to Remove or "Dig-into" a spherical region in one specific GeoForge Volume.
        /// allowedMaterialsMask restricts which EXISTING materials this dig can affect (e.g. tool
        /// tier vs. Impervious terrain).
        /// </summary>
        public static void RemoveSphere(
            RaycastHit hit, float radius, int delta, uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            var iVolume = hit.collider.transform.GetComponentInParent<IGeoVolume>();

            if (iVolume == null)
                return;

            var results = TerraformCommands.TerraformSphere(iVolume, hit.point, radius, (short)-delta);

            // A dig never crosses from empty into full, so materialIndex is never actually read by
            // the crossing rule downstream — 0 here is just a placeholder, not a meaningful value.
            gfManager.DistributeVoxelEdits(iVolume, results.edits, 0, allowedMaterialsMask);
        }

        /// <summary>
        /// Public method to Remove or "Dig-into" a spherical region for ALL GeoForge volumes in the
        /// scene, with no restriction on which existing materials can be affected.
        /// </summary>
        public static void RemoveSphereAllVolumes(RaycastHit hit, float radius, short delta) =>
                RemoveSphereAllVolumes(hit, radius, delta, AllMaterialsMask);

        /// <summary>
        /// Public method to Remove or "Dig-into" a spherical region for ALL GeoForge volumes in the scene.
        /// </summary>
        public static void RemoveSphereAllVolumes(
            RaycastHit hit, float radius, short delta, uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            var iVolume = hit.collider.transform.GetComponentInParent<IGeoVolume>();

            if (iVolume == null)
                return;

            gfManager.ExecuteTerraformAll(
                volume => TerraformCommands.TerraformSphere(volume, hit.point, radius, (short)-delta),
                0,
                allowedMaterialsMask);
        }

        /// <summary>
        /// Public method to Add or "Deposit-onto" a spherical region for one specific GeoForge
        /// geoVolume, with no restriction on which existing materials can be filled into.
        /// </summary>
        public static void AddSphere(RaycastHit hit, float radius, short delta, byte material) =>
                AddSphere(hit, radius, delta, material, AllMaterialsMask);

        /// <summary>
        /// Public method to Add or "Deposit-onto" a spherical region for one specific GeoForge geoVolume. 
        /// </summary>
        public static void AddSphere(
            RaycastHit hit, float radius, short delta, byte material, uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            var iVolume = hit.collider.transform.GetComponentInParent<IGeoVolume>();

            if (iVolume == null)
                return;

            var results = TerraformCommands.TerraformSphere(iVolume, hit.point, radius, delta);
            gfManager.DistributeVoxelEdits(iVolume, results.edits, material, allowedMaterialsMask);
        }

        /// <summary>
        /// Public method to Remove or "Dig-into" an "arc" region -- half of a short, wide
        /// cylinder ("a coin held on edge") -- carved directly at worldPosition, with no
        /// raycast/spherecast involved. Since there's no cast pinning this to a single volume,
        /// this always acts like the AllVolumes sphere variant: every GeoForge volume the shape
        /// actually overlaps gets carved (volumes it doesn't intersect simply produce no edits).
        /// Facing direction picks which half of the disc is kept (only the side genuinely behind
        /// the impact point); upHint plus direction together orient the disc's face plane. No
        /// restriction on which existing materials can be affected. No delta -- an arc either
        /// commits or it's not the right brush to call.
        /// </summary>
        public static void RemoveArc(
            Vector3 worldPosition, Vector3 direction, Vector3 upHint, float radius, float thickness) =>
                RemoveArc(worldPosition, direction, upHint, radius, thickness, AllMaterialsMask);

        /// <summary>
        /// Public method to Remove or "Dig-into" an "arc" region for every GeoForge volume it
        /// overlaps. allowedMaterialsMask restricts which EXISTING materials this dig can affect
        /// (e.g. tool tier vs. Impervious terrain).
        /// </summary>
        public static void RemoveArc(
            Vector3 worldPosition, Vector3 direction, Vector3 upHint, float radius, float thickness,
            uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            gfManager.ExecuteTerraformAll(
                volume => TerraformCommands.TerraformArc(
                    volume, worldPosition, direction, upHint, radius, thickness),
                0,
                allowedMaterialsMask);
        }
    }
}
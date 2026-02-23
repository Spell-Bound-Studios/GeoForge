// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Spellbound.Core.Console;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class GeoForgeStatic {
        public static bool IsInitialized() =>
                SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out _)
                && SingletonManager.TryGetSingletonInstance<IVolume>(out _);

        public static bool IsActive() {
            var gfManager = SingletonManager.GetSingletonInstance<GeoForgeManager>();

            return gfManager.IsActive();
        }

        public static bool IsInsideTerrain(Vector3 position) {
            var gfManager = SingletonManager.GetSingletonInstance<GeoForgeManager>();
            var voxelData = gfManager.QueryVoxel(position, out var volume);

            return voxelData.Density >= volume.ConfigBlob.Value.DensityThreshold;
        }

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
            gfManager.ExecuteTerraformAll(
                volume => TerraformCommands.TerraformSphere(volume, hit.point, radius, -delta, matHashSet)
            );
        }

        public static void AddSphere(
            RaycastHit hit, float radius, int delta, byte material) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }
            
            var iVolume = hit.collider.transform.GetComponentInParent<IVolume>();

            if (iVolume == null)
                return;
            
            var matHashSet = new HashSet<byte>() {material};
            var results = TerraformCommands.TerraformSphere(iVolume, hit.point, radius, delta, matHashSet);
            gfManager.DistributeVoxelEdits(iVolume, results.edits);
        }
    }
}
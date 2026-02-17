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
            RaycastHit hit, Vector3 rotation, float radius = 3, int delta = byte.MaxValue, List<byte> materialTypes = null, bool snapToGrid = false) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            gfManager.ExecuteDigAll(
                volume => TerraformCommands.RemoveSphere(volume, snapToGrid, hit.point, radius, delta),
                materialTypes == null ? gfManager.GetAllMaterials() : materialTypes.ToHashSet()
            );
        }

        public static void AddSphere(
            RaycastHit hit, Vector3 rotation, float radius = 3, int delta = byte.MaxValue, byte materialType = byte.MinValue, bool snapToGrid = false) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            gfManager.ExecuteAdd(
                volume => TerraformCommands.AddSphere(volume, snapToGrid, hit.point, materialType, radius, delta),
                targetVolume: hit.collider.GetComponentInParent<IVolume>()
            );
        }
        
        public static void RemoveCube(
            RaycastHit hit,
            Vector3 direction,
            float size = 3, 
            int delta = byte.MaxValue, 
            List<byte> materialTypes = null, 
            bool snapToGrid = false) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            gfManager.ExecuteDigAll(
                volume => TerraformCommands.RemoveCube(volume, snapToGrid, hit.point, direction, size, delta),
                removableMatTypes: materialTypes == null ? gfManager.GetAllMaterials() : materialTypes.ToHashSet()
            );
        }

        public static void AddCube(
            RaycastHit hit,
            Vector3 direction,
            float size = 3, 
            int delta = byte.MaxValue, 
            byte materialType = byte.MinValue, 
            bool snapToGrid = false) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoForgeManager not found. Ensure it's in the current scene.");

                return;
            }

            gfManager.ExecuteAdd(
                volume => TerraformCommands.AddCube(volume, snapToGrid,hit.point, direction, materialType, size, delta),
                targetVolume: hit.collider.GetComponentInParent<IVolume>()
            );
        }
    }
}
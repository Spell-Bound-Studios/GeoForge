// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Spellbound.Core.Console;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public static class SbVoxel {
        public static bool IsInitialized() =>
                SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out _)
                && SingletonManager.TryGetSingletonInstance<IVolume>(out _);

        public static bool IsActive() {
            var mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();

            return mcManager.IsActive();
        }

        public static bool IsInsideTerrain(Vector3 position) {
            var mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
            var voxelData = mcManager.QueryVoxel(position, out var volume);

            return voxelData.Density >= volume.ConfigBlob.Value.DensityThreshold;
        }

        public static void RemoveSphere(
            RaycastHit hit, Vector3 rotation, float radius = 3, int delta = byte.MaxValue, List<byte> materialTypes = null, bool snapToGrid = false) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteDigAll(
                volume => TerraformCommands.RemoveSphere(volume, snapToGrid, hit.point, radius, delta),
                materialTypes == null ? mcManager.GetAllMaterials() : materialTypes.ToHashSet()
            );
        }

        public static void AddSphere(
            RaycastHit hit, Vector3 rotation, float radius = 3, int delta = byte.MaxValue, byte materialType = byte.MinValue, bool snapToGrid = false) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteAdd(
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
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteDigAll(
                volume => TerraformCommands.RemoveCube(volume, snapToGrid, hit.point, direction, size, delta),
                removableMatTypes: materialTypes == null ? mcManager.GetAllMaterials() : materialTypes.ToHashSet()
            );
        }

        public static void AddCube(
            RaycastHit hit,
            Vector3 direction,
            float size = 3, 
            int delta = byte.MaxValue, 
            byte materialType = byte.MinValue, 
            bool snapToGrid = false) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteAdd(
                volume => TerraformCommands.AddCube(volume, snapToGrid,hit.point, direction, materialType, size, delta),
                targetVolume: hit.collider.GetComponentInParent<IVolume>()
            );
        }
    }
}
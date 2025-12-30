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
            Vector3 position, float radius = 3, int delta = byte.MaxValue, List<byte> materialTypes = null) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteTerraform(
                volume => TerraformCommands.RemoveSphere(volume, position, radius, delta),
                materialTypes == null ? mcManager.GetAllMaterials() : materialTypes.ToHashSet()
            );
        }

        public static void RemoveSphere(
            RaycastHit hit, float radius = 3, int delta = byte.MaxValue, List<byte> materialTypes = null) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteTerraform(
                volume => TerraformCommands.RemoveSphere(volume, hit.point, radius, delta),
                materialTypes == null ? mcManager.GetAllMaterials() : materialTypes.ToHashSet(),
                hit.collider.GetComponentInParent<IVolume>()
            );
        }

        public static void RemoveSphere(
            Collision collision, float radius = 3, int delta = byte.MaxValue, List<byte> materialTypes = null) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteTerraform(
                volume => TerraformCommands.RemoveSphere(volume, collision.GetContact(0).point, radius, delta),
                materialTypes == null ? mcManager.GetAllMaterials() : materialTypes.ToHashSet(),
                collision.collider.GetComponentInParent<IVolume>()
            );
        }

        [ConsoleUtilityCommand("AddSphere", "Positive terraform with 2 radius")]
        public static void AddSphere(
            Vector3 position, float radius = 3, int delta = byte.MaxValue, byte materialType = byte.MinValue) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteTerraform(volume =>
                    TerraformCommands.AddSphere(volume, position, materialType, radius, delta)
            );
        }

        public static void AddSphere(
            RaycastHit hit, float radius = 3, int delta = byte.MaxValue, byte materialType = byte.MinValue) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteTerraform(
                volume => TerraformCommands.AddSphere(volume, hit.point, materialType, radius, delta),
                targetVolume: hit.collider.GetComponentInParent<IVolume>()
            );
        }

        public static void AddSphere(
            Collision collision, float radius = 3, int delta = byte.MaxValue, byte materialType = byte.MinValue) {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found. Ensure it's in the current scene.");

                return;
            }

            mcManager.ExecuteTerraform(
                volume => TerraformCommands.AddSphere(volume, collision.GetContact(0).point, materialType, radius,
                    delta),
                targetVolume: collision.collider.GetComponentInParent<IVolume>()
            );
        }
    }
}
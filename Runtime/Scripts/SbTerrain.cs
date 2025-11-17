// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Spellbound.Core.Console;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public static class SbTerrain {
        public static bool IsInitialized() =>
                SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out _)
                && SingletonManager.TryGetSingletonInstance<IVoxelTerrainChunkManager>(out _);

        public static bool IsActive() {
            var mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();

            return mcManager.IsActive();
        }

        public static bool IsInsideTerrain(Vector3 position) {
            var mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();

            return mcManager.QueryVoxel(position).Density >= mcManager.McConfigBlob.Value.DensityThreshold;
        }

        public static void RemoveSphere(Vector3 position) =>
                TerraformCommands.RemoveSphere(position,
                    new List<MaterialType> { MaterialType.Dirt, MaterialType.Swamp, MaterialType.Ice },
                    3f,
                    byte.MaxValue);

        public static void RemoveSphere(
            Vector3 position,
            List<MaterialType> diggableMaterialTypes,
            float radius,
            int delta) =>
                TerraformCommands.RemoveSphere(position, diggableMaterialTypes, radius, delta);

        public static void RemoveSphere(Vector3 position, float radius, int delta) =>
                TerraformCommands.RemoveSphere(position,
                    McStaticHelper.GetAllMaterialTypes().ToList(),
                    radius,
                    delta);
        
        /// <summary>
        /// AddSphere is the positive terraform technique where a user can pass in the position, radius, and voxel
        /// density to the method.
        /// </summary>
        /// Console Usage: AddSphere 2 255
        [ConsoleUtilityCommand("AddSphere", "Positive terraform with 2 radius")]
        public static void AddSphere(Vector3 position, float radius = 2f, int delta = 255) =>
                TerraformCommands.AddSphere(position,
                    MaterialType.Ice,
                    radius,
                    delta);
    }
}
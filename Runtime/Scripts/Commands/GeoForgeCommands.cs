// Copyright 2026 Spellbound Studio Inc.

using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Public entry points for the job-based terraform commands (TerraformCubeCommand,
    /// TerraformSphereCommand, TerraformArcCommand) - the Burst-parallelized alternative to
    /// GeoForgeStatic/TerraformCommands's original serial pipeline. Mirrors GeoForgeStatic's own
    /// naming and overload shape for familiarity, but is a genuinely separate, self-contained
    /// pathway - see each Command's own doc comment.
    ///
    /// Differences worth knowing about versus GeoForgeStatic:
    /// - Every method here operates on exactly one IGeoVolume, resolved by the caller. There is
    ///   no multi-volume "AllVolumes" variant yet (GeoForgeStatic.RemoveSphereAllVolumes/RemoveArc
    ///   iterate every registered volume via ExecuteTerraformAll - nothing here does that).
    /// - Every method can reject the whole action outright (returns false, logs a warning) if the
    ///   apron-expanded chunk range it would touch exceeds Edit-pool capacity, or - for non-finite
    ///   volumes only - includes a chunk that doesn't exist. This is all-or-nothing: unlike
    ///   GeoForgeStatic's DistributeVoxelEdits-based path (which silently skips individual edits
    ///   destined for missing chunks and applies the rest), a rejection here applies nothing.
    /// - RemoveArc here takes an explicit IGeoVolume rather than iterating every volume in the
    ///   scene - a real scope-narrowing from GeoForgeStatic.RemoveArc, not an oversight.
    /// </summary>
    public static class GeoForgeCommands {
        private static readonly uint4 AllMaterialsMask = new(uint.MaxValue);

        #region Cube

        /// <summary>
        /// Removes ("digs into") a uniform cube, no falloff, hard edges - halfExtent is in
        /// voxels, not world units. delta is the magnitude to subtract (always applied as
        /// negative internally, matching GeoForgeStatic.RemoveSphere's own convention). Returns
        /// false if the action was rejected - see the class doc comment.
        /// </summary>
        public static bool RemoveCube(IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, int delta) =>
                RemoveCube(geoVolume, worldPosition, halfExtent, delta, AllMaterialsMask);

        public static bool RemoveCube(
            IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, int delta, uint4 allowedMaterialsMask) =>
                TerraformCubeCommand.Execute(
                    geoVolume, worldPosition, halfExtent, (short)-delta, 0, allowedMaterialsMask);

        /// <summary>
        /// Adds ("fills") a uniform cube, no falloff, hard edges - halfExtent is in voxels, not
        /// world units. Returns false if the action was rejected - see the class doc comment.
        /// </summary>
        public static bool AddCube(
            IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, short delta, byte material) =>
                AddCube(geoVolume, worldPosition, halfExtent, delta, material, AllMaterialsMask);

        public static bool AddCube(
            IGeoVolume geoVolume, Vector3 worldPosition, int halfExtent, short delta, byte material,
            uint4 allowedMaterialsMask) =>
                TerraformCubeCommand.Execute(
                    geoVolume, worldPosition, halfExtent, delta, material, allowedMaterialsMask);

        #endregion

        #region Sphere

        /// <summary>
        /// Removes ("digs into") a spherical region with smooth falloff. Returns false if the
        /// action was rejected - see the class doc comment.
        /// </summary>
        public static bool RemoveSphere(IGeoVolume geoVolume, Vector3 worldPosition, float radius, int delta) =>
                RemoveSphere(geoVolume, worldPosition, radius, delta, 0, AllMaterialsMask);
        
        public static bool RemoveSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, int delta, uint4 allowedMaterialsMask) =>
            TerraformSphereCommand.Execute(
                geoVolume, worldPosition, radius, (short)-delta, 0, allowedMaterialsMask);

        public static bool RemoveSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, int delta, byte materialIndex, uint4 allowedMaterialsMask) =>
                TerraformSphereCommand.Execute(
                    geoVolume, worldPosition, radius, (short)-delta, materialIndex, allowedMaterialsMask);

        /// <summary>
        /// Adds ("deposits onto") a spherical region with smooth falloff. Returns false if the
        /// action was rejected - see the class doc comment.
        /// </summary>
        public static bool AddSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, short delta, byte material) =>
                AddSphere(geoVolume, worldPosition, radius, delta, material, AllMaterialsMask);

        public static bool AddSphere(
            IGeoVolume geoVolume, Vector3 worldPosition, float radius, short delta, byte material,
            uint4 allowedMaterialsMask) =>
                TerraformSphereCommand.Execute(
                    geoVolume, worldPosition, radius, delta, material, allowedMaterialsMask);

        #endregion

        #region Arc

        /// <summary>
        /// Removes ("digs into") a half-disc "arc" region - see TerraformArcCommand's own doc
        /// comment for the shape's exact geometry. No delta parameter: an arc always subtracts a
        /// fixed maximum, same reasoning as the original TerraformCommands.TerraformArc. Unlike
        /// GeoForgeStatic.RemoveArc, this operates on a single explicit IGeoVolume rather than
        /// every registered volume - see the class doc comment. Returns false if rejected.
        /// </summary>
        public static bool RemoveArc(
            IGeoVolume geoVolume, Vector3 worldPosition, Vector3 direction, Vector3 upHint, float radius,
            float thickness) =>
                RemoveArc(geoVolume, worldPosition, direction, upHint, radius, thickness, AllMaterialsMask);

        public static bool RemoveArc(
            IGeoVolume geoVolume, Vector3 worldPosition, Vector3 direction, Vector3 upHint, float radius,
            float thickness, uint4 allowedMaterialsMask) =>
                TerraformArcCommand.Execute(
                    geoVolume, worldPosition, direction, upHint, radius, thickness, allowedMaterialsMask);

        #endregion
    }
}
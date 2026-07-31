// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Terraform commands. Internal and "DRY". Should be accessed through the public GeoForgeStatic class.
    /// </summary>
    internal static class TerraformCommands {
        internal static (List<RawVoxelEdit> edits, Bounds bounds) TerraformSphere(
            IGeoVolume iGeoVoxelVolume,
            Vector3 worldPosition,
            float size,
            short delta) {
            var voxelCenter = iGeoVoxelVolume.WorldToVoxelSpace(worldPosition);
            var halfSizeVoxels = size * 0.5f / iGeoVoxelVolume.ConfigBlob.Value.Resolution;
            var r = Mathf.CeilToInt(halfSizeVoxels);

            var diameter = 2 * r + 1;
            var rawVoxelEdits = new List<RawVoxelEdit>(diameter * diameter * diameter);

            for (var x = -r; x <= r; x++)
            for (var y = -r; y <= r; y++)
            for (var z = -r; z <= r; z++) {
                var dist = Mathf.Sqrt(x * x + y * y + z * z);
                var voxelPos = voxelCenter + new Vector3Int(x, y, z);

                var normalizedDist = dist - (halfSizeVoxels - 1f);
                var falloff = 1f - Mathf.Clamp01(normalizedDist);
                var scaledDelta = Mathf.RoundToInt(delta * falloff);

                // Skip voxels this stroke doesn't actually touch (falloff decayed to zero at this
                // distance). Emitting a zero-delta edit here would make Delta stamp the voxel
                // "mature" even though it was never part of this edit — mature means
                // generated/authored, not merely "sat inside this brush's bounding cube."
                if (scaledDelta == 0)
                    continue;

                rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, (short)scaledDelta));
            }

            var voxelBounds = new Bounds(voxelCenter, Vector3.one * halfSizeVoxels * 2f);

            return (rawVoxelEdits, voxelBounds);
        }

        /// <summary>
        /// A pickaxe-hit "arc" dig: half of a short, wide cylinder ("a coin held on edge"),
        /// carving a shallow patch out of the wall rather than boring deep into it. No raycast
        /// or spherecast involved -- purely geometric, defined by a center position, a facing
        /// direction, a radius, and a thickness.
        ///
        /// Axes, relative to worldPosition:
        ///   - thinAxis = normalize(cross(direction, upHint)) -- the cylinder's actual axis (the
        ///     "short way through the coin"). thickness extends along this axis, split evenly
        ///     on either side of worldPosition.
        ///   - direction and upHint together span the disc's face plane (the flat circular
        ///     cross-section you actually see carved into the wall). direction is used directly
        ///     to decide which half of that disc to keep.
        ///
        /// Only the half of the disc with a non-negative component along direction is carved --
        /// i.e. only material genuinely behind the impact point, never the half that would stick
        /// out toward the player. This isn't an artificial clip papering over a flaw (like the
        /// flat-mouth issue on the old egg shape); it's the intended shape, so no rounding/cap is
        /// needed here the way it was there.
        ///
        /// The curved rim (radius) and the thickness slab's two faces both use a smooth falloff,
        /// same style as TerraformSphere's own falloff -- applied to a fixed maximum subtraction
        /// (bandMaxSubtract) rather than a caller-supplied delta, since TerraformArc has no delta.
        /// Each axis falls off independently over about one voxel (full-strength inside its own
        /// core, ramping to zero at its own outer bound, floored at 1 voxel so a very small
        /// radius/thickness still guarantees at least one voxel of effect), and the two falloffs
        /// are multiplied together -- so a voxel near the curved rim AND near a thickness face
        /// (e.g. digging along a diagonal) tapers smoothly in both dimensions at once, rather
        /// than getting a hard cutoff from whichever axis's binary test happened to trigger
        /// first (the previous hard thickness cutoff produced a visible sawtooth pattern on
        /// diagonal swings for exactly this reason -- the rim was smooth but the thickness faces
        /// weren't, so the two edges disagreed with each other at an angle).
        ///
        /// Being smooth, deterministic functions of distance (not per-voxel random) means
        /// neighboring voxels always land close in value on both axes, so this can't produce the
        /// scattered/isolated-voxel topology problem random values could.
        ///
        /// The flat diameter cut (the half-disc split) stays a crisp/hard cutoff -- that's a
        /// deliberate gameplay-simplification edge, not a stand-in for a real fracture surface,
        /// so leaving it clean rather than smoothed seemed right; revisit if it looks wrong in
        /// practice.
        ///
        /// No delta parameter, same reasoning as the shapes before it: an arc either commits or
        /// it's not the right brush to call.
        /// </summary>
        internal static (List<RawVoxelEdit> edits, Bounds bounds) TerraformArc(
            IGeoVolume iGeoVoxelVolume,
            Vector3 worldPosition,
            Vector3 direction,
            Vector3 upHint,
            float radius,
            float thickness) {
            var impactVoxelPosF = iGeoVoxelVolume.WorldToVoxelSpaceContinuous(worldPosition);
            var voxelCenter = Vector3Int.RoundToInt(impactVoxelPosF);

            var resolution = iGeoVoxelVolume.ConfigBlob.Value.Resolution;

            // NOTE: radius here is a true radius (unlike "size" on TerraformSphere/Flake/Chip,
            // which was a diameter) -- matches how you described this shape ("radius is the
            // radius of the cylinder"). Worth double-checking this convention is what you want
            // at the call site, since it differs from the other Terraform* methods.
            var radiusVoxels = radius / resolution;
            var halfThicknessVoxels = thickness * 0.5f / resolution;

            if (direction.sqrMagnitude < 1e-6f) {
                Debug.LogWarning("TerraformArc: direction is zero-length; defaulting to +Z.");
                direction = Vector3.forward;
            }

            direction.Normalize();

            var thinAxis = Vector3.Cross(direction, upHint);

            if (thinAxis.sqrMagnitude < 1e-6f) {
                // direction is parallel (or near-parallel) to upHint -- cross product is
                // degenerate. Fall back to a different reference axis to still get a sane thin
                // axis perpendicular to direction.
                thinAxis = Vector3.Cross(direction, Vector3.forward);

                if (thinAxis.sqrMagnitude < 1e-6f)
                    thinAxis = Vector3.Cross(direction, Vector3.right);
            }

            thinAxis.Normalize();

            const float bandMinOuterRadius = 1.0f;
            const int bandMaxSubtract = 255;

            var coreRadius = Mathf.Max(0f, radiusVoxels - 1f);
            var bandOuterRadius = Mathf.Max(radiusVoxels, bandMinOuterRadius);

            var thicknessCoreHalf = Mathf.Max(0f, halfThicknessVoxels - 1f);
            var thicknessOuterHalf = Mathf.Max(halfThicknessVoxels, bandMinOuterRadius);

            // Iteration bound: covers the disc's radius and the thickness slab, both including
            // their outer falloff bounds, plus +1 slack for the sub-voxel impact position.
            var boundRadius = Mathf.Max(bandOuterRadius, thicknessOuterHalf);
            var r = Mathf.CeilToInt(boundRadius) + 1;
            var diameter = 2 * r + 1;
            var rawVoxelEdits = new List<RawVoxelEdit>(diameter * diameter * diameter);

            for (var x = -r; x <= r; x++)
            for (var y = -r; y <= r; y++)
            for (var z = -r; z <= r; z++) {
                var voxelPos = voxelCenter + new Vector3Int(x, y, z);
                var offset = (Vector3)voxelPos - impactVoxelPosF;

                var tThin = Vector3.Dot(offset, thinAxis);
                var absTThin = Mathf.Abs(tThin);

                if (absTThin > thicknessOuterHalf)
                    continue;

                // Component of the offset lying within the disc's face plane (perpendicular to
                // thinAxis by construction, since thinAxis is orthogonal to both direction and
                // upHint).
                var inPlane = offset - tThin * thinAxis;

                // Half-disc cutoff: hard, keep only the side genuinely behind the impact point.
                var dDir = Vector3.Dot(inPlane, direction);

                if (dDir < 0f)
                    continue;

                var p = inPlane.magnitude;

                if (p > bandOuterRadius)
                    continue;

                // Two independent smooth falloffs -- radial (curved rim) and thickness (the two
                // flat faces) -- each 1.0 at/inside their own core, ramping to 0.0 at their own
                // outer bound over ~1 voxel. Multiplied together so a voxel near both edges at
                // once (a diagonal swing) tapers smoothly on both axes rather than snapping off
                // wherever the stricter of the two hard cutoffs used to trigger.
                var radialFalloff = 1f - Mathf.Clamp01(p - coreRadius);
                var thicknessFalloff = 1f - Mathf.Clamp01(absTThin - thicknessCoreHalf);
                var combinedFalloff = radialFalloff * thicknessFalloff;
                var scaledSubtract = Mathf.RoundToInt(bandMaxSubtract * combinedFalloff);

                // Same reasoning as TerraformSphere: skip voxels the falloff decayed to zero at
                // this distance, rather than emitting a zero-value edit.
                if (scaledSubtract == 0)
                    continue;

                rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, (short)-scaledSubtract));

                // Outside the radius, in front of the impact plane, or outside the thickness
                // slab -- untouched, no edit emitted.
            }

            // Conservative bounding box: centered on the impact point, offset slightly into the
            // wall along direction to account for the half-disc's lopsidedness, sized to the
            // larger of radius or thickness. Looser than the true half-disc silhouette, but
            // simple and correct regardless of orientation.
            var boundsCenter = (Vector3)voxelCenter + direction * (bandOuterRadius * 0.5f);
            var boundsSize = Vector3.one * (Mathf.Max(bandOuterRadius, thicknessOuterHalf) * 2f);
            var voxelBounds = new Bounds(boundsCenter, boundsSize);

            return (rawVoxelEdits, voxelBounds);
        }
    }
}
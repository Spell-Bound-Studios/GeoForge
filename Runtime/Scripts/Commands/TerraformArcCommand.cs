// Copyright 2026 Spellbound Studio Inc.

using Spellbound.Core.Tooling;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Per-voxel core of TerraformArcCommand: for every voxel in a cube bounding the arc's
    /// disc+thickness slab, replicates TerraformCommands.TerraformArc's exact per-voxel math
    /// (thin-axis/in-plane decomposition, half-disc cutoff, two independent smooth falloffs) and
    /// scatters a VoxelDensityDelta for each non-zero result. Direction/ThinAxis/etc are computed
    /// once on the main thread by TerraformArcCommand (managed Vector3 math, including the
    /// degenerate-direction fallback) and passed in as plain float3 fields - none of that setup
    /// needs to run per-voxel. See TerraformArcCommand for the pre-validation this depends on.
    /// </summary>
    [BurstCompile]
    internal struct TerraformArcJob : IJobParallelFor {
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;
        public int3 VoxelCenter;
        public float3 ImpactPosF;
        public float3 Direction;
        public float3 ThinAxis;
        public float CoreRadius;
        public float BandOuterRadius;
        public float ThicknessCoreHalf;
        public float ThicknessOuterHalf;
        public int Radius; // bounding cube half-extent (r in the original TerraformCommands.TerraformArc)
        public NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>.ParallelWriter Writer;

        private const int BandMaxSubtract = 255;

        public void Execute(int index) {
            var side = Radius * 2 + 1;

            var dz = index / (side * side);
            var remainder = index - dz * side * side;
            var dy = remainder / side;
            var dx = remainder - dy * side;

            var offset = new int3(dx, dy, dz) - Radius;
            var voxelPos = VoxelCenter + offset;
            var offsetF = new float3(voxelPos.x, voxelPos.y, voxelPos.z) - ImpactPosF;

            var tThin = math.dot(offsetF, ThinAxis);
            var absTThin = math.abs(tThin);

            if (absTThin > ThicknessOuterHalf)
                return;

            var inPlane = offsetF - tThin * ThinAxis;
            var dDir = math.dot(inPlane, Direction);

            if (dDir < 0f)
                return;

            var p = math.length(inPlane);

            if (p > BandOuterRadius)
                return;

            var radialFalloff = 1f - math.saturate(p - CoreRadius);
            var thicknessFalloff = 1f - math.saturate(absTThin - ThicknessCoreHalf);
            var combinedFalloff = radialFalloff * thicknessFalloff;
            var scaledSubtract = (int)math.round(BandMaxSubtract * combinedFalloff);

            if (scaledSubtract == 0)
                return;

            ref var config = ref ConfigBlob.Value;

            TerraformCommandUtility.ScatterVoxelDelta(
                voxelPos, (short)-scaledSubtract, config.ChunkSize, config.ChunkDataAreaSize,
                config.ChunkDataWidthSize, Writer);
        }
    }

    /// <summary>
    /// Standalone, job-based terraform command: the half-disc "arc" shape from
    /// TerraformCommands.TerraformArc, reimplemented as a fused shape-generation-plus-chunk-fanout
    /// job. Internal - reached only through GeoForgeCommands. No delta parameter, same reasoning
    /// as the original: an arc either commits or it's not the right brush to call.
    /// </summary>
    internal static class TerraformArcCommand {
        internal static bool Execute(
            IGeoVolume geoVolume,
            Vector3 worldPosition,
            Vector3 direction,
            Vector3 upHint,
            float radius,
            float thickness,
            uint4 allowedMaterialsMask) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("TerraformArcCommand: GeoForgeManager not found. Ensure it's in the current scene.");

                return false;
            }

            ref var config = ref geoVolume.ConfigBlob.Value;
            var isFiniteVolume = config.IsFiniteSize;
            var resolution = config.Resolution;

            var impactVoxelPosF = geoVolume.WorldToVoxelSpaceContinuous(worldPosition);
            var voxelCenterInt = Vector3Int.RoundToInt(impactVoxelPosF);
            var voxelCenter = new int3(voxelCenterInt.x, voxelCenterInt.y, voxelCenterInt.z);

            var radiusVoxels = radius / resolution;
            var halfThicknessVoxels = thickness * 0.5f / resolution;

            if (direction.sqrMagnitude < 1e-6f) {
                Debug.LogWarning("TerraformArcCommand: direction is zero-length; defaulting to +Z.");
                direction = Vector3.forward;
            }

            direction.Normalize();

            var thinAxis = Vector3.Cross(direction, upHint);

            if (thinAxis.sqrMagnitude < 1e-6f) {
                thinAxis = Vector3.Cross(direction, Vector3.forward);

                if (thinAxis.sqrMagnitude < 1e-6f)
                    thinAxis = Vector3.Cross(direction, Vector3.right);
            }

            thinAxis.Normalize();

            const float bandMinOuterRadius = 1.0f;

            var coreRadius = Mathf.Max(0f, radiusVoxels - 1f);
            var bandOuterRadius = Mathf.Max(radiusVoxels, bandMinOuterRadius);

            var thicknessCoreHalf = Mathf.Max(0f, halfThicknessVoxels - 1f);
            var thicknessOuterHalf = Mathf.Max(halfThicknessVoxels, bandMinOuterRadius);

            var boundRadius = Mathf.Max(bandOuterRadius, thicknessOuterHalf);
            var radiusInt = Mathf.CeilToInt(boundRadius) + 1;

            var paddedRadius = radiusInt + TerraformCommandUtility.PaddingMargin;
            var minVoxel = voxelCenterInt - Vector3Int.one * paddedRadius;
            var maxVoxel = voxelCenterInt + Vector3Int.one * paddedRadius;

            if (!TerraformCommandUtility.TryValidateChunkRange(
                    geoVolume, gfManager, minVoxel, maxVoxel, nameof(TerraformArcCommand), worldPosition,
                    out _, out _)) {
                return false;
            }

            var side = radiusInt * 2 + 1;
            var voxelCount = side * side * side;

            var resultMap = new NativeParallelMultiHashMap<ChunkCoordKey, VoxelDensityDelta>(
                voxelCount * 8, Allocator.TempJob);

            var job = new TerraformArcJob {
                ConfigBlob = geoVolume.ConfigBlob,
                VoxelCenter = voxelCenter,
                ImpactPosF = new float3(impactVoxelPosF.x, impactVoxelPosF.y, impactVoxelPosF.z),
                Direction = new float3(direction.x, direction.y, direction.z),
                ThinAxis = new float3(thinAxis.x, thinAxis.y, thinAxis.z),
                CoreRadius = coreRadius,
                BandOuterRadius = bandOuterRadius,
                ThicknessCoreHalf = thicknessCoreHalf,
                ThicknessOuterHalf = thicknessOuterHalf,
                Radius = radiusInt,
                Writer = resultMap.AsParallelWriter()
            };

            job.Schedule(voxelCount, 32).Complete();

            // No delta parameter (arc always subtracts, per the original's own reasoning) - 0 as
            // materialIndex mirrors GeoForgeStatic.RemoveArc's own placeholder value, since a dig
            // never crosses from empty into full and materialIndex is never actually read by the
            // crossing rule downstream for this shape.
            TerraformCommandUtility.DispatchEdits(
                gfManager, geoVolume, worldPosition, resultMap, 0, allowedMaterialsMask, isFiniteVolume,
                nameof(TerraformArcCommand));

            return true;
        }
    }
}
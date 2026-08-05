// Copyright 2026 Spellbound Studio Inc.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Shared Burst-compatible helpers extracted from the four marching cubes jobs
    /// (MarchingCubeJob, TransitionMarchingCubeJob, FlatBaryMarchJob, TransFlatBaryMarchJob) to
    /// eliminate copy-pasted logic between them. Static methods on a Burst-compiled struct carry
    /// no call overhead beyond what the duplicated inline code already cost - this exists purely
    /// so a fix (like the caseCode sign-extension bug) only ever needs to land once.
    ///
    /// Not every method here is used by every job - see each method's doc comment for which jobs
    /// actually call it. Sharing a method doesn't mean all four jobs use it; the FlatBary jobs in
    /// particular use a different material/maturity scheme entirely and have no use for
    /// AddMaterialWeight, ResolveMaturity, or SampleNeighborGradient.
    /// </summary>
    [BurstCompile]
    internal static class GfMarchHelper {
        /// <summary>
        /// Used by all four jobs, identically, in their triangle loops.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDegenerateTriangle(float3 a, float3 b, float3 c) {
            var area = math.length(math.cross(b - a, c - a));

            return area < 1e-5f; // Tweak epsilon if needed
        }

        /// <summary>
        /// Used by TransitionMarchingCubeJob and TransFlatBaryMarchJob, identically, to map a
        /// (leafSize-relative) 2D face coordinate into the leaf's own 3D local space depending on
        /// which of the 6 transition faces is currently being generated.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 FaceToLocalSpace(
            GfStaticHelper.TransitionFaceMask direction,
            int leafSize,
            int x,
            int y,
            int z) =>
                direction switch {
                    GfStaticHelper.TransitionFaceMask.XMin => new int3(z, x, y),
                    GfStaticHelper.TransitionFaceMask.XMax => new int3(leafSize - z, y, x),
                    GfStaticHelper.TransitionFaceMask.YMin => new int3(y, z, x),
                    GfStaticHelper.TransitionFaceMask.YMax => new int3(x, leafSize - z, y),
                    GfStaticHelper.TransitionFaceMask.ZMin => new int3(x, y, z),
                    GfStaticHelper.TransitionFaceMask.ZMax => new int3(y, x, leafSize - z),
                    _ => new int3(x, y, z)
                };

        /// <summary>
        /// Used by MarchingCubeJob and FlatBaryMarchJob, identically: samples the 8 corners of a
        /// regular (non-transition) cube into cellValues and returns the resulting caseCode. This
        /// is the exact site of bug 10 (the sign-extension early-out bug from the byte->sbyte
        /// migration) - having it in exactly one place is the whole point of this file existing.
        /// Callers still do their own "if (caseCode == 0x00 || caseCode == 0xFF) continue;" - a
        /// continue can't cross a method boundary, so that one line necessarily stays inline at
        /// each call site, but it's simple enough now that duplicating it carries none of the risk
        /// the old bit-twiddle version did.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte GatherRegularCornersAndComputeCaseCode(
            in NativeArray<VoxelData> voxelArray,
            ref McTablesBlobAsset tables,
            int3 cellPos,
            int padding,
            int lodScale,
            int chunkDataAreaSize,
            int chunkDataWidthSize,
            ref NativeArray<VoxelData> cellValues) {
            for (var i = 0; i < 8; ++i) {
                var voxelPosition = cellPos + new int3(padding, padding, padding) +
                                    tables.RegularCornerOffset[i] * lodScale;

                cellValues[i] = voxelArray[GfStaticHelper.Coord3DToIndex(
                    voxelPosition.x, voxelPosition.y, voxelPosition.z,
                    chunkDataAreaSize, chunkDataWidthSize
                )];
            }

            return (byte)((cellValues[0].Density >= 0 ? 0x01 : 0)
                          | (cellValues[1].Density >= 0 ? 0x02 : 0)
                          | (cellValues[2].Density >= 0 ? 0x04 : 0)
                          | (cellValues[3].Density >= 0 ? 0x08 : 0)
                          | (cellValues[4].Density >= 0 ? 0x10 : 0)
                          | (cellValues[5].Density >= 0 ? 0x20 : 0)
                          | (cellValues[6].Density >= 0 ? 0x40 : 0)
                          | (cellValues[7].Density >= 0 ? 0x80 : 0));
        }

        /// <summary>
        /// Used by all four jobs, identically in shape (some pass Lod as the iteration count, the
        /// transition jobs pass subEdges instead - that distinction stays the caller's decision).
        /// Repeatedly bisects the edge between pos0 and pos1 to find where Density crosses zero,
        /// narrowing toward whichever endpoint the midpoint agrees with. Takes pos0/pos1 by ref and
        /// mutates them directly, rather than maintaining a separate float3 accumulator alongside
        /// the int3 the caller actually indexes with - MarchingCubeJob and FlatBaryMarchJob
        /// previously tracked both (p0/p1 as float3, vertLocalPos0/1 as int3, always kept in sync);
        /// (float3)(intA + intB) * 0.5f is bit-identical to ((float3)intA + (float3)intB) * 0.5f for
        /// any voxel-scale coordinate, so this drops the redundant tracking without changing output.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SubdivideToSurfaceCrossing(
            in NativeArray<VoxelData> voxelArray,
            int chunkDataAreaSize,
            int chunkDataWidthSize,
            int iterations,
            bool isVert0Full,
            ref int3 pos0,
            ref int3 pos1) {
            for (var j = 0; j < iterations; ++j) {
                var mid = (float3)(pos0 + pos1) * 0.5f;
                var samplePos = (int3)math.round(mid);

                var midPointDensity = voxelArray[
                    GfStaticHelper.Coord3DToIndex(samplePos.x, samplePos.y, samplePos.z,
                        chunkDataAreaSize, chunkDataWidthSize)
                ].Density;

                var isMidPointFull = midPointDensity >= 0;

                var isVertexNearerToVert1 =
                        (isMidPointFull && isVert0Full)
                        || (!isMidPointFull && !isVert0Full);

                if (isVertexNearerToVert1)
                    pos0 = samplePos;
                else
                    pos1 = samplePos;
            }
        }

        /// <summary>
        /// The 14-voxel neighborhood sampled once per new vertex by the blended-material march
        /// jobs (MarchingCubeJob, TransitionMarchingCubeJob) - NOT used by the FlatBary jobs, which
        /// have their own unrelated material scheme. Bundles the 12 axis-neighbor voxels (needed
        /// individually for AddMaterialWeight's dominance vote) together with the two endpoint
        /// gradient normals computed from those same density differences, so both jobs share one
        /// gather instead of duplicating 12 near-identical VoxelArray lookups each.
        /// </summary>
        internal struct NeighborGradientSample {
            public VoxelData V0011, V0211, V0101, V0121, V0110, V0112;
            public VoxelData V1011, V1211, V1101, V1121, V1110, V1112;
            public float3 Normal0;
            public float3 Normal1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NeighborGradientSample SampleNeighborGradient(
            in NativeArray<VoxelData> voxelArray,
            int3 vertLocalPos0,
            int3 vertLocalPos1,
            int chunkDataAreaSize,
            int chunkDataWidthSize) {
            var x0 = vertLocalPos0.x;
            var y0 = vertLocalPos0.y;
            var z0 = vertLocalPos0.z;
            var x1 = vertLocalPos1.x;
            var y1 = vertLocalPos1.y;
            var z1 = vertLocalPos1.z;

            var sample = new NeighborGradientSample {
                V0011 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x0 - 1, y0, z0, chunkDataAreaSize, chunkDataWidthSize)],
                V0211 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x0 + 1, y0, z0, chunkDataAreaSize, chunkDataWidthSize)],
                V0101 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x0, y0 - 1, z0, chunkDataAreaSize, chunkDataWidthSize)],
                V0121 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x0, y0 + 1, z0, chunkDataAreaSize, chunkDataWidthSize)],
                V0110 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x0, y0, z0 - 1, chunkDataAreaSize, chunkDataWidthSize)],
                V0112 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x0, y0, z0 + 1, chunkDataAreaSize, chunkDataWidthSize)],

                V1011 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x1 - 1, y1, z1, chunkDataAreaSize, chunkDataWidthSize)],
                V1211 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x1 + 1, y1, z1, chunkDataAreaSize, chunkDataWidthSize)],
                V1101 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x1, y1 - 1, z1, chunkDataAreaSize, chunkDataWidthSize)],
                V1121 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x1, y1 + 1, z1, chunkDataAreaSize, chunkDataWidthSize)],
                V1110 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x1, y1, z1 - 1, chunkDataAreaSize, chunkDataWidthSize)],
                V1112 = voxelArray[
                    GfStaticHelper.Coord3DToIndex(x1, y1, z1 + 1, chunkDataAreaSize, chunkDataWidthSize)]
            };

            sample.Normal0 = new float3(
                sample.V0011.Density - sample.V0211.Density,
                sample.V0101.Density - sample.V0121.Density,
                sample.V0110.Density - sample.V0112.Density
            );

            sample.Normal1 = new float3(
                sample.V1011.Density - sample.V1211.Density,
                sample.V1101.Density - sample.V1121.Density,
                sample.V1110.Density - sample.V1112.Density
            );

            return sample;
        }

        /// <summary>
        /// Used by MarchingCubeJob and TransitionMarchingCubeJob, identically. Returns true only
        /// if every endpoint (of voxel0/voxel1) whose demodulated material matches targetMat is
        /// mature. If neither endpoint's material matches targetMat (i.e. targetMat only came from
        /// the wider 14-voxel dominance neighborhood, not the endpoints themselves), this defaults
        /// to false rather than guessing at maturity from data outside the endpoints.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ResolveMaturity(
            byte targetMat, byte matIndex0, bool isMature0, byte matIndex1, bool isMature1) {
            var matched = false;
            var allMature = true;

            if (matIndex0 == targetMat) {
                matched = true;
                allMature &= isMature0;
            }

            if (matIndex1 == targetMat) {
                matched = true;
                allMature &= isMature1;
            }

            return matched && allMature;
        }

        /// <summary>
        /// Used by MarchingCubeJob and TransitionMarchingCubeJob, identically. Skips any voxel
        /// that isn't actually "full" (density >= 0) - the same zero split the mesher uses for the
        /// case code, so a voxel can never simultaneously count as "empty" for geometry and "a real
        /// material" for this vote. Also guarantees the null/sentinel material (always negative
        /// density) can never contribute weight here, and therefore can never be selected as a
        /// dominant material by the caller.
        /// </summary>
        internal static void AddMaterialWeight(
            in VoxelData voxel,
            float baseWeight,
            ref NativeList<byte> uniqueMaterials,
            ref NativeList<float> materialWeights) {
            if (voxel.Density < 0) return;

            // Demodulate: material identity (0-127) only. Maturity is resolved separately via
            // ResolveMaturity, checked only against voxel0/voxel1, not this wider neighborhood.
            var matIndex = voxel.GetPlainMatIndex();
            var densityWeight = voxel.Density / (float)sbyte.MaxValue;
            var weight = baseWeight * densityWeight;

            var existingIndex = -1;

            for (var k = 0; k < uniqueMaterials.Length; k++) {
                if (uniqueMaterials[k] == matIndex) {
                    existingIndex = k;

                    break;
                }
            }

            if (existingIndex >= 0)
                materialWeights[existingIndex] += weight;
            else {
                uniqueMaterials.Add(matIndex);
                materialWeights.Add(weight);
            }
        }
    }
}
// Copyright 2026 Spellbound Studio Inc.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Job to March the Cubes (generate vertices and triangles from voxels) for the transition
    /// regions of a leaf of terrain, for the FlatShaded/Barycentric material scheme. Every
    /// triangle gets exclusive vertices and a face normal, and material is the "full" voxel on
    /// each vertex's edge (no blending) - same scheme as FlatBaryMarchJob, packed the same way.
    /// </summary>
    [BurstCompile]
    public struct TransFlatBaryMarchJob : IJob {
        [ReadOnly] public BlobAssetReference<McTablesBlobAsset> TablesBlob;
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> TransitionMeshingVertexData;
        public NativeList<int> TransitionTriangles;
        public NativeArray<int2> TransitionRanges;

        public int Lod;
        public int3 Start;

        /// <summary>
        /// Lightweight per-edge-vertex data cached across cube marches. Unlike FlatBaryMarchJob's
        /// cache, this one needs an explicit IsValid flag: the transition algorithm's cacheValidator
        /// bitmask isn't always sufficient by itself to guarantee a cache slot was actually
        /// populated (matching the original TransitionMarchingCubeJob's "vertexIndex == -1" check).
        /// default(EdgeVertex) has IsValid == false for free.
        /// </summary>
        private struct EdgeVertex {
            public float3 Position;
            public byte RawMaterial;
            public bool IsValid;
        }

        public void Execute() {
            var currentStart = 0;
            GenerateTransitionMesh(GfStaticHelper.TransitionFaceMask.XMin);
            TransitionRanges[0] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(GfStaticHelper.TransitionFaceMask.YMin);
            TransitionRanges[1] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(GfStaticHelper.TransitionFaceMask.ZMin);
            TransitionRanges[2] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(GfStaticHelper.TransitionFaceMask.XMax);
            TransitionRanges[3] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(GfStaticHelper.TransitionFaceMask.YMax);
            TransitionRanges[4] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(GfStaticHelper.TransitionFaceMask.ZMax);
            TransitionRanges[5] = new int2(currentStart, TransitionTriangles.Length);
        }

        private void GenerateTransitionMesh(GfStaticHelper.TransitionFaceMask direction) {
            ref var tables = ref TablesBlob.Value;
            ref var config = ref ConfigBlob.Value;
            const int padding = 1;
            var lodScale = 1 << Lod;

            var transitionCurrentCache =
                    new NativeArray<EdgeVertex>(config.CubesMarchedPerOctreeLeaf * 10, Allocator.Temp);

            var transitionPreviousCache =
                    new NativeArray<EdgeVertex>(config.CubesMarchedPerOctreeLeaf * 10, Allocator.Temp);
            var transitionVertexIndices = new NativeArray<EdgeVertex>(36, Allocator.Temp);
            var transitionCellValues = new NativeArray<VoxelData>(13, Allocator.Temp);

            for (var y = 0; y < config.CubesMarchedPerOctreeLeaf; y++) {
                for (var x = 0; x < config.CubesMarchedPerOctreeLeaf; x++) {
                    for (var i = 0; i < 13; i++) {
                        var offset = tables.TransitionCornerOffset[i];

                        var voxelPosition = Start + new int3(padding, padding, padding) + FaceToLocalSpace(direction,
                                    config.CubesMarchedPerOctreeLeaf * 2, x * 2 + offset.x, y * 2 + offset.y,
                                    0) *
                                (lodScale >> 1);

                        transitionCellValues[i] = VoxelArray[GfStaticHelper.Coord3DToIndex(
                            voxelPosition.x, voxelPosition.y, voxelPosition.z, config.ChunkDataAreaSize,
                            config.ChunkDataWidthSize)];
                    }

                    var caseCode = (transitionCellValues[0].Density >= config.DensityThreshold ? 1 : 0)
                                   | (transitionCellValues[1].Density >= config.DensityThreshold ? 2 : 0)
                                   | (transitionCellValues[2].Density >= config.DensityThreshold ? 4 : 0)
                                   | (transitionCellValues[5].Density >= config.DensityThreshold ? 8 : 0)
                                   | (transitionCellValues[8].Density >= config.DensityThreshold ? 16 : 0)
                                   | (transitionCellValues[7].Density >= config.DensityThreshold ? 32 : 0)
                                   | (transitionCellValues[6].Density >= config.DensityThreshold ? 64 : 0)
                                   | (transitionCellValues[3].Density >= config.DensityThreshold ? 128 : 0)
                                   | (transitionCellValues[4].Density >= config.DensityThreshold ? 256 : 0);

                    transitionCurrentCache[0 * config.CubesMarchedPerOctreeLeaf + x] = default;
                    transitionCurrentCache[1 * config.CubesMarchedPerOctreeLeaf + x] = default;
                    transitionCurrentCache[2 * config.CubesMarchedPerOctreeLeaf + x] = default;
                    transitionCurrentCache[7 * config.CubesMarchedPerOctreeLeaf + x] = default;

                    if (caseCode == 0 || caseCode == 511)
                        continue;

                    var cacheValidator = (x != 0 ? 0b01 : 0)
                                         | (y != 0 ? 0b10 : 0);

                    int cellClass = tables.TransitionCellClass[caseCode];
                    ref var edgeCodes = ref tables.TransitionVertexData[caseCode];
                    ref var cellVertCount = ref tables.TransitionVertexCount[cellClass & 0x7F];

                    for (var i = 0; i < cellVertCount; ++i) {
                        var edgeCode = edgeCodes[i];
                        var cornerIdx0 = (ushort)((edgeCode >> 4) & 0x0F);
                        var cornerIdx1 = (ushort)(edgeCode & 0x0F);
                        var cacheIdx = (byte)((edgeCode >> 8) & 0x0F);
                        var cacheDir = (byte)(edgeCode >> 12);

                        if (transitionCellValues[cornerIdx1].Density == config.DensityThreshold) {
                            var trCornerData = tables.TransitionCornerData[cornerIdx1];
                            cacheDir = (byte)((trCornerData >> 4) & 0x0F);
                            cacheIdx = (byte)(trCornerData & 0x0F);
                        }
                        else if (transitionCellValues[cornerIdx0].Density == config.DensityThreshold) {
                            var trCornerData = tables.TransitionCornerData[cornerIdx0];
                            cacheDir = (byte)((trCornerData >> 4) & 0x0F);
                            cacheIdx = (byte)(trCornerData & 0x0F);
                        }

                        var isVertexCacheable = (cacheDir & cacheValidator) == cacheDir;
                        var cachePosX = x - (cacheDir & 1);

                        var selectedCacheDock = (cacheDir & 2) > 0 ? transitionPreviousCache : transitionCurrentCache;

                        var edgeVertex = isVertexCacheable
                                ? selectedCacheDock[cacheIdx * config.CubesMarchedPerOctreeLeaf + cachePosX]
                                : default;

                        if (!isVertexCacheable || !edgeVertex.IsValid) {
                            var cornerOffset0 = tables.TransitionCornerOffset[cornerIdx0];
                            var cornerOffset1 = tables.TransitionCornerOffset[cornerIdx1];

                            var corner0Copy = Start + new int3(padding, padding, padding) + FaceToLocalSpace(
                                direction,
                                config.CubesMarchedPerOctreeLeaf * 2,
                                x * 2 + cornerOffset0.x, y * 2 + cornerOffset0.y, 0) * (lodScale >> 1);

                            var corner1Copy = Start + new int3(padding, padding, padding) + FaceToLocalSpace(
                                direction,
                                config.CubesMarchedPerOctreeLeaf * 2,
                                x * 2 + cornerOffset1.x, y * 2 + cornerOffset1.y, 0) * (lodScale >> 1);

                            var bIsLowResFace = cacheIdx > 6;
                            var subEdges = bIsLowResFace ? Lod : Lod - 1;

                            var initIndex0 = GfStaticHelper.Coord3DToIndex(corner0Copy.x, corner0Copy.y,
                                corner0Copy.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                            var initVoxel0 = VoxelArray[initIndex0];
                            var isVert0DensityAboveThreshold = initVoxel0.Density >= config.DensityThreshold;

                            var initIndex1 = GfStaticHelper.Coord3DToIndex(corner1Copy.x, corner1Copy.y,
                                corner1Copy.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                            var initVoxel1 = VoxelArray[initIndex1];

                            // Same reasoning as FlatBaryMarchJob's originalFullVoxel: capture the ORIGINAL
                            // corner pair's full voxel before the subdivision loop below moves anything,
                            // since the mc case table guarantees these two original corners are
                            // complementary - immune to the subdivision search degenerating under
                            // non-monotonic (dug/edited) density.
                            var originalFullVoxel = isVert0DensityAboveThreshold ? initVoxel0 : initVoxel1;

                            for (var j = 0; j < subEdges; ++j) {
                                var midPointLocalPos = (float3)(corner0Copy + corner1Copy) * 0.5f;
                                var samplePos = (int3)math.round(midPointLocalPos);

                                var midPointDensity =
                                        VoxelArray[
                                                    GfStaticHelper.Coord3DToIndex(samplePos.x, samplePos.y,
                                                        samplePos.z,
                                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)]
                                                .Density;

                                var isMidPointDensityAboveThreshold = midPointDensity >= config.DensityThreshold;

                                var isVertexNearerToVert1 =
                                        (isMidPointDensityAboveThreshold && isVert0DensityAboveThreshold)
                                        || (!isMidPointDensityAboveThreshold && !isVert0DensityAboveThreshold);

                                if (isVertexNearerToVert1)
                                    corner0Copy = samplePos;
                                else
                                    corner1Copy = samplePos;
                            }

                            var index0 = GfStaticHelper.Coord3DToIndex(corner0Copy.x, corner0Copy.y,
                                corner0Copy.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                            var voxel0 = VoxelArray[index0];

                            var index1 = GfStaticHelper.Coord3DToIndex(corner1Copy.x, corner1Copy.y,
                                corner1Copy.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                            var voxel1 = VoxelArray[index1];

                            var t = ((float)config.DensityThreshold - voxel0.Density) /
                                    (voxel1.Density - voxel0.Density);
                            t = math.clamp(t, 0, 1);

                            var vertex = math.lerp(corner0Copy, corner1Copy, t);
                            var centeredVertex = (vertex + config.OffsetBurst) * config.Resolution;

                            edgeVertex = new EdgeVertex {
                                Position = centeredVertex,
                                RawMaterial = originalFullVoxel.MaterialIndex,
                                IsValid = true
                            };

                            if (bIsLowResFace) {
                                if (cacheDir == 8) {
                                    transitionCurrentCache[cacheIdx * config.CubesMarchedPerOctreeLeaf + x] =
                                            edgeVertex;
                                }
                                else if (isVertexCacheable) {
                                    selectedCacheDock[cacheIdx * config.CubesMarchedPerOctreeLeaf + cachePosX] =
                                            edgeVertex;
                                }
                            }

                            if (cacheDir == 8)
                                transitionCurrentCache[cacheIdx * config.CubesMarchedPerOctreeLeaf + x] = edgeVertex;
                            else if (isVertexCacheable && cacheDir != 4) {
                                selectedCacheDock[cacheIdx * config.CubesMarchedPerOctreeLeaf + cachePosX] =
                                        edgeVertex;
                            }
                        }

                        transitionVertexIndices[i] = edgeVertex;
                    }

                    var indexCount = tables.TransitionTriangleCount[cellClass & 0x7F];
                    ref var cellIndices = ref tables.TransitionIndices[cellClass & 0x7F];
                    var bFlipWinding = (cellClass & 0x80) > 0;

                    for (var i = 0; i < indexCount; i += 3) {
                        var vA = transitionVertexIndices[cellIndices[i + 0]];
                        var vB = transitionVertexIndices[cellIndices[i + 1]];
                        var vC = transitionVertexIndices[cellIndices[i + 2]];

                        if (IsDegenerateTriangle(vA.Position, vB.Position, vC.Position)) continue;

                        // Face normal depends on the ACTUAL push order below, which flips with
                        // bFlipWinding - unlike FlatBaryMarchJob (always ic,ib,ia), this job pushes
                        // (ia,ib,ic) by default and (ic,ib,ia) when flipped, matching the original
                        // TransitionMarchingCubeJob's Triangles.Add order exactly. The formula in
                        // both branches is the general "cross(P1-P0, P2-P0)" for whichever 3
                        // positions (P0,P1,P2) actually get pushed, in that order.
                        var normal = bFlipWinding
                                ? math.normalize(math.cross(vB.Position - vC.Position, vA.Position - vC.Position))
                                : math.normalize(math.cross(vB.Position - vA.Position, vC.Position - vA.Position));

                        var matTriple = new Color32(vA.RawMaterial, vB.RawMaterial, vC.RawMaterial, 255);

                        var newIa = TransitionMeshingVertexData.Length;
                        TransitionMeshingVertexData.Add(
                            new MeshingVertexData(vA.Position, normal, matTriple, new float2(1, 0)));

                        var newIb = TransitionMeshingVertexData.Length;
                        TransitionMeshingVertexData.Add(
                            new MeshingVertexData(vB.Position, normal, matTriple, new float2(0, 1)));

                        var newIc = TransitionMeshingVertexData.Length;
                        TransitionMeshingVertexData.Add(
                            new MeshingVertexData(vC.Position, normal, matTriple, new float2(0, 0)));

                        if (bFlipWinding) {
                            TransitionTriangles.Add(newIc);
                            TransitionTriangles.Add(newIb);
                            TransitionTriangles.Add(newIa);
                        }
                        else {
                            TransitionTriangles.Add(newIa);
                            TransitionTriangles.Add(newIb);
                            TransitionTriangles.Add(newIc);
                        }
                    }
                }

                (transitionCurrentCache, transitionPreviousCache) = (transitionPreviousCache, transitionCurrentCache);
            }
        }

        private bool IsDegenerateTriangle(float3 a, float3 b, float3 c) {
            var area = math.length(math.cross(b - a, c - a));

            return area < 1e-5f; // Tweak epsilon if needed
        }

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int3 FaceToLocalSpace(
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
    }
}
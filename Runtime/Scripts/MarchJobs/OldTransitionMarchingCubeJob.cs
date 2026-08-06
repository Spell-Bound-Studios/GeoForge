// Copyright 2026 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Job to March the Cubes (generate vertices and triangles from voxels) for the transition regions of a leaf of
    /// terrain.
    /// </summary>
    [BurstCompile]
    internal struct OldTransitionMarchingCubeJob : IJob {
        [ReadOnly] public BlobAssetReference<McTablesBlobAsset> TablesBlob;
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> TransitionMeshingVertexData;
        public NativeList<int> TransitionTriangles;
        public NativeArray<int2> TransitionRanges;

        public int Lod;
        public int3 Start;

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
                    new NativeArray<int>(config.CubesMarchedPerOctreeLeaf * 10, Allocator.Temp);

            var transitionPreviousCache =
                    new NativeArray<int>(config.CubesMarchedPerOctreeLeaf * 10, Allocator.Temp);
            var transitionVertexIndices = new NativeArray<int>(36, Allocator.Temp);
            var transitionCellValues = new NativeArray<VoxelData>(13, Allocator.Temp);

            // Material blending structures - allocated once and reused for all vertices.
            // uniqueMaterials stores the DEMODULATED material index (0-127) only — maturity is resolved
            // separately in GetNormalAndColor, checked only against voxel0/voxel1, not this dominance vote.
            var uniqueMaterials = new NativeList<byte>(14, Allocator.Temp);
            var materialWeights = new NativeList<float>(14, Allocator.Temp);

            for (var y = 0; y < config.CubesMarchedPerOctreeLeaf; y++) {
                for (var x = 0; x < config.CubesMarchedPerOctreeLeaf; x++) {
                    for (var i = 0; i < 13; i++) {
                        var offset = tables.TransitionCornerOffset[i];

                        var voxelPosition = Start + new int3(padding, padding, padding) +
                                GfMarchHelper.FaceToLocalSpace(direction,
                                    config.CubesMarchedPerOctreeLeaf * 2, x * 2 + offset.x, y * 2 + offset.y,
                                    0) *
                                (lodScale >> 1);

                        transitionCellValues[i] = VoxelArray[GfStaticHelper.Coord3DToIndex(
                            voxelPosition.x, voxelPosition.y, voxelPosition.z, config.ChunkDataAreaSize,
                            config.ChunkDataWidthSize)];
                    }

                    var caseCode = (transitionCellValues[0].Density >= 0 ? 1 : 0)
                                   | (transitionCellValues[1].Density >= 0 ? 2 : 0)
                                   | (transitionCellValues[2].Density >= 0 ? 4 : 0)
                                   | (transitionCellValues[5].Density >= 0 ? 8 : 0)
                                   | (transitionCellValues[8].Density >= 0 ? 16 : 0)
                                   | (transitionCellValues[7].Density >= 0 ? 32 : 0)
                                   | (transitionCellValues[6].Density >= 0 ? 64 : 0)
                                   | (transitionCellValues[3].Density >= 0 ? 128 : 0)
                                   | (transitionCellValues[4].Density >= 0 ? 256 : 0);

                    transitionCurrentCache[0 * config.CubesMarchedPerOctreeLeaf + x] = -1;
                    transitionCurrentCache[1 * config.CubesMarchedPerOctreeLeaf + x] = -1;
                    transitionCurrentCache[2 * config.CubesMarchedPerOctreeLeaf + x] = -1;
                    transitionCurrentCache[7 * config.CubesMarchedPerOctreeLeaf + x] = -1;

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

                        if (transitionCellValues[cornerIdx1].Density == 0) {
                            var trCornerData = tables.TransitionCornerData[cornerIdx1];
                            cacheDir = (byte)((trCornerData >> 4) & 0x0F);
                            cacheIdx = (byte)(trCornerData & 0x0F);
                        }
                        else if (transitionCellValues[cornerIdx0].Density == 0) {
                            var trCornerData = tables.TransitionCornerData[cornerIdx0];
                            cacheDir = (byte)((trCornerData >> 4) & 0x0F);
                            cacheIdx = (byte)(trCornerData & 0x0F);
                        }

                        var isVertexCacheable = (cacheDir & cacheValidator) == cacheDir;
                        var vertexIndex = -1;

                        var cachePosX = x - (cacheDir & 1);

                        var selectedCacheDock = (cacheDir & 2) > 0 ? transitionPreviousCache : transitionCurrentCache;

                        if (isVertexCacheable) {
                            vertexIndex =
                                    selectedCacheDock[cacheIdx * config.CubesMarchedPerOctreeLeaf + cachePosX];
                        }

                        if (!isVertexCacheable || vertexIndex == -1) {
                            float3 vertex;
                            float3 normal;
                            Color32 color;
                            vertexIndex = TransitionMeshingVertexData.Length;

                            var cornerOffset0 = tables.TransitionCornerOffset[cornerIdx0];
                            var cornerOffset1 = tables.TransitionCornerOffset[cornerIdx1];

                            var corner0Copy = Start + new int3(padding, padding, padding) +
                                    GfMarchHelper.FaceToLocalSpace(direction,
                                config.CubesMarchedPerOctreeLeaf * 2,
                                x * 2 + cornerOffset0.x, y * 2 + cornerOffset0.y, 0) * (lodScale >> 1);

                            var corner1Copy = Start + new int3(padding, padding, padding) +
                                    GfMarchHelper.FaceToLocalSpace(direction,
                                config.CubesMarchedPerOctreeLeaf * 2,
                                x * 2 + cornerOffset1.x, y * 2 + cornerOffset1.y, 0) * (lodScale >> 1);

                            var bIsLowResFace = cacheIdx > 6;

                            var subEdges = bIsLowResFace ? Lod : Lod - 1;

                            // isVert0Full only needs to be sampled once, from the original
                            // corner0Copy - by construction, corner0Copy only ever moves to a
                            // point whose fullness matches its own current fullness, so
                            // re-sampling it every iteration (as this job used to) always
                            // reproduces the same value. Shared with the other three march jobs.
                            var initIndex0 = GfStaticHelper.Coord3DToIndex(corner0Copy.x, corner0Copy.y,
                                corner0Copy.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                            var isVert0Full = VoxelArray[initIndex0].Density >= 0;

                            GfMarchHelper.SubdivideToSurfaceCrossing(
                                VoxelArray, config.ChunkDataAreaSize, config.ChunkDataWidthSize, subEdges,
                                isVert0Full, ref corner0Copy, ref corner1Copy);

                            var index0 = GfStaticHelper.Coord3DToIndex(corner0Copy.x, corner0Copy.y,
                                corner0Copy.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                            var voxel0 = VoxelArray[index0];

                            var index1 = GfStaticHelper.Coord3DToIndex(corner1Copy.x, corner1Copy.y,
                                corner1Copy.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                            var voxel1 = VoxelArray[index1];

                            var t = (float)-voxel0.Density / (voxel1.Density - voxel0.Density);

                            t = math.clamp(t, 0, 1); // safety clamp

                            vertex = math.lerp(corner0Copy, corner1Copy, t);

                            GetNormalAndColor(corner0Copy, corner1Copy, t, ref uniqueMaterials, ref materialWeights,
                                out var n, out var c);
                            normal = n;
                            color = c;
                            var colorInterp = new float4((float)c.r / byte.MaxValue, 0, 0, 0);

                            if (bIsLowResFace) {
                                if (cacheDir == 8) {
                                    transitionCurrentCache[cacheIdx * config.CubesMarchedPerOctreeLeaf + x] =
                                            vertexIndex;
                                }
                                else if (isVertexCacheable) {
                                    selectedCacheDock[cacheIdx * config.CubesMarchedPerOctreeLeaf + cachePosX] =
                                            vertexIndex;
                                }
                            }

                            if (cacheDir == 8)
                                transitionCurrentCache[cacheIdx * config.CubesMarchedPerOctreeLeaf + x] = vertexIndex;
                            else if (isVertexCacheable && cacheDir != 4) {
                                selectedCacheDock[cacheIdx * config.CubesMarchedPerOctreeLeaf + cachePosX] =
                                        vertexIndex;
                            }

                            var centeredVertex = (vertex + config.OffsetBurst) * config.Resolution;

                            TransitionMeshingVertexData.Add(new MeshingVertexData(centeredVertex, normal,
                                color, colorInterp));
                        }

                        transitionVertexIndices[i] = vertexIndex;
                    }

                    var indexCount = tables.TransitionTriangleCount[cellClass & 0x7F];

                    ref var cellIndices = ref tables.TransitionIndices[cellClass & 0x7F];

                    var bFlipWinding = (cellClass & 0x80) > 0;

                    for (var i = 0; i < indexCount; i += 3) {
                        var ia = transitionVertexIndices[cellIndices[i + 0]];
                        var ib = transitionVertexIndices[cellIndices[i + 1]];
                        var ic = transitionVertexIndices[cellIndices[i + 2]];

                        if (!GfMarchHelper.IsDegenerateTriangle(TransitionMeshingVertexData[ia].Position,
                                TransitionMeshingVertexData[ib].Position, TransitionMeshingVertexData[ic].Position)) {
                            if (bFlipWinding) {
                                TransitionTriangles.Add(ic);
                                TransitionTriangles.Add(ib);
                                TransitionTriangles.Add(ia);
                            }
                            else {
                                TransitionTriangles.Add(ia);
                                TransitionTriangles.Add(ib);
                                TransitionTriangles.Add(ic);
                            }
                        }
                    }
                }

                (transitionCurrentCache, transitionPreviousCache) = (transitionPreviousCache, transitionCurrentCache);
            }

            // Dispose reused structures
            uniqueMaterials.Dispose();
            materialWeights.Dispose();
        }

        private void GetNormalAndColor(
            int3 corner0, int3 corner1, float t, ref NativeList<byte> uniqueMaterials,
            ref NativeList<float> materialWeights, out float3 normal, out Color32 color) {
            ref var config = ref ConfigBlob.Value;

            var index0 = GfStaticHelper.Coord3DToIndex(corner0.x, corner0.y, corner0.z, config.ChunkDataAreaSize,
                config.ChunkDataWidthSize);
            var voxel0 = VoxelArray[index0];

            var index1 = GfStaticHelper.Coord3DToIndex(corner1.x, corner1.y, corner1.z, config.ChunkDataAreaSize,
                config.ChunkDataWidthSize);
            var voxel1 = VoxelArray[index1];

            // The 14-voxel neighborhood (endpoints' 6 axis-neighbors each) plus the two endpoint
            // gradient normals derived from it - shared with MarchingCubeJob.
            var sample = GfMarchHelper.SampleNeighborGradient(
                VoxelArray, corner0, corner1, config.ChunkDataAreaSize, config.ChunkDataWidthSize);

            // The normal is a weighted average of the normals at the ends of the edges, same as
            // the vertex position.
            normal = math.lerp(sample.Normal0, sample.Normal1, t);
            normal = math.normalize(normal);

            // Clear lists for reuse
            uniqueMaterials.Clear();
            materialWeights.Clear();

            var weight0 = 1f - t;

            // Add all voxel contributions (14-voxel neighborhood — this decides which TWO materials
            // dominate this vertex, purely by material identity). Voxels with negative density
            // (including the null/sentinel material) never contribute — see GfMarchHelper.AddMaterialWeight.
            GfMarchHelper.AddMaterialWeight(voxel0, weight0, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V0011, weight0, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V0211, weight0, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V0101, weight0, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V0121, weight0, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V0110, weight0, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V0112, weight0, ref uniqueMaterials, ref materialWeights);

            GfMarchHelper.AddMaterialWeight(voxel1, t, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V1011, t, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V1211, t, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V1101, t, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V1121, t, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V1110, t, ref uniqueMaterials, ref materialWeights);
            GfMarchHelper.AddMaterialWeight(sample.V1112, t, ref uniqueMaterials, ref materialWeights);

            // Find top 2 materials (0-127 only — maturity plays no role in this selection).
            byte matA = 0;
            byte matB = 0;
            float matAWeight = 0;
            float matBWeight = 0;

            for (var l = 0; l < uniqueMaterials.Length; l++) {
                if (materialWeights[l] > matAWeight) {
                    matB = matA;
                    matBWeight = matAWeight;
                    matA = uniqueMaterials[l];
                    matAWeight = materialWeights[l];
                }
                else if (materialWeights[l] > matBWeight) {
                    matB = uniqueMaterials[l];
                    matBWeight = materialWeights[l];
                }
            }

            // Maturity is checked ONLY against the two edge endpoints (voxel0/voxel1), not the wider
            // 14-voxel dominance neighborhood above — "is this specific crossing point mature," not
            // "is this whole neighborhood uniformly mature." Same rule as MarchingCubeJob.
            var matIndex0 = voxel0.GetPlainMatIndex();
            var isMature0 = voxel0.IsMature();
            var matIndex1 = voxel1.GetPlainMatIndex();
            var isMature1 = voxel1.IsMature();

            var matAAllMature = GfMarchHelper.ResolveMaturity(matA, matIndex0, isMature0, matIndex1, isMature1);
            var matBAllMature = GfMarchHelper.ResolveMaturity(matB, matIndex0, isMature0, matIndex1, isMature1);

            color = new Color32(
                matA,
                matB,
                (byte)(matAAllMature ? 255 : 0),
                (byte)(matBAllMature ? 255 : 0)
            );
        }
    }
}
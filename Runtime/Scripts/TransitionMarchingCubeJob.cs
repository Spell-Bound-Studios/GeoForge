// Copyright 2025 Spellbound Studio Inc.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [BurstCompile]
    public struct TransitionMarchingCubeJob : IJob {
        [ReadOnly] public BlobAssetReference<MCTablesBlobAsset> Tables;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> TransitionMeshingVertexData;
        public NativeList<int> TransitionTriangles;
        public NativeArray<int2> TransitionRanges;

        public int Lod;
        public int3 Start;

        public void Execute() {
            var currentStart = 0;
            GenerateTransitionMesh(McStaticHelper.TransitionFaceMask.XMin);
            TransitionRanges[0] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(McStaticHelper.TransitionFaceMask.YMin);
            TransitionRanges[1] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(McStaticHelper.TransitionFaceMask.ZMin);
            TransitionRanges[2] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(McStaticHelper.TransitionFaceMask.XMax);
            TransitionRanges[3] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(McStaticHelper.TransitionFaceMask.YMax);
            TransitionRanges[4] = new int2(currentStart, TransitionTriangles.Length);
            currentStart = TransitionTriangles.Length;
            GenerateTransitionMesh(McStaticHelper.TransitionFaceMask.ZMax);
            TransitionRanges[5] = new int2(currentStart, TransitionTriangles.Length);
        }

        private void GenerateTransitionMesh(McStaticHelper.TransitionFaceMask direction) {
            const int padding = 1;
            var lodScale = 1 << Lod;

            var transitionCurrentCache =
                    new NativeArray<int>(McStaticHelper.CubesMarchedPerOctreeLeaf * 10, Allocator.Temp);

            var transitionPreviousCache =
                    new NativeArray<int>(McStaticHelper.CubesMarchedPerOctreeLeaf * 10, Allocator.Temp);
            var transitionVertexIndices = new NativeArray<int>(36, Allocator.Temp);
            var transitionCellValues = new NativeArray<VoxelData>(13, Allocator.Temp);

            //var vertexData = transitionMeshData.VertexData;
            //var indices = transitionMeshData.Indices;

            for (var y = 0; y < McStaticHelper.CubesMarchedPerOctreeLeaf; y++) {
                for (var x = 0; x < McStaticHelper.CubesMarchedPerOctreeLeaf; x++) {
                    for (var i = 0; i < 13; i++) {
                        var offset = Tables.Value.TransitionCornerOffset[i];

                        var voxelPosition = Start + new int3(padding, padding, padding) + FaceToLocalSpace(direction,
                                    McStaticHelper.CubesMarchedPerOctreeLeaf * 2, x * 2 + offset.x, y * 2 + offset.y,
                                    0) *
                                (lodScale >> 1);

                        transitionCellValues[i] = VoxelArray[McStaticHelper.Coord3DToIndex(
                            voxelPosition.x, voxelPosition.y, voxelPosition.z)];
                    }

                    var caseCode = (transitionCellValues[0].Density >= McStaticHelper.DensityThreshold ? 1 : 0)
                                   | (transitionCellValues[1].Density >= McStaticHelper.DensityThreshold ? 2 : 0)
                                   | (transitionCellValues[2].Density >= McStaticHelper.DensityThreshold ? 4 : 0)
                                   | (transitionCellValues[5].Density >= McStaticHelper.DensityThreshold ? 8 : 0)
                                   | (transitionCellValues[8].Density >= McStaticHelper.DensityThreshold ? 16 : 0)
                                   | (transitionCellValues[7].Density >= McStaticHelper.DensityThreshold ? 32 : 0)
                                   | (transitionCellValues[6].Density >= McStaticHelper.DensityThreshold ? 64 : 0)
                                   | (transitionCellValues[3].Density >= McStaticHelper.DensityThreshold ? 128 : 0)
                                   | (transitionCellValues[4].Density >= McStaticHelper.DensityThreshold ? 256 : 0);

                    transitionCurrentCache[0 * McStaticHelper.CubesMarchedPerOctreeLeaf + x] = -1;
                    transitionCurrentCache[1 * McStaticHelper.CubesMarchedPerOctreeLeaf + x] = -1;
                    transitionCurrentCache[2 * McStaticHelper.CubesMarchedPerOctreeLeaf + x] = -1;
                    transitionCurrentCache[7 * McStaticHelper.CubesMarchedPerOctreeLeaf + x] = -1;

                    if (caseCode == 0 || caseCode == 511) continue;

                    var cacheValidator = (x != 0 ? 0b01 : 0)
                                         | (y != 0 ? 0b10 : 0);

                    int cellClass = Tables.Value.TransitionCellClass[caseCode];
                    ref var edgeCodes = ref Tables.Value.TransitionVertexData[caseCode];
                    ref var cellVertCount = ref Tables.Value.TransitionVertexCount[cellClass & 0x7F];

                    for (var i = 0; i < cellVertCount; ++i) {
                        var edgeCode = edgeCodes[i];
                        var cornerIdx0 = (ushort)((edgeCode >> 4) & 0x0F);
                        var cornerIdx1 = (ushort)(edgeCode & 0x0F);

                        float density0 = transitionCellValues[cornerIdx0].Density;
                        float density1 = transitionCellValues[cornerIdx1].Density;

                        var cacheIdx = (byte)((edgeCode >> 8) & 0x0F);
                        var cacheDir = (byte)(edgeCode >> 12);

                        if (density1 == 0) {
                            var trCornerData = Tables.Value.TransitionCornerData[cornerIdx1];
                            cacheDir = (byte)((trCornerData >> 4) & 0x0F);
                            cacheIdx = (byte)(trCornerData & 0x0F);
                        }
                        else if (density0 == 0) {
                            var trCornerData = Tables.Value.TransitionCornerData[cornerIdx0];
                            cacheDir = (byte)((trCornerData >> 4) & 0x0F);
                            cacheIdx = (byte)(trCornerData & 0x0F);
                        }

                        var isVertexCacheable = (cacheDir & cacheValidator) == cacheDir;
                        var vertexIndex = -1;

                        var cachePosX = x - (cacheDir & 1);

                        var selectedCacheDock = (cacheDir & 2) > 0 ? transitionPreviousCache : transitionCurrentCache;

                        if (isVertexCacheable) {
                            vertexIndex =
                                    selectedCacheDock[cacheIdx * McStaticHelper.CubesMarchedPerOctreeLeaf + cachePosX];
                        }

                        if (!isVertexCacheable || vertexIndex == -1) {
                            float3 vertex;
                            float3 normal;
                            Color32 color;
                            vertexIndex = TransitionMeshingVertexData.Length;

                            var cornerOffset0 = Tables.Value.TransitionCornerOffset[cornerIdx0];
                            var cornerOffset1 = Tables.Value.TransitionCornerOffset[cornerIdx1];

                            var corner0Copy = Start + new int3(padding, padding, padding) + FaceToLocalSpace(direction,
                                McStaticHelper.CubesMarchedPerOctreeLeaf * 2,
                                x * 2 + cornerOffset0.x, y * 2 + cornerOffset0.y, 0) * (lodScale >> 1);

                            var corner1Copy = Start + new int3(padding, padding, padding) + FaceToLocalSpace(direction,
                                McStaticHelper.CubesMarchedPerOctreeLeaf * 2,
                                x * 2 + cornerOffset1.x, y * 2 + cornerOffset1.y, 0) * (lodScale >> 1);

                            var bIsLowResFace = cacheIdx > 6;

                            var subEdges = bIsLowResFace ? Lod : Lod - 1;

                            for (var j = 0; j < subEdges; ++j) {
                                var midPointLocalPos = ((float3)corner0Copy + (float3)corner1Copy) * 0.5f;

                                var samplePos = (int3)math.round(midPointLocalPos);

                                var midPointDensity =
                                        VoxelArray[McStaticHelper.Coord3DToIndex(samplePos.x, samplePos.y, samplePos.z)]
                                                .Density;

                                var isMidPointDensityAboveThreshold =
                                        midPointDensity >= McStaticHelper.DensityThreshold;

                                var isVert0DensityAboveThreshold =
                                        VoxelArray[
                                                    McStaticHelper.Coord3DToIndex(corner0Copy.x, corner0Copy.y,
                                                        corner0Copy.z)]
                                                .Density >= McStaticHelper.DensityThreshold;

                                var isVertexNearerToVert1 =
                                        (isMidPointDensityAboveThreshold && isVert0DensityAboveThreshold)
                                        || (!isMidPointDensityAboveThreshold && !isVert0DensityAboveThreshold);

                                if (isVertexNearerToVert1)
                                    corner0Copy = samplePos;

                                else
                                    corner1Copy = samplePos;
                            }

                            var index0 = McStaticHelper.Coord3DToIndex(corner0Copy.x, corner0Copy.y,
                                corner0Copy.z);
                            var voxel0 = VoxelArray[index0];

                            var index1 = McStaticHelper.Coord3DToIndex(corner1Copy.x, corner1Copy.y,
                                corner1Copy.z);
                            var voxel1 = VoxelArray[index1];

                            var t = ((float)McStaticHelper.DensityThreshold - voxel0.Density) /
                                    (voxel1.Density - voxel0.Density);

                            t = math.clamp(t, 0, 1); // safety clamp

                            vertex = math.lerp(corner0Copy, corner1Copy, t);

                            GetNormalAndColor(corner0Copy, corner1Copy, t, out var n, out var c);
                            normal = n;
                            color = c;

                            //Debug.Log($"vertexIndex being loaded into transitionVertexIndices: {vertexIndex}");

                            transitionVertexIndices[i] = TransitionMeshingVertexData.Length;

                            // This puts the vertex data into the vertex array, which is used to Build the Mesh

                            if (cacheDir == 8) {
                                transitionCurrentCache[cacheIdx * McStaticHelper.CubesMarchedPerOctreeLeaf + x] =
                                        vertexIndex;
                            }
                            else if (isVertexCacheable && cacheDir != 4) {
                                selectedCacheDock[cacheIdx * McStaticHelper.CubesMarchedPerOctreeLeaf + cachePosX] =
                                        vertexIndex;
                            }

                            TransitionMeshingVertexData.Add(new MeshingVertexData(vertex, normal,
                                color));
                        }

                        transitionVertexIndices[i] = vertexIndex;
                    }

                    var indexCount = Tables.Value.TransitionTriangleCount[cellClass & 0x7F];

                    ref var cellIndices = ref Tables.Value.TransitionIndices[cellClass & 0x7F];

                    var bFlipWinding = (cellClass & 0x80) > 0;

                    for (var i = 0; i < indexCount; i += 3) {
                        var ia = transitionVertexIndices[cellIndices[i + 0]];
                        var ib = transitionVertexIndices[cellIndices[i + 1]];
                        var ic = transitionVertexIndices[cellIndices[i + 2]];

                        if (!IsDegenerateTriangle(TransitionMeshingVertexData[ia].position,
                                TransitionMeshingVertexData[ib].position, TransitionMeshingVertexData[ic].position)) {
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
        }

        private void GetNormalAndColor(int3 corner0, int3 corner1, float t, out float3 normal, out Color32 color) {
            var vertPosX0 = corner0.x;
            var vertPosY0 = corner0.y;
            var vertPosZ0 = corner0.z;
            var vertPosX1 = corner1.x;
            var vertPosY1 = corner1.y;
            var vertPosZ1 = corner1.z;

            var voxel0 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0, vertPosZ0)];

            var voxel1 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1, vertPosZ1)];

            var v0_011 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX0 - 1, vertPosY0, vertPosZ0)];

            var v0_211 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX0 + 1, vertPosY0, vertPosZ0)];

            var v0_101 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0 - 1, vertPosZ0)];

            var v0_121 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0 + 1, vertPosZ0)];

            var v0_110 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0, vertPosZ0 - 1)];

            var v0_112 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0, vertPosZ0 + 1)];

            var v1_011 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX1 - 1, vertPosY1, vertPosZ1)];

            var v1_211 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX1 + 1, vertPosY1, vertPosZ1)];

            var v1_101 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1 - 1, vertPosZ1)];

            var v1_121 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1 + 1, vertPosZ1)];

            var v1_110 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1, vertPosZ1 - 1)];

            var v1_112 = VoxelArray[
                McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1, vertPosZ1 + 1)];

            var normal0 = new float3(v0_011.Density - v0_211.Density,
                v0_101.Density - v0_121.Density,
                v0_110.Density - v0_112.Density
            );

            var normal1 = new float3(v1_011.Density - v1_211.Density,
                v1_101.Density - v1_121.Density,
                v1_110.Density - v1_112.Density
            );

            // The normal is a weighted average of the normals at the ends of the edges, same as
            // the vertex position.
            normal = math.lerp(normal0, normal1, t);
            normal = math.normalize(normal);

            float matA_Weight = 0;
            float matB_Weight = 0;

            byte matA = 0;
            byte matB = 1;

            if (voxel0.MatIndex == 1) t = 1 - t;

            var matS0 = new NativeArray<byte>(6, Allocator.Temp);
            matS0[0] = v0_011.MatIndex;
            matS0[1] = v0_211.MatIndex;
            matS0[2] = v0_101.MatIndex;
            matS0[3] = v0_121.MatIndex;
            matS0[4] = v0_110.MatIndex;
            matS0[5] = v0_112.MatIndex;

            var matS1 = new NativeArray<byte>(6, Allocator.Temp);
            matS1[0] = v1_011.MatIndex;
            matS1[1] = v1_211.MatIndex;
            matS1[2] = v1_101.MatIndex;
            matS1[3] = v1_121.MatIndex;
            matS1[4] = v1_110.MatIndex;
            matS1[5] = v1_112.MatIndex;

            foreach (var mat in matS0) {
                if (mat == matA) matA_Weight += 1 - t;
                else if (mat == matB) matB_Weight += 1 - t;
            }

            foreach (var mat in matS1) {
                if (mat == matA) matA_Weight += t;
                else if (mat == matB) matB_Weight += t;
            }

            matS0.Dispose();
            matS1.Dispose();

            var blend = (float)matB_Weight / (matA_Weight + matB_Weight + 1e-5f);

            var blendByte = (byte)Mathf.RoundToInt(blend * 255f);

            color = new Color32(
                voxel0.MatIndex, // R: material A
                voxel1.MatIndex, // G: material B
                blendByte,       // B: blend T for material B
                0                // A: unused (optional)
            );
        }

        private bool IsDegenerateTriangle(float3 a, float3 b, float3 c) {
            var area = math.length(math.cross(b - a, c - a));

            return area < 1e-5f; // Tweak epsilon if needed
        }

        [BurstCompile, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int3 FaceToLocalSpace(
            McStaticHelper.TransitionFaceMask direction,
            int leafSize,
            int x,
            int y,
            int z) =>
                direction switch {
                    McStaticHelper.TransitionFaceMask.XMin => new int3(z, x, y),
                    McStaticHelper.TransitionFaceMask.XMax => new int3(leafSize - z, y, x),
                    McStaticHelper.TransitionFaceMask.YMin => new int3(y, z, x),
                    McStaticHelper.TransitionFaceMask.YMax => new int3(x, leafSize - z, y),
                    McStaticHelper.TransitionFaceMask.ZMin => new int3(x, y, z),
                    McStaticHelper.TransitionFaceMask.ZMax => new int3(y, x, leafSize - z),
                    _ => new int3(x, y, z)
                };
    }
}
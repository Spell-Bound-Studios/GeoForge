// Copyright 2025 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Job to March the Cubes (generate vertices and triangles from voxels) for the main region of a leaf of terrain.
    /// </summary>
    [BurstCompile]
    internal struct MarchingCubeJob : IJob {
        [ReadOnly] public BlobAssetReference<McTablesBlobAsset> TablesBlob;
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> Vertices;
        public NativeList<int> Triangles;

        public int Lod;
        public int3 Start;

        public void Execute() {
            ref var tables = ref TablesBlob.Value;
            ref var config = ref ConfigBlob.Value;

            // Extract config values to locals for faster access
            var densityThreshold = config.DensityThreshold;
            var chunkDataAreaSize = config.ChunkDataAreaSize;
            var chunkDataWidthSize = config.ChunkDataWidthSize;
            var cubesMarchedPerLeaf = config.CubesMarchedPerOctreeLeaf;
            var resolution = config.Resolution;
            var offsetBurst = config.OffsetBurst;

            // Padding is the offset between the index in the voxel array and the local position of the voxel.
            const int padding = 1;
            var lodScale = 1 << Lod;

            // Caches hold vertex indices from previous cubes. 2 "decks" in y-axis, and 4 positions on the leading
            // corner/edges of each cube.
            var currentCache = new NativeArray<int>(
                cubesMarchedPerLeaf * cubesMarchedPerLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            var previousCache = new NativeArray<int>(
                cubesMarchedPerLeaf * cubesMarchedPerLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            // Vertex indices holds the vertex indices to be entered into the triangle array as one of the last parts
            // of marching the cube.
            var vertexIndices = new NativeArray<int>(16, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // CellValues holds the densities of the voxels at each corner of the cube.
            var cellValues = new NativeArray<VoxelData>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // Material blending structures - allocated once and reused for all vertices
            var uniqueMaterials = new NativeList<byte>(14, Allocator.Temp);
            var materialWeights = new NativeList<float>(14, Allocator.Temp);

            // Inside this nested for loop is where a single cube is marched.
            for (var y = 0; y < cubesMarchedPerLeaf; y++) {
                for (var z = 0; z < cubesMarchedPerLeaf; z++) {
                    for (var x = 0; x < cubesMarchedPerLeaf; x++) {
                        var cellPos = Start + new int3(x, y, z) * lodScale;

                        // Inside this loop we are looping through the 8 corners of the cube.
                        for (var i = 0; i < 8; ++i) {
                            var voxelPosition = cellPos + new int3(padding, padding, padding) +
                                                tables.RegularCornerOffset[i] * lodScale;

                            cellValues[i] = VoxelArray[McStaticHelper.Coord3DToIndex(
                                voxelPosition.x, voxelPosition.y, voxelPosition.z,
                                chunkDataAreaSize, chunkDataWidthSize
                            )];
                        }

                        // CaseCode indicates what kind of cube it is. Empty, full, or a mixture that needs a mesh.
                        var caseCode = (byte)((cellValues[0].Density >= densityThreshold ? 0x01 : 0)
                                              | (cellValues[1].Density >= densityThreshold ? 0x02 : 0)
                                              | (cellValues[2].Density >= densityThreshold ? 0x04 : 0)
                                              | (cellValues[3].Density >= densityThreshold ? 0x08 : 0)
                                              | (cellValues[4].Density >= densityThreshold ? 0x10 : 0)
                                              | (cellValues[5].Density >= densityThreshold ? 0x20 : 0)
                                              | (cellValues[6].Density >= densityThreshold ? 0x40 : 0)
                                              | (cellValues[7].Density >= densityThreshold ? 0x80 : 0));

                        // This is a "bit twiddle" to more efficiently check if the cube is "empty" caseCode = 0
                        // or "full" caseCode = 255.
                        if ((caseCode ^ ((cellValues[7].Density >> 7) & 0xFF)) == 0) continue;

                        // Cache validator is a bitwise mask to see if the cube is on any minimal edge of the chunk
                        // where some data does not exist.
                        var cacheValidator = (x != 0 ? 0x01 : 0)
                                             | (z != 0 ? 0x02 : 0)
                                             | (y != 0 ? 0x04 : 0);

                        // CellClass, edgeCodes, and CellData are pre-computed solutions for how to march the cube,
                        // based on what type of cube (caseCode).
                        int cellClass = tables.RegularCellClass[caseCode];
                        ref var edgeCodes = ref tables.RegularVertexData[caseCode];

                        // CellVertCount indicates how many vertices are in the cube.
                        var cellVertCount = tables.VertexCount[cellClass];

                        // Inside this loop we are solving for a particular vertex of the cube
                        for (var i = 0; i < cellVertCount; ++i) {
                            // The following code extracts the bitwise information from the edgeCode
                            var edgeCode = edgeCodes[i];
                            var cornerIdx0 = (ushort)((edgeCode >> 4) & 0x0F);
                            var cornerIdx1 = (ushort)(edgeCode & 0x0F);
                            var cacheIdx = (byte)((edgeCode >> 8) & 0x0F);
                            var cacheDir = (byte)(edgeCode >> 12);
                            var cachePosX = x - (cacheDir & 1);
                            var cachePosZ = z - ((cacheDir >> 1) & 1);

                            var selectedCacheDock =
                                    ((cacheDir >> 2) & 1) == 1 ? previousCache : currentCache;

                            // IsVertexCache-able indicates of an existing vertex exists.
                            // It synthesizes where in the cube the vertex is, and where in the chunk the cube is.
                            var isVertexCacheable = (cacheDir & cacheValidator) == cacheDir;

                            // VertexIndex indicates what vertex will go into the triangle array to wind the triangle
                            int vertexIndex;

                            // This is the case where the vertex is available from a previous cube
                            if (isVertexCacheable) {
                                vertexIndex =
                                        selectedCacheDock[
                                            cachePosX * cubesMarchedPerLeaf * 4 + cachePosZ * 4 +
                                            cacheIdx];
                            }

                            // This is the case where a new vertex must be created.
                            else {
                                // Declare the vertex and the normal and the color and the vertexIndex (for the
                                // triangle array).
                                vertexIndex = Vertices.Length;

                                // This is caching the vertexIndex for cubes marched later in the loop. 
                                // Could be optimized to also cache more stuff when the cache validator is non-zero
                                // (aka on an edge of the chunk).
                                if (cornerIdx1 == 7) {
                                    currentCache[x * cubesMarchedPerLeaf * 4 + z * 4 + cacheIdx] =
                                            vertexIndex;
                                }

                                //Local positions of the ends of the edge along which the vertex belongs
                                var vertLocalPos0 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx0] * lodScale;

                                var vertLocalPos1 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx1] * lodScale;

                                var p0 = (float3)vertLocalPos0;
                                var p1 = (float3)vertLocalPos1;

                                // Get voxel data at endpoints early
                                var index0 = McStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel0 = VoxelArray[index0];

                                var index1 = McStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel1 = VoxelArray[index1];

                                // Cache these for the subdivision loop
                                var isVert0DensityAboveThreshold = voxel0.Density >= densityThreshold;

                                // This consecutively subdivides the coarser LOD to find the exact place the density crosses the threshold.
                                for (var j = 0; j < Lod; ++j) {
                                    var mid = (p0 + p1) * 0.5f;
                                    var samplePos = (int3)math.round(mid);

                                    var midPointDensity =
                                            VoxelArray[
                                                        McStaticHelper.Coord3DToIndex(samplePos.x, +samplePos.y,
                                                            samplePos.z, chunkDataAreaSize,
                                                            chunkDataWidthSize)]
                                                    .Density;

                                    var isMidPointDensityAboveThreshold =
                                            midPointDensity >= densityThreshold;

                                    var isVertexNearerToVert1 =
                                            (isMidPointDensityAboveThreshold && isVert0DensityAboveThreshold)
                                            || (!isMidPointDensityAboveThreshold && !isVert0DensityAboveThreshold);

                                    if (isVertexNearerToVert1) {
                                        p0 = samplePos;
                                        vertLocalPos0 = samplePos;
                                    }
                                    else {
                                        p1 = samplePos;
                                        vertLocalPos1 = samplePos;
                                    }
                                }

                                // Recompute voxel data after subdivision
                                index0 = McStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                voxel0 = VoxelArray[index0];

                                index1 = McStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, chunkDataAreaSize, chunkDataWidthSize);
                                voxel1 = VoxelArray[index1];

                                //Interpolating the vertex position based on the densities at the ends of the edge
                                //along which the vertex belongs.
                                var t = ((float)densityThreshold - voxel0.Density) /
                                        (voxel1.Density - voxel0.Density);
                                t = math.clamp(t, 0, 1); // safety clamp

                                var vertex = math.lerp(vertLocalPos0, vertLocalPos1, t);

                                // Splitting some int3 into components to make math easier.
                                var vertPosX0 = vertLocalPos0.x;
                                var vertPosY0 = vertLocalPos0.y;
                                var vertPosZ0 = vertLocalPos0.z;
                                var vertPosX1 = vertLocalPos1.x;
                                var vertPosY1 = vertLocalPos1.y;
                                var vertPosZ1 = vertLocalPos1.z;

                                var v0011 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0 - 1, vertPosY0, vertPosZ0,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v0211 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0 + 1, vertPosY0, vertPosZ0,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v0101 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0 - 1, vertPosZ0,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v0121 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0 + 1, vertPosZ0,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v0110 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0, vertPosZ0 - 1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v0112 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0, vertPosZ0 + 1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v1011 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1 - 1, vertPosY1, vertPosZ1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v1211 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1 + 1, vertPosY1, vertPosZ1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v1101 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1 - 1, vertPosZ1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v1121 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1 + 1, vertPosZ1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v1110 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1, vertPosZ1 - 1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var v1112 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1, vertPosZ1 + 1,
                                        chunkDataAreaSize, chunkDataWidthSize)];

                                var normal0 = new float3(v0011.Density - v0211.Density,
                                    v0101.Density - v0121.Density,
                                    v0110.Density - v0112.Density
                                );

                                var normal1 = new float3(v1011.Density - v1211.Density,
                                    v1101.Density - v1121.Density,
                                    v1110.Density - v1112.Density
                                );

                                // The normal is a weighted average of the normals at the ends of the edges, same as
                                // the vertex position.
                                var normal = math.lerp(normal0, normal1, t);
                                normal = math.normalize(normal);

                                // Clear lists for reuse
                                uniqueMaterials.Clear();
                                materialWeights.Clear();

                                var weight0 = 1f - t;

                                // Add all voxel contributions
                                AddMaterialWeight(voxel0, weight0, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v0011, weight0, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v0211, weight0, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v0101, weight0, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v0121, weight0, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v0110, weight0, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v0112, weight0, ref uniqueMaterials, ref materialWeights);

                                AddMaterialWeight(voxel1, t, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v1011, t, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v1211, t, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v1101, t, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v1121, t, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v1110, t, ref uniqueMaterials, ref materialWeights);
                                AddMaterialWeight(v1112, t, ref uniqueMaterials, ref materialWeights);

                                // Find top 2 materials
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

                                var colorInterp = new float2((float)matA / byte.MaxValue, 0);

                                var color = new Color32((byte)matA, (byte)matB, 0, 0);
                                var centeredVertex = (vertex + offsetBurst) * resolution;

                                Vertices.Add(new MeshingVertexData(centeredVertex, normal, color,
                                    colorInterp));
                            }

                            // For both new vertices and vertices re-used from previous cubes, the vertex index is
                            // stored in the _vertexIndices Array.
                            vertexIndices[i] = vertexIndex;
                        }

                        // IndexCount and cellIndices come from the pre-computed solutions for how to march the cube.
                        var indexCount = tables.TriangleCount[cellClass];
                        ref var cellIndices = ref tables.Indices[cellClass];

                        // Inside this loop we are looping through the triangles
                        for (var i = 0; i < indexCount; i += 3) {
                            var ia = vertexIndices[cellIndices[i + 0]];
                            var ib = vertexIndices[cellIndices[i + 1]];
                            var ic = vertexIndices[cellIndices[i + 2]];

                            if (!IsDegenerateTriangle(Vertices[ia].Position, Vertices[ib].Position,
                                    Vertices[ic].Position)) {
                                Triangles.Add(ic);
                                Triangles.Add(ib);
                                Triangles.Add(ia);
                            }
                        }
                    }
                }

                // This is setting the right caches. It is done every time the y-value increments. It changes to a
                // new "deck" of cached values.
                (currentCache, previousCache) = (previousCache, currentCache);
            }

            // Dispose reused structures
            uniqueMaterials.Dispose();
            materialWeights.Dispose();
        }

        private bool IsDegenerateTriangle(float3 a, float3 b, float3 c) {
            var area = math.length(math.cross(b - a, c - a));

            return area < 1e-5f; // Tweak epsilon if needed
        }

        [BurstCompile]
        private static void AddMaterialWeight(
            in VoxelData voxel, // Changed from 'VoxelData voxel' to 'in VoxelData voxel'
            float baseWeight,
            ref NativeList<byte> uniqueMaterials,
            ref NativeList<float> materialWeights) {
            // Skip voxels with zero density (air)
            if (voxel.Density == 0) return;

            var matIndex = voxel.MaterialIndex;
            var densityWeight = voxel.Density / 255f;
            var weight = baseWeight * densityWeight;

            // Check if material already exists
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
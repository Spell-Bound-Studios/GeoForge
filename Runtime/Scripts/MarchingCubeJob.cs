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
        [ReadOnly] public BlobAssetReference<McConfigBlobAsset> ConfigBlob;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> Vertices;
        public NativeList<int> Triangles;

        public int Lod;
        public int3 Start;

        public void Execute() {
            ref var tables = ref TablesBlob.Value;
            ref var config = ref ConfigBlob.Value;
            // Padding is the offset between the index in the voxel array and the local position of the voxel.
            const int padding = 1;
            var lodScale = 1 << Lod;

            // Caches hold vertex indices from previous cubes. 2 "decks" in y-axis, and 4 positions on the leading
            // corner/edges of each cube.
            var currentCache = new NativeArray<int>(
                config.CubesMarchedPerOctreeLeaf * config.CubesMarchedPerOctreeLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            var previousCache = new NativeArray<int>(
                config.CubesMarchedPerOctreeLeaf * config.CubesMarchedPerOctreeLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            // Vertex indices holds the vertex indices to be entered into the triangle array as one of the last parts
            // of marching the cube.
            var vertexIndices = new NativeArray<int>(16, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // CellValues holds the densities of the voxels at each corner of the cube.
            var cellValues = new NativeArray<VoxelData>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // Inside this nested for loop is where a single cube is marched.
            for (var y = 0; y < config.CubesMarchedPerOctreeLeaf; y++) {
                for (var z = 0; z < config.CubesMarchedPerOctreeLeaf; z++) {
                    for (var x = 0; x < config.CubesMarchedPerOctreeLeaf; x++) {
                        var cellPos = Start + new int3(x, y, z) * lodScale;

                        // Inside this loop we are looping through the 8 corners of the cube.
                        for (var i = 0; i < 8; ++i) {
                            var voxelPosition = cellPos + new int3(padding, padding, padding) +
                                                tables.RegularCornerOffset[i] * lodScale;

                            cellValues[i] = VoxelArray[McStaticHelper.Coord3DToIndex(
                                voxelPosition.x, voxelPosition.y, voxelPosition.z,
                                config.ChunkDataAreaSize, config.ChunkDataWidthSize
                            )];
                        }

                        // CaseCode indicates what kind of cube it is. Empty, full, or a mixture that needs a mesh.
                        var caseCode = (byte)((cellValues[0].Density >= config.DensityThreshold ? 0x01 : 0)
                                              | (cellValues[1].Density >= config.DensityThreshold ? 0x02 : 0)
                                              | (cellValues[2].Density >= config.DensityThreshold ? 0x04 : 0)
                                              | (cellValues[3].Density >= config.DensityThreshold ? 0x08 : 0)
                                              | (cellValues[4].Density >= config.DensityThreshold ? 0x10 : 0)
                                              | (cellValues[5].Density >= config.DensityThreshold ? 0x20 : 0)
                                              | (cellValues[6].Density >= config.DensityThreshold ? 0x40 : 0)
                                              | (cellValues[7].Density >= config.DensityThreshold ? 0x80 : 0));

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
                                            cachePosX * config.CubesMarchedPerOctreeLeaf * 4 + cachePosZ * 4 +
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
                                    currentCache[x * config.CubesMarchedPerOctreeLeaf * 4 + z * 4 + cacheIdx] =
                                            vertexIndex;
                                }

                                //Local positions of the ends of the edge along which the vertex belongs
                                var vertLocalPos0 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx0] * lodScale;

                                var vertLocalPos1 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx1] * lodScale;

                                var p0 = (float3)vertLocalPos0;
                                var p1 = (float3)vertLocalPos1;

                                // This consecutively subdivides the coarser LOD to find the exact place the density crosses the threshold.
                                for (var j = 0; j < Lod; ++j) {
                                    var mid = (p0 + p1) * 0.5f;
                                    var samplePos = (int3)math.round(mid);

                                    var midPointDensity =
                                            VoxelArray[
                                                        McStaticHelper.Coord3DToIndex(samplePos.x, +samplePos.y,
                                                            samplePos.z, config.ChunkDataAreaSize,
                                                            config.ChunkDataWidthSize)]
                                                    .Density;

                                    var isMidPointDensityAboveThreshold =
                                            midPointDensity >= config.DensityThreshold;

                                    var isVert0DensityAboveThreshold =
                                            VoxelArray[
                                                McStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                                    vertLocalPos0.z, config.ChunkDataAreaSize,
                                                    config.ChunkDataWidthSize)].Density >= config.DensityThreshold;

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

                                var index0 = McStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                                var voxel0 = VoxelArray[index0];

                                var index1 = McStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);
                                var voxel1 = VoxelArray[index1];

                                //Interpolating the vertex position based on the densities at the ends of the edge
                                //along which the vertex belongs.
                                var t = ((float)config.DensityThreshold - voxel0.Density) /
                                        (voxel1.Density - voxel0.Density);
                                t = math.clamp(t, 0, 1); // safety clamp

                                var vertex = math.lerp(vertLocalPos0, vertLocalPos1, t);

                                // Splitting some int3 into components to make math easier. Not sure if this is
                                // costly to define 6 new ints.
                                var vertPosX0 = vertLocalPos0.x;
                                var vertPosY0 = vertLocalPos0.y;
                                var vertPosZ0 = vertLocalPos0.z;
                                var vertPosX1 = vertLocalPos1.x;
                                var vertPosY1 = vertLocalPos1.y;
                                var vertPosZ1 = vertLocalPos1.z;

                                var v0011 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0 - 1, vertPosY0, vertPosZ0,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v0211 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0 + 1, vertPosY0, vertPosZ0,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v0101 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0 - 1, vertPosZ0,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v0121 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0 + 1, vertPosZ0,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v0110 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0, vertPosZ0 - 1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v0112 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX0, vertPosY0, vertPosZ0 + 1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v1011 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1 - 1, vertPosY1, vertPosZ1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v1211 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1 + 1, vertPosY1, vertPosZ1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v1101 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1 - 1, vertPosZ1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v1121 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1 + 1, vertPosZ1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v1110 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1, vertPosZ1 - 1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

                                var v1112 = VoxelArray[
                                    McStaticHelper.Coord3DToIndex(vertPosX1, vertPosY1, vertPosZ1 + 1,
                                        config.ChunkDataAreaSize, config.ChunkDataWidthSize)];

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

                                // More efficient version without unsafe code
                                var uniqueMaterials = new NativeList<MaterialType>(14, Allocator.Temp);
                                var materialWeights = new NativeList<float>(14, Allocator.Temp);

                                var weight0 = 1f - t;

                                // Create array of voxels to process
                                var voxelsToProcess = new NativeArray<VoxelData>(14, Allocator.Temp);

                                voxelsToProcess[0] = voxel0;
                                voxelsToProcess[1] = v0011;
                                voxelsToProcess[2] = v0211;
                                voxelsToProcess[3] = v0101;
                                voxelsToProcess[4] = v0121;
                                voxelsToProcess[5] = v0110;
                                voxelsToProcess[6] = v0112;

                                voxelsToProcess[7] = voxel1;
                                voxelsToProcess[8] = v1011;
                                voxelsToProcess[9] = v1211;
                                voxelsToProcess[10] = v1101;
                                voxelsToProcess[11] = v1121;
                                voxelsToProcess[12] = v1110;
                                voxelsToProcess[13] = v1112;

                                for (var v = 0; v < 14; v++) {
                                    var voxel = voxelsToProcess[v];

                                    // Skip voxels with zero density (air)
                                    if (voxel.Density == 0) continue;

                                    var matIndex = voxel.MaterialType;
                                    var baseWeight = v < 7 ? weight0 : t;

                                    // Weight by density (normalized to 0-1 range, assuming density is 0-255)
                                    var densityWeight = voxel.Density / 255f;
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

                                voxelsToProcess.Dispose();

                                // Find top 2 materials
                                MaterialType matA = 0;
                                MaterialType matB = 0;
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

                                uniqueMaterials.Dispose();
                                materialWeights.Dispose();

                                var colorInterp = new float2((float)matA / byte.MaxValue, 0);

                                var color = new Color32((byte)matA, (byte)matB, 0, 0);
                                Vertices.Add(new MeshingVertexData(vertex, normal, color, colorInterp));
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
        }

        private bool IsDegenerateTriangle(float3 a, float3 b, float3 c) {
            var area = math.length(math.cross(b - a, c - a));

            return area < 1e-5f; // Tweak epsilon if needed
        }
    }
}
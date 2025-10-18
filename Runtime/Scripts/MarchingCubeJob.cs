// Copyright 2025 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// This job marches the cubes of one chunk of terrain at full resolution
    /// </summary>
    [BurstCompile]
    internal struct MarchingCubeJob : IJob {
        [ReadOnly] public BlobAssetReference<MCTablesBlobAsset> Tables;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> Vertices;
        public NativeList<int> Triangles;

        public int Lod;
        public int3 Start;

        public void Execute() {
            // Padding is the offset between the index in the voxel array and the local position of the voxel.
            const int padding = 1;
            var lodScale = 1 << Lod;

            // Caches hold vertex indices from previous cubes. 2 "decks" in y-axis, and 4 positions on the leading
            // corner/edges of each cube.
            var currentCache = new NativeArray<int>(
                McStaticHelper.CubesMarchedPerOctreeLeaf * McStaticHelper.CubesMarchedPerOctreeLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            var previousCache = new NativeArray<int>(
                McStaticHelper.CubesMarchedPerOctreeLeaf * McStaticHelper.CubesMarchedPerOctreeLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            // Vertex indices holds the vertex indices to be entered into the triangle array as one of the last parts
            // of marching the cube.
            var vertexIndices = new NativeArray<int>(16, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // CellValues holds the densities of the voxels at each corner of the cube.
            var cellValues = new NativeArray<VoxelData>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // Inside this nested for loop is where a single cube is marched.
            for (var y = 0; y < McStaticHelper.CubesMarchedPerOctreeLeaf; y++) {
                for (var z = 0; z < McStaticHelper.CubesMarchedPerOctreeLeaf; z++) {
                    for (var x = 0; x < McStaticHelper.CubesMarchedPerOctreeLeaf; x++) {
                        var cellPos = Start + new int3(x, y, z) * lodScale;

                        // Inside this loop we are looping through the 8 corners of the cube.
                        for (var i = 0; i < 8; ++i) {
                            var voxelPosition = cellPos + new int3(padding, padding, padding) +
                                                Tables.Value.RegularCornerOffset[i] * lodScale;

                            cellValues[i] = VoxelArray[McStaticHelper.Coord3DToIndex(
                                voxelPosition.x, voxelPosition.y, voxelPosition.z
                            )];
                        }

                        // CaseCode indicates what kind of cube it is. Empty, full, or a mixture that needs a mesh.
                        var caseCode = (byte)((cellValues[0].Density >= McStaticHelper.DensityThreshold ? 0x01 : 0)
                                              | (cellValues[1].Density >= McStaticHelper.DensityThreshold ? 0x02 : 0)
                                              | (cellValues[2].Density >= McStaticHelper.DensityThreshold ? 0x04 : 0)
                                              | (cellValues[3].Density >= McStaticHelper.DensityThreshold ? 0x08 : 0)
                                              | (cellValues[4].Density >= McStaticHelper.DensityThreshold ? 0x10 : 0)
                                              | (cellValues[5].Density >= McStaticHelper.DensityThreshold ? 0x20 : 0)
                                              | (cellValues[6].Density >= McStaticHelper.DensityThreshold ? 0x40 : 0)
                                              | (cellValues[7].Density >= McStaticHelper.DensityThreshold ? 0x80 : 0));

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
                        int cellClass = Tables.Value.RegularCellClass[caseCode];
                        ref var edgeCodes = ref Tables.Value.RegularVertexData[caseCode];

                        // CellVertCount indicates how many vertices are in the cube.
                        var cellVertCount = Tables.Value.VertexCount[cellClass];

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
                                            cachePosX * McStaticHelper.CubesMarchedPerOctreeLeaf * 4 + cachePosZ * 4 +
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
                                    currentCache[x * McStaticHelper.CubesMarchedPerOctreeLeaf * 4 + z * 4 + cacheIdx] =
                                            vertexIndex;
                                }

                                //Local positions of the ends of the edge along which the vertex belongs
                                var vertLocalPos0 = cellPos + new int3(padding, padding, padding) +
                                                    Tables.Value.RegularCornerOffset[cornerIdx0] * lodScale;

                                var vertLocalPos1 = cellPos + new int3(padding, padding, padding) +
                                                    Tables.Value.RegularCornerOffset[cornerIdx1] * lodScale;
                                var voxel0 = cellValues[cornerIdx0];
                                var voxel1 = cellValues[cornerIdx1];

                                /*
                                if (cellValues[cornerIdx0].Density > cellValues[cornerIdx1].Density) {
                                    (vertLocalPos0, vertLocalPos1) = (vertLocalPos1, vertLocalPos0);
                                    (voxel0, voxel1) = (voxel1, voxel0);
                                }
                                */

                                // Density0 and density1 are the densities at each end of the edge along which the vertex
                                // belongs.

                                var p0 = (float3)vertLocalPos0;
                                var p1 = (float3)vertLocalPos1;

                                // This consecutively subdivides the coarser LOD to find the exact place the density crosses the threshhold.
                                for (var j = 0; j < Lod; ++j) {
                                    var mid = (p0 + p1) * 0.5f;
                                    var samplePos = (int3)math.round(mid);

                                    var midPointDensity =
                                            VoxelArray[
                                                        McStaticHelper.Coord3DToIndex(samplePos.x, +samplePos.y,
                                                            samplePos.z)]
                                                    .Density;

                                    var isMidPointDensityAboveThreshold =
                                            midPointDensity >= McStaticHelper.DensityThreshold;

                                    var isVert0DensityAboveThreshold =
                                            VoxelArray[
                                                McStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                                    vertLocalPos0.z)].Density >= McStaticHelper.DensityThreshold;

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
                                    vertLocalPos0.z);
                                voxel0 = VoxelArray[index0];

                                var index1 = McStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z);
                                voxel1 = VoxelArray[index1];

                                //Interpolating the vertex position based on the densities at the ends of the edge
                                //along which the vertex belongs.
                                var t = ((float)128 - voxel0.Density) / (voxel1.Density - voxel0.Density);
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
                                var normal = math.lerp(normal0, normal1, t);
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

                                var color = new Color32(
                                    voxel0.MatIndex, // R: material A
                                    voxel1.MatIndex, // G: material B
                                    blendByte,       // B: blend T for material B
                                    0                // A: unused (optional)
                                );

                                // This puts the vertex data into the vertex array, which is used to Build the Mesh
                                Vertices.Add(new MeshingVertexData(vertex, normal, color));
                            }

                            // For both new vertices and vertices re-used from previous cubes, the vertex index is
                            // stored in the _vertexIndices Array.
                            vertexIndices[i] = vertexIndex;
                        }

                        // IndexCount and cellIndices come from the pre-computed solutions for how to march the cube.
                        var indexCount = Tables.Value.TriangleCount[cellClass];
                        ref var cellIndices = ref Tables.Value.Indices[cellClass];

                        // Inside this loop we are looping through the triangles
                        for (var i = 0; i < indexCount; i += 3) {
                            var ia = vertexIndices[cellIndices[i + 0]];
                            var ib = vertexIndices[cellIndices[i + 1]];
                            var ic = vertexIndices[cellIndices[i + 2]];

                            if (!IsDegenerateTriangle(Vertices[ia].position, Vertices[ib].position,
                                    Vertices[ic].position)) {
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
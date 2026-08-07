// Copyright 2026 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Job to March the Cubes (generate vertices and triangles from voxels) for the main region of a leaf of terrain.
    /// Produces a mixed flat/smooth shaded mesh. Triangles whose dominant material appears in flatShadedLookUp get
    /// exclusive vertex ownership and face normals ("any flat material wins" rule). All other triangles share
    /// vertices and use gradient normals, identical to the smooth-shaded job.
    /// </summary>
    [BurstCompile]
    internal struct OldMarchingCubeJob : IJob {
        [ReadOnly] public BlobAssetReference<McTablesBlobAsset> TablesBlob;
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;
        [ReadOnly] public NativeArray<bool> IsFlatShadedLookUp;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> Vertices;
        public NativeList<int> Triangles;

        // Computed inline as vertices are created, instead of calling Mesh.RecalculateBounds() on
        // the main thread afterward - the job is already touching every unique vertex position
        // once, so tracking running min/max here is essentially free.
        public NativeReference<Bounds> ComputedBounds;

        public int Lod;
        public int3 Start;

        public void Execute() {
            ref var tables = ref TablesBlob.Value;
            ref var config = ref ConfigBlob.Value;

            // Extract config values to locals for faster access
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
            // In the mixed shading job the cache always stores the original computed vertex index, identical to
            // the smooth job. Whether to reuse or clone that index is decided later in the triangle loop.
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

            // Material blending structures - allocated once and reused for all vertices.
            // uniqueMaterials stores the DEMODULATED material index (0-127) only — maturity is resolved
            // separately (see below), so this dominance selection is purely about material identity.
            var uniqueMaterials = new NativeList<byte>(14, Allocator.Temp);
            var materialWeights = new NativeList<float>(14, Allocator.Temp);

            // Running bounds, updated only at the point a genuinely new vertex is created (see
            // below) - cache-hit reused vertices and flat-shading clones are exact copies of
            // already-tracked positions, so re-tracking them would be redundant, not incorrect.
            var boundsMin = new float3(float.MaxValue);
            var boundsMax = new float3(float.MinValue);

            // Inside this nested for loop is where a single cube is marched.
            for (var y = 0; y < cubesMarchedPerLeaf; y++) {
                for (var z = 0; z < cubesMarchedPerLeaf; z++) {
                    for (var x = 0; x < cubesMarchedPerLeaf; x++) {
                        var cellPos = Start + new int3(x, y, z) * lodScale;

                        // Gathers the 8 corner voxels into cellValues and returns the caseCode -
                        // shared with FlatBaryMarchJob (identical logic; this is also the exact
                        // site of the sign-extension early-out bug, kept in one place now).
                        var caseCode = GfMarchHelper.GatherRegularCornersAndComputeCaseCode(
                            VoxelArray, ref tables, cellPos, padding, lodScale,
                            chunkDataAreaSize, chunkDataWidthSize, ref cellValues);

                        // Uniform-cube early-out: skip cubes that are fully solid (caseCode == 0xFF) or
                        // fully empty (caseCode == 0x00) — the MC tables produce zero triangles for both,
                        // so there's nothing to mesh.
                        if (caseCode == 0x00 || caseCode == 0xFF) continue;

                        // Cache validator is a bitwise mask to see if the cube is on any minimal edge of the geoChunk
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

                        // Inside this loop we are solving for a particular vertex of the cube.
                        // This is identical to the smooth job: cache hits reuse the existing index, new vertices
                        // are computed and cached. The flat/smooth decision is deferred to the triangle loop.
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

                            // IsVertexCache-able indicates if an existing vertex exists.
                            // It synthesizes where in the cube the vertex is, and where in the geoChunk the cube is.
                            var isVertexCacheable = (cacheDir & cacheValidator) == cacheDir;

                            // VertexIndex indicates what vertex will go into the triangle array to wind the triangle.
                            int vertexIndex;

                            // Cache hit: reuse the existing vertex index. If the triangle loop later determines this
                            // triangle is flat-shaded, it will clone these vertices at that point instead.
                            if (isVertexCacheable) {
                                vertexIndex = selectedCacheDock[
                                    cachePosX * cubesMarchedPerLeaf * 4 + cachePosZ * 4 + cacheIdx];
                            }

                            // This is the case where a new vertex must be created.
                            else {
                                // Declare the vertex and the normal and the color and the vertexIndex (for the
                                // triangle array).
                                vertexIndex = Vertices.Length;

                                // This is caching the vertexIndex for cubes marched later in the loop.
                                // Could be optimized to also cache more stuff when the cache validator is non-zero
                                // (aka on an edge of the geoChunk).
                                if (cornerIdx1 == 7) {
                                    currentCache[x * cubesMarchedPerLeaf * 4 + z * 4 + cacheIdx] =
                                            vertexIndex;
                                }

                                //Local positions of the ends of the edge along which the vertex belongs
                                var vertLocalPos0 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx0] * lodScale;

                                var vertLocalPos1 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx1] * lodScale;

                                // Get voxel data at endpoints early
                                var index0 = GfStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel0 = VoxelArray[index0];

                                // Cache this for the subdivision loop
                                var isVert0Full = voxel0.Density >= 0;

                                // This consecutively subdivides the coarser LOD to find the exact place Density
                                // crosses zero. Shared with the other three march jobs.
                                GfMarchHelper.SubdivideToSurfaceCrossing(
                                    VoxelArray, chunkDataAreaSize, chunkDataWidthSize, Lod, isVert0Full,
                                    ref vertLocalPos0, ref vertLocalPos1);

                                // Recompute voxel data after subdivision
                                index0 = GfStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                voxel0 = VoxelArray[index0];

                                var index1 = GfStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel1 = VoxelArray[index1];

                                //Interpolating the vertex position based on the densities at the ends of the edge
                                //along which the vertex belongs.
                                var t = (float)-voxel0.Density / (voxel1.Density - voxel0.Density);
                                t = math.clamp(t, 0, 1); // safety clamp

                                var vertex = math.lerp(vertLocalPos0, vertLocalPos1, t);

                                // The 14-voxel neighborhood (endpoints' 6 axis-neighbors each) plus the two
                                // endpoint gradient normals derived from it - shared with TransitionMarchingCubeJob.
                                var sample = GfMarchHelper.SampleNeighborGradient(
                                    VoxelArray, vertLocalPos0, vertLocalPos1, chunkDataAreaSize, chunkDataWidthSize);

                                // The normal is a weighted average of the normals at the ends of the edges, same as
                                // the vertex position. For smooth triangles this is the final normal. For flat
                                // triangles it will be overwritten with the face normal in the triangle loop.
                                var normal = math.lerp(sample.Normal0, sample.Normal1, t);
                                normal = math.normalize(normal);

                                // Clear lists for reuse
                                uniqueMaterials.Clear();
                                materialWeights.Clear();

                                var weight0 = 1f - t;

                                // Add all voxel contributions (14-voxel neighborhood — this decides which
                                // TWO materials dominate this vertex, purely by material identity). Voxels
                                // with negative density (including the null/sentinel material) never
                                // contribute — see GfMarchHelper.AddMaterialWeight.
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

                                // Maturity is checked ONLY against the two edge endpoints (voxel0/voxel1),
                                // not the wider 14-voxel dominance neighborhood above — "is this specific
                                // crossing point mature," not "is this whole neighborhood uniformly mature."
                                var matIndex0 = voxel0.GetPlainMatIndex();
                                var isMature0 = voxel0.IsMature();
                                var matIndex1 = voxel1.GetPlainMatIndex();
                                var isMature1 = voxel1.IsMature();

                                var matAAllMature =
                                        GfMarchHelper.ResolveMaturity(matA, matIndex0, isMature0, matIndex1, isMature1);
                                var matBAllMature =
                                        GfMarchHelper.ResolveMaturity(matB, matIndex0, isMature0, matIndex1, isMature1);

                                var colorInterp = new Color32(matA, 0, 0, 0);

                                var color = new Color32(
                                    matA,
                                    matB,
                                    (byte)(matAAllMature ? 255 : 0),
                                    (byte)(matBAllMature ? 255 : 0)
                                );

                                var centeredVertex = (vertex + offsetBurst) * resolution;

                                boundsMin = math.min(boundsMin, centeredVertex);
                                boundsMax = math.max(boundsMax, centeredVertex);

                                Vertices.Add(new MeshingVertexData(centeredVertex, normal, color,
                                    colorInterp));
                            }

                            // For both new and cached vertices, the vertex index is stored in the vertexIndices array.
                            vertexIndices[i] = vertexIndex;
                        }

                        // IndexCount and cellIndices come from the pre-computed solutions for how to march the cube.
                        var indexCount = tables.TriangleCount[cellClass];
                        ref var cellIndices = ref tables.Indices[cellClass];

                        // Inside this loop we are looping through the triangles.
                        // "Any flat material wins": if matA of any vertex is in flatShadedLookUp, the whole
                        // triangle is flat-shaded. Its three vertices are cloned so each triangle owns them
                        // exclusively, and the normal is overwritten with the face normal. Otherwise the original
                        // shared vertices and gradient normals are used unchanged (smooth shading).
                        for (var i = 0; i < indexCount; i += 3) {
                            var ia = vertexIndices[cellIndices[i + 0]];
                            var ib = vertexIndices[cellIndices[i + 1]];
                            var ic = vertexIndices[cellIndices[i + 2]];

                            var posA = Vertices[ia].Position;
                            var posB = Vertices[ib].Position;
                            var posC = Vertices[ic].Position;

                            if (GfMarchHelper.IsDegenerateTriangle(posA, posB, posC)) continue;
                            
                            Triangles.Add(ic);
                            Triangles.Add(ib);
                            Triangles.Add(ia);
                        }
                    }
                }

                // This is setting the right caches. It is done every time the y-value increments. It changes to a
                // new "deck" of cached values.
                (currentCache, previousCache) = (previousCache, currentCache);
            }

            ComputedBounds.Value = Vertices.Length > 0
                    ? new Bounds((Vector3)((boundsMin + boundsMax) * 0.5f), (Vector3)(boundsMax - boundsMin))
                    : new Bounds();

            // Dispose reused structures
            uniqueMaterials.Dispose();
            materialWeights.Dispose();
        }
    }
}
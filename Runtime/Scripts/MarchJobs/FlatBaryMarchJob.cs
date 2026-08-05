// Copyright 2026 Spellbound Studio Inc.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Marching cubes job for the FlatShaded/Barycentric material scheme. Every triangle gets
    /// exclusive vertices and a face normal - there is no mixed flat/smooth branching here, unlike
    /// MarchingCubeJob. Material is NOT blended: each vertex's material is simply the "full" voxel
    /// on the edge it sits on (using the ORIGINAL pre-subdivision cube corner, not the
    /// post-subdivision voxel0/voxel1 - see the comment at originalFullVoxel below). All three of
    /// a triangle's corner materials are packed identically onto all three of its vertices
    /// (FixedColor.rgb = ia/ib/ic's raw VoxelData.MaterialIndex byte, including the maturity bit),
    /// together with a per-vertex barycentric role marker in ColorInterp, so the fragment shader
    /// can pick the nearest corner's material per-pixel with a hard boundary instead of
    /// interpolating indices.
    /// </summary>
    [BurstCompile]
    internal struct FlatBaryMarchJob : IJob {
        [ReadOnly] public BlobAssetReference<McTablesBlobAsset> TablesBlob;
        [ReadOnly] public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob;

        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeArray<VoxelData> VoxelArray;

        public NativeList<MeshingVertexData> Vertices;
        public NativeList<int> Triangles;

        // Computed inline as vertices are added, instead of calling Mesh.RecalculateBounds() on
        // the main thread afterward.
        public NativeReference<Bounds> ComputedBounds;

        public int Lod;
        public int3 Start;

        /// <summary>
        /// Lightweight per-edge-vertex data cached across cube marches (spatial reuse of shared
        /// edges only - NEVER reused in the final mesh, since every triangle always gets its own
        /// exclusive vertices here). Storing just this instead of a full MeshingVertexData avoids
        /// re-running the subdivision search when an adjacent cube references the same edge.
        /// </summary>
        private struct EdgeVertex {
            public float3 Position;

            // Raw packed byte: demodulated material index (0-127) plus VoxelData.MatureBitValue (128)
            // when mature, giving the full 0-255 range the shader expects. Must stay byte, not sbyte -
            // a mature high-index material (e.g. 127 + 128 = 255) doesn't fit in sbyte's -128..127 range.
            public byte RawMaterial;
            public sbyte Density; // density of that same original full corner (always >= 0, it's a full voxel) - used as a confidence weight
        }

        public void Execute() {
            ref var tables = ref TablesBlob.Value;
            ref var config = ref ConfigBlob.Value;

            var chunkDataAreaSize = config.ChunkDataAreaSize;
            var chunkDataWidthSize = config.ChunkDataWidthSize;
            var cubesMarchedPerLeaf = config.CubesMarchedPerOctreeLeaf;
            var resolution = config.Resolution;
            var offsetBurst = config.OffsetBurst;

            const int padding = 1;
            var lodScale = 1 << Lod;

            var currentCache = new NativeArray<EdgeVertex>(
                cubesMarchedPerLeaf * cubesMarchedPerLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            var previousCache = new NativeArray<EdgeVertex>(
                cubesMarchedPerLeaf * cubesMarchedPerLeaf * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );

            var vertexIndices =
                    new NativeArray<EdgeVertex>(16, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            var cellValues = new NativeArray<VoxelData>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // Running bounds, updated once per triangle (every triangle here always creates 3
            // fresh mesh vertices - unlike MarchingCubeJob there's no vertex-index cache to
            // dedupe against, so tracking happens at the point all 3 are actually added).
            var boundsMin = new float3(float.MaxValue);
            var boundsMax = new float3(float.MinValue);

            for (var y = 0; y < cubesMarchedPerLeaf; y++) {
                for (var z = 0; z < cubesMarchedPerLeaf; z++) {
                    for (var x = 0; x < cubesMarchedPerLeaf; x++) {
                        var cellPos = Start + new int3(x, y, z) * lodScale;

                        // Gathers the 8 corner voxels into cellValues and returns the caseCode -
                        // shared with MarchingCubeJob (identical logic).
                        var caseCode = GfMarchHelper.GatherRegularCornersAndComputeCaseCode(
                            VoxelArray, ref tables, cellPos, padding, lodScale,
                            chunkDataAreaSize, chunkDataWidthSize, ref cellValues);

                        // Uniform-cube early-out: skip cubes that are fully solid (caseCode == 0xFF) or
                        // fully empty (caseCode == 0x00) — the MC tables produce zero triangles for both,
                        // so there's nothing to mesh.
                        if (caseCode == 0x00 || caseCode == 0xFF) continue;

                        var cacheValidator = (x != 0 ? 0x01 : 0)
                                             | (z != 0 ? 0x02 : 0)
                                             | (y != 0 ? 0x04 : 0);

                        int cellClass = tables.RegularCellClass[caseCode];
                        ref var edgeCodes = ref tables.RegularVertexData[caseCode];
                        var cellVertCount = tables.VertexCount[cellClass];

                        for (var i = 0; i < cellVertCount; ++i) {
                            var edgeCode = edgeCodes[i];
                            var cornerIdx0 = (ushort)((edgeCode >> 4) & 0x0F);
                            var cornerIdx1 = (ushort)(edgeCode & 0x0F);
                            var cacheIdx = (byte)((edgeCode >> 8) & 0x0F);
                            var cacheDir = (byte)(edgeCode >> 12);
                            var cachePosX = x - (cacheDir & 1);
                            var cachePosZ = z - ((cacheDir >> 1) & 1);

                            var selectedCacheDock = ((cacheDir >> 2) & 1) == 1 ? previousCache : currentCache;
                            var isVertexCacheable = (cacheDir & cacheValidator) == cacheDir;

                            EdgeVertex edgeVertex;

                            if (isVertexCacheable) {
                                edgeVertex = selectedCacheDock[
                                    cachePosX * cubesMarchedPerLeaf * 4 + cachePosZ * 4 + cacheIdx];
                            }
                            else {
                                var vertLocalPos0 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx0] * lodScale;

                                var vertLocalPos1 = cellPos + new int3(padding, padding, padding) +
                                                    tables.RegularCornerOffset[cornerIdx1] * lodScale;

                                var index0 = GfStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel0 = VoxelArray[index0];

                                var index1 = GfStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel1 = VoxelArray[index1];

                                var isVert0Full = voxel0.Density >= 0;

                                // Capture the ORIGINAL cube corners (before bisection moves anything) and which
                                // one is really full. The marching-cubes case table guarantees these two original
                                // corners are complementary - exactly one is full - so this is a reliable material
                                // source even when the bisection search below degenerates under non-monotonic
                                // (dug/edited) density fields. Using the post-subdivision voxel0/voxel1 instead
                                // can, in that degenerate case, resolve to two voxels that are BOTH on the empty
                                // side - both carrying the null/sentinel material - which would then render.
                                var originalFullVoxel = isVert0Full ? voxel0 : voxel1;
                                var wasVoxel0Mature = voxel0.IsMature();
                                var wasVoxel1Mature = voxel1.IsMature();

                                // Shared with the other three march jobs.
                                GfMarchHelper.SubdivideToSurfaceCrossing(
                                    VoxelArray, chunkDataAreaSize, chunkDataWidthSize, Lod, isVert0Full,
                                    ref vertLocalPos0, ref vertLocalPos1);

                                index0 = GfStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                voxel0 = VoxelArray[index0];

                                index1 = GfStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, chunkDataAreaSize, chunkDataWidthSize);
                                voxel1 = VoxelArray[index1];

                                var t = (float)-voxel0.Density / (voxel1.Density - voxel0.Density);
                                t = math.clamp(t, 0, 1);

                                var vertex = math.lerp(vertLocalPos0, vertLocalPos1, t);
                                var centeredVertex = (vertex + offsetBurst) * resolution;

                                // materialIndexOnly is already the demodulated 0-127 index (VoxelData.MaterialIndex
                                // returns it pre-stripped). Maturity is packed back in additively, matching the
                                // shader's raw-byte contract - NOT via sign, which can't distinguish material 0
                                // mature from material 0 immature (there's no negative zero).
                                var materialIndexOnly = originalFullVoxel.MaterialIndex;
                                var combinedIsMature = wasVoxel0Mature && wasVoxel1Mature;
                                var packedRawMaterial =
                                        (byte)(materialIndexOnly + (combinedIsMature ? VoxelData.MatureBitValue : 0));

                                edgeVertex = new EdgeVertex {
                                    Position = centeredVertex,
                                    RawMaterial = packedRawMaterial,
                                    Density = originalFullVoxel.Density
                                };

                                if (cornerIdx1 == 7) {
                                    currentCache[x * cubesMarchedPerLeaf * 4 + z * 4 + cacheIdx] = edgeVertex;
                                }
                            }

                            vertexIndices[i] = edgeVertex;
                        }

                        var indexCount = tables.TriangleCount[cellClass];
                        ref var cellIndices = ref tables.Indices[cellClass];

                        for (var i = 0; i < indexCount; i += 3) {
                            var vA = vertexIndices[cellIndices[i + 0]];
                            var vB = vertexIndices[cellIndices[i + 1]];
                            var vC = vertexIndices[cellIndices[i + 2]];

                            if (GfMarchHelper.IsDegenerateTriangle(vA.Position, vB.Position, vC.Position)) continue;

                            boundsMin = math.min(boundsMin, math.min(vA.Position, math.min(vB.Position, vC.Position)));
                            boundsMax = math.max(boundsMax, math.max(vA.Position, math.max(vB.Position, vC.Position)));

                            // Face normal matching the (ic, ib, ia) push order below - same convention
                            // MarchingCubeJob's flat-shaded branch already uses.
                            var faceNormal = math.normalize(
                                math.cross(vB.Position - vC.Position, vA.Position - vC.Position));

                            // The three vertices share the same RawMaterial triple in their Color32 rgb
                            // (triangle-constant, safe to "interpolate" since it never varies across the
                            // triangle); alpha carries the per-vertex barycentric marker u (exactly 0 or
                            // 255 at any given vertex - the GPU interpolates the in-between values across
                            // the triangle for us). densityTriple carries the same three corner densities
                            // into ColorInterp/float4 for shader-side confidence weighting.
                            var densityTriple = new float3(vA.Density, vB.Density, vC.Density);

                            var iaIndex = Vertices.Length;
                            Vertices.Add(new MeshingVertexData(
                                vA.Position, faceNormal,
                                new Color32(vA.RawMaterial, vB.RawMaterial, vC.RawMaterial, 255),
                                new float4(densityTriple, 0)));

                            var ibIndex = Vertices.Length;
                            Vertices.Add(new MeshingVertexData(
                                vB.Position, faceNormal,
                                new Color32(vA.RawMaterial, vB.RawMaterial, vC.RawMaterial, 0),
                                new float4(densityTriple, 1)));

                            var icIndex = Vertices.Length;
                            Vertices.Add(new MeshingVertexData(
                                vC.Position, faceNormal,
                                new Color32(vA.RawMaterial, vB.RawMaterial, vC.RawMaterial, 0),
                                new float4(densityTriple, 0)));

                            Triangles.Add(icIndex);
                            Triangles.Add(ibIndex);
                            Triangles.Add(iaIndex);
                        }
                    }
                }

                (currentCache, previousCache) = (previousCache, currentCache);
            }

            ComputedBounds.Value = Vertices.Length > 0
                    ? new Bounds((Vector3)((boundsMin + boundsMax) * 0.5f), (Vector3)(boundsMax - boundsMin))
                    : new Bounds();
        }
    }
}
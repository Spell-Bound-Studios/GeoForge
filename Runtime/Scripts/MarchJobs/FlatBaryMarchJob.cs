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
            public byte RawMaterial; // raw VoxelData.MaterialIndex of the original full corner (includes maturity bit)
            public byte Density; // density (0-255) of that same original full corner - used as a confidence weight
        }

        public void Execute() {
            ref var tables = ref TablesBlob.Value;
            ref var config = ref ConfigBlob.Value;

            var densityThreshold = config.DensityThreshold;
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

            for (var y = 0; y < cubesMarchedPerLeaf; y++) {
                for (var z = 0; z < cubesMarchedPerLeaf; z++) {
                    for (var x = 0; x < cubesMarchedPerLeaf; x++) {
                        var cellPos = Start + new int3(x, y, z) * lodScale;

                        for (var i = 0; i < 8; ++i) {
                            var voxelPosition = cellPos + new int3(padding, padding, padding) +
                                                tables.RegularCornerOffset[i] * lodScale;

                            cellValues[i] = VoxelArray[GfStaticHelper.Coord3DToIndex(
                                voxelPosition.x, voxelPosition.y, voxelPosition.z,
                                chunkDataAreaSize, chunkDataWidthSize
                            )];
                        }

                        var caseCode = (byte)((cellValues[0].Density >= densityThreshold ? 0x01 : 0)
                                              | (cellValues[1].Density >= densityThreshold ? 0x02 : 0)
                                              | (cellValues[2].Density >= densityThreshold ? 0x04 : 0)
                                              | (cellValues[3].Density >= densityThreshold ? 0x08 : 0)
                                              | (cellValues[4].Density >= densityThreshold ? 0x10 : 0)
                                              | (cellValues[5].Density >= densityThreshold ? 0x20 : 0)
                                              | (cellValues[6].Density >= densityThreshold ? 0x40 : 0)
                                              | (cellValues[7].Density >= densityThreshold ? 0x80 : 0));

                        if ((caseCode ^ ((cellValues[7].Density >> 7) & 0xFF)) == 0) continue;

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

                                var p0 = (float3)vertLocalPos0;
                                var p1 = (float3)vertLocalPos1;

                                var index0 = GfStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel0 = VoxelArray[index0];

                                var index1 = GfStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, chunkDataAreaSize, chunkDataWidthSize);
                                var voxel1 = VoxelArray[index1];

                                var isVert0DensityAboveThreshold = voxel0.Density >= densityThreshold;

                                // Capture the ORIGINAL cube corners (before bisection moves anything) and which
                                // one is really full. The marching-cubes case table guarantees these two original
                                // corners are complementary - exactly one is full - so this is a reliable material
                                // source even when the bisection search below degenerates under non-monotonic
                                // (dug/edited) density fields. Using the post-subdivision voxel0/voxel1 instead
                                // can, in that degenerate case, resolve to two voxels that are BOTH on the empty
                                // side - both carrying the null/sentinel material - which would then render.
                                var originalFullVoxel = isVert0DensityAboveThreshold ? voxel0 : voxel1;
                                var wasVoxel0Mature = voxel0.MaterialIndex >= VoxelData.MatureBitValue;
                                var wasVoxel1Mature = voxel1.MaterialIndex >= VoxelData.MatureBitValue;

                                for (var j = 0; j < Lod; ++j) {
                                    var mid = (p0 + p1) * 0.5f;
                                    var samplePos = (int3)math.round(mid);

                                    var midPointDensity =
                                            VoxelArray[
                                                        GfStaticHelper.Coord3DToIndex(samplePos.x, +samplePos.y,
                                                            samplePos.z, chunkDataAreaSize,
                                                            chunkDataWidthSize)]
                                                    .Density;

                                    var isMidPointDensityAboveThreshold = midPointDensity >= densityThreshold;

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

                                index0 = GfStaticHelper.Coord3DToIndex(vertLocalPos0.x, vertLocalPos0.y,
                                    vertLocalPos0.z, chunkDataAreaSize, chunkDataWidthSize);
                                voxel0 = VoxelArray[index0];

                                index1 = GfStaticHelper.Coord3DToIndex(vertLocalPos1.x, vertLocalPos1.y,
                                    vertLocalPos1.z, chunkDataAreaSize, chunkDataWidthSize);
                                voxel1 = VoxelArray[index1];

                                var t = ((float)densityThreshold - voxel0.Density) /
                                        (voxel1.Density - voxel0.Density);
                                t = math.clamp(t, 0, 1);

                                var vertex = math.lerp(vertLocalPos0, vertLocalPos1, t);
                                var centeredVertex = (vertex + offsetBurst) * resolution;
                                
                                var materialIndexOnly = (byte)(originalFullVoxel.MaterialIndex % VoxelData.MatureBitValue);
                                var combinedIsMature = wasVoxel0Mature && wasVoxel1Mature;
                                var packedRawMaterial = (byte)(materialIndexOnly + (combinedIsMature ? VoxelData.MatureBitValue : 0));

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

                            if (IsDegenerateTriangle(vA.Position, vB.Position, vC.Position)) continue;

                            // Face normal matching the (ic, ib, ia) push order below - same convention
                            // MarchingCubeJob's flat-shaded branch already uses.
                            var faceNormal = math.normalize(
                                math.cross(vB.Position - vC.Position, vA.Position - vC.Position));

                            // matTriple's rgb is triangle-constant (safe to "interpolate" since it never
                            // varies across the triangle); alpha carries the per-vertex barycentric marker u
                            // (exactly 0 or 255 at any given vertex - the GPU interpolates the in-between
                            // values across the triangle for us).
                            var densityTriple = new float3(vA.Density - densityThreshold, 
                                vB.Density - densityThreshold, 
                                vC.Density - densityThreshold);

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
        }

        private bool IsDegenerateTriangle(float3 a, float3 b, float3 c) {
            var area = math.length(math.cross(b - a, c - a));

            return area < 1e-5f; // Tweak epsilon if needed
        }
    }
}
// Copyright 2025 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Blob Asset to hold Marching Cubes Settings.
    /// </summary>
    public struct McChunkInterpolationBlobAsset {
        public BlobArray<float4> ChunkCornerWeights;
    }

    public static class McChunkInterpolationBlobCreator {
        public static BlobAssetReference<McChunkInterpolationBlobAsset>
                CreateMcChunkInterpolationBlobAsset(TerrainConfig terrainConfig) {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var chunkInterpolations = ref builder.ConstructRoot<McChunkInterpolationBlobAsset>();

            var weightArray = builder.Allocate(ref chunkInterpolations.ChunkCornerWeights,
                (terrainConfig.chunkSize + 3) * (terrainConfig.chunkSize + 3));

            var innerMin = 1;
            var innerMax = terrainConfig.chunkSize + 1;
            var innerSize = innerMax - innerMin;

            for (var i = 0; i < weightArray.Length; i++) {
                McStaticHelper.IndexToInt2(i, terrainConfig.chunkSize + 3, out var x, out var z);
                var weights = float4.zero;

                // 🟩 CASE 1: Corners (exact match to CornerPositions)
                if (x == innerMin && z == innerMin) weights.x = 1f;      // corner 0
                else if (x == innerMin && z == innerMax) weights.y = 1f; // corner 1
                else if (x == innerMax && z == innerMin) weights.z = 1f; // corner 2
                else if (x == innerMax && z == innerMax) weights.w = 1f; // corner 3

                // 🟨 CASE 2: Edges (interpolate linearly between 2 adjacent corners)
                else if (x == innerMin) {
                    var v = (float)(z - innerMin) / innerSize;
                    v = math.smoothstep(0f, 1f, v);
                    weights.x = 1f - v; // top-left
                    weights.y = v;      // bottom-left
                }
                else if (x == innerMax) {
                    var v = (float)(z - innerMin) / innerSize;
                    v = math.smoothstep(0f, 1f, v);
                    weights.z = 1f - v; // top-right
                    weights.w = v;      // bottom-right
                }
                else if (z == innerMin) {
                    var u = (float)(x - innerMin) / innerSize;
                    u = math.smoothstep(0f, 1f, u);
                    weights.x = 1f - u; // top-left
                    weights.z = u;      // top-right
                }
                else if (z == innerMax) {
                    var u = (float)(x - innerMin) / innerSize;
                    u = math.smoothstep(0f, 1f, u);
                    weights.y = 1f - u; // bottom-left
                    weights.w = u;      // bottom-right
                }

                // 🟦 CASE 3: Interior → full bilinear interpolation
                else {
                    var u = (float)(x - innerMin) / innerSize;
                    var v = (float)(z - innerMin) / innerSize;

                    u = math.smoothstep(0f, 1f, u);
                    v = math.smoothstep(0f, 1f, v);

                    weights = new float4(
                        (1f - u) * (1f - v), // top-left (corner 0)
                        (1f - u) * v,        // bottom-left (corner 1)
                        u * (1f - v),        // top-right (corner 2)
                        u * v                // bottom-right (corner 3)
                    );
                }

                var sum = math.csum(weights);
                weights /= sum;

                weightArray[i] = weights;
            }

            var result = builder.CreateBlobAssetReference<McChunkInterpolationBlobAsset>(Allocator.Persistent);
            builder.Dispose();

            return result;
        }
    }
}
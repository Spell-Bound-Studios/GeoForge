// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public static class SbTerrain {
        private static bool TryGetChunkManager(out IVoxelTerrainChunkManager chunkManager) {
            if (!SingletonManager.TryGetSingletonInstance(out chunkManager)) {
#if UNITY_EDITOR
                Debug.LogError("Unable To Find ChunkManager");
#endif
                return false;
            }

            return true;
        }

        public static VoxelData QueryVoxel(Vector3 position) {
            if (!TryGetChunkManager(out var chunkManager)) return new VoxelData();

            var chunk = chunkManager.GetChunkByPosition(position);

            if (chunk == null) {
#if UNITY_EDITOR
                //Debug.LogWarning("Trying to Query a Voxel but no IVoxelTerrainChunk is found bounding the requested position");
#endif
                return new VoxelData();
            }

            var positionRounded = new Vector3Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                Mathf.RoundToInt(position.z));

            if (!chunk.HasVoxelData())
                return new VoxelData();

            return chunk.GetVoxelData(positionRounded);
        }

        public static bool IsInsideTerrain(Vector3 position) =>
                QueryVoxel(position).Density >= McStaticHelper.DensityThreshold;

        public static void RemoveSphere(
            Vector3 position,
            List<MaterialType> diggableMaterialTypes = null,
            float radius = 2f,
            int delta = byte.MaxValue) {
            if (diggableMaterialTypes == null)
                diggableMaterialTypes = McStaticHelper.GetAllMaterialTypes();

            var rawVoxelEdits = new List<RawVoxelEdit>();
            var center = Vector3Int.RoundToInt(position);
            var r = Mathf.CeilToInt(radius);

            for (var x = -r; x <= r; x++) {
                for (var y = -r; y <= r; y++) {
                    for (var z = -r; z <= r; z++) {
                        var offset = new Vector3Int(x, y, z);
                        var voxelPos = center + offset;
                        var dist = Vector3.Distance(position, voxelPos);

                        if (!(dist <= radius))
                            continue;

                        var falloff = 1f - dist / radius;
                        var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                        if (adjustedDelta != 0)
                            rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, -adjustedDelta, 0));
                    }
                }
            }

            MapVoxelEditsToChunkCoords(rawVoxelEdits, diggableMaterialTypes.ToHashSet());
        }

        public static void AddSphere(
            Vector3 position, MaterialType depositType,
            float radius = 2f,
            int delta = byte.MaxValue) {
            var rawVoxelEdits = new List<RawVoxelEdit>();
            var center = Vector3Int.RoundToInt(position);
            var r = Mathf.CeilToInt(radius);

            for (var x = -r; x <= r; x++) {
                for (var y = -r; y <= r; y++) {
                    for (var z = -r; z <= r; z++) {
                        var offset = new Vector3Int(x, y, z);
                        var voxelPos = center + offset;
                        var dist = Vector3.Distance(position, voxelPos);

                        if (!(dist <= radius))
                            continue;

                        var falloff = 1f - dist / radius;
                        var adjustedDelta = Mathf.RoundToInt(delta * falloff);

                        if (adjustedDelta != 0)
                            rawVoxelEdits.Add(new RawVoxelEdit(voxelPos, adjustedDelta, depositType));
                    }
                }
            }

            MapVoxelEditsToChunkCoords(rawVoxelEdits, new HashSet<MaterialType>());
        }

        /// <summary>
        /// Expected to run on server only.
        /// Maps "raw" (world space) voxel edit to Chunks and Lists of local changes in each chunk.
        /// This is required because there's data overlap between the chunks. 
        /// </summary>
        private static void MapVoxelEditsToChunkCoords(
            List<RawVoxelEdit> rawVoxelEdits, HashSet<MaterialType> diggableMaterialTypes) {
            var chunkBounds = new BoundsInt(
                0,
                0,
                0,
                SpellboundStaticHelper.ChunkSize + 3,
                SpellboundStaticHelper.ChunkSize + 3,
                SpellboundStaticHelper.ChunkSize + 3
            );

            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelEdit>>();

            if (!TryGetChunkManager(out var chunkManager)) return;

            foreach (var rawEdit in rawVoxelEdits) {
                var affectedChunksByCoord = GetNChunksTouchingPosition(rawEdit.WorldPosition);

                foreach (var coord in affectedChunksByCoord) {
                    var localPos = rawEdit.WorldPosition - coord * SpellboundStaticHelper.ChunkSize;

                    if (!chunkBounds.Contains(localPos))
                        continue;

                    var index = McStaticHelper.Coord3DToIndex(localPos.x, localPos.y, localPos.z);

                    if (!editsByChunkCoord.TryGetValue(coord, out var localEdits)) {
                        localEdits = new List<VoxelEdit>();
                        editsByChunkCoord[coord] = localEdits;
                    }

                    var chunk = chunkManager.GetChunkByCoord(coord);

                    if (chunk == null) continue;

                    var existingVoxel = chunk.GetVoxelData(index);

                    if (!diggableMaterialTypes.Contains(existingVoxel.MaterialType) &&
                        rawEdit.DensityChange < 0) continue;

                    var newDensity = (byte)Mathf.Clamp(existingVoxel.Density + rawEdit.DensityChange, 0, 255);

                    if (existingVoxel.Density == newDensity)
                        continue;

                    var mat = rawEdit.NewMatIndex;

                    if (existingVoxel.Density >= McStaticHelper.DensityThreshold && newDensity > byte.MinValue)
                        mat = existingVoxel.MaterialType;

                    var localEdit = new VoxelEdit(index, newDensity, mat);
                    localEdits.Add(localEdit);
                }
            }

            chunkManager.HandleVoxelEdits(editsByChunkCoord);
        }

        /// <summary>
        /// This is meant to be called externally so that a reference to a chunk can be retrieved from game logic.
        /// </summary>
        private static List<Vector3Int> GetNChunksTouchingPosition(Vector3Int position) {
            //Should be able to create a map to do this efficiently instead of bruteforce
            var chunkKeys = new HashSet<Vector3Int>();

            // Compute base chunk coord (floor division handles negatives correctly)
            var baseCoord = SpellboundStaticHelper.WorldToChunk(position);

            for (var dx = -1; dx <= 1; dx++) {
                for (var dy = -1; dy <= 1; dy++) {
                    for (var dz = -1; dz <= 1; dz++) {
                        var neighborCoord = baseCoord + new Vector3Int(dx, dy, dz);
                        chunkKeys.Add(neighborCoord);
                    }
                }
            }

            return chunkKeys.ToList();
        }
    }
}
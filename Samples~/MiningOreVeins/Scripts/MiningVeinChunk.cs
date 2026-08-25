// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Sample2 {
    /// <summary>
    /// Custom Chunk Implementation for Sample Two, Mining Ore Veins.
    /// Aggregates requested changes as if the voxels have a "Health Pool".
    /// Makes no changes until voxel runs out of health, at which point it empties the voxel entirely. 
    /// </summary>
    public class MiningVeinChunk : MonoBehaviour, IGeoChunk {
        [SerializeField] private int oreHealth;
        private Dictionary<int, int> _damagedVoxels = new();
        
        public GeoChunkEngine GeoChunkEngine { get; private set; }
        
        public void InitializeGeoChunk(Vector3Int coord, IGeoEditStore geoEditStore) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, geoEditStore, coord);
            GeoChunkEngine.IGeoEditStore.geoChunkEngine = GeoChunkEngine;
        }

        public void HandleMeshReady() {
        }

        private void OnDestroy() {
            if (GeoChunkEngine == null)
                return;

            GeoChunkEngine.Dispose();
        }

        public void PassVoxelEditOperation(VoxelEditOperation operation) {
            var trueEdits = new List<(int, VoxelData)>();

            foreach (var voxelDelta in operation.Deltas) {
                var voxelData = GeoChunkEngine.GetVoxelData(voxelDelta.Index);
                var existingMatIndex = voxelData.MaterialIndex;
                var wasFull = voxelData.Density >= 0;

                // Same gate as the crossing rule elsewhere: a disallowed/Impervious voxel that's
                // already full takes no damage at all - additions or subtractions alike.
                if (wasFull && !operation.IsAllowed(existingMatIndex))
                    continue;

                // Damage this hit is the magnitude of density being removed, not the resulting
                // density plus current density (the old formula effectively added the voxel's
                // whole current density on every hit, which would blow past oreHealth almost
                // immediately regardless of dig strength). Negative deltas (digs) deal damage;
                // positive deltas (fills) heal it back, clamped at zero.
                _damagedVoxels.TryGetValue(voxelDelta.Index, out var existingDamage);
                var damageThisHit = -voxelDelta.DensityDelta;
                var totalDamage = Mathf.Max(0, existingDamage + damageThisHit);
                _damagedVoxels[voxelDelta.Index] = totalDamage;

                if (totalDamage >= oreHealth) {
                    trueEdits.Add((voxelDelta.Index, VoxelData.CreateImmature(sbyte.MinValue, VoxelData.NullSentinelValue)));
                    _damagedVoxels.Remove(voxelDelta.Index); // reset in case this voxel is later refilled
                }
            }

            if (GeoChunkEngine.ApplyVoxelEdits(trueEdits, out var editBounds))
                GeoChunkEngine.ScheduleOctreeEditValidation(editBounds);
        }
    }
}
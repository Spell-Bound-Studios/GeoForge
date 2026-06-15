// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Sample2 {
    /// <summary>
    /// Custom Chunk Implementation for Sample Two, Mining Ore Veins.
    /// Aggregates requested changes as if the voxels have a "Health Pool".
    /// Makes no changes until voxel runs out of health, at which point it empties the voxel entirely. 
    /// </summary>
    public class MiningVeinChunk : SimpleGeoChunk {
        [SerializeField] private int oreHealth;
        private Dictionary<int, int> _damagedVoxels = new();

        public void PassVoxelEdits(List<VoxelDelta> newVoxelDeltas) {
            var trueEdits = new List<(int, VoxelData)>();

            foreach (var voxelDelta in newVoxelDeltas) {
                _damagedVoxels.TryGetValue(voxelDelta.index, out var existing);
                var delta = _geoChunk.GetVoxelData(voxelDelta.index).Density - voxelDelta.densityDelta;
                _damagedVoxels[voxelDelta.index] = existing + delta;

                if (_damagedVoxels[voxelDelta.index] > oreHealth) trueEdits.Add((voxelDelta.index, new VoxelData(0,0)));
            }

            if (_geoChunk.ApplyVoxelEdits(trueEdits, out var editBounds))
                _geoChunk.ValidateOctreeEdits(editBounds);
        }
    }
}
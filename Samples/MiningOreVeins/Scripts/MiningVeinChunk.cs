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

        public override void PassVoxelEdits(List<VoxelDelta> newVoxelEdits) {
            var trueEdits = new List<(int, VoxelData)>();

            foreach (var newVoxelEdit in newVoxelEdits) {
                _damagedVoxels.TryGetValue(newVoxelEdit.index, out var existing);
                var delta = _geoChunk.GetVoxelData(newVoxelEdit.index).Density - newVoxelEdit.densityDelta;
                _damagedVoxels[newVoxelEdit.index] = existing + delta;

                if (_damagedVoxels[newVoxelEdit.index] > oreHealth) trueEdits.Add((newVoxelEdit.index, new VoxelData(0,0)));
            }

            if (_geoChunk.ApplyVoxelEdits(trueEdits, out var editBounds))
                _geoChunk.ValidateOctreeEdits(editBounds);
        }
    }
}
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

        public void PassVoxelEdits(List<VoxelEdit> newVoxelEdits) {
            var trueEdits = new List<VoxelEdit>();

            foreach (var voxelEdit in newVoxelEdits) {
                _damagedVoxels.TryGetValue(voxelEdit.index, out var existing);
                var delta = _geoChunk.GetVoxelData(voxelEdit.index).Density - voxelEdit.density;
                _damagedVoxels[voxelEdit.index] = existing + delta;

                if (_damagedVoxels[voxelEdit.index] > oreHealth) trueEdits.Add(new VoxelEdit(voxelEdit.index, 0, 0));
            }

            if (_geoChunk.ApplyVoxelEdits(trueEdits, out var editBounds))
                _geoChunk.ValidateOctreeEdits(editBounds);
        }
    }
}
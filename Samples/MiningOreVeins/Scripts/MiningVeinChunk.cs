// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Sample2 {
    public class MiningVeinChunk : SimpleChunk {
        [SerializeField] private int oreHealth;
        private Dictionary<int, int> _damagedVoxels = new();
        
        public override void PassVoxelEdits(List<VoxelEdit> newVoxelEdits) {
            var trueEdits = new List<VoxelEdit>();
            foreach (var voxelEdit in newVoxelEdits) {
                _damagedVoxels.TryGetValue(voxelEdit.index, out var existing);
                var delta = _baseChunk.GetVoxelData(voxelEdit.index).Density - voxelEdit.density;
                _damagedVoxels[voxelEdit.index] = existing + delta;

                if (_damagedVoxels[voxelEdit.index] > oreHealth) {
                    trueEdits.Add(new VoxelEdit(voxelEdit.index, 0, 0));
                }
            }
            if (_baseChunk.ApplyVoxelEdits(trueEdits, out var editBounds))
                _baseChunk.ValidateOctreeEdits(editBounds);
        }
    }
}

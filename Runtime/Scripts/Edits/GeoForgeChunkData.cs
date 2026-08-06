// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Aggregated changes to the voxels in a chunk. Essentically is like a living save-file of how the voxels have
    /// deviated from its deterministic generation.
    /// </summary>
    public class GeoForgeChunkData {
        public readonly Dictionary<int, VoxelData> Edits = new();

        #region Read / Write

        public bool TryReadEdit(int index, out VoxelData voxelData) => Edits.TryGetValue(index, out voxelData);

        public void WriteEdit(int index, VoxelData voxelData) => Edits[index] = voxelData;

        public void ClearEdits() => Edits.Clear();

        #endregion

        #region Queries

        public int EditCount => Edits.Count;

        #endregion
    }
}
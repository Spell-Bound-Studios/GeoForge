// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Per-chunk slice for voxel edits layered over seed-procedural voxels.
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

// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Packing;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Aggregated changes to the voxels in a chunk. Essentically is like a living save-file of how the voxels have
    /// deviated from its deterministic generation.
    /// </summary>
    public class GeoForgeChunkData : IPacker {
        public readonly Dictionary<int, VoxelData> Edits = new();

        #region Read / Write

        public bool TryReadEdit(int index, out VoxelData voxelData) => Edits.TryGetValue(index, out voxelData);

        public void WriteEdit(int index, VoxelData voxelData) => Edits[index] = voxelData;

        public void ClearEdits() => Edits.Clear();

        #endregion

        #region Queries

        public int EditCount => Edits.Count;

        #endregion

        #region Pack / Unpack

        // Straight count-prefixed (index, VoxelData) pairs - no wrapper struct needed since
        // VoxelData already implements IPacker directly. Unpack replaces Edits wholesale rather
        // than merging - this class is a pure data container, so it has no opinion on whether the
        // caller is doing a fresh load or something else; that judgment call belongs to whoever's
        // driving the load (see GeoChunk/GeoVolume's load path, which still needs to route
        // restored edits through IGeoEditStore.Write() to trigger the live mesh/pool pipeline -
        // this class only knows how to serialize its own dictionary, not how to apply it).
        public void Pack(ref Span<byte> buffer) {
            Packer.WriteInt(ref buffer, Edits.Count);

            foreach (var (index, voxel) in Edits) {
                Packer.WriteInt(ref buffer, index);
                voxel.Pack(ref buffer);
            }
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            Edits.Clear();

            var count = Packer.ReadInt(ref buffer);

            for (var i = 0; i < count; i++) {
                var index = Packer.ReadInt(ref buffer);
                var voxel = new VoxelData();
                voxel.Unpack(ref buffer);
                Edits[index] = voxel;
            }
        }

        #endregion
    }
}
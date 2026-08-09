// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Packing;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Pairs one chunk's coordinate with its edits for save/load purposes. Packs the (index,
    /// VoxelData) pairs directly - not via GeoForgeChunkData, which stays purely a live in-memory
    /// store's internal representation and never needs to be constructed just to serialize.
    /// </summary>
    internal struct ChunkSaveEntry : IPacker {
        public int X;
        public int Y;
        public int Z;
        public List<(int Index, VoxelData Voxel)> Edits;

        public void Pack(ref Span<byte> buffer) {
            Packer.WriteInt(ref buffer, X);
            Packer.WriteInt(ref buffer, Y);
            Packer.WriteInt(ref buffer, Z);

            Packer.WriteInt(ref buffer, Edits?.Count ?? 0);

            if (Edits == null)
                return;

            foreach (var (index, voxel) in Edits) {
                Packer.WriteInt(ref buffer, index);
                voxel.Pack(ref buffer);
            }
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            X = Packer.ReadInt(ref buffer);
            Y = Packer.ReadInt(ref buffer);
            Z = Packer.ReadInt(ref buffer);

            var count = Packer.ReadInt(ref buffer);
            Edits = new List<(int, VoxelData)>(count);

            for (var i = 0; i < count; i++) {
                var index = Packer.ReadInt(ref buffer);
                var voxel = new VoxelData();
                voxel.Unpack(ref buffer);
                Edits.Add((index, voxel));
            }
        }
    }
}
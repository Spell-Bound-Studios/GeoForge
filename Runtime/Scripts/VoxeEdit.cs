// Copyright 2025 Spellbound Studio Inc.

using System;
using Spellbound.Core;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Represents a saved modification to a voxel at an index
    /// </summary>
    [Serializable]
    public struct VoxelEdit : IPacker {
        public int index;
        public byte density;
        public byte matIndex;

        public VoxelEdit(int index, byte density, byte matIndex) {
            this.index = index;
            this.density = density;
            this.matIndex = matIndex;
        }

        public void Pack(ref Span<byte> buffer) {
            Packer.WriteInt(ref buffer, index);
            Packer.WriteByte(ref buffer, density);
            Packer.WriteByte(ref buffer, matIndex);
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            index = Packer.ReadInt(ref buffer);
            density = Packer.ReadByte(ref buffer);
            matIndex = Packer.ReadByte(ref buffer);
        }
    }
}
// Copyright 2026 Spellbound Studio Inc.

using System;
using Spellbound.Core.Packing;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Represents a saved modification to a voxel at an index
    /// </summary>
    [Serializable]
    public struct VoxelDelta : IPacker {
        public int index;
        public short densityDelta;
        public byte materialType;

        public VoxelDelta(int index, short densityDelta, byte matIndex) {
            this.index = index;
            this.densityDelta = densityDelta;
            materialType = matIndex;
        }

        public void Pack(ref Span<byte> buffer) {
            Packer.WriteInt(ref buffer, index);
            Packer.WriteShort(ref buffer, densityDelta);
            Packer.WriteByte(ref buffer, materialType);
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            index = Packer.ReadInt(ref buffer);
            densityDelta = Packer.ReadShort(ref buffer);
            materialType = Packer.ReadByte(ref buffer);
        }
    }
}
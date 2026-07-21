// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Packing;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    public struct VoxelDensityDelta : IPacker {
        public int Index;
        public short DensityDelta;

        public VoxelDensityDelta(int index, short densityDelta) {
            Index = index;
            DensityDelta = densityDelta;
        }

        public void Pack(ref Span<byte> buffer) {
            Packer.WriteInt(ref buffer, Index);
            Packer.WriteShort(ref buffer, DensityDelta);
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            Index = Packer.ReadInt(ref buffer);
            DensityDelta = Packer.ReadShort(ref buffer);
        }

        public override string ToString() => $"VoxelDensityDelta(Index={Index}, DensityDelta={DensityDelta})";
    }

    public struct VoxelEditOperation : IPacker {
        public byte MaterialIndex;
        public VoxelDensityDelta[] Deltas;
        public uint4 AllowedMaterialsMask;

        public VoxelEditOperation(byte materialIndex, List<VoxelDensityDelta> deltas, uint4 allowedMaterialsMask) {
            MaterialIndex = materialIndex;
            Deltas = deltas.ToArray();
            AllowedMaterialsMask = allowedMaterialsMask;
        }

        public VoxelEditOperation(byte materialIndex, List<VoxelDensityDelta> deltas) {
            MaterialIndex = materialIndex;
            Deltas = deltas.ToArray();
            AllowedMaterialsMask = new uint4(uint.MaxValue);
        }

        public bool IsAllowed(byte materialIndex) {
            var lane = (materialIndex / 32) switch {
                0 => AllowedMaterialsMask.x,
                1 => AllowedMaterialsMask.y,
                2 => AllowedMaterialsMask.z,
                _ => AllowedMaterialsMask.w
            };

            return (lane & (1u << (materialIndex % 32))) != 0;
        }

        public void Pack(ref Span<byte> buffer) {
            Packer.WriteByte(ref buffer, MaterialIndex);

            // uint4 has no native Packer support - pack each lane manually.
            Packer.WriteUInt(ref buffer, AllowedMaterialsMask.x);
            Packer.WriteUInt(ref buffer, AllowedMaterialsMask.y);
            Packer.WriteUInt(ref buffer, AllowedMaterialsMask.z);
            Packer.WriteUInt(ref buffer, AllowedMaterialsMask.w);

            Packer.PackArray(ref buffer, Deltas);
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            MaterialIndex = Packer.ReadByte(ref buffer);

            var x = Packer.ReadUInt(ref buffer);
            var y = Packer.ReadUInt(ref buffer);
            var z = Packer.ReadUInt(ref buffer);
            var w = Packer.ReadUInt(ref buffer);
            AllowedMaterialsMask = new uint4(x, y, z, w);

            Deltas = Packer.UnpackArray<VoxelDensityDelta>(ref buffer);
        }

        public override string ToString() =>
                $"VoxelEditOperation(MaterialIndex={MaterialIndex}, Deltas={Deltas?.Length ?? 0}, " +
                $"Mask=({AllowedMaterialsMask.x},{AllowedMaterialsMask.y},{AllowedMaterialsMask.z},{AllowedMaterialsMask.w}))";
    }
}
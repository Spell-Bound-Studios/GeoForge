// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Packing;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// The density change part of a VoxelEditOperation.
    /// It is specific to a chunk, because the index is the index of the chunk, not some kind of world position.
    /// </summary>
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

    /// <summary>
    /// Describes a terraform operation targeting a specific chunk. 
    /// </summary>
    public struct VoxelEditOperation : IPacker {
        public byte MaterialIndex;
        public VoxelDensityDelta[] Deltas;
        public uint4 AllowedMaterialsMask;
        public Vector3 WorldPosition;

        public VoxelEditOperation(byte materialIndex, List<VoxelDensityDelta> deltas, uint4 allowedMaterialsMask, Vector3 worldPosition) {
            MaterialIndex = materialIndex;
            Deltas = deltas.ToArray();
            AllowedMaterialsMask = allowedMaterialsMask;
            WorldPosition = worldPosition;
        }

        public VoxelEditOperation(byte materialIndex, List<VoxelDensityDelta> deltas, Vector3 worldPosition) {
            MaterialIndex = materialIndex;
            Deltas = deltas.ToArray();
            AllowedMaterialsMask = new uint4(uint.MaxValue);
            WorldPosition = worldPosition;
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
            Packer.WriteVector3(ref buffer, WorldPosition);
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            MaterialIndex = Packer.ReadByte(ref buffer);

            var x = Packer.ReadUInt(ref buffer);
            var y = Packer.ReadUInt(ref buffer);
            var z = Packer.ReadUInt(ref buffer);
            var w = Packer.ReadUInt(ref buffer);
            AllowedMaterialsMask = new uint4(x, y, z, w);

            Deltas = Packer.UnpackArray<VoxelDensityDelta>(ref buffer);
            WorldPosition = Packer.ReadVector3(ref buffer);
        }

        public override string ToString() =>
                $"VoxelEditOperation(MaterialIndex={MaterialIndex}, Deltas={Deltas?.Length ?? 0}, " +
                $"Mask=({AllowedMaterialsMask.x},{AllowedMaterialsMask.y},{AllowedMaterialsMask.z},{AllowedMaterialsMask.w})) +" +
                $"WorldPosition=({WorldPosition.x}, {WorldPosition.y},{WorldPosition.z})";
    }
}
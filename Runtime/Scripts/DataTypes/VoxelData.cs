// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Runtime.CompilerServices;
using Spellbound.Core.Packing;
using Unity.Burst;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Represents a single cubic dimension in the game world. It is a discrete cube that characterizes
    /// a geoVolume in the game world with a material and Density.
    /// This doesn't get sent on the network or saved.
    /// </summary>
    [Serializable]
    public struct VoxelData : IEquatable<VoxelData>, IPacker {
        /// <summary>
        /// The mature/undisturbed bit is the high bit of MaterialIndex. Values 0-127 are immature,
        /// 128-255 are the same material index (mod 128) marked mature. MaterialIndex is the RAW
        /// packed byte (includes the maturity bit) - use GetPlainMatIndex() whenever you need the
        /// material's identity alone (e.g. keying a dominance vote or comparing across voxels), and
        /// IsMature() for the maturity flag. Comparing/keying on MaterialIndex directly treats a
        /// mature and immature version of the same material as two different materials.
        /// </summary>
        public const byte MatureBitValue = 128;
        public const byte NullSentinelValue = 127;

        public sbyte Density;
        public byte MaterialIndex;

        private VoxelData(sbyte density, byte matIndex) {
            Density = density;
            MaterialIndex = matIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VoxelData CreateImmature(sbyte density, byte matIndex) =>
                new(density, (byte)(matIndex % MatureBitValue));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VoxelData CreateMature(sbyte density, byte matIndex) =>
                new(density, (byte)((matIndex % MatureBitValue) + MatureBitValue));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMature() => MaterialIndex >= MatureBitValue;

        /// <summary>
        /// The demodulated material identity (0-127), with the maturity bit stripped. Always use
        /// this instead of MaterialIndex directly when comparing or keying by material identity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetPlainMatIndex() => (byte)(MaterialIndex % MatureBitValue);

        public void Pack(ref Span<byte> buffer) {
            Packer.WriteSByte(ref buffer, Density);
            Packer.WriteByte(ref buffer, MaterialIndex);
        }

        public void Unpack(ref ReadOnlySpan<byte> buffer) {
            Density = Packer.ReadSByte(ref buffer);
            MaterialIndex = Packer.ReadByte(ref buffer);
        }

        // Implement IEquatable<VoxelData>. This enables checking if structA == structB, etc.
        public bool Equals(VoxelData other) => Density == other.Density && MaterialIndex == other.MaterialIndex;

        [BurstDiscard]
        public override bool Equals(object obj) => obj is VoxelData other && Equals(other);

        public override int GetHashCode() =>
                // Combine hashes of fields; since these are bytes, simple mixing is enough
                (Density.GetHashCode() * 397) ^ MaterialIndex.GetHashCode();

        public static bool operator ==(VoxelData left, VoxelData right) => left.Equals(right);

        public static bool operator !=(VoxelData left, VoxelData right) => !(left == right);
    }
}
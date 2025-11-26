// Copyright 2025 Spellbound Studio Inc.

using System;
using Unity.Burst;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Represents a single cubic dimension in the game world. It is a discrete cube that characterizes
    /// a volume in the game world with a material and density.
    /// This doesn't get sent on the network or saved.
    /// </summary>
    [System.Serializable]
    public struct VoxelData : IEquatable<VoxelData> {
        public byte Density;
        public MaterialType MaterialType;

        public VoxelData(byte density, MaterialType matIndex) {
            Density = density;
            MaterialType = matIndex;
        }

        // Implement IEquatable<VoxelData>. This enables checking if structA == structB, etc.
        public bool Equals(VoxelData other) => Density == other.Density && MaterialType == other.MaterialType;

        [BurstDiscard]
        public override bool Equals(object obj) => obj is VoxelData other && Equals(other);

        public override int GetHashCode() =>
                // Combine hashes of fields; since these are bytes, simple mixing is enough
                (Density.GetHashCode() * 397) ^ MaterialType.GetHashCode();

        public static bool operator ==(VoxelData left, VoxelData right) => left.Equals(right);

        public static bool operator !=(VoxelData left, VoxelData right) => !(left == right);
    }
}
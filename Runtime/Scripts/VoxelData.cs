// Copyright 2025 Spellbound Studio Inc.

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Represents a single cubic dimension in the game world. It is a discrete cube that characterizes
    /// a volume in the game world with a material and density.
    /// This doesn't get sent on the network or saved.
    /// </summary>
    public struct VoxelData {
        public byte Density;
        public byte MatIndex;

        public VoxelData(byte density, byte matIndex) {
            Density = density;
            MatIndex = matIndex;
        }
    }
}
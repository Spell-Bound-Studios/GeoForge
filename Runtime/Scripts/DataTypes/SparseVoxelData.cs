// Copyright 2025 Spellbound Studio Inc.

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Represents a run of the same voxels.
    /// A NativeList of these structs can represent the full voxel data of a chunk within less memory.
    /// The Marching Cubes Algorithm CANNOT operate on this representation of voxel data.
    /// It must be unpacked for marching.
    /// </summary>
    public struct SparseVoxelData {
        public VoxelData Voxel;
        public readonly int StartIndex;

        public SparseVoxelData(VoxelData voxel, int startIndex) {
            Voxel = voxel;
            StartIndex = startIndex;
        }
    }
}
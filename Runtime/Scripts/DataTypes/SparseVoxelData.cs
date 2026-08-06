// Copyright 2026 Spellbound Studio Inc.

namespace Spellbound.GeoForge {
    /// <summary>
    /// Represents a run of the same voxels.
    /// A NativeList of these structs can represent the full voxel data of a geoChunk within less memory.
    /// The Marching Cubes Algorithm CANNOT operate on this representation of voxel data.
    /// It must be decompressed for marching.
    /// </summary>
    internal struct SparseVoxelData {
        internal VoxelData Voxel;
        internal readonly int StartIndex;

        public SparseVoxelData(VoxelData voxel, int startIndex) {
            Voxel = voxel;
            StartIndex = startIndex;
        }
    }
}
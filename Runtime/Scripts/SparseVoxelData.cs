// Copyright 2025 Spellbound Studio Inc.

namespace Spellbound.MarchingCubes {
    public struct SparseVoxelData {
        public VoxelData Voxel;
        public int StartIndex;

        public SparseVoxelData(VoxelData voxel, int startIndex) {
            Voxel = voxel;
            StartIndex = startIndex;
        }
    }
}
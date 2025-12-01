// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Defines the contract that a chunk must fulfill to integrate with the Marching Cubes Voxel System.
    /// </summary>
    public interface IChunk {
        VoxChunk VoxelChunk { get; }

        public void InitializeChunk(NativeArray<VoxelData> voxels); //polymorphic

        public void PassVoxelEdits(List<VoxelEdit> newVoxelEdits); //polymorphic
    }
}
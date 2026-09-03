// Copyright 2026 Spellbound Studio Inc.

using System;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Represents a run of the same voxels.
    /// A NativeList of these structs can represent the full voxel data of a geoChunk within less memory.
    /// The Marching Cubes Algorithm CANNOT operate on this representation of voxel data.
    /// It must be decompressed for marching.
    ///
    [Serializable]
    public struct SparseVoxelData {
        public VoxelData Voxel;

        [SerializeField] private int startIndex;
        public int StartIndex => startIndex;

        public SparseVoxelData(VoxelData voxel, int startIndex) {
            Voxel = voxel;
            this.startIndex = startIndex;
        }
    }
}
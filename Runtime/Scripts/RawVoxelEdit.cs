// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// VoxelEdit prior to being distributed to the relevant chunks it modifies
    /// </summary>
    public readonly struct RawVoxelEdit {
        public Vector3Int WorldPosition { get; }
        public int DensityChange { get; }
        public MaterialType NewMatIndex { get; }

        public RawVoxelEdit(Vector3Int worldPosition, int densityChange, MaterialType newMatIndex) {
            WorldPosition = worldPosition;
            DensityChange = densityChange;
            NewMatIndex = newMatIndex;
        }
    }
}
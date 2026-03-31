// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// VoxelEdit relative to IGeoVolume position and scale.
    /// </summary>
    public readonly struct RawVoxelEdit {
        public Vector3Int VoxelSpacePosition { get; }
        public short DensityDelta { get; }
        public byte NewMatIndex { get; }

        public RawVoxelEdit(Vector3Int voxelSpacePosition, short densityDelta, byte newMatIndex) {
            this.VoxelSpacePosition = voxelSpacePosition;
            this.DensityDelta = densityDelta;
            NewMatIndex = newMatIndex;
        }
    }
}
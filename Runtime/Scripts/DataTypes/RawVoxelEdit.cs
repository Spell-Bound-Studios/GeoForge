// Copyright 2026 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// VoxelDelta relative to IGeoVolume position and scale.
    /// </summary>
    public readonly struct RawVoxelEdit {
        public Vector3Int VoxelSpacePosition { get; }
        public short DensityDelta { get; }
        public byte NewMatIndex { get; }

        public RawVoxelEdit(Vector3Int voxelSpacePosition, short densityDelta, byte newMatIndex) {
            VoxelSpacePosition = voxelSpacePosition;
            DensityDelta = densityDelta;
            NewMatIndex = newMatIndex;
        }
    }
}
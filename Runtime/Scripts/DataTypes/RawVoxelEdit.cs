// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// VoxelEdit relative to IVolume position and scale.
    /// </summary>
    public readonly struct RawVoxelEdit {
        public Vector3Int voxelSpacePosition { get; }
        public byte NewDensity { get; }
        public byte NewMatIndex { get; }

        public RawVoxelEdit(Vector3Int voxelSpacePosition, byte newDensity, byte newMatIndex) {
            this.voxelSpacePosition = voxelSpacePosition;
            NewDensity = newDensity;
            NewMatIndex = newMatIndex;
        }
    }
}
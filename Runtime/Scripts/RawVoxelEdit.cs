// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.WorldSystem {
    /// <summary>
    /// This never gets sent on the network.
    /// </summary>
    public readonly struct RawVoxelEdit {
        public Vector3Int WorldPosition { get; }
        public int DensityChange { get; }
        public byte NewMatIndex { get; }

        public RawVoxelEdit(Vector3Int worldPosition, int densityChange, byte newMatIndex) {
            WorldPosition = worldPosition;
            DensityChange = densityChange;
            NewMatIndex = newMatIndex;
        }
    }
}
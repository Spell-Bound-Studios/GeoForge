// Copyright 2026 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// A raw (world/volume-space) density change, before it's been mapped to a chunk-local index.
    /// Material is NOT stored here — it lives once on the parent RawVoxelEditOperation, matching
    /// VoxelEditOperation's convention that a whole terraform action shares one candidate material.
    /// </summary>
    public readonly struct RawVoxelEdit {
        public Vector3Int VoxelSpacePosition { get; }
        public short DensityDelta { get; }

        public RawVoxelEdit(Vector3Int voxelSpacePosition, short densityDelta) {
            VoxelSpacePosition = voxelSpacePosition;
            DensityDelta = densityDelta;
        }
    }
}
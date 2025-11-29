// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.MarchingCubes {
    public interface IVolume {
        VoxVolume VoxelVolume { get; }
        VoxelVolumeConfig Config { get; }
        Vector2[] ViewDistanceLodRanges { get; }

        Transform LodTarget { get; }

        void ManageVolume();
    }
}
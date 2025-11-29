// Copyright 2025 Spellbound Studio Inc.

namespace Spellbound.MarchingCubes {
    public interface IVolume {
        VoxVolume VoxelVolume { get; }

        void ManageVolume(); // config as parameter later
    }
}
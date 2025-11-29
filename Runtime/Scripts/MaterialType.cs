// Copyright 2025 Spellbound Studio Inc.

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Material Types for Voxel Terrain
    /// WARNING: The order of this enum MUST match the order of the textures in the TerrainTextureArray
    /// </summary>
    public enum MaterialType : byte {
        Dirt,
        Swamp,
        Sand,
        Ice
    }
}
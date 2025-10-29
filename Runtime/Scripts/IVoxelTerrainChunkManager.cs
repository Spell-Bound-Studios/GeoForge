// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.MarchingCubes {
    public interface IVoxelTerrainChunkManager {
        public IVoxelTerrainChunk GetChunkByPosition(Vector3 position);

        public IVoxelTerrainChunk GetChunkByCoord(Vector3Int coord);
    }
}
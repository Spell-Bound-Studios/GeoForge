// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Spellbound.MarchingCubes;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public interface IVoxelTerrainChunkManager {
        public IVoxelTerrainChunk GetChunkByPosition(Vector3 position);

        public IVoxelTerrainChunk GetChunkByCoord(Vector3Int coord);
    }
}
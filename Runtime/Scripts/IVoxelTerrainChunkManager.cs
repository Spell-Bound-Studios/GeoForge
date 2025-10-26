// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Spellbound.MarchingCubes;
using UnityEngine;

namespace Spellbound.Core {
    public interface IVoxelTerrainChunkManager {
        public IVoxelTerrainChunk GetChunkByPosition(Vector3 position);

        public IVoxelTerrainChunk GetChunkByCoord(Vector3Int coord);

        public void HandleVoxelEdits(Dictionary<Vector3Int, List<VoxelEdit>> editsByChunkCoord);
    }
}
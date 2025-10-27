
using System.Collections.Generic;
using UnityEngine;
using Spellbound.Core;

namespace Spellbound.MarchingCubes {
    public class TerrainChunkManager : MonoBehaviour, IVoxelTerrainChunkManager {
        
        private Dictionary<Vector3Int, IVoxelTerrainChunk> _chunkDict = new();

        public IVoxelTerrainChunk GetChunkByPosition(Vector3 position) {
            var coord = SpellboundStaticHelper.WorldToChunk(position);

            return GetChunkByCoord(coord);
        }

        public IVoxelTerrainChunk GetChunkByCoord(Vector3Int coord) => _chunkDict.GetValueOrDefault(coord);

    }
}



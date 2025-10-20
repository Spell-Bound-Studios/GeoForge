// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public interface IVoxelTerrainChunk {
        public NativeArray<VoxelData> GetVoxelArray();

        public bool IsChunkAllOneSideOfThreshold();
        public Vector3Int GetChunkCoord();

        public Transform GetChunkTransform();

        public void ReceivedProcGenData(
            NativeList<SparseVoxelData> voxels,
            byte minDensity,
            byte maxDensity);
        
        public void UpdateSparseVoxels(NativeList<SparseVoxelData> voxels);

        public void BroadcastNewLeaf(OctreeNode newLeaf, Vector3 pos, int index);

        public void AddToVoxelEdits(List<VoxelEdit> newVoxelEdits);

        public VoxelData GetVoxelData(int index);

        public VoxelData GetVoxelData(Vector3Int position);
    }
}
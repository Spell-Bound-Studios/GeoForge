// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Defines the contract that a chunk must fulfill to integrate with the Marching Cubes Voxel System.
    /// </summary>
    public interface IVoxelTerrainChunk {
        public NativeArray<VoxelData> GetVoxelData();

        public DensityRange GetDensityRange();

        public void SetDensityRange(DensityRange densityRange);
        public Vector3Int GetChunkCoord();

        public Transform GetChunkTransform();

        public void InitializeVoxelData(NativeList<SparseVoxelData> voxels);

        public void UpdateVoxelData(NativeList<SparseVoxelData> voxels);

        public bool IsDirty();

        public void BroadcastNewLeaf(OctreeNode newLeaf, Vector3 pos, int index);

        public void AddToVoxelEdits(List<VoxelEdit> newVoxelEdits);

        public VoxelData GetVoxelData(int index);

        public VoxelData GetVoxelData(Vector3Int position);

        public bool HasVoxelData();

        public void ValidateOctreeEdits(Bounds bounds);
    }
}
// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class SampleChunk : MonoBehaviour, IChunk {
        private VoxChunk _voxChunk;
        public VoxChunk VoxelChunk => _voxChunk;

        private void Awake() => _voxChunk = new VoxChunk(this);

        public void InitializeChunk(NativeArray<VoxelData> voxels) => VoxelChunk.InitializeVoxels(voxels);

        public void PassVoxelEdits(List<VoxelEdit> newVoxelEdits) {
            if (VoxelChunk.ApplyVoxelEdits(newVoxelEdits, out var editBounds)) {
                VoxelChunk.ValidateOctreeEdits(editBounds);
            }
        }

        private void OnDestroy() => _voxChunk.Dispose();

        private void OnDrawGizmos() {
            var worldSize = (Vector3)VoxelChunk.Bounds.size *
                            VoxelChunk.ParentVolume.VoxelVolume.ConfigBlob.Value.Resolution;
            var localOffset = worldSize * 0.5f;
            var worldCenter = transform.position + transform.TransformDirection(localOffset);
            Gizmos.DrawWireCube(worldCenter, worldSize);
        }
    }
}
// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class SimpleChunk : MonoBehaviour, IChunk {
        private BaseChunk _baseChunk;
        public BaseChunk BaseChunk => _baseChunk;

        private void Awake() => _baseChunk = new BaseChunk(this, this);

        public void InitializeChunk(NativeArray<VoxelData> voxels) => _baseChunk.InitializeVoxels(voxels);

        public void PassVoxelEdits(List<VoxelEdit> newVoxelEdits) {
            if (_baseChunk.ApplyVoxelEdits(newVoxelEdits, out var editBounds))
                _baseChunk.ValidateOctreeEdits(editBounds);
        }

        private void OnDestroy() => _baseChunk.Dispose();

        private void OnDrawGizmos() {
            var worldSize = (Vector3)_baseChunk.Bounds.size *
                            _baseChunk.ParentVolume.ConfigBlob.Value.Resolution;
            var localOffset = worldSize * 0.5f;
            var worldCenter = transform.position + transform.TransformDirection(localOffset);
            Gizmos.DrawWireCube(worldCenter, worldSize);
        }
    }
}
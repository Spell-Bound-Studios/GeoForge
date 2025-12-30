// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class SimpleChunk : MonoBehaviour, IChunk {
        [Tooltip("Preset for what voxel data is generated in the volume"), SerializeField]
        protected DataFactory dataFactory;

        [Tooltip("Rules for immutable voxels on the external faces of the volume"), SerializeField]
        protected BoundaryOverrides boundaryOverrides;

        private BaseChunk _baseChunk;
        public BaseChunk BaseChunk => _baseChunk;

        private void Awake() => _baseChunk = new BaseChunk(this, this);

        private void Start() => InitializeChunk();

        /// <summary>
        /// Generates voxels with the datafactory.
        /// </summary>
        /// <param name="voxels"></param>
        public void InitializeChunk(NativeArray<VoxelData> voxels = default) {
            _baseChunk.ParentVolume.BaseVolume.RegisterChunk(_baseChunk.ChunkCoord, this);

            if (boundaryOverrides != null) {
                var overrides = boundaryOverrides.BuildChunkOverrides(
                    _baseChunk.ChunkCoord, _baseChunk.ParentVolume.ConfigBlob);
                _baseChunk.SetOverrides(overrides);
            }

            if (voxels == default) {
                voxels =
                        new NativeArray<VoxelData>(_baseChunk.ParentVolume.ConfigBlob.Value.ChunkDataVolumeSize,
                            Allocator.Persistent);
            }

            dataFactory.FillDataArray(_baseChunk.ChunkCoord, _baseChunk.ParentVolume.ConfigBlob, voxels);
            _baseChunk.InitializeVoxels(voxels);

            if (voxels.IsCreated)
                voxels.Dispose();
        }

        /// <summary>
        /// Calls ApplyVoxelEdits on the base chunk, and if the return value is true then some real changes occured
        /// and must validate the octree within the bounds of the edits.
        /// </summary>
        /// <param name="newVoxelEdits"></param> VoxelEdits distributed to this chunk by the Marching Cubes Manager.
        public void PassVoxelEdits(List<VoxelEdit> newVoxelEdits) {
            if (_baseChunk.ApplyVoxelEdits(newVoxelEdits, out var editBounds))
                _baseChunk.ValidateOctreeEdits(editBounds);
        }

        /// <summary>
        /// This must be done on ALL IChunk implementers to prevent memory leaks.
        /// </summary>
        private void OnDestroy() => _baseChunk.Dispose();
    }
}
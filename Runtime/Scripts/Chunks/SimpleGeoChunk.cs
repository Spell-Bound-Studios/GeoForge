// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    public class SimpleGeoChunk : MonoBehaviour, IGeoChunk {
        protected DataFactory dataFactory;

        protected BoundaryOverrides boundaryOverrides;

        protected GeoChunk _geoChunk;
        public GeoChunk GeoChunk => _geoChunk;

        private void Awake() => _geoChunk = new GeoChunk(this, this);

        public void SetDataFactory(DataFactory factory) => dataFactory = factory;

        public void SetBoundaryOverrides(BoundaryOverrides overrides) => boundaryOverrides = overrides;

        /// <summary>
        /// Generates voxels with the datafactory.
        /// </summary>
        /// <param name="voxels"></param>
        public void InitializeChunk(NativeArray<VoxelData> voxels = default) {
            _geoChunk.ParentGeoVolume.GeoVolume.RegisterChunk(_geoChunk.ChunkCoord, this);

            if (boundaryOverrides != null) {
                var overrides = boundaryOverrides.BuildChunkOverrides(
                    _geoChunk.ChunkCoord, _geoChunk.ParentGeoVolume.ConfigBlob);
                _geoChunk.SetOverrides(overrides);
            }

            if (voxels == default) {
                voxels =
                        new NativeArray<VoxelData>(_geoChunk.ParentGeoVolume.ConfigBlob.Value.ChunkDataVolumeSize,
                            Allocator.Persistent);
            }

            dataFactory.FillDataArray(_geoChunk.ChunkCoord, _geoChunk.ParentGeoVolume.ConfigBlob, voxels);
            _geoChunk.InitializeVoxels(voxels);

            if (voxels.IsCreated)
                voxels.Dispose();
        }
        
        /// <summary>
        /// This must be done on ALL IGeoChunk implementers to prevent memory leaks.
        /// </summary>
        private void OnDestroy() => _geoChunk.Dispose();
    }
}
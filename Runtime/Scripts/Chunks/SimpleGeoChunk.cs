// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    public class SimpleGeoChunk : MonoBehaviour, IGeoChunk {
        protected DataFactory dataFactory;

        protected BoundaryOverrides boundaryOverrides;

        protected GeoChunk _geoChunk;
        public GeoChunk GeoChunk => _geoChunk;

        public void InitializeGeoChunk(Vector3Int coord) {
            _geoChunk = new GeoChunk(this, transform, new SimpleGeoEditStore(), coord);
            GeoChunk.IGeoEditStore.DefaultVoxelDataFunc = GeoChunk.GetVoxelData;
        }

        public virtual void PassVoxelEdits(VoxelEditOperation operation) => GeoChunk.IGeoEditStore.Delta(operation);

        public void SetDataFactory(DataFactory factory) => dataFactory = factory;

        public void SetBoundaryOverrides(BoundaryOverrides overrides) => boundaryOverrides = overrides;

        /// <summary>
        /// Generates voxels with the datafactory.
        /// </summary>
        /// <param name="voxels"></param>
        public void ActivateGeoChunk(NativeArray<VoxelData> voxels = default) {
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
            _geoChunk.InitializeChunk(voxels);
        }

        /// <summary>
        /// This must be done on ALL IGeoChunk implementers to prevent memory leaks.
        /// _geoChunk is only assigned in InitializeGeoChunk, which nothing guarantees ran before
        /// this GameObject can be destroyed (e.g. a chunk prefab instantiated/inspected directly in
        /// the editor without going through GeoVolume.CreateChunk, then deleted) - guard against
        /// that instead of assuming initialization always happened first. GeoChunk.Dispose() itself
        /// is idempotent by design, so it's also safe if this fires after GeoVolume.Dispose()
        /// already disposed this chunk explicitly - no ordering assumption needed either way.
        /// </summary>
        private void OnDestroy() {
            if (_geoChunk == null)
                return;

            _geoChunk.Dispose();
        }
    }
}
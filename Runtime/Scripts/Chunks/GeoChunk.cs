// Copyright 2026 Spellbound Studio Inc.

using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Simple implementation of IGeoChunk for samples, and small projects.
    /// </summary>
    public class GeoChunk : MonoBehaviour, IGeoChunk {
        public GeoChunkEngine GeoChunkEngine { get; private set; }
        
        public void InitializeGeoChunk(Vector3Int coord, IGeoEditStore geoEditStore) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, geoEditStore, coord);
            GeoChunkEngine.IGeoEditStore.DefaultVoxelDataFunc = GeoChunkEngine.GetVoxelData;
        }

        void IGeoChunk.SetVoxels(
            Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlobAsset,
            GeoForgeDataGenerator geoDataGenerator) {
            GeoChunkEngine.SetVoxels(geoDataGenerator.GenerateProceduralVoxels(chunkCoord, configBlobAsset));

            OnVoxelsSet();
        }

        void IGeoChunk.HandleMeshReady() => OnMeshReady();

        protected virtual void OnMeshReady() { }

        protected virtual void OnVoxelsSet() { }

        private void OnDestroy() {
            if (GeoChunkEngine == null)
                return;

            GeoChunkEngine.Dispose();
        }
    }
}
// Copyright 2026 Spellbound Studio Inc.

using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IGeoVolume {
        #region Abstract Methods and Properties

        /// <summary>
        ///     Getter Property for Base Volume, which holds the core functionality of a Volume.
        /// </summary>
        GeoVolumeEngine GeoVolumeEngine { get; }

        /// <summary>
        ///     Ranges for each Level of Detail and the view distance range where you see it.
        /// </summary>
        Vector2[] ViewDistanceLodRanges { get; }

        /// <summary>
        ///     Target that the view distances are calculated from. Camera.Main is a good candidate for this.
        /// </summary>
        Transform LodTarget { get; }

        /// <summary>
        ///     Indication of if the Volume is moving or is capable of moving, or if not.
        /// </summary>
        bool IsMoving { get; set; }

        /// <summary>
        ///     Indication of if the Volume is the primary Terrain, making it the default Volume to Query.
        /// </summary>
        bool IsPrimaryTerrain { get; set; }

        #endregion

        #region Default Implmentations

        bool TryQueryVoxel(Vector3 worldPosition, out VoxelData voxel) {
            return GeoVolumeEngine.TryQueryVoxel(worldPosition, out voxel);
        }

        bool TryQuerySurfaceMaterial(Vector3 worldPosition, out byte materialIndex) {
            return GeoVolumeEngine.TryQuerySurfaceMaterial(worldPosition, out materialIndex);
        }

        void OnTerraform(Vector3 worldPosition, int voxelCount) { }

        Transform VolumeTransform => GeoVolumeEngine.Transform;

        (Vector3, Quaternion) SnapToGrid(Vector3 pos) {
            return GeoVolumeEngine.SnapToGrid(pos);
        }

        BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob => GeoVolumeEngine.ConfigBlob;

        bool IntersectsVolume(Bounds voxelBounds) {
            return GeoVolumeEngine.IntersectsVolume(voxelBounds);
        }

        async Awaitable ValidateChunkLods() {
            await GeoVolumeEngine.ValidateChunkLodsAsync();
        }

        Vector3Int WorldToVoxelSpace(Vector3 worldPosition) {
            return GeoVolumeEngine.WorldToVoxelSpace(worldPosition);
        }

        Vector3 WorldToVoxelSpaceContinuous(Vector3 worldPosition) {
            return GeoVolumeEngine.WorldToVoxelSpaceContinuous(worldPosition);
        }

        IGeoChunk GetChunkByCoord(Vector3Int coord) {
            return GeoVolumeEngine.GetChunkByCoord(coord);
        }

        IGeoChunk GetChunkByWorldPosition(Vector3 worldPos) {
            return GeoVolumeEngine.GetChunkByWorldPosition(worldPos);
        }

        IGeoChunk GetChunkByVoxelPosition(Vector3Int voxelPos) {
            return GeoVolumeEngine.GetChunkByVoxelPosition(voxelPos);
        }

        Vector3Int GetCoordByVoxelPosition(Vector3Int voxelPos) {
            return GeoVolumeEngine.GetCoordByVoxelPosition(voxelPos);
        }

        /// <summary>
        ///     Packs every chunk's edits in this volume into one byte[] - see
        ///     GeoVolumeEngine.SaveToByteArray for the wire format and what gets skipped/included.
        /// </summary>
        byte[] SaveToByteArray() {
            return GeoVolumeEngine.SaveToByteArray();
        }

        /// <summary>
        ///     Restores edits from a payload written by SaveToByteArray. Returns false (and logs) if
        ///     the payload is malformed or doesn't match this volume's current chunk size - see
        ///     GeoVolumeEngine.TryLoadFromByteArray for the full contract, including its batching behavior
        ///     and how it handles a saved coordinate with no loaded chunk.
        /// </summary>
        bool TryLoadFromByteArray(byte[] data) {
            return GeoVolumeEngine.TryLoadFromByteArray(data);
        }

        void ResetEditedChunksToProcedural();

        void HandleAllChunksMeshed() { }

        #endregion
    }
}
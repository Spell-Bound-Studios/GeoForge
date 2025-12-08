// Copyright 2025 Spellbound Studio Inc.

using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(menuName = "Spellbound/MarchingCubes/VoxelVolumeConfig")]
    public class VoxelVolumeConfig : ScriptableObject {
        [Tooltip("Voxel Fill threshold separating full and empty. 128 is default."),
         Range(1, 255)]
        public byte threshold = 128;

        [Tooltip("Determines how many voxels are marched at a time, per side. Higher number may affect performance"),
         Range(8, 32)]
        public int cubesPerMarch = 16;

        [Tooltip("Max number of Levels of Details"),
         Range(1, 5)]
        public int levelsOfDetail = 3;

        [Tooltip("Size in voxels of one chunk of data. Higher number may affect performance."),
         Range(8, 128)]
        public int maxChunkSize = 128;

        [Tooltip("Actual chunksize, generated from maxChunkSize and Levels of Detail"),
         SerializeField]
        private int chunkSize;

        public int ChunkSize => chunkSize;

        [Tooltip("Number of Voxels per one unit/meter. Lower number may affect performance."),
         Range(0.1f, 10f)]
        public float resolution = 1;

        [Tooltip("Indicates if the volume is finite, rather than like an endless terrain")]
        public bool isFiniteSize = true;

        [Tooltip("For a finite volume, how many chunks it is in each axis")]
        public Vector3Int sizeInChunks;

        [Tooltip("This dimension is derived from your other settings. Indicates actual size of volume, if its finite."),
         SerializeField]
        private Vector3 volumeSize;

        public Vector3 VolumeSize => volumeSize;

        private void OnValidate() {
            ValidateChunkSize();
            ValidateSizeInChunks();
            ValidateVolumeSize();
        }

        private void ValidateChunkSize() {
            chunkSize = cubesPerMarch;

            // Keep doubling until we reach maxChunkSize
            while (chunkSize * 2 <= maxChunkSize) chunkSize *= 2;

            // Now calculate LOD levels based on resulting chunk size
            // LOD levels = log2(chunkSize / cubesPerMarch)
            var calculatedLod = 0;
            var tempSize = cubesPerMarch;

            while (tempSize < chunkSize) {
                tempSize *= 2;
                calculatedLod++;
            }

            // Make sure levelsOfDetail matches
            if (levelsOfDetail != calculatedLod + 1) {
                Debug.LogWarning(
                    $"Adjusted levelsOfDetail from {levelsOfDetail} to {calculatedLod + 1} to match chunkSize {chunkSize}");
                levelsOfDetail = calculatedLod + 1;
            }
        }

        private void ValidateVolumeSize() => volumeSize = (Vector3)sizeInChunks * chunkSize * resolution;

        private void ValidateSizeInChunks() {
            sizeInChunks.x = Mathf.Max(1, sizeInChunks.x);
            sizeInChunks.y = Mathf.Max(1, sizeInChunks.y);
            sizeInChunks.z = Mathf.Max(1, sizeInChunks.z);
        }
    }
}
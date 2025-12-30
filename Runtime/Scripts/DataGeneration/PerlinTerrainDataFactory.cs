// Copyright 2025 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(menuName = "Spellbound/MarchingCubes/DataFactory/PerlinTerrain")]
    public class PerlinTerrainDataFactory : DataFactory {
        [Header("Data Factory Settings"), Tooltip("Offset of Terrain from Volume Origin"), SerializeField]
        private Vector3 offset = Vector3.zero;

        [Tooltip("Not recommended to change from default value of 32"), SerializeField]
        private float sdfGradientSteepness = 32f;

        [Tooltip("Material for the shape to be generated as. " +
                 "Refer to MarchingCubeManager for what index corresponds to what material"),
         SerializeField]
        private byte materialIndex = 0;

        [Header("Perlin Noise Settings"), Tooltip("Amplitude of Noise"), SerializeField]
        private float amplitude = 10;

        [Tooltip("Scale of Noise. One Over Wavelength"), SerializeField]
        private float baseNoiseScale = 0.05f;

        [Tooltip("Number of layers of noise"), SerializeField]
        private int octaves = 2;

        [Tooltip("How noise scale changes with each octave"), SerializeField]
        private float lacunarity = 2;

        [Tooltip("How amplitude changes with each octave"), SerializeField]
        private float persistence = 0.5f;

        public override void FillDataArray(
            Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> data) {
            ref var config = ref configBlob.Value;
            var chunkOrigin = GetChunkOrigin(chunkCoord, config);

            for (var i = 0; i < data.Length; ++i) {
                var voxelPos = GetVoxelPosition(i, chunkOrigin, config);
                var signedDistance = PerlinTerrainSDF(voxelPos, offset);
                var densityByte = SignedDistanceToDensity(signedDistance, sdfGradientSteepness, config);
                data[i] = new VoxelData(densityByte, materialIndex);
            }
        }

        private float PerlinTerrainSDF(Vector3 point, Vector3 terrainOrigin) {
            var noiseValue = 0f;
            var currentAmplitude = 1f;
            var currentFrequency = 1f;
            var maxValue = 0f;

            // Large offset to avoid negative sampling coordinates
            var offsetX = 10000f + offset.x;
            var offsetZ = 10000f + offset.z;

            for (var i = 0; i < octaves; i++) {
                var sampleX = (point.x + terrainOrigin.x + offsetX) * baseNoiseScale * currentFrequency;
                var sampleZ = (point.z + terrainOrigin.z + offsetZ) * baseNoiseScale * currentFrequency;

                var perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);

                noiseValue += perlinValue * currentAmplitude;
                maxValue += currentAmplitude;

                currentAmplitude *= persistence;
                currentFrequency *= lacunarity;
            }

            noiseValue /= maxValue;
            var terrainHeight = terrainOrigin.y + noiseValue * amplitude;

            return point.y - terrainHeight;
        }
    }
}
// Copyright 2025 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(menuName = "Spellbound/MarchingCubes/DataFactory/SimpleShapesDataFactory")]
    public class SimpleShapesDataFactory : DataFactory {
        public enum ShapeType {
            AllFilled,
            AllEmpty,
            Plane,
            Sphere,
            NoisySphere,
            PerlinTerrain
        }

        [SerializeField] private ShapeType shape = ShapeType.Sphere;
        [SerializeField] private float size = 16f;

        [Tooltip("false means the size is in Worldspace, true means the size is in Voxelspace"), SerializeField]
        private bool normalizedSize = false;

        [SerializeField] private Vector3 offset = Vector3.zero;

        [Tooltip("Controls smoothness of generation. Low number will be blocky"), SerializeField]
        private float sdfGradientSteepness = 32f;

        [Tooltip("Material for the shape to be generated as. " +
                 "Refer to MarchingCubeManager for what index corresponds to what material"),
         SerializeField]
        private byte materialIndex = 0;

        [Tooltip("Flip what part of the shape is full of material, and what part of the shape is air/empty"),
         SerializeField]
        private bool invertShape = false;

        public override void FillDataArray(
            Vector3Int chunkCoord,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> data) {
            ref var config = ref configBlob.Value;
            var chunkOrigin = GetChunkOrigin(chunkCoord, config);
            var shapeSizeInVoxels = normalizedSize ? size : size / config.Resolution;

            for (var i = 0; i < data.Length; ++i) {
                var voxelPos = GetVoxelPosition(i, chunkOrigin, config);
                var signedDistance = GetSignedDistance(voxelPos, shapeSizeInVoxels);
                signedDistance = invertShape ? -signedDistance : signedDistance;
                var densityByte = SignedDistanceToDensity(signedDistance, sdfGradientSteepness, config);
                data[i] = new VoxelData(densityByte, materialIndex);
            }
        }

        private float GetSignedDistance(Vector3 voxelPos, float voxelSize) =>
                shape switch {
                    ShapeType.AllFilled => float.MinValue,
                    ShapeType.AllEmpty => float.MaxValue,
                    ShapeType.Plane => PlaneSDF(voxelPos, offset),
                    ShapeType.Sphere => SphereSDF(voxelPos, offset, voxelSize),
                    ShapeType.NoisySphere => NoisySphereSDF(voxelPos, offset, voxelSize),
                    ShapeType.PerlinTerrain => PerlinTerrainSDF(voxelPos, offset, voxelSize),
                    _ => 0f
                };

        private float PlaneSDF(Vector3 point, Vector3 planeOrigin) => point.y - planeOrigin.y;

        private float SphereSDF(Vector3 point, Vector3 center, float radius) =>
                Vector3.Distance(point, center) - radius;

        private float NoisySphereSDF(Vector3 point, Vector3 center, float radius) {
            var noiseScale = 3f;
            var noiseAmplitude = radius / 5;
            var direction = point - center;
            var distance = direction.magnitude;
            var normalized = distance > 0.001f ? direction / distance : Vector3.up;

            var noise = Mathf.PerlinNoise(
                (normalized.x + center.x) * noiseScale,
                (normalized.y + center.y) * noiseScale
            );

            noise += Mathf.PerlinNoise(
                (normalized.z + center.z) * noiseScale,
                (normalized.x + center.x) * noiseScale
            ) * 0.5f;

            var modulatedRadius = radius + (noise - 0.5f) * noiseAmplitude;

            return distance - modulatedRadius;
        }

        private float PerlinTerrainSDF(Vector3 point, Vector3 terrainOrigin, float amplitude) {
            var noiseScale = 0.05f;

            var noiseValue = Mathf.PerlinNoise(
                (point.x + terrainOrigin.x) * noiseScale,
                (point.z + terrainOrigin.z) * noiseScale
            );

            var terrainHeight = terrainOrigin.y + noiseValue * amplitude;

            return point.y - terrainHeight;
        }
    }
}
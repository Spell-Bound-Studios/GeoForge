// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    public class BaseVolume : IDisposable {
        private readonly MonoBehaviour _owner;
        private readonly IVolume _ownerAsIVolume;
        private Dictionary<Vector3Int, IChunk> _chunkDict = new();
        private Bounds _bounds;
        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; private set; }

        public Transform Transform => _owner.transform;
        public Dictionary<Vector3Int, IChunk> ChunkDict => _chunkDict;

        public BaseVolume(MonoBehaviour owner, IVolume ownerAsIVolume, VoxelVolumeConfig config) {
            _owner = owner;
            _ownerAsIVolume = ownerAsIVolume;
            ConfigBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);
            _bounds = CalculateVolumeBounds();
        }

        public Vector3Int WorldToVoxelSpace(Vector3 worldPosition) {
            ref var config = ref ConfigBlob.Value;
            var localPos = Transform.InverseTransformPoint(worldPosition);

            return new Vector3Int(
                Mathf.RoundToInt(localPos.x / config.Resolution) - config.Offset.x,
                Mathf.RoundToInt(localPos.y / config.Resolution) - config.Offset.y,
                Mathf.RoundToInt(localPos.z / config.Resolution) - config.Offset.z
            );
        }

        public void RegisterVolume() {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var mcManager)) {
                Debug.LogError("GeoForgeManager is null." + this);

                return;
            }

            mcManager.RegisterVoxelVolume(_ownerAsIVolume);
        }

        public IChunk GetChunkByCoord(Vector3Int coord) => _chunkDict.GetValueOrDefault(coord);

        public IChunk GetChunkByWorldPosition(Vector3 worldPos) {
            var voxelPos = WorldToVoxelSpace(worldPos);

            return GetChunkByVoxelPosition(voxelPos);
        }

        public IChunk GetChunkByVoxelPosition(Vector3Int voxelPos) {
            var coord = GetCoordByVoxelPosition(voxelPos);

            return GetChunkByCoord(coord);
        }

        public Vector3Int GetCoordByVoxelPosition(Vector3Int voxelPos) {
            ref var config = ref ConfigBlob.Value;

            return new Vector3Int(
                Mathf.FloorToInt((voxelPos.x - 1f) / config.ChunkSize),
                Mathf.FloorToInt((voxelPos.y - 1f) / config.ChunkSize),
                Mathf.FloorToInt((voxelPos.z - 1f) / config.ChunkSize)
            );
        }

        public async Awaitable ValidateChunkLodsAsync() {
            var chunkList = new List<Vector3Int>(_chunkDict.Keys.ToList());

            foreach (var coord in chunkList) {
                if (!_chunkDict.TryGetValue(coord, out var chunk))
                    continue;

                if (!chunk.HasVoxelData())
                    continue;

                if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out _))
                    continue;

                var lodDistanceTargetVoxelSpace = WorldToVoxelSpace(_ownerAsIVolume.LodTarget.position);
                chunk.ValidateOctreeLods(lodDistanceTargetVoxelSpace);

                await Awaitable.NextFrameAsync();
            }
        }

        public bool RegisterChunk(Vector3Int chunkCoord, IChunk chunk) {
            if (chunk == null)
                return false;

            if (_chunkDict.TryAdd(chunkCoord, chunk))
                return true;

            return false;
        }

        public T CreateChunk<T>(Vector3Int chunkCoord, GameObject chunkPrefab) where T : class, IChunk {
            ref var config = ref ConfigBlob.Value;

            var localChunkPos = (Vector3)chunkCoord * (config.ChunkSize * config.Resolution);
            var worldChunkPos = Transform.TransformPoint(localChunkPos);

            var chunkObj = Object.Instantiate(
                chunkPrefab,
                worldChunkPos,
                Transform.rotation,
                Transform
            );

            if (!chunkObj.TryGetComponent(out T chunk)) {
                Debug.LogError($"Chunk prefab missing component of type {typeof(T).Name}");
                Object.Destroy(chunkObj); // Clean up failed instantiation

                return null;
            }

            chunk.SetCoordAndFields(chunkCoord);

            return chunk;
        }

        public void UpdateVolumeOrigin() {
            foreach (var chunk in _chunkDict.Values)
                chunk.OnVolumeMovement();
        }

        public static Vector2[] ValidateLodRanges(Vector2[] lodRanges, VoxelVolumeConfig config) {
            // Ensure correct array length
            if (lodRanges == null || lodRanges.Length != config.levelsOfDetail)
                lodRanges = new Vector2[config.levelsOfDetail];

            var dist = 0f;

            for (var i = 0; i < lodRanges.Length; i++) {
                lodRanges[i].x = dist;

                lodRanges[i].y = Mathf.Max(lodRanges[i].y,
                    lodRanges[i].x + 2 * config.resolution * (config.cubesPerMarch << i));
                dist = lodRanges[i].y;
            }

            return lodRanges;
        }

        public bool IntersectsVolume(Bounds voxelBounds) => _bounds.Intersects(voxelBounds);

        private Bounds CalculateVolumeBounds() {
            ref var config = ref ConfigBlob.Value;

            var sizeInVoxels = new Vector3(
                config.SizeInChunks.x * config.ChunkSize,
                config.SizeInChunks.y * config.ChunkSize,
                config.SizeInChunks.z * config.ChunkSize
            );

            var center = Vector3.zero - config.Offset;

            return new Bounds(center, sizeInVoxels);
        }

        public (Vector3, Quaternion) SnapToGrid(Vector3 pos) {
            var localPos = Transform.InverseTransformPoint(pos);
            var resolution = ConfigBlob.Value.Resolution;

            var snappedLocal = resolution * new Vector3(
                Mathf.Round(localPos.x / resolution),
                Mathf.Round(localPos.y / resolution),
                Mathf.Round(localPos.z / resolution)
            );

            var snappedWorld = Transform.TransformPoint(snappedLocal);

            return (snappedWorld, Transform.rotation);
        }

        public void Dispose() {
            if (ConfigBlob.IsCreated)
                ConfigBlob.Dispose();
        }
    }
}
// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.MarchingCubes {
    public class VoxVolume : IDisposable {
        private readonly MonoBehaviour _owner;
        private readonly IVolume _ownerAsIVolume;
        private readonly MarchingCubesManager _mcManager;
        private Dictionary<Vector3Int, IChunk> _chunkDict = new();
        private GameObject _chunkPrefab;
        private bool _isPrimaryTerrain = false;

        private BoundsInt _bounds;
        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; private set; }
        
        public Transform Transform => _owner.transform;
        public Dictionary<Vector3Int, IChunk> ChunkDict => _chunkDict;

        public bool IsPrimaryTerrain {
            get => _isPrimaryTerrain;
            set => _isPrimaryTerrain = value;
        }

        public BoundsInt Bounds {
            get => _bounds;
            set => _bounds = value;
        }

        public bool IntersectsVolume(Bounds voxelBounds) {
            if (IsPrimaryTerrain)
                return true;

            var volumeBounds = new Bounds(_bounds.center, _bounds.size);
            return volumeBounds.Intersects(voxelBounds);
        }

        public VoxVolume(MonoBehaviour owner, IVolume ownerAsIVolume, GameObject chunkPrefab) {
            _owner = owner;
            _ownerAsIVolume = ownerAsIVolume;
            _chunkPrefab = chunkPrefab;
            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
            ConfigBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(_ownerAsIVolume.Config);
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

                if (!chunk.VoxelChunk.HasVoxelData())
                    continue;

                if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out _))
                    continue;

                var lodDistanceTargetVoxelSpace = WorldToVoxelSpace(_ownerAsIVolume.LodTarget.position);
                chunk.VoxelChunk.ValidateOctreeLods(lodDistanceTargetVoxelSpace);

                await Awaitable.NextFrameAsync();
            }
        }

        public IChunk RegisterChunk(Vector3Int chunkCoord) {
            var newChunk = CreateChunk(chunkCoord);
            _chunkDict[chunkCoord] = newChunk;

            return newChunk;
        }

        private IChunk CreateChunk(Vector3Int chunkCoord) {
            ref var config = ref ConfigBlob.Value;

            var localChunkPos = (Vector3)chunkCoord * (config.ChunkSize * _ownerAsIVolume.Config.resolution);
            var worldChunkPos = Transform.TransformPoint(localChunkPos);

            var chunkObj = Object.Instantiate(
                _chunkPrefab,
                worldChunkPos,
                Transform.rotation,
                Transform
            );

            if (!chunkObj.TryGetComponent(out IChunk chunk)) return null;

            chunk.VoxelChunk.SetCoordAndFields(chunkCoord);

            return chunk;
        }

        public void UpdateVolumeOrigin() {
            foreach (var chunk in _chunkDict.Values) chunk.VoxelChunk.OnVolumeMovement();
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
        
        private BoundsInt CalculateVolumeBounds() {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");

                return new BoundsInt();
            }

            ref var config = ref ConfigBlob.Value;

            // Calculate total size in voxels
            var sizeInVoxels = new Vector3Int(
                config.SizeInChunks.x * config.ChunkSize,
                config.SizeInChunks.y * config.ChunkSize,
                config.SizeInChunks.z * config.ChunkSize
            );

            // Calculate center offset (since chunks are centered around origin)
            var offset = new Vector3Int(
                config.SizeInChunks.x / 2,
                config.SizeInChunks.y / 2,
                config.SizeInChunks.z / 2
            );

            var centerInVoxels = new Vector3Int(
                -offset.x * config.ChunkSize + sizeInVoxels.x / 2,
                -offset.y * config.ChunkSize + sizeInVoxels.y / 2,
                -offset.z * config.ChunkSize + sizeInVoxels.z / 2
            );

            // Create bounds centered at the calculated center
            return new BoundsInt(
                centerInVoxels.x - sizeInVoxels.x / 2,
                centerInVoxels.y - sizeInVoxels.y / 2,
                centerInVoxels.z - sizeInVoxels.z / 2,
                sizeInVoxels.x,
                sizeInVoxels.y,
                sizeInVoxels.z
            );
        }
        
        public IEnumerator InitializeChunks(
            Action<Vector3Int, NativeArray<VoxelData>> generateDenseData,
            Func<Vector3Int, VoxelOverrides> buildOverridesFunc = null)
        {
            var size = ConfigBlob.Value.SizeInChunks;
            var offset = new Vector3Int(size.x / 2, size.y / 2, size.z / 2);
            var denseVoxels = new NativeArray<VoxelData>(ConfigBlob.Value.ChunkDataVolumeSize, Allocator.Persistent);

            for (var x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    for (var z = 0; z < size.z; z++) {
                        var chunkCoord = new Vector3Int(x, y, z) - offset;
                        var chunk = RegisterChunk(chunkCoord);
                        generateDenseData(chunkCoord, denseVoxels);
                        
                        if (buildOverridesFunc != null) {
                            chunk.VoxelChunk.SetOverrides(buildOverridesFunc(chunkCoord));
                        }
                        
                        chunk.InitializeChunk(denseVoxels);
                        yield return null;
                    }
                }
            }
            denseVoxels.Dispose();
        }

        public void Dispose() {
            if (ConfigBlob.IsCreated)
                ConfigBlob.Dispose();
        }
    }
}
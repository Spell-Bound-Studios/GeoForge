// Copyright 2025 Spellbound Studio Inc.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class VoxVolume {
        private readonly MonoBehaviour _owner;
        private readonly MarchingCubesManager _mcManager;
        private Dictionary<Vector3Int, IChunk> _chunkDict = new();
        private GameObject _chunkPrefab;
        private Transform _lodTarget;
        private bool _isPrimaryTerrain = false;
        private BoundsInt _bounds;

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

        public bool IntersectsVolume(Bounds worldBounds) {
            if (IsPrimaryTerrain)
                return true;
            
            float resolution = _mcManager.McConfigBlob.Value.Resolution;
    
            // Convert the 8 corners of the world bounds into local voxel space
            Vector3[] worldCorners = new Vector3[8];
            worldCorners[0] = worldBounds.min;
            worldCorners[1] = new Vector3(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z);
            worldCorners[2] = new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z);
            worldCorners[3] = new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z);
            worldCorners[4] = new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z);
            worldCorners[5] = new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z);
            worldCorners[6] = new Vector3(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z);
            worldCorners[7] = worldBounds.max;
    
            // Transform all corners to local voxel space
            Vector3 localMin = Vector3.positiveInfinity;
            Vector3 localMax = Vector3.negativeInfinity;
    
            for (int i = 0; i < 8; i++) {
                Vector3 localCorner = Transform.InverseTransformPoint(worldCorners[i]) / resolution;
                localMin = Vector3.Min(localMin, localCorner);
                localMax = Vector3.Max(localMax, localCorner);
            }
    
            // Create bounds in local voxel space
            Bounds localWorldBounds = new Bounds();
            localWorldBounds.SetMinMax(localMin, localMax);
    
            // Check intersection with volume bounds
            Bounds volumeBounds = new Bounds(_bounds.center, _bounds.size);
            return volumeBounds.Intersects(localWorldBounds);
        }

        public VoxVolume(MonoBehaviour owner, GameObject chunkPrefab) {
            _owner = owner;
            _chunkPrefab = chunkPrefab;
            _mcManager = SingletonManager.GetSingletonInstance<MarchingCubesManager>();
        }

        public Vector3Int WorldToVoxelSpace(Vector3 worldPosition) {
            ref var config = ref SingletonManager.GetSingletonInstance<MarchingCubesManager>().McConfigBlob.Value;
            var localPos = Transform.InverseTransformPoint(worldPosition);

            return new Vector3Int(
                Mathf.FloorToInt(localPos.x / config.Resolution),
                Mathf.FloorToInt(localPos.y / config.Resolution),
                Mathf.FloorToInt(localPos.z / config.Resolution)
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
            ref var config = ref _mcManager.McConfigBlob.Value;

            return new Vector3Int(
                Mathf.FloorToInt((voxelPos.x - 1f) / config.ChunkSize),
                Mathf.FloorToInt((voxelPos.y - 1f) / config.ChunkSize),
                Mathf.FloorToInt((voxelPos.z - 1f) / config.ChunkSize)
            );
        }

        public IEnumerator ValidateChunkLods() {
            while (true) {
                var chunkList = new List<Vector3Int>(_chunkDict.Keys.ToList());

                foreach (var coord in chunkList) {
                    if (!_chunkDict.TryGetValue(coord, out var chunk))
                        continue;

                    if (!chunk.VoxelChunk.HasVoxelData())
                        continue;

                    if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out _))
                        continue;

                    var lodDistanceTargetTransformed = Transform.InverseTransformPoint(_lodTarget.position);
                    chunk.VoxelChunk.ValidateOctreeLods(lodDistanceTargetTransformed);

                    yield return null;
                }

                yield return null;
            }
        }

        public IChunk RegisterChunk(Vector3Int chunkCoord) {
            var newChunk = CreateChunk(chunkCoord);
            _chunkDict[chunkCoord] = newChunk;

            return newChunk;
        }

        private IChunk CreateChunk(Vector3Int chunkCoord) {
            ref var config = ref SingletonManager.GetSingletonInstance<MarchingCubesManager>().McConfigBlob.Value;

            var localChunkPos = (Vector3)(chunkCoord * config.ChunkSizeResolution);
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

        public void SetLodTarget(Transform target) => _lodTarget = target;
    }
}
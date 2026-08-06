// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core.Tooling;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    public class GeoVolume : IDisposable {
        private readonly MonoBehaviour _owner;
        private readonly IGeoVolume _ownerAsIGeoVolume;
        private Dictionary<Vector3Int, IGeoChunk> _chunkDict = new();
        private Bounds _bounds;
        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; private set; }

        public Transform Transform => _owner.transform;
        public Dictionary<Vector3Int, IGeoChunk> ChunkDict => _chunkDict;

        public GeoVolume(MonoBehaviour owner, IGeoVolume ownerAsIGeoVolume, VoxelVolumeConfig config) {
            _owner = owner;
            _ownerAsIGeoVolume = ownerAsIGeoVolume;
            ConfigBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);
            _bounds = CalculateVolumeBounds();
        }

        public bool AllChunksReady() {
            if (!ConfigBlob.Value.IsFiniteSize)
                return false;
            
            if (ConfigBlob.Value.TotalChunks != _chunkDict.Count)
                return false;
            
            return true;
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
        
        public Vector3 WorldToVoxelSpaceContinuous(Vector3 worldPosition) {
            ref var config = ref ConfigBlob.Value;
            var localPos = Transform.InverseTransformPoint(worldPosition);

            return new Vector3(
                localPos.x / config.Resolution - config.Offset.x,
                localPos.y / config.Resolution - config.Offset.y,
                localPos.z / config.Resolution - config.Offset.z
            );
        }

        public void RegisterVolume() {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var mcManager)) {
                Debug.LogError("GeoForgeManager is null." + this);

                return;
            }

            mcManager.RegisterVoxelVolume(_ownerAsIGeoVolume);
        }

        public IGeoChunk GetChunkByCoord(Vector3Int coord) => _chunkDict.GetValueOrDefault(coord);

        public IGeoChunk GetChunkByWorldPosition(Vector3 worldPos) {
            var voxelPos = WorldToVoxelSpace(worldPos);

            return GetChunkByVoxelPosition(voxelPos);
        }

        public IGeoChunk GetChunkByVoxelPosition(Vector3Int voxelPos) {
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

        // Batches chunks into groups of ConfigBlob.Value.ValidatesPerFrame: schedule each chunk's
        // LOD validation (checkout + octree cascade, via GeoChunk.ScheduleOctreeLodValidation) but
        // don't complete or release any of them until the whole batch has been scheduled, so their
        // march jobs can actually run concurrently on worker threads via one shared Complete()
        // instead of one Complete() per chunk serializing everything.
        //
        // Fixed at exactly ValidatesPerFrame per batch, not "up to and including" - the old
        // (++count <= ValidatesPerFrame) check let one extra chunk through per batch, which was
        // harmless when each chunk released before the next was scheduled but would exhaust the
        // Validation pool now, since that pool is sized to exactly ValidatesPerFrame slots.
        public async Awaitable ValidateChunkLodsAsync() {
            var chunkList = new List<Vector3Int>(_chunkDict.Keys.ToList());

            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var mcManager))
                return;

            var lodTarget = _ownerAsIGeoVolume.LodTarget;

            if (lodTarget == null)
                return;

            var validatesPerFrame = ConfigBlob.Value.ValidatesPerFrame;
            var scheduledChunks = new List<IGeoChunk>(validatesPerFrame);

            foreach (var coord in chunkList) {
                if (!_chunkDict.TryGetValue(coord, out var chunk))
                    continue;

                if (!chunk.HasVoxelData())
                    continue;

                if (chunk.DensityRange.IsSkippable()) {
                    continue;
                }
                    

                var lodDistanceTargetVoxelSpace = WorldToVoxelSpace(lodTarget.position);
                chunk.ScheduleOctreeLodValidation(lodDistanceTargetVoxelSpace);
                scheduledChunks.Add(chunk);

                if (scheduledChunks.Count < validatesPerFrame)
                    continue;

                mcManager.CompleteAndApplyMarchingCubesJobs();

                foreach (var scheduledChunk in scheduledChunks)
                    scheduledChunk.ReleaseLodValidation();

                scheduledChunks.Clear();

                await Awaitable.NextFrameAsync();

                // Re-check liveness after the yield, once per batch rather than once per chunk
                // (the old loop re-checked the manager singleton on every single chunk). Bails out
                // and abandons any remaining chunks in this call if the manager is gone.
                if (!SingletonManager.TryGetSingletonInstance(out mcManager))
                    return;
            }

            // Flush a partial final batch that never reached validatesPerFrame.
            if (scheduledChunks.Count > 0) {
                mcManager.CompleteAndApplyMarchingCubesJobs();

                foreach (var scheduledChunk in scheduledChunks)
                    scheduledChunk.ReleaseLodValidation();
            }
        }

        public bool RegisterChunk(Vector3Int chunkCoord, IGeoChunk geoChunk) {
            if (geoChunk == null)
                return false;

            if (_chunkDict.TryAdd(chunkCoord, geoChunk)) {
                return true;
            }
                

            return false;
        }

        public T CreateChunk<T>(Vector3Int chunkCoord, GameObject chunkPrefab) where T : class, IGeoChunk {
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
                Debug.LogError($"Chunk bakePrefab missing component of type {typeof(T).Name}");
                Object.Destroy(chunkObj); // Clean up failed instantiation

                return null;
            }

            chunk.InitializeGeoChunk(chunkCoord);

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
            var chunkList = new List<IGeoChunk>(_chunkDict.Values);
            foreach (var chunk in chunkList) {
                chunk.GeoChunk.Dispose();

                // Destroy (deferred to end-of-frame) instead of DestroyImmediate: DestroyImmediate
                // synchronously re-enters SimpleGeoChunk.OnDestroy() -> _geoChunk.Dispose() right
                // here in the middle of this loop, which is the risky ordering this exit criterion
                // calls out. GeoChunk.Dispose() is now idempotent by design (see its own guard),
                // so whichever order the deferred OnDestroy fires in, it's safe either way.
                Object.Destroy(chunk.GeoChunk.Transform.gameObject);
            }
            
            if (SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var mcManager)) {
                mcManager.UnRegisterVoxelVolume(_ownerAsIGeoVolume);
            }
            
            if (ConfigBlob.IsCreated)
                ConfigBlob.Dispose();
        }
    }
}
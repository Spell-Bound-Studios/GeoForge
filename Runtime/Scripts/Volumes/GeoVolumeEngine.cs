// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core.Packing;
using Spellbound.Core.Tooling;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    public class GeoVolumeEngine : IDisposable {
        private const int SaveFormatVersion = 1;

        private readonly MonoBehaviour _owner;
        private readonly IGeoVolume _ownerAsIGeoVolume;
        private Dictionary<Vector3Int, IGeoChunk> _chunkDict = new();
        private Bounds _bounds;
        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; private set; }

        public Transform Transform => _owner.transform;
        public Dictionary<Vector3Int, IGeoChunk> ChunkDict => _chunkDict;
        private HashSet<IGeoChunk> _meshedChunks = new();

        public GeoVolumeEngine(MonoBehaviour owner, IGeoVolume ownerAsIGeoVolume, VoxelVolumeConfig config) {
            _owner = owner;
            _ownerAsIGeoVolume = ownerAsIGeoVolume;
            ConfigBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);
            _bounds = CalculateVolumeBounds();
        }

        public void HandleChunkMeshReady(IGeoChunk chunk) {
            if (!_meshedChunks.Add(chunk))
                return;

            var isAllMeshed = _meshedChunks.Count == ConfigBlob.Value.TotalChunks
                              || (!ConfigBlob.Value.IsFiniteSize && _meshedChunks.Count == _chunkDict.Count);

            if (isAllMeshed) {
                _ownerAsIGeoVolume.HandleAllChunksMeshed();
            }
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

        // Resets every chunk whose edit-store currently has recorded edits back to deterministic
        // procedural baseline - chunks that are already clean are skipped, since there's nothing to
        // wipe and regenerating them would just be wasted work reproducing what's already there.
        // Intended to run immediately before applying a loaded save (see TryLoadFromByteArray), so a
        // load fully replaces the current session's state rather than merging with it.
        //
        // Also seeds each reset chunk's brand-new octree root with one real ValidateOctreeLods
        // pass, batched in groups of ValidatesPerFrame through the Validation pool (same pattern
        // ValidateChunkLodsAsync uses). This is required, not optional: SetVoxels (inside
        // RegenerateFromProceduralData) disposes and replaces the octree root entirely, and every
        // node in a brand new root starts un-leaf-initialized (OctreeNode._leafInitialized is only
        // ever set by MakeLeaf, which only ever runs from ValidateOctreeLods). ValidateOctreeEdits's
        // own UpdateLeaf explicitly no-ops on a node that's never been leaf-initialized - by design,
        // edits assume the octree structure already reflects the correct LOD and only re-march an
        // existing leaf, they never construct one from nothing. Without this seeding pass, a
        // reset-then-immediately-loaded chunk's restored edits get correctly written into voxel
        // data but never actually meshed until the background ValidateAllVolumesLodsAsync loop
        // happens to reach that chunk on its own.
        public void ResetEditedChunksToProcedural(GeoForgeDataGenerator  dataGenerator) {
            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager))
                return;
            
            _meshedChunks.Clear();

            var resetChunks = new List<IGeoChunk>();

            foreach (var kvp in _chunkDict) {
                var chunk = kvp.Value;

                using var enumerator = chunk.GeoChunkEngine.IGeoEditStore.ReadAllEdits().GetEnumerator();
                var hasEdits = enumerator.MoveNext();

                if (!hasEdits)
                    continue;

                chunk.SetVoxels(kvp.Key, ConfigBlob, dataGenerator);
                resetChunks.Add(chunk);
            }

            if (resetChunks.Count == 0)
                return;

            var lodTarget = _ownerAsIGeoVolume.LodTarget;

            if (lodTarget == null)
                return;

            var lodDistanceTargetVoxelSpace = WorldToVoxelSpace(lodTarget.position);
            var validatesPerFrame = ConfigBlob.Value.ValidatesPerFrame;
            
            for (var i = 0; i < resetChunks.Count; i += validatesPerFrame) {
                var batchSize = Mathf.Min(validatesPerFrame, resetChunks.Count - i);
                var batch = resetChunks.GetRange(i, batchSize);

                foreach (var chunk in batch)
                    chunk.ScheduleOctreeLodValidation(lodDistanceTargetVoxelSpace);

                gfManager.CompleteAndApplyMarchingCubesJobs();

                foreach (var chunk in batch)
                    chunk.ReleaseLodValidation();
            }
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
            var chunkList = _chunkDict.Keys.ToList();

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

        // Gathers every chunk's edits (via IGeoEditStore.ReadAllEdits, the interface's generic
        // walk-the-edits primitive - no assumption about which concrete IGeoEditStore each chunk
        // uses) into one packed byte[]. Chunks with zero edits are skipped entirely - most of a
        // large map is untouched, and there's nothing useful to persist for a chunk that's still
        // purely procedural. Payload layout: [SaveFormatVersion:int][ChunkSize:int][List<ChunkSaveEntry>].
        // ChunkSize is stamped so TryLoadFromByteArray can refuse a save whose stored voxel indices
        // wouldn't mean the same thing under the current chunk size, rather than silently
        // misinterpreting them.
        public byte[] SaveToByteArray() {
            var entries = new List<ChunkSaveEntry>();

            foreach (var kvp in _chunkDict) {
                var edits = new List<(int, VoxelData)>(kvp.Value.GeoChunkEngine.IGeoEditStore.ReadAllEdits());

                if (edits.Count == 0)
                    continue;

                entries.Add(new ChunkSaveEntry {
                    X = kvp.Key.x, Y = kvp.Key.y, Z = kvp.Key.z, Edits = edits
                });
            }

            ref var config = ref ConfigBlob.Value;
            var chunkSize = config.ChunkSize;

            return Packer.BuildPayload((ref Span<byte> buffer) => {
                Packer.WriteInt(ref buffer, SaveFormatVersion);
                Packer.WriteInt(ref buffer, chunkSize);
                Packer.PackList(ref buffer, entries);
            });
        }

        // Restores edits from a payload written by SaveToByteArray. Refuses to load (returns
        // false, logs) on a format-version or chunk-size mismatch, rather than risking silently
        // misinterpreted voxel indices.
        //
        // Applies edits in batches sized to the Edit pool's actual capacity (GetEditPoolCapacity),
        // NOT one giant batch across every saved chunk - a save file can easily touch more chunks
        // than a single realtime terraform action ever could (the Edit pool is sized around
        // terraform's apron fan-out, e.g. 8 for a fully 3D volume, not around "how many chunks a
        // save might contain"). Each restored chunk's IGeoEditStore.Write ultimately checks out an
        // Edit-pool slot (via ApplyVoxelEdits(isEdit: true)), so a single unbounded batch here would
        // hit the pool's exhaustion throw partway through a large load. Each batch is still wrapped
        // in GeoForgeManager.BeginEditBatch/EndEditBatch - same pattern DistributeVoxelEdits and the
        // job-based terraform commands use - so chunks within one batch still get their march jobs
        // scheduled together instead of completing one at a time.
        //
        // A saved coordinate with no loaded chunk (GetChunkByCoord returns null) logs a warning and
        // is skipped rather than failing the whole load - most likely means this was called before
        // every chunk finished spawning (see AllChunksReady()).
        public bool TryLoadFromByteArray(byte[] data) {
            if (data == null || data.Length == 0) {
                Debug.LogWarning("GeoVolume.TryLoadFromByteArray: empty payload.");

                return false;
            }
            
            _meshedChunks.Clear();

            ReadOnlySpan<byte> span = data;
            var version = Packer.ReadInt(ref span);

            if (version != SaveFormatVersion) {
                Debug.LogError(
                    $"GeoVolume.TryLoadFromByteArray: save format version mismatch ({version} vs " +
                    $"{SaveFormatVersion}) - refusing to load.");

                return false;
            }

            var savedChunkSize = Packer.ReadInt(ref span);
            ref var config = ref ConfigBlob.Value;

            if (savedChunkSize != config.ChunkSize) {
                Debug.LogError(
                    $"GeoVolume.TryLoadFromByteArray: chunk size mismatch (save={savedChunkSize}, " +
                    $"volume={config.ChunkSize}) - refusing to load, saved indices would be misinterpreted.");

                return false;
            }

            var entries = Packer.UnpackList<ChunkSaveEntry>(ref span);

            if (!SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var gfManager)) {
                Debug.LogError("GeoVolume.TryLoadFromByteArray: GeoForgeManager not found.");

                return false;
            }

            var editPoolCapacity = gfManager.GetEditPoolCapacity(config.ChunkSize);

            if (editPoolCapacity <= 0) {
                Debug.LogError(
                    "GeoVolume.TryLoadFromByteArray: Edit pool capacity is 0 for this chunk size - " +
                    "was RegisterVoxelVolume ever called for this volume? Refusing to load.");

                return false;
            }

            for (var i = 0; i < entries.Count; i += editPoolCapacity) {
                var batchSize = Mathf.Min(editPoolCapacity, entries.Count - i);

                gfManager.BeginEditBatch();

                try {
                    for (var j = i; j < i + batchSize; j++) {
                        var entry = entries[j];
                        var coord = new Vector3Int(entry.X, entry.Y, entry.Z);
                        var chunk = GetChunkByCoord(coord);

                        if (chunk == null) {
                            Debug.LogWarning(
                                $"GeoVolume.TryLoadFromByteArray: no chunk loaded at {coord} - skipping " +
                                "its saved edits. Was this called before AllChunksReady()?");

                            continue;
                        }

                        chunk.GeoChunkEngine.IGeoEditStore.Write(entry.Edits);
                    }
                }
                finally {
                    gfManager.EndEditBatch();
                }
            }

            return true;
        }

        public bool RegisterChunk(Vector3Int chunkCoord, IGeoChunk geoChunk) {
            if (geoChunk == null)
                return false;

            if (_chunkDict.TryAdd(chunkCoord, geoChunk)) {
                return true;
            }
                

            return false;
        }

        public T CreateChunk<T, TStore>(Vector3Int chunkCoord, GameObject chunkPrefab, TStore store) 
            where T : class, IGeoChunk
            where TStore : IGeoEditStore{
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

            chunk.InitializeGeoChunk(chunkCoord, store);

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
                chunk.GeoChunkEngine.Dispose();

                // Destroy (deferred to end-of-frame) instead of DestroyImmediate: DestroyImmediate
                // synchronously re-enters SimpleGeoChunk.OnDestroy() -> _geoChunk.Dispose() right
                // here in the middle of this loop, which is the risky ordering this exit criterion
                // calls out. GeoChunk.Dispose() is now idempotent by design (see its own guard),
                // so whichever order the deferred OnDestroy fires in, it's safe either way.
                Object.Destroy(chunk.GeoChunkEngine.Transform.gameObject);
            }
            
            if (SingletonManager.TryGetSingletonInstance<GeoForgeManager>(out var mcManager)) {
                mcManager.UnRegisterVoxelVolume(_ownerAsIGeoVolume);
            }
            
            if (ConfigBlob.IsCreated)
                ConfigBlob.Dispose();
        }
    }
}
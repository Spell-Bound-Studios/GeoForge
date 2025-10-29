// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using McHelper = Spellbound.MarchingCubes.McStaticHelper;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Manager for handling the LODs and cached Dense/Unpacked Voxel Arrays for Marching Cubes.
    /// </summary>
    public class MarchingCubesManager : MonoBehaviour {
        public BlobAssetReference<McTablesBlobAsset> McTablesBlob;
        [SerializeField] public GameObject octreePrefab;
        private readonly Stack<GameObject> _objectPool = new();
        private bool _isActive;
        private Dictionary<int, List<Vector3Int>> _sharedIndicesLookup = new();
        
        public bool IsActive() => _isActive;
        private bool _isShuttingDown;
        private Transform _objectPoolParent;
        [Range(300f, 1000f), SerializeField] public float viewDistance = 350;

        [SerializeField] private bool useColliders = true;
        public bool UseColliders => useColliders;

        //This MUST have a length of MaxLevelOfDetail + 1
        [SerializeField] public Vector2[] lodRanges = {
            new(0, 80),
            new(60, 120),
            new(120, 250),
            new(200, 350)
        };

        private const int MaxEntries = 10;

        private readonly NativeArray<VoxelData>[] _denseBuffers = new NativeArray<VoxelData>[MaxEntries];
        private readonly Dictionary<Vector3Int, int> _keyToSlot = new();
        private readonly Queue<(int, IVoxelTerrainChunk)> _slotEvictionQueue = new();
        private readonly Vector3Int[] _slotToKey = new Vector3Int[MaxEntries];

        public event Action OctreeBatchTransitionUpdate;

        private void Awake() {
            SingletonManager.RegisterSingleton(this);
            McTablesBlob = McTablesBlobCreator.CreateMcTablesBlobAsset();
            AllocateDenseBuffers(McHelper.ChunkDataVolumeSize);
            _objectPoolParent = new GameObject("OctreeLeafPool").transform;
            _objectPoolParent.SetParent(transform);
            InitializeSharedIndicesLookup();
        }

        private void LateUpdate() => OctreeBatchTransitionUpdate?.Invoke();

        private void OnValidate() {
            lodRanges = new Vector2[McStaticHelper.MaxLevelOfDetail + 1];

            for (var i = 0; i < lodRanges.Length; i++) {
                var div = Mathf.Pow(2, lodRanges.Length - 1 - i);

                if (i == 0) {
                    lodRanges[i] = new Vector2(0, Mathf.Clamp(viewDistance, 0, viewDistance / div));

                    continue;
                }

                lodRanges[i] = new Vector2(lodRanges[i - 1].y, Mathf.Clamp(viewDistance, 0, viewDistance / div));
            }
        }

        private void OnDestroy() {
            _isShuttingDown = true;

            if (McTablesBlob.IsCreated)
                McTablesBlob.Dispose();

            ClearPool();
            DisposeDenseBuffers();
        }

        public GameObject GetPooledObject(Transform parent) {
            GameObject go;

            if (_objectPool.Count > 0) {
                go = _objectPool.Pop();
                go.SetActive(true);
            }
            else
                go = Instantiate(octreePrefab);

            go.transform.SetParent(parent, false);

            return go;
        }

        public void ReleasePooledObject(GameObject go) {
            if (go == null) return;

            go.SetActive(false);

            if (_objectPoolParent != null && !_isShuttingDown)
                go.transform.SetParent(_objectPoolParent);
            else
                go.transform.SetParent(null); // Detach to avoid parenting to destroyed object
            _objectPool.Push(go);
        }

        private void ClearPool() {
            while (_objectPool.Count > 0) Destroy(_objectPool.Pop());
        }

        private void AllocateDenseBuffers(int arraySize) {
            for (var i = 0; i < MaxEntries; i++)
                _denseBuffers[i] = new NativeArray<VoxelData>(arraySize, Allocator.Persistent);
        }

        public NativeArray<VoxelData> GetOrCreate(
            Vector3Int coord, IVoxelTerrainChunk chunk,
            NativeList<SparseVoxelData> sparseData) {
            if (_keyToSlot.TryGetValue(coord, out var existingSlot)) return _denseBuffers[existingSlot];

            var slot = _keyToSlot.Count < MaxEntries ? _keyToSlot.Count : EvictDenseBuffer();

            var buffer = _denseBuffers[slot];

            var densityRangeArray = new NativeArray<DensityRange>(1, Allocator.TempJob);
            densityRangeArray[0] = new DensityRange(byte.MaxValue, byte.MinValue);

            var unpackJob = new SparseToDenseVoxelDataJob {
                Voxels = buffer,
                SparseVoxels = sparseData,
                DensityRange = densityRangeArray
            };
            var jobHandle = unpackJob.Schedule(McHelper.ChunkDataWidthSize, 1);
            jobHandle.Complete();
            chunk.SetDensityRange(unpackJob.DensityRange[0]);
            unpackJob.DensityRange.Dispose();
            _keyToSlot[coord] = slot;
            _slotToKey[slot] = coord;
            _slotEvictionQueue.Enqueue((slot, chunk));

            return buffer;
        }

        private int EvictDenseBuffer() {
            var indexAndChunkTuple = _slotEvictionQueue.Dequeue();
            var oldKey = _slotToKey[indexAndChunkTuple.Item1];
            _keyToSlot.Remove(oldKey);

            if (indexAndChunkTuple.Item2 == null || !indexAndChunkTuple.Item2.IsDirty())
                return indexAndChunkTuple.Item1;

            var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);

            var packJob = new DenseToSparseVoxelDataJob {
                Voxels = _denseBuffers[indexAndChunkTuple.Item1],
                SparseVoxels = sparseData
            };
            var jobHandle = packJob.Schedule();
            jobHandle.Complete();

            indexAndChunkTuple.Item2.UpdateVoxelData(sparseData);
            sparseData.Dispose();

            return indexAndChunkTuple.Item1;
        }

        private void DisposeDenseBuffers() {
            for (var i = 0; i < MaxEntries; i++) {
                if (_denseBuffers[i].IsCreated)
                    _denseBuffers[i].Dispose();
            }

            _keyToSlot.Clear();
            _slotEvictionQueue.Clear();
        }
        
        
        /// <summary>
        /// Expected to run on server only.
        /// Maps "raw" (world space) voxel edit to Chunks and Lists of local changes in each chunk.
        /// This is required because there's data overlap between the chunks. 
        /// </summary>
        public void DistributeVoxelEdits(
            List<RawVoxelEdit> rawVoxelEdits, HashSet<MaterialType> removableMatTypes) {
            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelEdit>>();

            var chunkManager = GetComponent<IVoxelTerrainChunkManager>();

            foreach (var rawEdit in rawVoxelEdits) {
                var centralCoord = SpellboundStaticHelper.WorldToChunk(rawEdit.WorldPosition);
                var centralLocalPos = rawEdit.WorldPosition - centralCoord * SpellboundStaticHelper.ChunkSize;
                var index = McStaticHelper.Coord3DToIndex(centralLocalPos.x, centralLocalPos.y, centralLocalPos.z);
                
                if (!editsByChunkCoord.TryGetValue(centralCoord, out var localEdits)) {
                    localEdits = new List<VoxelEdit>();
                    editsByChunkCoord[centralCoord] = localEdits;
                }
                
                var chunk = chunkManager.GetChunkByCoord(centralCoord);

                if (chunk == null)
                    continue;

                var existingVoxel = chunk.GetVoxelData(index);

                if (!removableMatTypes.Contains(existingVoxel.MaterialType) &&
                    rawEdit.DensityChange < 0) continue;

                var newDensity = (byte)Mathf.Clamp(existingVoxel.Density + rawEdit.DensityChange, 0, 255);
                    
                var mat = existingVoxel.Density > rawEdit.DensityChange 
                        ? existingVoxel.MaterialType :  rawEdit.NewMatIndex;
                    
                var localEdit = new VoxelEdit(index, newDensity, mat);
                localEdits.Add(localEdit);
                
                if (_sharedIndicesLookup.TryGetValue(index, out var neighborCoords)) {
                    foreach (var neighborCoord in neighborCoords) {
                        var neighborLocalPos = rawEdit.WorldPosition - (centralCoord +  neighborCoord) * SpellboundStaticHelper.ChunkSize;
                        var neighborIndex = McStaticHelper.Coord3DToIndex(neighborLocalPos.x, neighborLocalPos.y, neighborLocalPos.z);
                        var trueNeighborCoord = neighborCoord + centralCoord;
                        if (!editsByChunkCoord.TryGetValue(trueNeighborCoord, out var localNeighborEdits)) {
                            localNeighborEdits = new List<VoxelEdit>();
                            editsByChunkCoord[trueNeighborCoord] = localNeighborEdits;
                        }
                        var localNeighborEdit = new VoxelEdit(neighborIndex, newDensity, mat);
                        localNeighborEdits.Add(localNeighborEdit);
                    }
                }
            }

            foreach (var kvp in editsByChunkCoord) {
                var chunk = chunkManager.GetChunkByCoord(kvp.Key);

                if (chunk == null) {
                    continue;
                }
                    
                chunk.AddToVoxelEdits(kvp.Value);
            }
        }
        
        public VoxelData QueryVoxel(Vector3 position) {
            var chunkManager = GetComponent<IVoxelTerrainChunkManager>();
            if (chunkManager == null)
                return new VoxelData();

            var chunk = chunkManager.GetChunkByPosition(position);

            if (chunk == null) {
                return new VoxelData();
            }

            var positionRounded = new Vector3Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                Mathf.RoundToInt(position.z));

            if (!chunk.HasVoxelData())
                return new VoxelData();

            return chunk.GetVoxelData(positionRounded);
        }

        private void InitializeSharedIndicesLookup() {
            List<Vector3Int> neighborCoords = new();

            for (var dx = -1; dx <= 1; dx++) {
                for (var dy = -1; dy <= 1; dy++) {
                    for (var dz = -1; dz <= 1; dz++) {
                        var coordDelta = new Vector3Int(dx, dy, dz);
                        if (coordDelta == Vector3Int.zero)
                            continue;
                        
                        neighborCoords.Add(new Vector3Int(dx, dy, dz));
                    }
                }
            }
            
            var chunkBounds = new BoundsInt(
                0,
                0,
                0,
                SpellboundStaticHelper.ChunkSize + 3,
                SpellboundStaticHelper.ChunkSize + 3,
                SpellboundStaticHelper.ChunkSize + 3
            );

            for (var i = 0; i < McHelper.ChunkDataVolumeSize; i++) {
                McStaticHelper.IndexToInt3(i, out var x, out var y, out var z);
                var localPos = new Vector3Int(x, y, z);

                foreach (var coord in neighborCoords) {
                    var localPosNeighbor = localPos -  coord * SpellboundStaticHelper.ChunkSize;

                    if (!chunkBounds.Contains(localPosNeighbor)) 
                        continue;

                    if (!_sharedIndicesLookup.TryGetValue(i, out var coordsSharingIndex)) {
                        coordsSharingIndex = new List<Vector3Int>();
                        _sharedIndicesLookup[i] = coordsSharingIndex;
                    }
                    coordsSharingIndex.Add(coord);
                }
            }
        }
        
        /// <summary>
        /// Expected to run on server only.
        /// Maps "raw" (world space) voxel edit to Chunks and Lists of local changes in each chunk.
        /// This is required because there's data overlap between the chunks. 
        /// </summary>
        public void DistributeVoxelEditsOld(
            List<RawVoxelEdit> rawVoxelEdits, HashSet<MaterialType> removableMatTypes) {
            var chunkBounds = new BoundsInt(
                0,
                0,
                0,
                SpellboundStaticHelper.ChunkSize + 3,
                SpellboundStaticHelper.ChunkSize + 3,
                SpellboundStaticHelper.ChunkSize + 3
            );

            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelEdit>>();

            var chunkManager = GetComponent<IVoxelTerrainChunkManager>();

            foreach (var rawEdit in rawVoxelEdits) {
                var affectedChunksByCoord = McStaticHelper.GetChunksTouchingPosition(rawEdit.WorldPosition);

                foreach (var coord in affectedChunksByCoord) {
                    var localPos = rawEdit.WorldPosition - coord * SpellboundStaticHelper.ChunkSize;

                    if (!chunkBounds.Contains(localPos))
                        continue;

                    var index = McStaticHelper.Coord3DToIndex(localPos.x, localPos.y, localPos.z);

                    if (!editsByChunkCoord.TryGetValue(coord, out var localEdits)) {
                        localEdits = new List<VoxelEdit>();
                        editsByChunkCoord[coord] = localEdits;
                    }

                    var chunk = chunkManager.GetChunkByCoord(coord);

                    if (chunk == null) continue;

                    var existingVoxel = chunk.GetVoxelData(index);

                    if (!removableMatTypes.Contains(existingVoxel.MaterialType) &&
                        rawEdit.DensityChange < 0) continue;

                    var newDensity = (byte)Mathf.Clamp(existingVoxel.Density + rawEdit.DensityChange, 0, 255);
                    
                    var mat = existingVoxel.Density > rawEdit.DensityChange 
                            ? existingVoxel.MaterialType :  rawEdit.NewMatIndex;
                    
                    var localEdit = new VoxelEdit(index, newDensity, mat);
                    localEdits.Add(localEdit);
                }
            }

            foreach (var kvp in editsByChunkCoord) {
                chunkManager.GetChunkByCoord(kvp.Key).AddToVoxelEdits(kvp.Value);
            }
        }
        
    }
}
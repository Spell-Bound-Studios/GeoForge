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
        }

        private void Update() => OctreeBatchTransitionUpdate?.Invoke();

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

            var unpackJob = new SparseToDenseVoxelDataJob {
                Voxels = buffer,
                SparseVoxels = sparseData
            };
            var jobHandle = unpackJob.Schedule(McHelper.ChunkDataWidthSize, 1);
            jobHandle.Complete();

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

            indexAndChunkTuple.Item2.UpdateSparseVoxels(sparseData);
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
    }
}
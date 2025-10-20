// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Spellbound.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using McHelper = Spellbound.MarchingCubes.McStaticHelper;

namespace Spellbound.MarchingCubes {
    public class MarchingCubesManager : MonoBehaviour {
        public BlobAssetReference<MCTablesBlobAsset> McTablesBlob;
        [SerializeField] public GameObject octreePrefab;

        [Range(300f, 1000f), SerializeField] public float viewDistance = 350;

        //This MUST have a length of MaxLevelOfDetail + 1
        [SerializeField] public Vector2[] lodRanges = {
            new(0, 80),
            new(60, 120),
            new(120, 250),
            new(200, 350)
        };


        private const int MaxEntries = 10;
        
        private NativeArray<VoxelData>[] _denseBuffers = new NativeArray<VoxelData>[MaxEntries];
        private Dictionary<Vector3Int, int> _keyToSlot = new();
        private Queue<(int, IVoxelTerrainChunk)> _slotEvictionQueue = new();
        private Vector3Int[] _slotToKey = new Vector3Int[MaxEntries];

        public void AllocateDenseBuffers(int arraySize) {
            for (var i = 0; i < MaxEntries; i++) {
                _denseBuffers[i] = new NativeArray<VoxelData>(arraySize, Allocator.Persistent);
            }
        }
        
        public NativeArray<VoxelData> GetOrCreate(Vector3Int coord, IVoxelTerrainChunk chunk, NativeList<SparseVoxelData> sparseData) {
            if (_keyToSlot.TryGetValue(coord, out int existingSlot)) {
                return _denseBuffers[existingSlot];
            }

            int slot;
            if (_keyToSlot.Count < MaxEntries) {
                slot = _keyToSlot.Count;
            } else {
                slot = EvictDenseBuffer();
            }

            // Reuse existing buffer
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

        public void PreloadDenseData(Vector3Int coord, IVoxelTerrainChunk chunk, NativeArray<VoxelData> denseData) {
            if (_keyToSlot.TryGetValue(coord, out int existingSlot)) {
                _denseBuffers[existingSlot].CopyFrom(denseData);
                return;
            }

            int slot;
            if (_keyToSlot.Count >= MaxEntries) {
                slot = EvictDenseBuffer();
            }
            else {
                slot = _keyToSlot.Count;
            }
            _denseBuffers[slot].CopyFrom(denseData);
            _keyToSlot[coord] = slot;
            _slotToKey[slot] = coord;
            _slotEvictionQueue.Enqueue((slot, chunk));
        }

        private int EvictDenseBuffer() {
            
            // Evict the oldest
            var tuple = _slotEvictionQueue.Dequeue();
            var oldKey = _slotToKey[tuple.Item1];
            _keyToSlot.Remove(oldKey);

            if (tuple.Item2 == null) {
                return tuple.Item1;
            }
            
            var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);
            var packJob = new DenseToSparseVoxelDataJob {
                Voxels = _denseBuffers[tuple.Item1],
                SparseVoxels = sparseData
            };
            var jobHandle = packJob.Schedule();
            jobHandle.Complete();
            
            tuple.Item2.UpdateSparseVoxels(sparseData);
            sparseData.Dispose();
            return tuple.Item1;
          
        }

        public void DisposeDenseBuffers() {
            for (var i = 0; i < MaxEntries; i++) {
                if (_denseBuffers[i].IsCreated) {
                    _denseBuffers[i].Dispose();
                }
            }
            _keyToSlot.Clear();
            _slotEvictionQueue.Clear();
        }
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        

        private void Awake() {
            SingletonManager.RegisterSingleton(this);
            McTablesBlob = MCTablesBlobCreator.CreateMCTablesBlobAsset();
            AllocateDenseBuffers(McHelper.ChunkDataVolumeSize);

        }
        

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
            if (McTablesBlob.IsCreated)
                McTablesBlob.Dispose();

            DisposeDenseBuffers();

        }
    }
}
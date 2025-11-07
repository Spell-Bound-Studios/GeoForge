using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class VoxelArrayLruCache {
        private readonly int _capacity;
        private readonly int _voxelArraySize;
        private readonly Dictionary<Vector3Int, LinkedListNode<CacheEntry>> _cache;
        private readonly LinkedList<CacheEntry> _lruList;
        private readonly List<NativeArray<VoxelData>> _allAllocatedArrays; // Track all arrays for disposal

        private class CacheEntry {
            public Vector3Int ChunkCoord;
            public NativeArray<VoxelData> VoxelArray;
        }

        public VoxelArrayLruCache(int capacity, int voxelArraySize) {
            _capacity = capacity;
            _voxelArraySize = voxelArraySize;
            _cache = new Dictionary<Vector3Int, LinkedListNode<CacheEntry>>(capacity);
            _lruList = new LinkedList<CacheEntry>();
            _allAllocatedArrays = new List<NativeArray<VoxelData>>(capacity);
            
            // Pre-allocate ALL NativeArrays and create empty cache entries
            for (int i = 0; i < capacity; i++) {
                var array = new NativeArray<VoxelData>(voxelArraySize, Allocator.Persistent);
                _allAllocatedArrays.Add(array);
                
                var entry = new CacheEntry { 
                    ChunkCoord = new Vector3Int(int.MinValue, int.MinValue, int.MinValue), // Invalid sentinel
                    VoxelArray = array 
                };
                _lruList.AddLast(entry);
            }
        }

        public bool TryGet(Vector3Int chunkCoord, out NativeArray<VoxelData> voxelArray) {
            if (_cache.TryGetValue(chunkCoord, out var node)) {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                voxelArray = node.Value.VoxelArray;
                return true;
            }

            voxelArray = default;
            return false;
        }

        public void Put(Vector3Int chunkCoord, NativeArray<VoxelData> sourceArray) {
            if (_cache.TryGetValue(chunkCoord, out var existingNode)) {
                // Copy data into existing array and move to front
                NativeArray<VoxelData>.Copy(sourceArray, existingNode.Value.VoxelArray);
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // Take the LRU node (always exists since we pre-allocated)
            var lruNode = _lruList.Last;
            _lruList.RemoveLast();
            
            // Remove old mapping if this node was in use
            if (lruNode.Value.ChunkCoord.x != int.MinValue) {
                _cache.Remove(lruNode.Value.ChunkCoord);
            }
            
            // Reuse the node's pre-allocated array
            NativeArray<VoxelData>.Copy(sourceArray, lruNode.Value.VoxelArray);
            lruNode.Value.ChunkCoord = chunkCoord;
            
            // Add to front and update cache
            _lruList.AddFirst(lruNode);
            _cache[chunkCoord] = lruNode;
        }

        public void Clear() {
            // Just clear the mappings, keep arrays allocated
            _cache.Clear();
            
            // Reset all entries to invalid state
            var node = _lruList.First;
            while (node != null) {
                node.Value.ChunkCoord = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                node = node.Next;
            }
        }
        
        public void Dispose() {
            // Dispose all pre-allocated arrays
            foreach (var array in _allAllocatedArrays) {
                if (array.IsCreated) {
                    array.Dispose();
                }
            }
            
            _allAllocatedArrays.Clear();
            _cache.Clear();
            _lruList.Clear();
        }
        
        ~VoxelArrayLruCache() {
            Dispose();
        }
    }
}
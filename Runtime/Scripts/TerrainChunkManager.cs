// Copyright 2025 Spellbound Studio Inc.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class TerrainChunkManager : MonoBehaviour, IVoxelTerrainChunkManager {
        private Dictionary<Vector3Int, IVoxelTerrainChunk> _chunkDict = new();
        [SerializeField] private GameObject _chunkPrefab;

        [SerializeField] private Vector3Int cubeSizeInChunks = new(3, 1, 3);

        private NativeList<SparseVoxelData> _dummyData;

        public IVoxelTerrainChunk GetChunkByPosition(Vector3 position) {
            var coord = McStaticHelper.WorldToChunk(position, SingletonManager.GetSingletonInstance<MarchingCubesManager>().McConfigBlob.Value.ChunkSize);

            return GetChunkByCoord(coord);
        }

        public IVoxelTerrainChunk GetChunkByCoord(Vector3Int coord) => _chunkDict.GetValueOrDefault(coord);

        private void Awake() => SingletonManager.RegisterSingleton<IVoxelTerrainChunkManager>(this);

        private void Start() {
            GenerateSimpleData();
            StartCoroutine(Initialize());
            StartCoroutine(ValidateChunkLods());
        }

        private void OnDestroy() {
            if (_dummyData.IsCreated) _dummyData.Dispose();
        }

        public void GenerateSimpleData() {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");
                return;
            }
            ref var config = ref mcManager.McConfigBlob.Value;
                
            _dummyData = new NativeList<SparseVoxelData>(Allocator.Persistent);
            var emptyVoxel = new VoxelData(byte.MinValue, MaterialType.Dirt);
            var fullVoxel = new VoxelData(byte.MaxValue, MaterialType.Dirt);
            _dummyData.Add(new SparseVoxelData(new VoxelData(byte.MaxValue, MaterialType.Ice), 0));

            _dummyData.Add(new SparseVoxelData(new VoxelData(byte.MaxValue, MaterialType.Sand),
                config.ChunkDataAreaSize * 30));

            _dummyData.Add(new SparseVoxelData(new VoxelData(byte.MaxValue, MaterialType.Swamp),
                config.ChunkDataAreaSize * 60));

            _dummyData.Add(new SparseVoxelData(new VoxelData(byte.MaxValue, MaterialType.Dirt),
                config.ChunkDataAreaSize * 90));

            _dummyData.Add(new SparseVoxelData(new VoxelData(byte.MinValue, MaterialType.Dirt),
                config.ChunkDataAreaSize * 120));
        }

        IEnumerator Initialize() {
            for (var x = 0; x < cubeSizeInChunks.x; x++) {
                for (var y = 0; y < cubeSizeInChunks.y; y++) {
                    for (var z = 0; z < cubeSizeInChunks.z; z++) {
                        var chunk = RegisterChunk(new Vector3Int(x, y, z));
                        chunk.InitializeVoxelData(_dummyData);
                        yield return null;
                    }
                }
            }
        }

        private IVoxelTerrainChunk RegisterChunk(Vector3Int chunkCoord) {
            var newChunk = CreateNChunk(chunkCoord);
            _chunkDict[chunkCoord] = newChunk;
            //OnChunkCountChanged?.Invoke(ClientChunkDict.Count);

            return newChunk;
        }

        private IVoxelTerrainChunk CreateNChunk(Vector3Int chunkCoord) {
            var chunkObj = Instantiate(
                _chunkPrefab,
                chunkCoord * SpellboundStaticHelper.ChunkSize,
                Quaternion.identity,
                transform
            );

            if (!chunkObj.TryGetComponent(out IVoxelTerrainChunk chunk)) return null;

            chunk.SetChunkFields(chunkCoord);

            return chunk;
        }

        private IEnumerator ValidateChunkLods() {
            while (true) {
                var count = 0;
                var chunkList = new List<Vector3Int>(_chunkDict.Keys.ToList());

                foreach (var coord in chunkList) {
                    if (!_chunkDict.TryGetValue(coord, out var chunk))
                        continue;

                    if (!chunk.HasVoxelData())
                        continue;

                    if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager))
                        continue;

                    chunk.ValidateOctreeLods(Camera.main.transform.position);
                    mcManager.CompleteAndApplyMarchingCubesJobs();

                    count++;

                    if (count >= 1) {
                        count = 0;

                        yield return null;
                    }
                }

                yield return null;
            }
        }
    }
}
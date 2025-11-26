// Copyright 2025 Spellbound Studio Inc.

using System.Collections;
using System.Collections.Generic;
using Spellbound.Core;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class SampleVolume : MonoBehaviour, IVolume {
        [SerializeField] private GameObject _chunkPrefab;

        [SerializeField] private Vector3Int volumeSizeInChunks = new(3, 1, 3);

        private NativeList<SparseVoxelData> _data;
        private VoxVolume _voxVolume;
        
        [SerializeField] private List<BoundaryOverrides> _boundaryConditions;

        public VoxVolume VoxelVolume => _voxVolume;

        private void Awake() => _voxVolume = new VoxVolume(this, _chunkPrefab);

        private void Start() {
            if (SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager))
                mcManager.RegisterVoxelVolume(this, mcManager.McConfigBlob.Value.ChunkDataVolumeSize);
            else {
                Debug.LogError("MarchingCubesManager is null.");

                return;
            }

            ManageVolume();
        }

        public void ManageVolume() {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");

                return;
            }
            ref var config = ref mcManager.McConfigBlob.Value;
            GenerateSimpleData();
            StartCoroutine(Initialize(config.ChunkSize));
            VoxelVolume.SetLodTarget(Camera.main.transform);
            StartCoroutine(VoxelVolume.ValidateChunkLods());
        }

        private void OnDestroy() {
            if (_data.IsCreated)
                _data.Dispose();

            if (SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager))
                mcManager.UnregisterVoxelVolume(this);
        }

        private void Update() => VoxelVolume.UpdateVolumeOrigin();

        public void GenerateSimpleData() {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");

                return;
            }

            ref var config = ref mcManager.McConfigBlob.Value;

            var halfChunk = config.ChunkSize / 2;

            _data = new NativeList<SparseVoxelData>(Allocator.Persistent);
            _data.Add(new SparseVoxelData(new VoxelData(byte.MaxValue, MaterialType.Sand), 0));
            //_data.Add(new SparseVoxelData(new VoxelData(byte.MaxValue, MaterialType.Dirt), halfChunk * config.ChunkSize));
        }

        public IEnumerator Initialize(int chunkSize) {
            var offset = new Vector3Int(
                volumeSizeInChunks.x / 2,
                volumeSizeInChunks.y / 2,
                volumeSizeInChunks.z / 2
            );

            for (var x = 0; x < volumeSizeInChunks.x; x++) {
                for (var y = 0; y < volumeSizeInChunks.y; y++) {
                    for (var z = 0; z < volumeSizeInChunks.z; z++) {
                        var chunkCoord = new Vector3Int(x, y, z) - offset;
                        var chunk = VoxelVolume.RegisterChunk(chunkCoord);

                        var chunkBoundaryOverrides = new Dictionary<(BoundaryOverrides.Axis, int), VoxelData>();
                        foreach (var boundaryCondition in _boundaryConditions) {
                            if (boundaryCondition.axis == BoundaryOverrides.Axis.X) {
                                if (x == 0 && boundaryCondition.boundaryDirection ==
                                    BoundaryOverrides.BoundaryDirection.Min) {
                                    chunkBoundaryOverrides[(boundaryCondition.axis, 1)] = boundaryCondition.VoxelData;
                                }
                                else if (x == volumeSizeInChunks.x - 1 && boundaryCondition.boundaryDirection ==
                                         BoundaryOverrides.BoundaryDirection.Max) {
                                    chunkBoundaryOverrides[(boundaryCondition.axis, chunkSize + 1)] = boundaryCondition.VoxelData;
                                }
                            }
                            if (boundaryCondition.axis == BoundaryOverrides.Axis.Y) {
                                if (y == 0 && boundaryCondition.boundaryDirection ==
                                    BoundaryOverrides.BoundaryDirection.Min) {
                                    chunkBoundaryOverrides[(boundaryCondition.axis, 1)] = boundaryCondition.VoxelData;
                                }
                                else if (y == volumeSizeInChunks.y - 1 && boundaryCondition.boundaryDirection ==
                                         BoundaryOverrides.BoundaryDirection.Max) {
                                    chunkBoundaryOverrides[(boundaryCondition.axis, chunkSize + 1)] = boundaryCondition.VoxelData;
                                }
                            }
                            if (boundaryCondition.axis == BoundaryOverrides.Axis.Z) {
                                if (z == 0 && boundaryCondition.boundaryDirection ==
                                    BoundaryOverrides.BoundaryDirection.Min) {
                                    chunkBoundaryOverrides[(boundaryCondition.axis, 1)] = boundaryCondition.VoxelData;
                                }
                                else if (z == volumeSizeInChunks.z - 1 && boundaryCondition.boundaryDirection ==
                                         BoundaryOverrides.BoundaryDirection.Max) {
                                    chunkBoundaryOverrides[(boundaryCondition.axis, chunkSize + 1)] = boundaryCondition.VoxelData;
                                }
                            }
                        }
                        chunk.VoxelChunk.SetOverrides(chunkBoundaryOverrides);
                        chunk.InitializeChunk(_data);

                        yield return null;
                    }
                }
            }
        }
    }
}
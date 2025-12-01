// Copyright 2025 Spellbound Studio Inc.

using Spellbound.Core;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class SampleVolume : MonoBehaviour, IVolume {
        [SerializeField] private GameObject _chunkPrefab;
        [SerializeField] private VoxelVolumeConfig _config;
        [SerializeField] private Vector2[] _viewDistanceLodRanges;
        private VoxVolume _voxVolume;
        [SerializeField] private BoundaryOverrides _boundaryOverrides;
        public VoxVolume VoxelVolume => _voxVolume;
        public VoxelVolumeConfig Config => _config;
        public Vector2[] ViewDistanceLodRanges => _viewDistanceLodRanges;

        public Transform LodTarget =>
                Camera.main == null ? FindAnyObjectByType<Camera>().transform : Camera.main.transform;

#if UNITY_EDITOR
        private void OnValidate() {
            if (_config == null) {
                _viewDistanceLodRanges = null;

                return;
            }

            _viewDistanceLodRanges = VoxVolume.ValidateLodRanges(_viewDistanceLodRanges, _config);
        }
#endif

        private void Awake() => _voxVolume = new VoxVolume(this, this, _chunkPrefab);

        private void Start() {
            if (SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager))
                mcManager.RegisterVoxelVolume(this, VoxelVolume.ConfigBlob.Value.ChunkSize);
            else {
                Debug.LogError("MarchingCubesManager is null.");

                return;
            }
            
            ManageVolume();
        }

        public void ManageVolume() {
            ref var config = ref VoxelVolume.ConfigBlob.Value;
            var sizeInChunks = config.SizeInChunks;
            var chunkSize = config.ChunkSize;
            StartCoroutine(VoxelVolume.InitializeChunks(
                GenerateChunkData,
                coord => _boundaryOverrides.BuildChunkOverrides(coord, sizeInChunks, chunkSize)
            ));
        }
        
        private void Update() => VoxelVolume.UpdateVolumeOrigin();

        public void GenerateChunkData(Vector3Int chunkCoord, NativeArray<VoxelData> data) {
            
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");

                return;
            }

            var sandIndex = mcManager.materialDatabase.GetMaterialIndex("Sand");
            var sandVoxel = new VoxelData(byte.MaxValue, sandIndex);

            for (var i = 0; i < data.Length; ++i) {
                data[i] = sandVoxel;
            }
        }

        void OnDestroy() {
            _voxVolume?.Dispose();
        }
    }
}
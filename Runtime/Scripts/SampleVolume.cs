// Copyright 2025 Spellbound Studio Inc.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class SampleVolume : MonoBehaviour, IVolume {
        [SerializeField] private GameObject _chunkPrefab;
        [SerializeField] private VoxelVolumeConfig _config;
        [SerializeField] private Vector2[] _viewDistanceLodRanges;

        private NativeList<SparseVoxelData> _data;
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

            var bounds = CalculateVolumeBounds();
            VoxelVolume.Bounds = bounds;
            ManageVolume();
        }

        public void ManageVolume() {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");

                return;
            }

            ref var config = ref VoxelVolume.ConfigBlob.Value;
            GenerateSimpleData();
            StartCoroutine(Initialize(config.ChunkSize));
            StartCoroutine(VoxelVolume.ValidateChunkLods());
        }

        private void OnDestroy() {
            if (_data.IsCreated)
                _data.Dispose();
        }

        private void Update() => VoxelVolume.UpdateVolumeOrigin();

        public void GenerateSimpleData() {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");

                return;
            }

            ref var config = ref VoxelVolume.ConfigBlob.Value;

            var sandIndex = mcManager.materialDatabase.GetMaterialIndex("Sand");

            _data = new NativeList<SparseVoxelData>(Allocator.Persistent);
            _data.Add(new SparseVoxelData(new VoxelData(byte.MaxValue, sandIndex), 0));
        }

        public IEnumerator Initialize(int chunkSize) {
            var size = VoxelVolume.ConfigBlob.Value.SizeInChunks;

            var offset = new Vector3Int(
                size.x / 2,
                size.y / 2,
                size.z / 2
            );

            for (var x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    for (var z = 0; z < size.z; z++) {
                        var chunkCoord = new Vector3Int(x, y, z) - offset;
                        var chunk = VoxelVolume.RegisterChunk(chunkCoord);

                        var chunkOverrides = BuildChunkOverrides(x, y, z, chunkSize);

                        var tupleSeq = chunkOverrides.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value)
                        );

                        if (chunkOverrides.Count > 0) chunk.VoxelChunk.SetOverrides(tupleSeq);

                        chunk.InitializeChunk(_data);

                        yield return null;
                    }
                }
            }
        }

        private Dictionary<(Axis, int), VoxelData> BuildChunkOverrides(
            int x, int y, int z, int chunkSize) {
            var overrides = new Dictionary<(Axis, int), VoxelData>();

            var slices = new List<int>();

            foreach (var boundary in _boundaryOverrides.GetBoundaryOverrides()) {
                slices.Clear();

                switch (boundary.Axis) {
                    case Axis.X:
                        if (x == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }

                        else if (x == _config.sizeInChunks.x - 1 && boundary.Side == Side.Max) {
                            slices.Add(chunkSize + 1);
                            slices.Add(chunkSize + 2);
                        }

                        break;

                    case Axis.Y:
                        if (y == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }

                        else if (y == _config.sizeInChunks.y - 1 && boundary.Side == Side.Max) {
                            slices.Add(chunkSize + 1);
                            slices.Add(chunkSize + 2);
                        }

                        break;

                    case Axis.Z:
                        if (z == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }

                        else if (z == _config.sizeInChunks.z - 1 && boundary.Side == Side.Max) {
                            slices.Add(chunkSize + 1);
                            slices.Add(chunkSize + 2);
                        }

                        break;
                }

                foreach (var slice in slices) overrides[(boundary.Axis, slice)] = boundary.VoxelData;
            }

            return overrides;
        }

        private BoundsInt CalculateVolumeBounds() {
            if (!SingletonManager.TryGetSingletonInstance<MarchingCubesManager>(out var mcManager)) {
                Debug.LogError("MarchingCubesManager not found");

                return new BoundsInt();
            }

            ref var config = ref VoxelVolume.ConfigBlob.Value;

            // Calculate total size in voxels
            var sizeInVoxels = new Vector3Int(
                _config.sizeInChunks.x * config.ChunkSize,
                _config.sizeInChunks.y * config.ChunkSize,
                _config.sizeInChunks.z * config.ChunkSize
            );

            // Calculate center offset (since chunks are centered around origin)
            var offset = new Vector3Int(
                _config.sizeInChunks.x / 2,
                _config.sizeInChunks.y / 2,
                _config.sizeInChunks.z / 2
            );

            var centerInVoxels = new Vector3Int(
                -offset.x * config.ChunkSize + sizeInVoxels.x / 2,
                -offset.y * config.ChunkSize + sizeInVoxels.y / 2,
                -offset.z * config.ChunkSize + sizeInVoxels.z / 2
            );

            // Create bounds centered at the calculated center
            return new BoundsInt(
                centerInVoxels.x - sizeInVoxels.x / 2,
                centerInVoxels.y - sizeInVoxels.y / 2,
                centerInVoxels.z - sizeInVoxels.z / 2,
                sizeInVoxels.x,
                sizeInVoxels.y,
                sizeInVoxels.z
            );
        }
    }
}
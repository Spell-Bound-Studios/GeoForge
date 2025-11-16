// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Manager for handling the LODs and cached Dense/Unpacked Voxel Arrays for Marching Cubes.
    /// </summary>
    public partial class MarchingCubesManager : MonoBehaviour {
        public BlobAssetReference<McTablesBlobAsset> McTablesBlob { get; private set; }
        [SerializeField] private TerrainConfig _terrainConfig;
        public BlobAssetReference<McConfigBlobAsset> McConfigBlob { get; private set; }
        public BlobAssetReference<McChunkInterpolationBlobAsset> McChunkInterpolationBlob { get; private set; }
        [SerializeField] public GameObject octreePrefab;
        private readonly Stack<GameObject> _objectPool = new();
        private bool _isActive;
        private Dictionary<int, List<Vector3Int>> _sharedIndicesLookup = new();

        public bool IsActive() => _isActive;
        private bool _isShuttingDown;
        private Transform _objectPoolParent;

        [SerializeField] private bool useColliders = true;
        public bool UseColliders => useColliders;

        public event Action OctreeBatchTransitionUpdate;

        private void Awake() {
            
            Application.targetFrameRate = 60;   // Cap at 60 FPS
            
            if (_terrainConfig == null) {
                Debug.LogError("Marching Cubes TerrainConfig is null");

                return;
            }

            SingletonManager.RegisterSingleton(this);
            McTablesBlob = McTablesBlobCreator.CreateMcTablesBlobAsset();
            McConfigBlob = McConfigBlobCreator.CreateMcConfigBlobAsset(_terrainConfig);

            McChunkInterpolationBlob =
                    McChunkInterpolationBlobCreator.CreateMcChunkInterpolationBlobAsset(_terrainConfig);
            AllocateDenseBuffer(McConfigBlob.Value.ChunkDataVolumeSize);
            _objectPoolParent = new GameObject("OctreeLeafPool").transform;
            _objectPoolParent.SetParent(transform);
            InitializeSharedIndicesLookup();
        }

        private void LateUpdate() => OctreeBatchTransitionUpdate?.Invoke();

        private void OnDestroy() {
            _isShuttingDown = true;

            if (McTablesBlob.IsCreated)
                McTablesBlob.Dispose();

            if (McConfigBlob.IsCreated)
                McConfigBlob.Dispose();

            if (McChunkInterpolationBlob.IsCreated)
                McChunkInterpolationBlob.Dispose();

            ClearPool();
            DisposeDenseBuffer();
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
                go.transform.SetParent(null);
            _objectPool.Push(go);
        }

        private void ClearPool() {
            while (_objectPool.Count > 0) Destroy(_objectPool.Pop());
        }

        /// <summary>
        /// Expected to run on server only.
        /// Maps "raw" (world space) voxel edit to Chunks and Lists of local changes in each chunk.
        /// This is required because there's data overlap between the chunks. 
        /// </summary>
        public void DistributeVoxelEdits(
            List<RawVoxelEdit> rawVoxelEdits, HashSet<MaterialType> removableMatTypes = null) {
            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelEdit>>();

            var chunkManager = GetComponent<IVoxelTerrainChunkManager>();

            ref var config = ref McConfigBlob.Value;

            foreach (var rawEdit in rawVoxelEdits) {
                var centralCoord = McStaticHelper.WorldToChunk(rawEdit.WorldPosition, config.ChunkSize);
                var centralLocalPos = rawEdit.WorldPosition - centralCoord * McConfigBlob.Value.ChunkSize;

                var index = McStaticHelper.Coord3DToIndex(centralLocalPos.x, centralLocalPos.y, centralLocalPos.z,
                    config.ChunkDataAreaSize, config.ChunkDataWidthSize);

                var chunk = chunkManager.GetChunkByCoord(centralCoord);

                if (chunk == null)
                    continue;

                if (!editsByChunkCoord.TryGetValue(centralCoord, out var localEdits)) {
                    localEdits = new List<VoxelEdit>();
                    editsByChunkCoord[centralCoord] = localEdits;
                }

                var existingVoxel = chunk.GetVoxelData(index);

                if (rawEdit.DensityChange < 0 && !removableMatTypes.Contains(existingVoxel.MaterialType)) continue;

                var newDensity = (byte)Mathf.Clamp(existingVoxel.Density + rawEdit.DensityChange, 0, 255);

                var mat = math.abs(rawEdit.DensityChange) - existingVoxel.Density <= 0
                        ? existingVoxel.MaterialType
                        : rawEdit.NewMatIndex;

                var localEdit = new VoxelEdit(index, newDensity, mat);
                localEdits.Add(localEdit);

                if (_sharedIndicesLookup.TryGetValue(index, out var neighborCoords)) {
                    foreach (var neighborCoord in neighborCoords) {
                        var trueNeighborCoord = neighborCoord + centralCoord;
                        var neighborLocalPos = rawEdit.WorldPosition - trueNeighborCoord * config.ChunkSize;

                        var neighborIndex = McStaticHelper.Coord3DToIndex(neighborLocalPos.x, neighborLocalPos.y,
                            neighborLocalPos.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);

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

                if (chunk == null) continue;

                chunk.AddToVoxelEdits(kvp.Value);
            }
        }

        public VoxelData QueryVoxel(Vector3 position) {
            var chunkManager = GetComponent<IVoxelTerrainChunkManager>();

            if (chunkManager == null)
                return new VoxelData();

            var chunk = chunkManager.GetChunkByPosition(position);

            if (chunk == null) return new VoxelData();

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

            ref var config = ref McConfigBlob.Value;

            var chunkBounds = new BoundsInt(
                0,
                0,
                0,
                config.ChunkSize + 3,
                config.ChunkSize + 3,
                config.ChunkSize + 3
            );

            for (var i = 0; i < config.ChunkDataVolumeSize; i++) {
                McStaticHelper.IndexToInt3(i, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x, out var y,
                    out var z);
                var localPos = new Vector3Int(x, y, z);

                foreach (var coord in neighborCoords) {
                    var localPosNeighbor = localPos - coord * config.ChunkSize;

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
    }
}
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
    [DefaultExecutionOrder(-1000)]
    public partial class MarchingCubesManager : MonoBehaviour {
        public BlobAssetReference<McTablesBlobAsset> McTablesBlob { get; private set; }
        [SerializeField] private TerrainConfig _terrainConfig;
        public BlobAssetReference<McConfigBlobAsset> McConfigBlob { get; private set; }
        public BlobAssetReference<McChunkInterpolationBlobAsset> McChunkInterpolationBlob { get; private set; }
        [SerializeField] public GameObject octreePrefab;
        [SerializeField] public VoxelMaterialDatabase materialDatabase;
        private Material _runtimeVoxelMaterial;
        
        private readonly Stack<GameObject> _objectPool = new();
        private bool _isActive;
        private HashSet<IVolume> _voxelVolumes = new();
        private Dictionary<int, List<Vector3Int>> _sharedIndicesLookup = new();

        public bool IsActive() => _isActive;
        private bool _isShuttingDown;
        private Transform _objectPoolParent;

        [SerializeField] private bool useColliders = true;
        public bool UseColliders => useColliders;

        public event Action OctreeBatchTransitionUpdate;

        private void Awake() {
            if (_terrainConfig == null) {
                Debug.LogError("Marching Cubes TerrainConfig is null");

                return;
            }

            SingletonManager.RegisterSingleton(this);
            McTablesBlob = McTablesBlobCreator.CreateMcTablesBlobAsset();
            McConfigBlob = McConfigBlobCreator.CreateMcConfigBlobAsset(_terrainConfig);

            McChunkInterpolationBlob =
                    McChunkInterpolationBlobCreator.CreateMcChunkInterpolationBlobAsset(_terrainConfig);
            _objectPoolParent = new GameObject("OctreeLeafPool").transform;
            _objectPoolParent.SetParent(transform);
            InitializeSharedIndicesLookup();
            InitializeVoxelMaterial();
        }
        
        private void InitializeVoxelMaterial() {
            if (octreePrefab == null) {
                Debug.LogError("Octree prefab not assigned!");
                return;
            }
    
            var renderer = octreePrefab.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) {
                Debug.LogError("Octree prefab has no MeshRenderer or Material!");
                return;
            }
    
            // Create runtime instance from prefab's material
            _runtimeVoxelMaterial = new Material(renderer.sharedMaterial);
    
            // Apply texture arrays from database
            if (materialDatabase != null) {
                if (materialDatabase.albedoTextureArray != null && materialDatabase.masTextureArray != null) {
                    _runtimeVoxelMaterial.SetTexture("_TerrainAlbedoArray", materialDatabase.albedoTextureArray);
                    _runtimeVoxelMaterial.SetTexture("_TerrainMetalSmoothArray", materialDatabase.masTextureArray);
                } else {
                    Debug.LogError("Texture arrays not built! Use 'Build Texture Arrays' on VoxelMaterialDatabase.");
                }
            } else {
                Debug.LogError("No VoxelMaterialDatabase assigned!");
            }
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

            foreach (var kvp in _denseVoxelDataDict) {
                kvp.Value.Dispose();    
            }
        }

        public void RegisterVoxelVolume(IVolume volume, int dataSize) {
            _voxelVolumes.Add(volume);

            if (!_denseVoxelDataDict.ContainsKey(dataSize)) { 
                _denseVoxelDataDict.Add(dataSize, new DenseVoxelData(dataSize));
            }
        } 
        
        public GameObject GetPooledObject(Transform parent) {
            GameObject go;

            if (_objectPool.Count > 0) {
                go = _objectPool.Pop();
                go.SetActive(true);
            }
            else {
                go = Instantiate(octreePrefab);
        
                // Apply the runtime material to new instances
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) {
                    renderer.sharedMaterial = _runtimeVoxelMaterial;
                }
            }

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

        public void ExecuteTerraform(
            Func<IVolume, (List<RawVoxelEdit> edits, Bounds bounds)> terraformAction,
            HashSet<byte> removableMatTypes = null,
            IVolume targetVolume = null) {
    
            if (targetVolume != null) {
                var result = terraformAction(targetVolume);
                DistributeVoxelEdits(targetVolume, result.edits, removableMatTypes);
                return;
            }

            foreach (var voxelVolume in _voxelVolumes) {
                var result = terraformAction(voxelVolume);
                if (!voxelVolume.VoxelVolume.IntersectsVolume(result.bounds)) 
                    continue;
                
                DistributeVoxelEdits(voxelVolume, result.edits, removableMatTypes);
            }
        }

        /// <summary>
        /// Expected to run on server only.
        /// Maps "raw" (world space) voxel edit to Chunks and Lists of local changes in each chunk.
        /// This is required because there's data overlap between the chunks. 
        /// </summary>
        public void DistributeVoxelEdits(
            IVolume volume, List<RawVoxelEdit> rawVoxelEdits, HashSet<byte> removableMatTypes = null) {
            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelEdit>>();

            ref var config = ref McConfigBlob.Value;

            foreach (var rawEdit in rawVoxelEdits) {
                var centralCoord = volume.VoxelVolume.GetCoordByVoxelPosition(rawEdit.WorldPosition);
                var centralLocalPos = rawEdit.WorldPosition - centralCoord * McConfigBlob.Value.ChunkSize;

                var index = McStaticHelper.Coord3DToIndex(centralLocalPos.x, centralLocalPos.y, centralLocalPos.z,
                    config.ChunkDataAreaSize, config.ChunkDataWidthSize);

                var chunk = volume.VoxelVolume.GetChunkByCoord(centralCoord);

                if (chunk == null)
                    continue;

                if (!editsByChunkCoord.TryGetValue(centralCoord, out var localEdits)) {
                    localEdits = new List<VoxelEdit>();
                    editsByChunkCoord[centralCoord] = localEdits;
                }

                var existingVoxel = chunk.VoxelChunk.GetVoxelData(index);

                if (rawEdit.DensityChange < 0 && removableMatTypes != null &&
                    !removableMatTypes.Contains(existingVoxel.MaterialIndex)) continue;

                var newDensity = (byte)Mathf.Clamp(existingVoxel.Density + rawEdit.DensityChange, 0, 255);

                var mat = math.abs(rawEdit.DensityChange) - existingVoxel.Density <= 0
                        ? existingVoxel.MaterialIndex
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
                var chunk = volume.VoxelVolume.GetChunkByCoord(kvp.Key);

                if (chunk == null)
                    continue;

                chunk.PassVoxelEdits(kvp.Value);
            }
        }

        public VoxelData QueryVoxel(Vector3 position) {
            foreach (var voxelVolume in _voxelVolumes) {
                if (!voxelVolume.VoxelVolume.IsPrimaryTerrain)
                    continue;
                var chunk = voxelVolume.VoxelVolume.GetChunkByWorldPosition(position);

                if (chunk == null)
                    continue;

                if (!chunk.VoxelChunk.HasVoxelData())
                    continue;

                var voxelPosition = voxelVolume.VoxelVolume.WorldToVoxelSpace(position);
                var voxel = chunk.VoxelChunk.GetVoxelDataFromVoxelPosition(voxelPosition);

                return voxel;
            }

            return new VoxelData();
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
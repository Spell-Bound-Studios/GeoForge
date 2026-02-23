// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Manager for handling the LODs and cached Dense/Unpacked Voxel Arrays for Marching Cubes.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public partial class GeoForgeManager : MonoBehaviour {
        public BlobAssetReference<McTablesBlobAsset> McTablesBlob { get; private set; }

        [SerializeField] public GameObject octreePrefab;
        [SerializeField] public VoxelMaterialDatabase materialDatabase;
        private Material _runtimeVoxelMaterial;

        private readonly Stack<GameObject> _objectPool = new();
        private bool _isActive;
        private HashSet<IVolume> _voxelVolumes = new();

        public bool IsActive() => _isActive;
        private bool _isShuttingDown;
        private Transform _objectPoolParent;

        private HashSet<byte> _allMaterials;
        [SerializeField] private byte defaultMaterial;

        public HashSet<byte> GetAllMaterials() {
            if (_allMaterials == null) {
                _allMaterials = new HashSet<byte>();
                for (var i = 0; i < materialDatabase.materials.Count; ++i) _allMaterials.Add((byte)i);
            }

            return _allMaterials;
        }

        public byte GetDefaultMaterial() => defaultMaterial;

        public event Action OctreeBatchTransitionUpdate;

        private void Awake() {
            SingletonManager.RegisterSingleton(this);
            McTablesBlob = McTablesBlobCreator.CreateMcTablesBlobAsset();
            _objectPoolParent = new GameObject("OctreeLeafPool").transform;
            _objectPoolParent.SetParent(transform);
            InitializeVoxelMaterial();
            ValidateAllVolumesLodsAsync();
        }

        public async void ValidateAllVolumesLodsAsync() {
            try {
                while (true) {
                    foreach (var volume in _voxelVolumes) await volume.ValidateChunkLods();

                    await Awaitable.NextFrameAsync();
                }
            }
            finally {
                Debug.Log("ValidateAllVolumesLodsAsync stopped");
            }
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
                }
                else
                    Debug.LogError("Texture arrays not built! Use 'Build Texture Arrays' on VoxelMaterialDatabase.");
            }
            else
                Debug.LogError("No VoxelMaterialDatabase assigned!");
        }

        private void LateUpdate() => OctreeBatchTransitionUpdate?.Invoke();

        private void OnDestroy() {
            _isShuttingDown = true;

            if (McTablesBlob.IsCreated)
                McTablesBlob.Dispose();

            ClearPool();

            foreach (var kvp in _denseVoxelDataDict) kvp.Value.Dispose();
        }

        public void RegisterVoxelVolume(IVolume volume) {
            _voxelVolumes.Add(volume);
            var chunkSize = volume.ConfigBlob.Value.ChunkSize;

            if (!_denseVoxelDataDict.ContainsKey(chunkSize)) {
                var denseData = new DenseVoxelData(chunkSize);
                _denseVoxelDataDict.Add(chunkSize, denseData);
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
                if (renderer != null) renderer.sharedMaterial = _runtimeVoxelMaterial;
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

        /// <summary>
        /// For Terraforming Commands that might affect multiple volumes.
        /// </summary>
        public void ExecuteTerraformAll(
            Func<IVolume, (List<RawVoxelEdit> edits, Bounds bounds)> terraformAction) {
            foreach (var iVolume in _voxelVolumes) {
                var result = terraformAction(iVolume);

                if (!iVolume.IntersectsVolume(result.bounds))
                    continue;

                DistributeVoxelEdits(iVolume, result.edits);
            }
        }

        /// <summary>
        /// Expected to run on server only.
        /// Maps "raw" (world space) voxel edit to Chunks and Lists of local changes in each chunk.
        /// This is required because there's data overlap between the chunks. 
        /// </summary>
        public void DistributeVoxelEdits(
            IVolume volume, List<RawVoxelEdit> rawVoxelEdits) {
            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelEdit>>();

            ref var config = ref volume.ConfigBlob.Value;

            foreach (var rawEdit in rawVoxelEdits) {
                var centralCoord = volume.GetCoordByVoxelPosition(rawEdit.voxelSpacePosition);
                var centralLocalPos = rawEdit.voxelSpacePosition - centralCoord * config.ChunkSize;

                var index = GfStaticHelper.Coord3DToIndex(centralLocalPos.x, centralLocalPos.y, centralLocalPos.z,
                    config.ChunkDataAreaSize, config.ChunkDataWidthSize);

                var chunk = volume.GetChunkByCoord(centralCoord);

                if (chunk == null)
                    continue;

                if (!_denseVoxelDataDict.TryGetValue(volume.ConfigBlob.Value.ChunkSize,
                        out var denseVoxelData))
                    return;

                if (!editsByChunkCoord.TryGetValue(centralCoord, out var localEdits)) {
                    localEdits = new List<VoxelEdit>();
                    editsByChunkCoord[centralCoord] = localEdits;
                }

                var localEdit = new VoxelEdit(index, rawEdit.NewDensity, rawEdit.NewMatIndex);
                localEdits.Add(localEdit);

                if (denseVoxelData.SharedIndicesAcrossChunks.TryGetValue(index, out var neighborCoords)) {
                    foreach (var neighborCoord in neighborCoords) {
                        var trueNeighborCoord = neighborCoord + centralCoord;
                        var neighborLocalPos = rawEdit.voxelSpacePosition - trueNeighborCoord * config.ChunkSize;

                        var neighborIndex = GfStaticHelper.Coord3DToIndex(neighborLocalPos.x, neighborLocalPos.y,
                            neighborLocalPos.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);

                        if (!editsByChunkCoord.TryGetValue(trueNeighborCoord, out var localNeighborEdits)) {
                            localNeighborEdits = new List<VoxelEdit>();
                            editsByChunkCoord[trueNeighborCoord] = localNeighborEdits;
                        }

                        var localNeighborEdit = new VoxelEdit(neighborIndex, rawEdit.NewDensity, rawEdit.NewMatIndex);
                        localNeighborEdits.Add(localNeighborEdit);
                    }
                }
            }

            foreach (var kvp in editsByChunkCoord) {
                var chunk = volume.GetChunkByCoord(kvp.Key);

                if (chunk == null)
                    continue;

                chunk.PassVoxelEdits(kvp.Value);
            }
        }

        public VoxelData QueryVoxel(Vector3 position, out IVolume queryvolume) {
            queryvolume = null;

            foreach (var voxelVolume in _voxelVolumes) {
                if (!voxelVolume.IsPrimaryTerrain)
                    continue;

                queryvolume = voxelVolume;

                var chunk = voxelVolume.GetChunkByWorldPosition(position);

                if (chunk == null)
                    continue;

                if (!chunk.HasVoxelData())
                    continue;

                var voxelPosition = voxelVolume.WorldToVoxelSpace(position);
                var voxel = chunk.GetVoxelDataFromVoxelPosition(voxelPosition);

                return voxel;
            }

            return new VoxelData();
        }
    }
}
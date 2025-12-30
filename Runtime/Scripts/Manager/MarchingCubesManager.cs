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

        public HashSet<byte> GetAllMaterials() {
            if (_allMaterials == null) {
                _allMaterials = new HashSet<byte>();
                for (var i = 0; i < materialDatabase.materials.Count; ++i) _allMaterials.Add((byte)i);
            }

            return _allMaterials;
        }

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
        /// Access point for digging/terraforming Marching Cubes Volume(s)
        /// </summary>
        /// <param name="terraformAction"></param> Func to return a List of Edits and a Bounds, from an IVolume Param.
        /// <param name="removableMatTypes"></param> Affected Materials if the Terraform does not affect them all.
        /// <param name="targetVolume"></param> IVolume if the Terraform affects only one. 
        public void ExecuteTerraform(
            Func<IVolume, (List<RawVoxelEdit> edits, Bounds bounds)> terraformAction,
            HashSet<byte> removableMatTypes = null,
            IVolume targetVolume = null) {
            if (targetVolume != null) {
                var result = terraformAction(targetVolume);
                DistributeVoxelEdits(targetVolume, result.edits, removableMatTypes);

                return;
            }

            foreach (var iVolume in _voxelVolumes) {
                var result = terraformAction(iVolume);

                if (!iVolume.IntersectsVolume(result.bounds))
                    continue;

                DistributeVoxelEdits(iVolume, result.edits, removableMatTypes);
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

            ref var config = ref volume.ConfigBlob.Value;

            foreach (var rawEdit in rawVoxelEdits) {
                var centralCoord = volume.GetCoordByVoxelPosition(rawEdit.WorldPosition);
                var centralLocalPos = rawEdit.WorldPosition - centralCoord * config.ChunkSize;

                var index = McStaticHelper.Coord3DToIndex(centralLocalPos.x, centralLocalPos.y, centralLocalPos.z,
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

                var existingVoxel = chunk.GetVoxelData(index);

                if (rawEdit.DensityChange < 0 && removableMatTypes != null &&
                    !removableMatTypes.Contains(existingVoxel.MaterialIndex)) continue;

                var newDensity = (byte)Mathf.Clamp(existingVoxel.Density + rawEdit.DensityChange, 0, 255);

                var mat = math.abs(rawEdit.DensityChange) - existingVoxel.Density <= 0
                        ? existingVoxel.MaterialIndex
                        : rawEdit.NewMatIndex;

                var localEdit = new VoxelEdit(index, newDensity, mat);
                localEdits.Add(localEdit);

                if (denseVoxelData.SharedIndicesAcrossChunks.TryGetValue(index, out var neighborCoords)) {
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
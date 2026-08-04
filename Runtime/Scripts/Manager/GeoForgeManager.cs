// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Tooling;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Manager for handling the LODs and cached Dense/Unpacked Voxel Arrays for Marching Cubes.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public partial class GeoForgeManager : MonoBehaviour {
        public BlobAssetReference<McTablesBlobAsset> McTablesBlob { get; private set; }

        [SerializeField] public GameObject octreePrefab;
        [SerializeField] public VoxelMaterialDatabase materialDatabase;

        private readonly Stack<GameObject> _objectPool = new();
        private bool _isActive;
        private HashSet<IGeoVolume> _voxelVolumes = new();

        public bool IsActive() => _isActive;
        private bool _isShuttingDown;
        private Transform _objectPoolParent;

        private HashSet<byte> _allMaterials;
        public NativeArray<bool> FlatShadedLookUp { get; private set; }

        public event Action OctreeBatchTransitionUpdate;

        private void Awake() {
            SingletonManager.RegisterSingleton(this);
            McTablesBlob = McTablesBlobCreator.CreateMcTablesBlobAsset();
            _objectPoolParent = new GameObject("OctreeLeafPool").transform;
            _objectPoolParent.SetParent(transform);
            ValidateAllVolumesLodsAsync();
            FlatShadedLookUp = new NativeArray<bool>(256, Allocator.Persistent);
            var lookUp = FlatShadedLookUp;
            for (var i = 0; i < materialDatabase.materials.Count; i++)
                lookUp[i] = materialDatabase.materials[i].isFlatShaded;

            _isActive = true;
        }

        public async void ValidateAllVolumesLodsAsync() {
            try {
                while (true) {
                    var volumeList = new List<IGeoVolume>(_voxelVolumes);
                    foreach (var volume in volumeList) {
                        if (volume == null)
                            continue;
                        await volume.ValidateChunkLods();
                    }


                    await Awaitable.NextFrameAsync();
                }
            }
            finally {
                Debug.Log("ValidateAllVolumesLodsAsync stopped");
            }
        }

        private void LateUpdate() => OctreeBatchTransitionUpdate?.Invoke();

        private void OnDestroy() {
            _isActive = false;
            _isShuttingDown = true;

            if (McTablesBlob.IsCreated)
                McTablesBlob.Dispose();

            if (FlatShadedLookUp.IsCreated)
                FlatShadedLookUp.Dispose();

            ClearPool();

            foreach (var kvp in _denseVoxelDataDict) kvp.Value.Dispose();
        }

        public void RegisterVoxelVolume(IGeoVolume geoVolume) {
            _voxelVolumes.Add(geoVolume);
            var chunkSize = geoVolume.ConfigBlob.Value.ChunkSize;

            if (!_denseVoxelDataDict.ContainsKey(chunkSize)) {
                var denseData = new DenseVoxelData(chunkSize);
                _denseVoxelDataDict.Add(chunkSize, denseData);
            }
        }

        public void UnRegisterVoxelVolume(IGeoVolume geoVolume) {
            _voxelVolumes.Remove(geoVolume);
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
                if (renderer != null) renderer.sharedMaterial = jobAndRenderProfile.Material;
            }

            go.transform.SetParent(parent, false);

            if (go.transform.parent == null) Debug.LogError("Pooled object is being provided with no parent");

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
        /// For Terraforming Commands that might affect multiple volumes. materialIndex and
        /// allowedMaterialsMask are shared across every volume the action touches; only the
        /// per-volume edits/bounds come from the terraformAction delegate.
        /// </summary>
        public void ExecuteTerraformAll(
            Func<IGeoVolume, (List<RawVoxelEdit> edits, Bounds bounds)> terraformAction,
            byte materialIndex,
            uint4 allowedMaterialsMask) {
            foreach (var iVolume in _voxelVolumes) {
                var result = terraformAction(iVolume);

                if (!iVolume.IntersectsVolume(result.bounds))
                    continue;

                DistributeVoxelEdits(iVolume, result.edits, materialIndex, allowedMaterialsMask);
            }
        }

        /// <summary>
        /// Expected to run on server only.
        /// Maps "raw" (world space) voxel edits to Chunks and builds one VoxelEditOperation per
        /// affected chunk. materialIndex and allowedMaterialsMask are properties of the whole
        /// terraform action and are copied onto every chunk's operation unchanged; only the
        /// per-chunk Deltas differ.
        /// </summary>
        public void DistributeVoxelEdits(
            IGeoVolume geoVolume,
            List<RawVoxelEdit> rawVoxelEdits,
            byte materialIndex,
            uint4 allowedMaterialsMask) {
            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelDensityDelta>>();

            ref var config = ref geoVolume.ConfigBlob.Value;

            foreach (var rawEdit in rawVoxelEdits) {
                var centralCoord = geoVolume.GetCoordByVoxelPosition(rawEdit.VoxelSpacePosition);
                var centralLocalPos = rawEdit.VoxelSpacePosition - centralCoord * config.ChunkSize;

                var index = GfStaticHelper.Coord3DToIndex(centralLocalPos.x, centralLocalPos.y, centralLocalPos.z,
                    config.ChunkDataAreaSize, config.ChunkDataWidthSize);

                var chunk = geoVolume.GetChunkByCoord(centralCoord);

                if (chunk == null)
                    continue;

                if (!_denseVoxelDataDict.TryGetValue(geoVolume.ConfigBlob.Value.ChunkSize,
                        out var denseVoxelData))
                    return;

                if (!editsByChunkCoord.TryGetValue(centralCoord, out var localEdits)) {
                    localEdits = new List<VoxelDensityDelta>();
                    editsByChunkCoord[centralCoord] = localEdits;
                }

                localEdits.Add(new VoxelDensityDelta(index, rawEdit.DensityDelta));

                if (denseVoxelData.SharedIndicesAcrossChunks.TryGetValue(index, out var neighborCoords)) {
                    foreach (var neighborCoord in neighborCoords) {
                        var trueNeighborCoord = neighborCoord + centralCoord;
                        var neighborLocalPos = rawEdit.VoxelSpacePosition - trueNeighborCoord * config.ChunkSize;

                        var neighborIndex = GfStaticHelper.Coord3DToIndex(neighborLocalPos.x, neighborLocalPos.y,
                            neighborLocalPos.z, config.ChunkDataAreaSize, config.ChunkDataWidthSize);

                        if (!editsByChunkCoord.TryGetValue(trueNeighborCoord, out var localNeighborEdits)) {
                            localNeighborEdits = new List<VoxelDensityDelta>();
                            editsByChunkCoord[trueNeighborCoord] = localNeighborEdits;
                        }

                        localNeighborEdits.Add(new VoxelDensityDelta(neighborIndex, rawEdit.DensityDelta));
                    }
                }
            }

            foreach (var kvp in editsByChunkCoord) {
                var chunk = geoVolume.GetChunkByCoord(kvp.Key);

                if (chunk == null)
                    continue;

                chunk.PassVoxelEdits(new VoxelEditOperation(materialIndex, kvp.Value, allowedMaterialsMask));
            }
        }

        /// <summary>
        /// Tries to query the voxel at a world position from whichever primary-terrain volume
        /// actually has data there. Returns false (with voxel/queryVolume left at their defaults)
        /// if no primary volume exists, or none of them have a loaded chunk with voxel data at
        /// this position - a default VoxelData (Density == 0) is indistinguishable from real solid
        /// terrain under the zero-threshold convention, so callers must not treat a false return
        /// as "voxel is default/empty"; it means "nothing was actually queryable here."
        /// queryVolume is only ever set on a true return, never left pointing at a volume whose
        /// data wasn't actually used.
        /// </summary>
        public bool TryQueryVoxel(Vector3 position, out VoxelData voxel, out IGeoVolume queryVolume) {
            voxel = default;
            queryVolume = null;

            foreach (var voxelVolume in _voxelVolumes) {
                if (!voxelVolume.IsPrimaryTerrain)
                    continue;

                var chunk = voxelVolume.GetChunkByWorldPosition(position);

                if (chunk == null)
                    continue;

                if (!chunk.HasVoxelData())
                    continue;

                var voxelPosition = voxelVolume.WorldToVoxelSpace(position);
                voxel = chunk.GetVoxelDataFromVoxelPosition(voxelPosition);
                queryVolume = voxelVolume;

                return true;
            }

            return false;
        }
    }
}
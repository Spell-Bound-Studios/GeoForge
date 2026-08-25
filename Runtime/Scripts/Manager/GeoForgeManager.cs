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
        // Internal: raw MC lookup tables, only ever consumed by the march jobs via
        // profile.ScheduleMarchingCubes(_gfManager.McTablesBlob, ...) - no external caller needs
        // direct access to the table blob itself.
        internal BlobAssetReference<McTablesBlobAsset> McTablesBlob { get; private set; }

        // Judgment call, leaning internal: no established "pluggable strategy" story for this one
        // the way jobAndRenderProfile has (see GeoForgeManager.JobManager.cs) - it's just which
        // GameObject GetPooledObject instantiates. Worth a deliberate look before narrowing though,
        // in case a consumer legitimately wants to swap this at runtime.
        [SerializeField] internal GameObject octreePrefab;

        // Judgment call, leaning public: plausibly useful for external code/UI to enumerate or
        // look up materials by index, not obviously pure-internal the way octreePrefab is.
        [SerializeField] public VoxelMaterialDatabase materialDatabase;

        private readonly Stack<GameObject> _objectPool = new();
        private bool _isActive;
        private HashSet<IGeoVolume> _voxelVolumes = new();

        // Judgment call, leaning public: reads as a legitimate external status-check API ("is
        // GeoForge ready") rather than internal plumbing, though nothing in what I've seen this
        // session actually calls it - worth confirming it's still wanted at all before deciding
        // its visibility.
        public bool IsActive() => _isActive;

        private bool _isShuttingDown;
        private Transform _objectPoolParent;

        private HashSet<byte> _allMaterials;

        // Internal: purely an internal coordination mechanism between this manager and OctreeNode
        // (HandleTransitionUpdate subscribes/unsubscribes to this) - not a public event story.
        internal event Action OctreeBatchTransitionUpdate;

        private void Awake() {
            SingletonManager.RegisterSingleton(this);
            McTablesBlob = McTablesBlobCreator.CreateMcTablesBlobAsset();
            _objectPoolParent = new GameObject("OctreeLeafPool").transform;
            _objectPoolParent.SetParent(transform);
            ValidateAllVolumesLodsAsync();
            _isActive = true;
        }

        // Internal: called once from Awake() and drives its own infinite validation loop - no
        // legitimate reason for external code to invoke this a second time.
        internal async void ValidateAllVolumesLodsAsync() {
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

            ClearPool();

            DisposeMarchBufferPools();
            foreach (var kvp in _denseVoxelDataDict)
                kvp.Value.Dispose();
        }

        // Left public: IGeoVolume is a public extension point (a custom volume implementation
        // outside this assembly needs to be able to register/unregister itself), so these two
        // can't be narrowed the way the rest of this file was.
        public void RegisterVoxelVolume(IGeoVolume geoVolume) {
            _voxelVolumes.Add(geoVolume);
            var chunkSize = geoVolume.ConfigBlob.Value.ChunkSize;
            var validatesPerFrame = geoVolume.ConfigBlob.Value.ValidatesPerFrame;
            var editPoolSize = ComputeEditPoolSize(geoVolume.ConfigBlob.Value.SizeInChunks);

            if (!_denseVoxelDataDict.TryGetValue(chunkSize, out var pool)) {
                _denseVoxelDataDict.Add(chunkSize, new DenseVoxelDataPool(chunkSize, editPoolSize, validatesPerFrame));

                return;
            }

            // Another volume already registered this chunk size, possibly with smaller
            // requirements. Both pools have to cover the largest requirement among every volume
            // sharing this chunk size, since each volume's edits/LOD validation are independent
            // and any of them could need this much capacity at once.
            pool.EnsureEditCapacity(editPoolSize);
            pool.EnsureValidationCapacity(validatesPerFrame);
        }

        // Edit pool capacity is geometry-derived, not a flat constant: for each axis (x/y/z) where
        // the volume spans more than one chunk, a terraform action can fan out to a neighbor along
        // that axis; the worst case is a corner where all three axes are straddled at once. That
        // gives 2^(axes with more than one chunk) as the max distinct chunks a single terraform
        // action can touch - 8 when the volume is genuinely 3D (matching the old fixed constant),
        // but less for a volume that's only one chunk deep along some axis, since edits can never
        // fan out along an axis that doesn't exist to fan out into.
        private static int ComputeEditPoolSize(Vector3Int sizeInChunks) {
            var axesWithMultipleChunks = 0;

            if (sizeInChunks.x > 1) axesWithMultipleChunks++;
            if (sizeInChunks.y > 1) axesWithMultipleChunks++;
            if (sizeInChunks.z > 1) axesWithMultipleChunks++;

            return 1 << axesWithMultipleChunks;
        }

        public void UnRegisterVoxelVolume(IGeoVolume geoVolume) {
            _voxelVolumes.Remove(geoVolume);
        }

        // Internal: pooling plumbing specifically tied to OctreeNode's leaf/transition GameObject
        // lifecycle (BuildLeaf/BuildTransitions/ReleaseLeafObjects) - not a general-purpose object
        // pool meant for external use.
        internal GameObject GetPooledObject(Transform parent) {
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

        internal void ReleasePooledObject(GameObject go) {
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
        /// Internal: matches TerraformCommands's own established convention ("should be accessed
        /// through the public GeoForgeStatic class") - GeoForgeStatic is the one sanctioned public
        /// entry point for terraform operations, this is the plumbing underneath it.
        /// </summary>
        [Obsolete]
        internal void ExecuteTerraformAll(
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
        /// Internal: same reasoning as ExecuteTerraformAll above - reachable only through
        /// GeoForgeStatic.
        /// </summary>
        [Obsolete]
        internal void DistributeVoxelEdits(
            IGeoVolume geoVolume,
            List<RawVoxelEdit> rawVoxelEdits,
            byte materialIndex,
            uint4 allowedMaterialsMask) {
            var editsByChunkCoord = new Dictionary<Vector3Int, List<VoxelDensityDelta>>();

            // Reused across every edit in this call to avoid a per-edit allocation - filled fresh
            // by GetSharedNeighborDirections each iteration.
            var neighborDirections = new List<Vector3Int>(7);

            ref var config = ref geoVolume.ConfigBlob.Value;

            foreach (var rawEdit in rawVoxelEdits) {
                var centralCoord = geoVolume.GetCoordByVoxelPosition(rawEdit.VoxelSpacePosition);
                var centralLocalPos = rawEdit.VoxelSpacePosition - centralCoord * config.ChunkSize;

                var index = GfStaticHelper.Coord3DToIndex(centralLocalPos.x, centralLocalPos.y, centralLocalPos.z,
                    config.ChunkDataAreaSize, config.ChunkDataWidthSize);

                var chunk = geoVolume.GetChunkByCoord(centralCoord);

                if (chunk == null)
                    continue;

                if (!_denseVoxelDataDict.ContainsKey(geoVolume.ConfigBlob.Value.ChunkSize))
                    return;

                if (!editsByChunkCoord.TryGetValue(centralCoord, out var localEdits)) {
                    localEdits = new List<VoxelDensityDelta>();
                    editsByChunkCoord[centralCoord] = localEdits;
                }

                localEdits.Add(new VoxelDensityDelta(index, rawEdit.DensityDelta));

                // Replaces the old SharedIndicesAcrossChunks dictionary lookup with direct
                // arithmetic - see GfStaticHelper.GetSharedNeighborDirections for the derivation.
                GfStaticHelper.GetSharedNeighborDirections(centralLocalPos, config.ChunkSize, neighborDirections);

                foreach (var neighborCoord in neighborDirections) {
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

            // Batched so every affected chunk's edit is applied and its march jobs scheduled
            // (synchronously, via HandleResolvedVoxelEdits reacting to PassVoxelEdits below) before
            // any of them complete or release - see GeoForgeManager.EditBatch.cs. try/finally
            // guarantees EndEditBatch runs even if a chunk lookup or PassVoxelEdits throws
            // partway through, so IsBatchingEdits can never get stuck true.
            BeginEditBatch();

            try {
                foreach (var kvp in editsByChunkCoord) {
                    var chunk = geoVolume.GetChunkByCoord(kvp.Key);

                    if (chunk == null)
                        continue;

                    chunk.PassVoxelEditOperation(new VoxelEditOperation(materialIndex, kvp.Value, allowedMaterialsMask, Vector3.zero));
                }
            }
            finally {
                EndEditBatch();
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
        /// Judgment call, leaning public: a safe, read-only gameplay query (checking terrain at a
        /// position) rather than lifecycle/pooling machinery - doesn't have the same clear
        /// "route through GeoForgeStatic" signal ExecuteTerraformAll/DistributeVoxelEdits have.
        /// </summary>
        public bool TryQueryVoxel(Vector3 position, out VoxelData voxel, out IGeoVolume queryVolume) {
            voxel = default;
            queryVolume = null;

            foreach (var voxelVolume in _voxelVolumes) {
                if (!voxelVolume.IsPrimaryTerrain)
                    continue;

                if (!voxelVolume.TryQueryVoxel(position, out voxel))
                    continue;

                queryVolume = voxelVolume;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to resolve the surface material at a world position from whichever
        /// primary-terrain volume actually has voxel data there - same
        /// try-each-primary-volume-in-turn shape as TryQueryVoxel above, just delegating to
        /// IGeoVolume.TryQuerySurfaceMaterial (-> GeoVolumeEngine.TryQuerySurfaceMaterial) instead
        /// of a plain single-voxel lookup. See that method for the 8-corner resolution and the
        /// nearest-solid-corner tie-break rule.
        /// Returns false (with materialIndex left at VoxelData.NullSentinelValue and queryVolume
        /// left null) if no primary volume exists, none of them have a loaded chunk with voxel
        /// data at this position, or the resolved cell has no solid corners at all. queryVolume is
        /// only ever set on a true return, never left pointing at a volume whose data wasn't
        /// actually used.
        /// </summary>
        public bool TryQuerySurfaceMaterial(Vector3 position, out byte materialIndex, out IGeoVolume queryVolume) {
            materialIndex = VoxelData.NullSentinelValue;
            queryVolume = null;

            foreach (var voxelVolume in _voxelVolumes) {
                if (!voxelVolume.IsPrimaryTerrain)
                    continue;

                if (!voxelVolume.TryQuerySurfaceMaterial(position, out materialIndex))
                    continue;

                queryVolume = voxelVolume;
                return true;
            }

            return false;
        }
    }
}
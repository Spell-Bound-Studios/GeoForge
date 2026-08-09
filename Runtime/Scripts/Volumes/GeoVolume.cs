// Copyright 2026 Spellbound Studio Inc.

using System.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Basic Implementation of IGeoVolume for a Volume of Finite Size.
    /// Initializes Chunks one per frame until all are initialized.
    /// All other management is the baseline wrappers for GeoVolume.
    /// Note IGeoVolume implementations are NOT virtual. The intent of GeoVolume is to be extendable,
    /// but not in terms of altering it how it implements IGeoVolume. If you want a unique implementation of IGeoVolume,
    /// create a new class instead of inheriting from GeoVolume.
    /// </summary>
    public class GeoVolume : MonoBehaviour, IGeoVolume {
        [field: Tooltip("Preset for what voxel data is generated in the geoVolume"), SerializeField]
        protected DataFactory DataFactory { get; set; }

        [field: Tooltip("Rules for immutable voxels on the external faces of the geoVolume"), SerializeField]
        protected BoundaryOverrides BoundaryOverrides { get; set; }

        [field: Header("Volume Settings"), Tooltip("Config for ChunkSize, VolumeSize, etc"), SerializeField]
        protected VoxelVolumeConfig Config { get; set; }

        [field: Tooltip("Initial State for if the geoVolume is moving. " +
                        "If true it updates the origin of the triplanar material shader"), SerializeField]
        public bool IsMoving { get; set; }

        [field: Tooltip("Initial State for if the geoVolume is the Primary Terrain. " +
                        "Affects whether it can be globally queried or not"), SerializeField]
        public bool IsPrimaryTerrain { get; set; }

        [field: Tooltip("View Distances to each Level of Detail. Enforces a floor to prohibit abrupt changes"), SerializeField]
        public Vector2[] ViewDistanceLodRanges { get; protected set; }

        [field: Tooltip("Prefab for the Chunk the Volume will build itself from. Must Implement IGeoChunk"), SerializeField]
        private GameObject ChunkPrefab { get; set; }

        [field: Tooltip("Optional explicit LOD target (e.g. the player camera). If left unset, falls back " +
                        "to Camera.main, then any Camera found in the scene. Resolved once and cached on " +
                        "first access - not re-evaluated per call."), SerializeField]
        private Transform LodTargetOverride { get; set; }

        private Transform _resolvedLodTarget;

        public GeoVolumeEngine GeoVolumeEngine { get; private set; }

#if UNITY_EDITOR
        /// <summary>
        /// Enforces a floor on view distances to prohibit abrupt changes.
        /// The TransVoxel Algorithm does not handle abrupt changes so they would leave visible seams.
        /// </summary>
        protected virtual void OnValidate() {
            if (Config == null) {
                ViewDistanceLodRanges = null;

                return;
            }

            ViewDistanceLodRanges = GeoVolumeEngine.ValidateLodRanges(ViewDistanceLodRanges, Config);
        }
#endif
        /// <summary>
        /// Chunk Prefab must have a IGeoChunk component.
        /// All IVolumes should create VoxelCoreLogic on Awake.
        /// </summary>
        protected virtual void Awake() {
            if (ChunkPrefab == null || !ChunkPrefab.TryGetComponent<IGeoChunk>(out _)) {
                Debug.LogError($"{name}: _chunkPrefab is null or does not have IGeoChunk Component");
                enabled = false;

                return;
            }

            CreateGeoVolume(Config);
        }

        protected virtual void CreateGeoVolume(VoxelVolumeConfig voxelVolumeConfig) {
            GeoVolumeEngine = new GeoVolumeEngine(this, this, voxelVolumeConfig);
            GeoVolumeEngine.RegisterVolume();
            OnVolumeRegistered();
            StartCoroutine(InitializeChunks());
        }

        public void ResetEditedChunksToProcedural() {
            if (GeoVolumeEngine == null)
                return;

            GeoVolumeEngine.ResetEditedChunksToProcedural(DataFactory);
        }

        void IGeoVolume.HandleAllChunksMeshed() => OnAllChunksMeshed();

        protected virtual void OnAllChunksMeshed() { }


        /// <summary>
        /// Initializes Chunks one per frame, centered on the Volume's transform
        /// One NativeArray of Voxels is maintained for all the chunks and simply overriden with new data.
        /// </summary>
        protected virtual IEnumerator InitializeChunks() {
            var size = GeoVolumeEngine.ConfigBlob.Value.SizeInChunks;
            var offset = new Vector3Int(size.x / 2, size.y / 2, size.z / 2);

            for (var x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    for (var z = 0; z < size.z; z++) {
                        var chunkCoord = new Vector3Int(x, y, z) - offset;
                        var chunk = GeoVolumeEngine.CreateChunk<IGeoChunk, GeoEditStore>(chunkCoord, ChunkPrefab, new GeoEditStore());

                        if (!GeoVolumeEngine.RegisterChunk(chunkCoord, chunk)) {
                            Debug.LogError($"{name}: failed to register chunk at {chunkCoord} - duplicate coord?");
                            Destroy(chunk.GeoChunkEngine.Transform.gameObject);

                            yield return null;

                            continue;
                        }

                        chunk.SetBoundaryOverrides(chunkCoord, GeoVolumeEngine.ConfigBlob, BoundaryOverrides);
                        chunk.SetVoxels(chunkCoord, GeoVolumeEngine.ConfigBlob, DataFactory);

                        yield return null;
                    }
                }
            }

            OnChunksInitialized();
        }

        /// <summary>
        /// Called once GeoVolumeEngine has been created and registered with GeoForgeManager, but
        /// before any chunks exist yet - the earliest point external systems can safely read
        /// GeoVolumeEngine/ConfigBlob/bounds off this volume.
        /// </summary>
        protected virtual void OnVolumeRegistered() { }

        /// <summary>
        /// Called once every chunk in the volume has been created, registered, had its boundary
        /// overrides applied, and had its initial voxel data set - i.e. once
        /// GeoVolumeEngine.AllChunksReady() is guaranteed true. Safe point to call
        /// TryLoadFromByteArray.
        /// </summary>
        protected virtual void OnChunksInitialized() { }

        /// <summary>
        /// Marching Cubes meshes utilize a triplanar shader. In order for textures to "stick to" their gemometry
        /// as the geoVolume moves, the geoVolume origin must be updated. This is costly so should be avoided for volumes
        /// that reliably will not move.
        /// </summary>
        protected virtual void Update() {
            if (!IsMoving)
                return;

            GeoVolumeEngine.UpdateVolumeOrigin();
        }

        /// <summary>
        /// This must be done on ALL IGeoVolume implementers to prevent memory leaks.
        /// </summary>
        protected virtual void OnDestroy() => GeoVolumeEngine?.Dispose();

        public Transform VolumeTransform => transform;

        /// <summary>
        /// Resolved once (lodTargetOverride, then Camera.main, then any Camera in the scene) and
        /// cached - no repeated scene search per call, and no NullReferenceException if nothing
        /// resolves (returns null and logs instead). Once resolved, later calls return the cached
        /// Transform directly regardless of any changes to Camera.main afterward.
        /// </summary>
        public Transform LodTarget {
            get {
                if (_resolvedLodTarget != null)
                    return _resolvedLodTarget;

                if (LodTargetOverride != null) {
                    _resolvedLodTarget = LodTargetOverride;

                    return _resolvedLodTarget;
                }

                if (Camera.main != null) {
                    _resolvedLodTarget = Camera.main.transform;

                    return _resolvedLodTarget;
                }

                var fallbackCamera = FindAnyObjectByType<Camera>();

                if (fallbackCamera != null) {
                    _resolvedLodTarget = fallbackCamera.transform;

                    return _resolvedLodTarget;
                }

                Debug.LogError(
                    $"{name}: LodTarget could not resolve - no lodTargetOverride assigned and no Camera found in the scene.");

                return null;
            }
        }
    }
}
// Copyright 2026 Spellbound Studio Inc.

using System.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Basic Implementation of IGeoVolume for a Volume of Finite Size.
    /// Initializes Chunks one per frame until all are initialized.
    /// All other management is the baseline wrappers for GeoVolume.
    /// Note IGeoVolume implementations are NOT virtual. The intent of SimpleGeoVolume is to be extendable,
    /// but not in terms of altering it how it implements IGeoVolume. If you want a unique implementation of IGeoVolume,
    /// create a new class instead of inheriting from SimpleGeoVolume.
    /// </summary>
    public class SimpleGeoVolume : MonoBehaviour, IGeoVolume {
        [Tooltip("Preset for what voxel data is generated in the geoVolume"), SerializeField]
        protected DataFactory dataFactory;

        [Tooltip("Rules for immutable voxels on the external faces of the geoVolume"), SerializeField]
        protected BoundaryOverrides boundaryOverrides;

        [Header("Volume Settings"), Tooltip("Config for ChunkSize, VolumeSize, etc"), SerializeField]
        protected VoxelVolumeConfig config;

        [Tooltip("Initial State for if the geoVolume is moving. " +
                 "If true it updates the origin of the triplanar material shader"), SerializeField]
        protected bool isMoving = false;

        [Tooltip("Initial State for if the geoVolume is the Primary Terrain. " +
                 "Affects whether it can be globally queried or not"), SerializeField]
        protected bool isPrimaryTerrain = false;

        [Tooltip("View Distances to each Level of Detail. Enforces a floor to prohibit abrupt changes"), SerializeField]
        protected Vector2[] viewDistanceLodRanges;

        [Tooltip("Prefab for the Chunk the Volume will build itself from. Must Implement IGeoChunk"), SerializeField]
        private GameObject chunkPrefab;

        [Tooltip("Optional explicit LOD target (e.g. the player camera). If left unset, falls back " +
                 "to Camera.main, then any Camera found in the scene. Resolved once and cached on " +
                 "first access - not re-evaluated per call."), SerializeField]
        private Transform lodTargetOverride;

        private Transform _resolvedLodTarget;

        private GeoVolume _geoVolume;

        public GeoVolume GeoVolume => _geoVolume;

#if UNITY_EDITOR
        /// <summary>
        /// Enforces a floor on view distances to prohibit abrupt changes.
        /// The TransVoxel Algorithm does not handle abrupt changes so they would leave visible seams.
        /// </summary>
        protected virtual void OnValidate() {
            if (config == null) {
                viewDistanceLodRanges = null;

                return;
            }

            viewDistanceLodRanges = GeoVolume.ValidateLodRanges(viewDistanceLodRanges, config);
        }
#endif
        /// <summary>
        /// Chunk Prefab must have a IGeoChunk component.
        /// All IVolumes should create VoxelCoreLogic on Awake.
        /// </summary>
        protected virtual void Awake() {
            if (chunkPrefab == null || !chunkPrefab.TryGetComponent<IGeoChunk>(out _)) {
                Debug.LogError($"{name}: _chunkPrefab is null or does not have IGeoChunk Component");

                return;
            }

            CreateGeoVolume(config);
        }

        protected virtual void CreateGeoVolume(VoxelVolumeConfig voxelVolumeConfig) {
            _geoVolume = new GeoVolume(this, this, voxelVolumeConfig);
            _geoVolume.RegisterVolume();
            StartCoroutine(InitializeChunks());
        }
        

        /// <summary>
        /// Initializes Chunks one per frame, centered on the Volume's transform
        /// One NativeArray of Voxels is maintained for all the chunks and simply overriden with new data.
        /// </summary>
        protected virtual IEnumerator InitializeChunks() {
            var size = _geoVolume.ConfigBlob.Value.SizeInChunks;
            var offset = new Vector3Int(size.x / 2, size.y / 2, size.z / 2);

            for (var x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    for (var z = 0; z < size.z; z++) {
                        var chunkCoord = new Vector3Int(x, y, z) - offset;
                        var chunk = _geoVolume.CreateChunk<IGeoChunk>(chunkCoord, chunkPrefab);

                        if (chunk is SimpleGeoChunk simpleChunk) {
                            simpleChunk.SetDataFactory(dataFactory);
                            simpleChunk.SetBoundaryOverrides(boundaryOverrides);
                        }

                        chunk.ActivateGeoChunk();

                        yield return null;
                    }
                }
            }
        }

        /// <summary>
        /// Marching Cubes meshes utilize a triplanar shader. In order for textures to "stick to" their gemometry
        /// as the geoVolume moves, the geoVolume origin must be updated. This is costly so should be avoided for volumes
        /// that reliably will not move.
        /// </summary>
        protected virtual void Update() {
            if (!isMoving)
                return;

            _geoVolume.UpdateVolumeOrigin();
        }

        /// <summary>
        /// This must be done on ALL IGeoVolume implementers to prevent memory leaks.
        /// </summary>
        protected virtual void OnDestroy() => _geoVolume?.Dispose();

        // IGeoVolume implementations
        public Vector2[] ViewDistanceLodRanges => viewDistanceLodRanges;

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

                if (lodTargetOverride != null) {
                    _resolvedLodTarget = lodTargetOverride;

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

        public bool IsMoving {
            get => isMoving;
            set => isMoving = value;
        }

        public bool IsPrimaryTerrain {
            get => isPrimaryTerrain;
            set => isPrimaryTerrain = value;
        }
    }
}
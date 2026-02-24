// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections;
using PurrNet;
using Spellbound.GeoForge;
using UnityEngine;

namespace GeoForge.Sample4 {
    public class NetworkedVolume : NetworkIdentity, IVolume {
        [Header("Volume Settings"), Tooltip("Config for ChunkSize, VolumeSize, etc"), SerializeField]
        protected VoxelVolumeConfig config;

        [Tooltip("Initial State for if the volume is moving. " +
                 "If true it updates the origin of the triplanar material shader"), SerializeField]
        protected bool isMoving;

        [Tooltip("Initial State for if the volume is the Primary Terrain. " +
                 "Affects whether it can be globally queried or not"), SerializeField]
        protected bool isPrimaryTerrain;

        [Tooltip("View Distances to each Level of Detail. Enforces a floor to prohibit abrupt changes"), SerializeField]
        protected Vector2[] viewDistanceLodRanges;

        [Tooltip("Prefab for the Chunk the Volume will build itself from. Must Implement IChunk"), SerializeField]
        private GameObject chunkPrefab;

        private BaseVolume _baseVolume;

        public BaseVolume BaseVolume => _baseVolume;

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

            viewDistanceLodRanges = BaseVolume.ValidateLodRanges(viewDistanceLodRanges, config);
        }
#endif

        /// <summary>
        /// Chunk Prefab must have a IChunk component.
        /// All IVolumes should create VoxelCoreLogic on Awake.
        /// </summary>
        protected override void OnEarlySpawn() {
            if (chunkPrefab == null || !chunkPrefab.TryGetComponent<IChunk>(out _)) {
                Debug.LogError($"{name}: _chunkPrefab is null or does not have IChunk Component", this);

                return;
            }

            _baseVolume = new BaseVolume(this, this, config);
        }

        protected override void OnSpawned() {
            BaseVolume.RegisterVolume();
            
            if (!isServer)
                return;
            
            InitializeVolume();
        }

        public virtual void InitializeVolume() {
            StartCoroutine(InitializeChunks());
        }

        /// <summary>
        /// Initializes Chunks one per frame, centered on the Volume's transform
        /// One NativeArray of Voxels is maintained for all the chunks and simply overriden with new data.
        /// </summary>
        protected virtual IEnumerator InitializeChunks() {
            var size = _baseVolume.ConfigBlob.Value.SizeInChunks;
            var offset = new Vector3Int(size.x / 2, size.y / 2, size.z / 2);
            for (var x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    for (var z = 0; z < size.z; z++) {
                        var chunkCoord = new Vector3Int(x, y, z) - offset;
                        _baseVolume.CreateChunk<IChunk>(chunkCoord, chunkPrefab);
                        yield return null;
                    }
                }
            }
        }

        /// <summary>
        /// Marching Cubes meshes utilize a triplanar shader. In order for textures to "stick to" their gemometry
        /// as the volume moves, the volume origin must be updated. This is costly so should be avoided for volumes
        /// that reliably will not move.
        /// </summary>
        protected virtual void Update() {
            if (!isMoving)
                return;

            _baseVolume.UpdateVolumeOrigin();
        }

        /// <summary>
        /// BaseVolume implements IDisposable to dispose it's BlobAssets. 
        /// </summary>
        protected override void OnDestroy() => _baseVolume?.Dispose();

        // IVolume implementations
        public Vector2[] ViewDistanceLodRanges => viewDistanceLodRanges;

        public Transform VolumeTransform => transform;

        public Transform LodTarget =>
                Camera.main == null
                        ? FindAnyObjectByType<Camera>().transform
                        : Camera.main.transform;

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
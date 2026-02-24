// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using PurrNet;
using Spellbound.Core;
using Spellbound.GeoForge;
using Unity.Collections;
using UnityEngine;

namespace GeoForge.Sample4 {
    /// <summary>
    /// Networked chunk implementation using PurrNet... Check with Valentin and Bobsi to see if the PurrNet section is
    /// how they picture it being structured.
    /// 
    /// Ownership Model:
    /// - 1 observer → That player owns the chunk (lag-free editing)
    /// - 0 or 2+ observers → Server owns the chunk (server-authoritative editing)
    /// 
    /// This script is intended to give users a robust example of how they might structure terrain chunks using the
    /// Marching Cubes API as well as the PurrNet framework. The script leverages PurrNet's lifecycles, network identity
    /// callbacks, NetworkModules, and the visibility system to handle ownership tracking, exchange, and handoffs to allow users
    /// to seamlessly swap between server authoritative and local terraforming based on client proximity. This capability
    /// creates lag-free environment regardless of host ping and location.
    /// </summary>
    public class NetworkedChunk : NetworkIdentity, IChunk {
        [SerializeField] DataFactory dataFactory;

        [SerializeField] BoundaryOverrides boundaryOverrides;
        
        
        [SerializeField] private VoxelSyncModule syncModule = new();

        public BaseChunk BaseChunk { get; private set; }

        #region PurrNet Lifecycles, Events and Callbacks

        protected override void OnEarlySpawn() => BaseChunk = new BaseChunk(this, this);

        protected override void OnSpawned() => syncModule.onVoxelsChanged += ApplyEditsToBaseChunk;

        protected override void OnDespawned() => syncModule.onVoxelsChanged -= ApplyEditsToBaseChunk;

        protected override void OnDestroy() {
            base.OnDestroy();
            BaseChunk?.Dispose();
        }

        /// <summary>
        /// Called when a player starts observing this chunk.
        /// Server-side only.
        /// </summary>
        protected override void OnObserverAdded(PlayerID player) {
            if (!isServer)
                Debug.Log($"[Client] {player.id} is running in observer added lol bug");
            
            if (isServer)
                Debug.Log($"[Server] {player.id} is running in observer added");
            
            SendToNewObserver(player, BaseChunk.ChunkCoord);

            // If this chunks observer count is less than or equal to 1 OR doesn't have an owner get out.
            if (observers.Count <= 1 || !hasOwner)
                return;

            // Otherwise it does have an owner, and it has more than one observer and therefore should be owned by the server.
            RemoveOwnership();

            Debug.Log(
                $"[Server] Chunk {BaseChunk?.ChunkCoord} - Multiple observers ({observers.Count}), server taking authority");
        }

        /// <summary>
        /// Called when a player stops observing this chunk.
        /// Server-side only.
        /// </summary>
        protected override void OnObserverRemoved(PlayerID player) {
            // If you're not the server get out.
            if (!isServer)
                return;

            // If this chunks observer count is not equal to 1 get out because that means there are still multiple
            // observers and therefore the server should still own it.
            if (observers.Count != 1)
                return;

            // Otherwise... there is 1 observer, and we need to verify that the server owns it.
            var isolatedPlayer = observers[0];

            // If it does own it then get out.
            if (owner == isolatedPlayer)
                return;

            // Otherwise we need the server to reclaim ownership.
            GiveOwnership(isolatedPlayer);

            Debug.Log(
                $"[Server] Chunk {BaseChunk?.ChunkCoord} - Single observer remaining, giving ownership to {isolatedPlayer}");
        }

        /// <summary>
        /// PurrNet callback that should trigger on any ownership change.
        /// </summary>
        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) {
            // Only print this if there is a new owner, I'm the owner, and I'm a client (avoid double prints).
            if (newOwner.HasValue && isOwner && isClient)
                Debug.Log($"[Client] I now own chunk {BaseChunk?.ChunkCoord} - lag-free editing enabled!");
        }

        [TargetRpc(bufferLast: true)]
        private void SendToNewObserver(PlayerID target, Vector3Int chunkCoord) {
            // Lets error handle properly. If you're following this example then it should fit into the PurrNet
            // lifecycle properly. However, if you're not and doing your own thing it's important to make sure it exists!
            if (BaseChunk == null) {
                Debug.LogError("[Client] BaseChunk is null. Please ensure BaseChunk is created.", this);

                return;
            }
            
            BaseChunk.SetCoordAndFields(chunkCoord);
            
            Debug.Log($"[Client] Initializing chunk at {BaseChunk?.ChunkCoord}");
            
            InitializeChunk();
        }

        #endregion

        #region IChunk Implementation
        
        public void InitializeChunk(NativeArray<VoxelData> voxels = default) {
            BaseChunk.ParentVolume.BaseVolume.RegisterChunk(BaseChunk.ChunkCoord, this);
            
            if (boundaryOverrides != null) {
                var overrides = boundaryOverrides.BuildChunkOverrides(
                    BaseChunk.ChunkCoord, BaseChunk.ParentVolume.ConfigBlob);
                BaseChunk.SetOverrides(overrides);
            }

            if (voxels == default)
                voxels = new NativeArray<VoxelData>(
                    BaseChunk.ParentVolume.ConfigBlob.Value.ChunkDataVolumeSize, Allocator.Persistent);
            
            dataFactory.FillDataArray(BaseChunk.ChunkCoord, BaseChunk.ParentVolume.ConfigBlob, voxels);
            Debug.Log("initialize chunk is running, about to initialze voxels");
            BaseChunk.InitializeVoxels(voxels);

            if (voxels.IsCreated) 
                voxels.Dispose();
        }

        /// <summary>
        /// Called by MarchingCubesManager.DistributeVoxelEdits.
        /// This is where interoperability between PurrNet and MarchingCubes begins.
        /// </summary>
        public void PassVoxelEdits(List<VoxelEdit> newVoxelEdits) => syncModule.ProcessEdits(newVoxelEdits);

        #endregion

        #region Local Methods

        private void ApplyEditsToBaseChunk(List<VoxelEdit> edits) {
            if (BaseChunk.ApplyVoxelEdits(edits, out var editBounds))
                BaseChunk.ValidateOctreeEdits(editBounds);
        }

        #endregion
    }
}
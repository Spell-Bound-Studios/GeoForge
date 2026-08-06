// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using PurrNet;
using Spellbound.Core;
using Spellbound.GeoForge;
using Spellbound.GeoForge.Sample4;
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
    ///
    /// Edit application hook: GeoChunk's own constructor subscribes PassVoxelEdits to
    /// IGeoEditStore.OnGeoEditChanged automatically, every time a GeoChunk is constructed - that
    /// is the single, canonical path from "the store's data changed" to "the octree gets
    /// revalidated." Do not add a second subscription for this (e.g. in OnSpawned) - a previous
    /// version of this sample did exactly that via ApplyEditsToBaseChunk, which duplicated
    /// PassVoxelEdits's own logic and ran a full unpack/release cycle a second time for every
    /// edit batch, for no effect beyond wasted work.
    /// </summary>
    public class NetworkedChunk : NetworkIdentity, IGeoChunk {
        [SerializeField] DataFactory dataFactory;

        [SerializeField] BoundaryOverrides boundaryOverrides;
        
        
        [SerializeField] private ExampleSyncModule _syncModule = new();
        
        private Vector3Int _chunkCoord;

        public GeoChunk GeoChunk { get; private set; }

        /// <summary>
        /// OnEarlySpawn (server-only) already constructs a placeholder GeoChunk using whatever
        /// _chunkCoord happens to be at that point - Vector3Int.zero for a server-created chunk,
        /// since nothing sets it before GeoVolume.CreateChunk calls this method. That placeholder
        /// was never registered in ChunkDict (RegisterChunk is only ever called from here, with
        /// the real coord), so a full GeoChunk.Dispose() on it would be unsafe: Dispose() calls
        /// ChunkDict.Remove(_chunkCoord), which for an unregistered placeholder at (0,0,0) could
        /// delete a DIFFERENT, legitimate chunk's entry if one genuinely exists at that coordinate.
        /// Unsubscribing the stale GeoChunk's event handler directly avoids that collision while
        /// still preventing the leaked subscription that would otherwise keep processing edits
        /// against stale/default data forever.
        /// </summary>
        public void InitializeGeoChunk(Vector3Int coord) {
            if (GeoChunk != null)
                GeoChunk.IGeoEditStore.OnGeoEditChanged -= GeoChunk.HandleResolvedVoxelEdits;

            _chunkCoord = coord;
            GeoChunk = new GeoChunk(this, transform, _syncModule, coord);
            GeoChunk.IGeoEditStore.DefaultVoxelDataFunc = GeoChunk.GetVoxelData;
            GeoChunk.ParentGeoVolume.GeoVolume.RegisterChunk(coord, this);
        }

        #region PurrNet Lifecycles, Events and Callbacks

        protected override void OnEarlySpawn() {
            _syncModule.SetChunkData();
            GeoChunk = new GeoChunk(this, transform, _syncModule, _chunkCoord);
        } 

        protected override void OnDestroy() {
            base.OnDestroy();
            GeoChunk?.Dispose();
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
            
            SendToNewObserver(player, GeoChunk.ChunkCoord);

            // If this chunks observer count is less than or equal to 1 OR doesn't have an owner get out.
            if (observers.Count <= 1 || !hasOwner)
                return;

            // Otherwise it does have an owner, and it has more than one observer and therefore should be owned by the server.
            RemoveOwnership();

            Debug.Log(
                $"[Server] Chunk {GeoChunk?.ChunkCoord} - Multiple observers ({observers.Count}), server taking authority");
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
                $"[Server] Chunk {GeoChunk?.ChunkCoord} - Single observer remaining, giving ownership to {isolatedPlayer}");
        }

        /// <summary>
        /// PurrNet callback that should trigger on any ownership change.
        /// </summary>
        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer) {
            // Only print this if there is a new owner, I'm the owner, and I'm a client (avoid double prints).
            if (newOwner.HasValue && isOwner && isClient)
                Debug.Log($"[Client] I now own chunk {GeoChunk?.ChunkCoord} - lag-free editing enabled!");
        }

        [TargetRpc(bufferLast: true)]
        private void SendToNewObserver(PlayerID target, Vector3Int chunkCoord) {
            // Lets error handle properly. If you're following this example then it should fit into the PurrNet
            // lifecycle properly. However, if you're not and doing your own thing it's important to make sure it exists!
            if (GeoChunk == null) {
                Debug.LogError("[Client] GeoChunk is null. Please ensure GeoChunk is created.", this);

                return;
            }
            
            _chunkCoord = chunkCoord;
            gameObject.name = chunkCoord.ToString();
            InitializeGeoChunk(_chunkCoord); // THIS LINE
            
            Debug.Log($"[Client] Initializing chunk at {GeoChunk?.ChunkCoord}");
            
            InitializeChunk();
        }
        

        #endregion

        #region IChunk Implementation
        
        public void InitializeChunk(NativeArray<VoxelData> voxels = default) {
            GeoChunk.ParentGeoVolume.GeoVolume.RegisterChunk(GeoChunk.ChunkCoord, this);
            
            if (boundaryOverrides != null) {
                var overrides = boundaryOverrides.BuildChunkOverrides(
                    GeoChunk.ChunkCoord, GeoChunk.ParentGeoVolume.ConfigBlob);
                GeoChunk.SetOverrides(overrides);
            }

            if (voxels == default)
                voxels = new NativeArray<VoxelData>(
                    GeoChunk.ParentGeoVolume.ConfigBlob.Value.ChunkDataVolumeSize, Allocator.Persistent);
            
            dataFactory.FillDataArray(GeoChunk.ChunkCoord, GeoChunk.ParentGeoVolume.ConfigBlob, voxels);
            GeoChunk.SetVoxels(voxels);

            if (voxels.IsCreated) 
                voxels.Dispose();
        }
        
        #endregion
    }
}
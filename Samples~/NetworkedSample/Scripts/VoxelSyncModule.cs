// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using PurrNet;
using Spellbound.Core.Packing;
using Spellbound.GeoForge;

namespace GeoForge.Sample4 {
    /// <summary>
    /// NetworkModule responsible for synchronizing batched voxel edits across the network.
    /// </summary>
    [Serializable]
    public class VoxelSyncModule : NetworkModule {
        private readonly Dictionary<int, VoxelEdit> _voxelEdits = new();
        
        /// <summary>
        /// An event that is invoked when voxels are changed from a terraform event.
        /// </summary>
        public event Action<List<VoxelEdit>> onVoxelsChanged;

        /// <summary>
        /// Constructor for this Module.
        /// </summary>
        public VoxelSyncModule() {
            if (!isServer)
                return;
            
            // We intend for the server to act as the scribe. Reason being: if someone dc's and reconnects they should
            // be able to get any data they need/require from the server.
            onVoxelsChanged += HandleScribeVoxelEdits;
        }

        /// <summary>
        /// Built in PurrNet override - we chose to unsubscribe here.
        /// </summary>
        public override void OnPoolReset() {
            base.OnPoolReset();
            onVoxelsChanged -= HandleScribeVoxelEdits;
        } 

        /// <summary>
        /// The actual recording of voxel edits.
        /// Recall that only the server is subscribing this method to the onVoxelChanged event.
        /// </summary>
        private void HandleScribeVoxelEdits(List<VoxelEdit> newEdits) {
            foreach (var edit in newEdits)
                _voxelEdits[edit.index] = edit;
        }

        /// <summary>
        /// Any time a terraform event occurs that changes the mesh a ProcessEdits is called with said edits.
        /// </summary>
        /// <remarks>
        /// This is the bread and butter of this module. Users should note that we have effectively given owners the
        /// ability to edit without latency. This should enable your gameplay to appear uninterrupted to players if and
        /// when they own their own chunk. Otherwise, the server owns it and the edits are server authoritative.
        /// If you're struggling with this concept - just imagine that your friend from across the world joins your game
        /// with you as the host, and they run off by themselves in your game world. If they are truly by
        /// themselves then they should be able to play lag free and simply relay their change events back to the server.
        /// The server will simply record their edits so that if they disconnect and rejoin - they come back to the
        /// edits they've made.
        /// </remarks>
        public void ProcessEdits(List<VoxelEdit> edits) {
            var packed = Packer.PackListToBytes(edits);
            //var ballsack = Packer.

            if (isOwner || isServer) {
                onVoxelsChanged?.Invoke(edits);
                HandleStateChangeToObservers(packed);
            }
            else
                HandleClientTryChangeToServer(packed);
        }

        /// <summary>
        /// PurrNet callback that is triggered anytime an observer is added to the NetworkIdentity.
        /// </summary>
        public override void OnObserverAdded(PlayerID player) {
            base.OnObserverAdded(player);
            
            var packed = Packer.PackListToBytes(_voxelEdits.Values.ToList());
            HandleInitialStateToTarget(player, packed);
        }

        /// <summary>
        /// Any client observing the chunk should receive the edits so that they can trigger their marching event.
        /// </summary>
        [ObserversRpc(excludeOwner: true)]
        private void HandleStateChangeToObservers(byte[] batchedPackedEdits) {
            var edits = Packer.UnpackListFromBytes<VoxelEdit>(batchedPackedEdits);
            onVoxelsChanged?.Invoke(edits);
        }

        /// <summary>
        /// Any client that is newly observing a chunk should receive its current edits.
        /// </summary>
        /// <remarks>
        /// Imagine you're far away from your friend, and you're not observing the chunk that they are in. Now imagine
        /// they perform some terraform events... this method is responsible for communicating back to you all of those
        /// changes once you get close enough to their chunk and begin observing it.
        /// </remarks>
        [TargetRpc]
        private void HandleInitialStateToTarget(PlayerID player, byte[] allEditsPacked) {
            var edits = Packer.UnpackListFromBytes<VoxelEdit>(allEditsPacked);
            onVoxelsChanged?.Invoke(edits);
        }
        

        /// <summary>
        /// For clients that want to edit a chunk they don't own.
        /// Routes the request through the server/owner.
        /// </summary>
        [ServerRpc(requireOwnership: false)]
        public void HandleClientTryChangeToServer(byte[] packedEdits, RPCInfo info = default) {
            var edits = Packer.UnpackListFromBytes<VoxelEdit>(packedEdits);
            onVoxelsChanged?.Invoke(edits);
            HandleStateChangeToObservers(packedEdits);
        }
    }
}
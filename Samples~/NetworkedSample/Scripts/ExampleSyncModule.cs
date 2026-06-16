// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using PurrNet;
using Spellbound.Core.Logging;
using Spellbound.Core.Packing;
using Spellbound.GeoForge;
using UnityEngine;

namespace Spellbound.GeoForge.Sample4 {
    /// <summary>
    /// Networked implementation of IGeoEditStore. Edits live in a GeoForgeChunkData
    /// shared by reference with the GeoForgeEditStore save section, so anything the
    /// authority resolves is what the world save persists.
    /// </summary>
    public class ExampleSyncModule : NetworkModule, IGeoEditStore, ITick {
        [SerializeField] private bool ownerAuth;

        #region Data

        private GeoForgeChunkData _chunkData;

        #endregion Data

        #region Changes This Tick
        
        private HashSet<int> _dirty = new();

        #endregion Changes This Tick

        #region Initialization

        public ExampleSyncModule(bool isOwnerAuth = true) {
            ownerAuth = isOwnerAuth;
        }

        /// <summary>
        /// This is a server only method.
        /// </summary>
        /// <param name="chunkData"></param>
        public void SetChunkData(GeoForgeChunkData chunkData = null) {
            chunkData ??= new GeoForgeChunkData();
            _chunkData = chunkData;
        }

        #endregion Initialization

        #region PurrNet Lifecycle

        public override void OnEarlySpawn() {
            base.OnEarlySpawn();

            if (isHost)
                return;
        }

        public override void OnObserverAdded(PlayerID player) {
            Log.Verbose($"OnObserverAdded running for player {player.ToString()}");

            if (player == localPlayer)
                return;

            var payload = Packer.BuildPayload((ref Span<byte> buffer) => WriteFullState(ref buffer));
            SendFullStateTargetRpc(player, payload);
        }

        public void OnTick(float delta) {
            if (!IsController(ownerAuth))
                return;

            if (_dirty.Count == 0)
                return;

            Flush();
        }

        public override void OnPoolReset() {
            _dirty?.Clear();

            OnGeoEditChanged = null;
            DefaultVoxelDataFunc = null;
        }

        public override void OnDespawned() {
            _chunkData = null;
            OnGeoEditChanged = null;
            DefaultVoxelDataFunc = null;
        }

        #endregion PurrNet Lifecycle

        #region Authoritative Actions

        private void Flush() {
            var payload = Packer.BuildPayload(WriteBatchTo);

            if (isServer)
                SendBatchObserversRpc(payload);
            else
                AuthorityDrivenSendBatchToServerRpc(payload);

            _dirty.Clear();
        }

        #endregion Authoritative Actions

        #region RPCs

        [TargetRpc]
        private void SendFullStateTargetRpc(PlayerID player, byte[] payload) {
            if (isHost)
                return;

            _chunkData ??= new GeoForgeChunkData();

            ReadFullStateFrom(payload);
        }

        [TargetRpc]
        private void SendAccumulatedBatchTargetRpc(PlayerID player, byte[] payload) {
            if (isHost)
                return;

            ApplyBatchFrom(payload);
        }

        [TargetRpc]
        private void SendMarkInitializedTargetRpc(PlayerID player) {
            if (isHost)
                return;

            Log.Info($"Mark initialized received. Edits: {_chunkData?.EditCount}");
        }

        /// <summary>
        /// Authority driven ServerRpc that forces server to sync a batch and then distribute to observers. This ServerRpc
        /// can only be called by the owner of this module.
        /// </summary>
        [ServerRpc(requireOwnership: true)]
        private void AuthorityDrivenSendBatchToServerRpc(byte[] payload) {
            if (!ownerAuth)
                return;

            ApplyBatchFrom(payload);
            AuthorityDrivenSendBatchToOthersRpc(payload);
        }

        [ObserversRpc]
        private void SendBatchObserversRpc(byte[] payload) {
            if (isHost)
                return;
            
            ApplyBatchFrom(payload);
        }

        [ObserversRpc(excludeOwner: true)]
        private void AuthorityDrivenSendBatchToOthersRpc(byte[] payload) {
            if (isServer || isHost)
                return;

            ApplyBatchFrom(payload);
        }

        [ServerRpc]
        private void SubmitWriteToServerRpc(byte[] payload) => ResolveWrite(ReadListVoxelData(payload));

        [ServerRpc]
        private void SubmitDeltaToServerRpc(byte[] payload) => ResolveDelta(ReadListVoxelDeltas(payload));

        /// <summary>
        /// Special ObserverRPC to clear the edits without utilizing the Dirty flag
        /// </summary>
        [ObserversRpc]
        private void ClearAllEditsObserversRpc() {
            if (isHost)
                return;

            _chunkData.ClearEdits();
            _dirty.Clear();
            NotifyGeoEditsChanged(new List<(int, VoxelData)>());
        }

        #endregion RPCs

        #region IGeoEditStore Implementation

        public event Action<List<(int, VoxelData)>> OnGeoEditChanged;

        public Func<int, VoxelData> DefaultVoxelDataFunc { get; set; }

        public bool TryRead(int idx, out VoxelData voxelData) {
            if (_chunkData.TryReadEdit(idx, out voxelData))
                return true;

            voxelData = DefaultVoxelDataFunc?.Invoke(idx) ?? new VoxelData();

            return false;
        }

        public void Write(List<(int, VoxelData)> voxelDatas) {
            if (IsController(ownerAuth))
                ResolveWrite(voxelDatas);
            else {
                var payload = Packer.BuildPayload((ref Span<byte> buffer) =>
                        WriteListVoxelData(voxelDatas, ref buffer));

                SubmitWriteToServerRpc(payload);
            }
        }

        public void Delta(List<VoxelDelta> voxelEdits) {
            if (IsController(ownerAuth))
                ResolveDelta(voxelEdits);
            else {
                var payload = Packer.BuildPayload((ref Span<byte> buffer) =>
                        WriteListVoxelDelta(voxelEdits, ref buffer));

                SubmitDeltaToServerRpc(payload);
            }
        }

        public IEnumerable<(int, VoxelData)> ReadAllEdits() {
            foreach (var (idx, voxelData) in _chunkData.Edits)
                yield return (idx, voxelData);
        }

        #endregion IGeoEditStore Implementation

        #region Resolve Logic

        private void ResolveWrite(List<(int, VoxelData)> voxelDatas) {
            var changes = new List<(int, VoxelData)>(voxelDatas.Count);

            foreach (var (idx, voxelData) in voxelDatas) {
                TryRead(idx, out var current);

                if (voxelData == current)
                    continue;

                _chunkData.WriteEdit(idx, voxelData);
                _dirty.Add(idx);
                changes.Add((idx, voxelData));
            }

            NotifyGeoEditsChanged(changes);
        }

        private void ResolveDelta(List<VoxelDelta> newDeltas) {
            var changes = new List<(int, VoxelData)>(newDeltas.Count);

            foreach (var newDelta in newDeltas) {
                if (!_chunkData.TryReadEdit(newDelta.index, out var voxelData))
                    voxelData = DefaultVoxelDataFunc(newDelta.index);

                var density = (byte)Mathf.Clamp(
                    voxelData.Density + newDelta.densityDelta,
                    byte.MinValue,
                    byte.MaxValue);

                var matIndex = voxelData.Density < newDelta.densityDelta
                        ? newDelta.materialType
                        : voxelData.MaterialIndex;

                var resolved = new VoxelData(density, matIndex);

                if (resolved == voxelData)
                    continue;

                _chunkData.WriteEdit(newDelta.index, resolved);
                _dirty.Add(newDelta.index);
                changes.Add((newDelta.index, resolved));
            }

            NotifyGeoEditsChanged(changes);
        }

        #endregion Resolve Logic

        #region Packer Batch Write

        private static void WriteListVoxelDelta(List<VoxelDelta> voxelDeltas, ref Span<byte> buffer) {
            Packer.WriteInt(ref buffer, voxelDeltas.Count);

            foreach (var voxelDelta in voxelDeltas)
                voxelDelta.Pack(ref buffer);
        }

        private static void WriteListVoxelData(List<(int, VoxelData)> voxelDatas, ref Span<byte> buffer) {
            Packer.WriteInt(ref buffer, voxelDatas.Count);

            foreach (var (index, voxelData) in voxelDatas) {
                Packer.WriteInt(ref buffer, index);
                voxelData.Pack(ref buffer);
            }
        }

        private void WriteBatchTo(ref Span<byte> buffer) {
            var changedCount = 0;

            foreach (var idx in _dirty) {
                if (_chunkData.Edits.ContainsKey(idx))
                    changedCount++;
            }

            Packer.WriteInt(ref buffer, changedCount);

            foreach (var idx in _dirty) {
                if (!_chunkData.TryReadEdit(idx, out var voxelData))
                    continue;

                Packer.WriteInt(ref buffer, idx);
                voxelData.Pack(ref buffer);
            }
        }

        private void WriteFullState(ref Span<byte> buffer) {


            Packer.WriteInt(ref buffer, _chunkData.EditCount);

            foreach (var (idx, voxelData) in  _chunkData.Edits) {
                Packer.WriteInt(ref buffer, idx);
                voxelData.Pack(ref buffer);
            }
        }

        #endregion Packer Batch Write

        #region Packer Batch Read

        private static List<VoxelDelta> ReadListVoxelDeltas(byte[] payload) {
            ReadOnlySpan<byte> span = payload;
            var count = Packer.ReadInt(ref span);
            var deltas = new List<VoxelDelta>(count);

            for (var i = 0; i < count; i++) {
                var voxelDelta = new VoxelDelta();
                voxelDelta.Unpack(ref span);
                deltas.Add(voxelDelta);
            }

            return deltas;
        }

        private static List<(int, VoxelData)> ReadListVoxelData(byte[] payload) {
            ReadOnlySpan<byte> span = payload;
            var count = Packer.ReadInt(ref span);
            var voxelDatas = new List<(int, VoxelData)>(count);

            for (var i = 0; i < count; i++) {
                var index = Packer.ReadInt(ref span);
                var voxelData = new VoxelData();
                voxelData.Unpack(ref span);
                voxelDatas.Add((index, voxelData));
            }

            return voxelDatas;
        }

        private void ApplyBatchFrom(byte[] payload) {
            ReadOnlySpan<byte> span = payload;
            var count = Packer.ReadInt(ref span);
            var changes = new List<(int, VoxelData)>(count);

            for (var i = 0; i < count; i++) {
                var idx = Packer.ReadInt(ref span);
                var voxelData = new VoxelData();
                voxelData.Unpack(ref span);

                _chunkData.WriteEdit(idx, voxelData);
                changes.Add((idx, voxelData));
            }

            NotifyGeoEditsChanged(changes);
        }

        private void ReadFullStateFrom(byte[] payload) {
            ReadOnlySpan<byte> span = payload;
            var count = Packer.ReadInt(ref span);
            var changes = new List<(int, VoxelData)>(count);

            for (var i = 0; i < count; i++) {
                var idx = Packer.ReadInt(ref span);
                var voxelData = new VoxelData();
                voxelData.Unpack(ref span);

                _chunkData.WriteEdit(idx, voxelData);
                changes.Add((idx, voxelData));
            }

            NotifyGeoEditsChanged(changes);
        }

        #endregion Packer Batch Read

        #region Notify Helpers

        private void NotifyGeoEditsChanged(List<(int, VoxelData)> changes) {
            if (changes.Count == 0)
                return;

            OnGeoEditChanged?.Invoke(changes);
        }

        #endregion Notify Helpers

        #region Queries

        public int EditCount => _chunkData?.EditCount ?? 0;

        #endregion Queries
    }
}

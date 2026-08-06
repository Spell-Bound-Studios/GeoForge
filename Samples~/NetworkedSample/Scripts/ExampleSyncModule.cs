// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using PurrNet;
using Spellbound.Core.Logging;
using Spellbound.Core.Packing;
using Spellbound.GeoForge;
using Unity.Mathematics;
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
        private void SubmitDeltaToServerRpc(byte[] payload) => ResolveDelta(ReadVoxelEditOperation(payload));

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

        public void PassVoxelEditOperation(VoxelEditOperation operation) {
            if (IsController(ownerAuth))
                ResolveDelta(operation);
            else {
                var payload = Packer.BuildPayload((ref Span<byte> buffer) =>
                        WriteVoxelEditOperation(operation, ref buffer));

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

        private void ResolveDelta(VoxelEditOperation operation) {
            var changes = new List<(int, VoxelData)>(operation.Deltas.Length);

            foreach (var voxelDelta in operation.Deltas) {
                if (!_chunkData.TryReadEdit(voxelDelta.Index, out var voxelData))
                    voxelData = DefaultVoxelDataFunc(voxelDelta.Index);

                var wasFull = voxelData.Density >= 0;
                var existingMatIndex = voxelData.GetPlainMatIndex();

                // Gate: a voxel that's already full and whose current material this operation
                // isn't permitted to affect (e.g. Impervious, or below the calling tool's tier)
                // rejects ALL density changes outright — additions as well as subtractions.
                if (wasFull && !operation.IsAllowed(existingMatIndex)) {
                    continue;
                }

                var density = (sbyte)Mathf.Clamp(
                    voxelData.Density + voxelDelta.DensityDelta,
                    sbyte.MinValue,
                    sbyte.MaxValue);

                var isFull = density >= 0;

                byte matIndex;
                VoxelData resolved;

                if (!isFull) {
                    // Core invariant: any voxel ending with negative density is the null/sentinel
                    // material, no exceptions. Always immature - there's no such thing as mature air.
                    matIndex = VoxelData.NullSentinelValue;
                    resolved = VoxelData.CreateImmature(density, matIndex);
                }
                else if (!wasFull && isFull) {
                    // Material is only ever claimed at the empty -> full crossing. Freshly placed
                    // material always starts immature, regardless of the existing voxel's prior state.
                    matIndex = operation.MaterialIndex;
                    resolved = VoxelData.CreateImmature(density, matIndex);
                }
                else {
                    // Already solid on both sides of this delta - material AND maturity persist
                    // unchanged. A minor density nudge on long-standing mature terrain shouldn't
                    // reset it back to immature; only a genuine empty -> full crossing (above)
                    // counts as "freshly placed." Same rule as SimpleGeoEditStore.
                    matIndex = existingMatIndex;
                    resolved = voxelData.IsMature()
                            ? VoxelData.CreateMature(density, matIndex)
                            : VoxelData.CreateImmature(density, matIndex);
                }

                if (resolved == voxelData)
                    continue;

                _chunkData.WriteEdit(voxelDelta.Index, resolved);
                _dirty.Add(voxelDelta.Index);
                changes.Add((voxelDelta.Index, resolved));
            }

            NotifyGeoEditsChanged(changes);
        }

        #endregion Resolve Logic

        #region Packer Batch Write

        private static void WriteVoxelEditOperation(VoxelEditOperation operation, ref Span<byte> buffer) {
            Packer.WriteByte(ref buffer, operation.MaterialIndex);

            // uint4 has no native Packer support, so each lane is round-tripped through int via
            // unchecked cast (bit pattern preserved, just relabeled as signed for the packer).
            Packer.WriteInt(ref buffer, unchecked((int)operation.AllowedMaterialsMask.x));
            Packer.WriteInt(ref buffer, unchecked((int)operation.AllowedMaterialsMask.y));
            Packer.WriteInt(ref buffer, unchecked((int)operation.AllowedMaterialsMask.z));
            Packer.WriteInt(ref buffer, unchecked((int)operation.AllowedMaterialsMask.w));

            Packer.WriteInt(ref buffer, operation.Deltas.Length);

            foreach (var delta in operation.Deltas) {
                Packer.WriteInt(ref buffer, delta.Index);
                Packer.WriteShort(ref buffer, delta.DensityDelta);
            }
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

        private static VoxelEditOperation ReadVoxelEditOperation(byte[] payload) {
            ReadOnlySpan<byte> span = payload;

            var materialIndex = Packer.ReadByte(ref span);

            var maskX = unchecked((uint)Packer.ReadInt(ref span));
            var maskY = unchecked((uint)Packer.ReadInt(ref span));
            var maskZ = unchecked((uint)Packer.ReadInt(ref span));
            var maskW = unchecked((uint)Packer.ReadInt(ref span));
            var mask = new uint4(maskX, maskY, maskZ, maskW);

            var count = Packer.ReadInt(ref span);
            var deltas = new List<VoxelDensityDelta>(count);

            for (var i = 0; i < count; i++) {
                var index = Packer.ReadInt(ref span);
                var densityDelta = Packer.ReadShort(ref span);
                deltas.Add(new VoxelDensityDelta(index, densityDelta));
            }

            return new VoxelEditOperation(materialIndex, deltas, mask);
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
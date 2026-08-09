// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Console;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager : MonoBehaviour {
        private Dictionary<int, DenseVoxelDataPool> _denseVoxelDataDict = new();

        internal NativeArray<VoxelData> GetOrUnpackVoxelArray(
            int dataSizeKey,
            GeoChunkEngine chunkEngine,
            NativeList<SparseVoxelData> sparseData,
            bool isEdit) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                throw new InvalidOperationException(
                    $"GetOrUnpackVoxelArray: no denseVoxelData registered for chunk size {dataSizeKey}. " +
                    "Was RegisterVoxelVolume called for this volume's chunk size?");
            }

            return pool.GetOrUnpack(dataSizeKey, chunkEngine, sparseData, isEdit);
        }

        internal void PackVoxelArray(int dataSizeKey, GeoChunkEngine chunkEngine, bool isEdit, BoundsInt editBounds) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                throw new InvalidOperationException(
                    $"PackVoxelArray: no denseVoxelData registered for chunk size {dataSizeKey}. " +
                    "Was RegisterVoxelVolume called for this volume's chunk size?");
            }

            pool.Pack(chunkEngine, isEdit, editBounds);
        }

        internal void ReleaseVoxelArray(int dataSizeKey, GeoChunkEngine chunkEngine, bool isEdit) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                ConsoleLogger.PrintError(
                    $"MarchingCubes Manager does not have a denseVoxelData Array of this size");

                return;
            }

            pool.Release(chunkEngine, isEdit);
        }

        internal bool TryGetResidentVoxelArray(int dataSizeKey, GeoChunkEngine chunkEngine, out NativeArray<VoxelData> voxels) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                voxels = default;

                return false;
            }

            return pool.TryGetResident(chunkEngine, out voxels);
        }

        // Evicts `chunk` from BOTH pools (edit and validation) for this chunk size, wherever it
        // happens to be resident, without touching whatever's currently checked out elsewhere.
        // Needed by any caller that rewrites a chunk's _sparseVoxels through a path OTHER than
        // GetOrUnpack -> mutate -> Pack (the only path that keeps pool residency correct on its
        // own, via Pack's SyncOtherPoolResidency step). GeoChunk.SetVoxels is exactly that other
        // path - it rebuilds _sparseVoxels directly from a caller-supplied array and has no pool
        // slot of its own to sync FROM, so the only correct move is to evict any stale cached
        // copy in either pool and force the next real checkout to do a fresh unpack from the now-
        // current _sparseVoxels, rather than silently handing back a stale array via the
        // "already resident" fast path in GetOrUnpack.
        internal void InvalidateChunkResidency(int dataSizeKey, GeoChunkEngine chunkEngine) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool))
                return;

            pool.InvalidateResidency(chunkEngine);
        }

        // Number of Edit-pool slots for the given chunk size - the hard ceiling on how many
        // distinct chunks a single terraform action or a single load batch can touch before
        // GetOrUnpack's exhaustion throw would fire. Exposed so a caller that fans work out
        // across chunks BEFORE scheduling any checkout (terraform commands' pre-validation,
        // GeoVolume.TryLoadFromByteArray's batching) can size itself correctly ahead of time.
        // Returns 0 if this chunk size was never registered - callers must treat that as
        // "reject/nothing to do," not "unlimited."
        internal int GetEditPoolCapacity(int chunkSize) =>
                _denseVoxelDataDict.TryGetValue(chunkSize, out var pool) ? pool.EditSlotCount : 0;

        internal class DenseVoxelDataPool : IDisposable {
            private readonly int _chunkSize;
            private readonly List<DenseVoxelData> _editSlots;
            private readonly List<DenseVoxelData> _validationSlots;
            private long _accessCounter;

            // Number of Edit slots this pool currently has - see GeoForgeManager.GetEditPoolCapacity
            // for why this needs to be externally visible.
            internal int EditSlotCount => _editSlots.Count;

            internal DenseVoxelDataPool(int chunkSize, int initialEditPoolSize, int initialValidationPoolSize) {
                _chunkSize = chunkSize;

                _editSlots = new List<DenseVoxelData>();
                EnsureEditCapacity(initialEditPoolSize);

                _validationSlots = new List<DenseVoxelData>();
                EnsureValidationCapacity(initialValidationPoolSize);
            }

            internal void EnsureEditCapacity(int minSize) {
                while (_editSlots.Count < minSize)
                    _editSlots.Add(new DenseVoxelData(_chunkSize));
            }

            internal void EnsureValidationCapacity(int minSize) {
                while (_validationSlots.Count < minSize)
                    _validationSlots.Add(new DenseVoxelData(_chunkSize));
            }

            private List<DenseVoxelData> SlotsFor(bool isEdit) => isEdit ? _editSlots : _validationSlots;

            internal NativeArray<VoxelData> GetOrUnpack(
                int dataSizeKey, GeoChunkEngine chunkEngine, NativeList<SparseVoxelData> sparseData, bool isEdit) {
                var slots = SlotsFor(isEdit);

                foreach (var slot in slots) {
                    if (slot.CurrentChunkEngine != chunkEngine)
                        continue;

                    if (slot.IsArrayInUse) {
                        Debug.LogError(
                            $"GetOrUnpackVoxelArray - Trying to unpack voxel array but array is in use for the same geoChunk. This is unexpected and bad.");

                        return slot.DenseVoxelArray;
                    }

                    slot.IsArrayInUse = true;
                    slot.LastAccessTick = ++_accessCounter;

                    return slot.DenseVoxelArray;
                }

                var claimIndex = -1;

                for (var i = 0; i < slots.Count; i++) {
                    if (!slots[i].IsArrayInUse && slots[i].CurrentChunkEngine == null) {
                        claimIndex = i;

                        break;
                    }
                }

                if (claimIndex == -1) {
                    var oldestTick = long.MaxValue;

                    for (var i = 0; i < slots.Count; i++) {
                        if (slots[i].IsArrayInUse)
                            continue;

                        if (slots[i].LastAccessTick >= oldestTick)
                            continue;

                        oldestTick = slots[i].LastAccessTick;
                        claimIndex = i;
                    }
                }

                if (claimIndex != -1) {
                    var slot = slots[claimIndex];
                    var evictedChunk = slot.CurrentChunkEngine;
                    var wasNeverOccupied = evictedChunk == null;

                    slot.IsArrayInUse = true;
                    slot.CurrentChunkEngine = chunkEngine;
                    slot.LastAccessTick = ++_accessCounter;

                    ref var config = ref chunkEngine.ParentGeoVolume.ConfigBlob.Value;

                    var unpackJob = new SparseToDenseVoxelDataJob {
                        ConfigBlob = chunkEngine.ParentGeoVolume.ConfigBlob,
                        Voxels = slot.DenseVoxelArray,
                        SparseVoxels = sparseData
                    };
                    var jobHandle = unpackJob.Schedule(config.ChunkDataWidthSize, 1);
                    jobHandle.Complete();
                    return slot.DenseVoxelArray;
                }

                throw new InvalidOperationException(
                    $"DenseVoxelDataPool.GetOrUnpack (isEdit={isEdit}): all {slots.Count} slots in " +
                    $"use and none resident for chunk {chunkEngine.ChunkCoord}. This should be structurally " +
                    "impossible under the current pool sizing - investigate the caller.");
            }

            internal void Pack(GeoChunkEngine chunkEngine, bool isEdit, BoundsInt editBounds) {
                var slot = FindSlot(chunkEngine, isEdit);

                if (slot == null) {
                    Debug.LogError(
                        $"PackVoxelArray - Trying to pack but chunk {chunkEngine.ChunkCoord} has no resident slot (isEdit={isEdit})");

                    return;
                }

                if (!slot.IsArrayInUse) {
                    Debug.LogError(
                        $"PackVoxelArray - Trying to pack but _isArrayInUse is false which is unexpected and bad");

                    return;
                }

                var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);

                var packJobHandle = new DenseToSparseVoxelDataJob {
                    Voxels = slot.DenseVoxelArray,
                    SparseVoxels = sparseData,
                    DensityRange = slot.DensityRange
                }.Schedule();

                packJobHandle.Complete();

                slot.CurrentChunkEngine.UpdateVoxelData(sparseData, slot.DensityRange[0]);
                sparseData.Dispose();

                SyncOtherPoolResidency(chunkEngine, isEdit, slot.DenseVoxelArray);
            }

            private void SyncOtherPoolResidency(GeoChunkEngine chunkEngine, bool packedIsEdit, NativeArray<VoxelData> freshVoxels) {
                foreach (var slot in SlotsFor(!packedIsEdit)) {
                    if (slot.CurrentChunkEngine != chunkEngine)
                        continue;

                    if (slot.IsArrayInUse) {
                        Debug.LogError(
                            $"SyncOtherPoolResidency: chunk {chunkEngine.ChunkCoord} is checked out in " +
                            "the other pool while being packed - can't safely overwrite a slot " +
                            "that's in use.");

                        return;
                    }

                    NativeArray<VoxelData>.Copy(freshVoxels, slot.DenseVoxelArray);

                    return;
                }
            }

            // Evicts `chunk` from BOTH _editSlots and _validationSlots, wherever resident, by
            // clearing CurrentChunk (never touching IsArrayInUse for a slot that's actually in
            // use elsewhere - see the in-use guard below). Unlike SyncOtherPoolResidency, this
            // has no fresh array to copy in - it's called from GeoChunk.SetVoxels, which has no
            // pool slot of its own, only a caller-supplied array it wrote directly into
            // _sparseVoxels. Forcing eviction means the next GetOrUnpack for this chunk, in
            // either pool, is guaranteed to do a real unpack job from the now-current
            // _sparseVoxels rather than the "already resident" fast path silently handing back
            // stale data.
            internal void InvalidateResidency(GeoChunkEngine chunkEngine) {
                InvalidateResidencyInSlots(_editSlots, chunkEngine);
                InvalidateResidencyInSlots(_validationSlots, chunkEngine);
            }

            private void InvalidateResidencyInSlots(List<DenseVoxelData> slots, GeoChunkEngine chunkEngine) {
                foreach (var slot in slots) {
                    if (slot.CurrentChunkEngine != chunkEngine)
                        continue;

                    if (slot.IsArrayInUse) {
                        // Shouldn't be reachable under today's fully-synchronous checkout model -
                        // nothing else can be holding a checkout on this chunk while SetVoxels is
                        // rewriting it on the same thread. Logged rather than silently ignored in
                        // case that assumption stops holding later (e.g. once P1 makes checkouts
                        // span frames).
                        Debug.LogError(
                            $"InvalidateResidency: chunk {chunkEngine.ChunkCoord} is checked out while " +
                            "SetVoxels is invalidating its pool residency - can't safely evict a " +
                            "slot that's in use.");

                        continue;
                    }

                    slot.CurrentChunkEngine = null;
                }
            }

            internal void Release(GeoChunkEngine chunkEngine, bool isEdit) {
                var slot = FindSlot(chunkEngine, isEdit);

                if (slot == null) {
                    ConsoleLogger.PrintError(
                        $"MarchingCubes Manager does not have a denseVoxelData Array for chunk {chunkEngine.ChunkCoord} (isEdit={isEdit})");

                    return;
                }

                slot.IsArrayInUse = false;
            }

            internal bool TryGetResident(GeoChunkEngine chunkEngine, out NativeArray<VoxelData> voxels) {
                foreach (var slot in _editSlots) {
                    if (slot.CurrentChunkEngine != chunkEngine)
                        continue;

                    voxels = slot.DenseVoxelArray;

                    return true;
                }

                foreach (var slot in _validationSlots) {
                    if (slot.CurrentChunkEngine != chunkEngine)
                        continue;

                    voxels = slot.DenseVoxelArray;

                    return true;
                }

                voxels = default;

                return false;
            }

            private DenseVoxelData FindSlot(GeoChunkEngine chunkEngine, bool isEdit) {
                foreach (var slot in SlotsFor(isEdit)) {
                    if (slot.CurrentChunkEngine == chunkEngine)
                        return slot;
                }

                return null;
            }

            public void Dispose() {
                foreach (var slot in _editSlots) slot.Dispose();
                foreach (var slot in _validationSlots) slot.Dispose();
            }
        }

        internal class DenseVoxelData : IDisposable {
            internal NativeArray<VoxelData> DenseVoxelArray;
            internal NativeArray<DensityRange> DensityRange;
            internal bool IsArrayInUse;
            internal GeoChunkEngine CurrentChunkEngine;
            internal long LastAccessTick;

            internal DenseVoxelData(int chunkSize, Allocator allocator = Allocator.Persistent) {
                var cs = chunkSize + 3;
                DenseVoxelArray = new NativeArray<VoxelData>(cs * cs * cs, allocator);
                DensityRange = new NativeArray<DensityRange>(1, allocator);
                IsArrayInUse = false;
                CurrentChunkEngine = null;
            }

            internal DenseVoxelData() {
                DenseVoxelArray = default;
                DensityRange = default;
                IsArrayInUse = false;
                CurrentChunkEngine = null;
            }

            public void Dispose() {
                if (DenseVoxelArray.IsCreated)
                    DenseVoxelArray.Dispose();

                if (DensityRange.IsCreated)
                    DensityRange.Dispose();
            }
        }
    }
}
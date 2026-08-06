// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Console;
using Spellbound.Core.Logging;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager : MonoBehaviour {
        private Dictionary<int, DenseVoxelDataPool> _denseVoxelDataDict = new();

        // isEdit selects which pool a checkout comes from. Edit-flow work (ApplyVoxelEdits and the
        // ValidateOctreeEdits validation that follows it) shares the Edit pool, sized per chunk
        // size from the largest edit-fan-out geometry among volumes registered at that chunk size
        // (see ComputeEditPoolSize/RegisterVoxelVolume in GeoForgeManager.cs). Per-frame LOD
        // validation (ValidateOctreeLods) uses the separate Validation pool, sized from the largest
        // ValidatesPerFrame among those same volumes.
        internal NativeArray<VoxelData> GetOrUnpackVoxelArray(
            int dataSizeKey,
            GeoChunk chunk,
            NativeList<SparseVoxelData> sparseData,
            bool isEdit) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                // No entry means RegisterVoxelVolume was never called for this chunk size - a
                // setup/lifecycle bug, not a normal runtime condition. Throw immediately instead
                // of handing back an uncreated NativeArray, which would only surface as a
                // confusing "array not allocated" exception far away at the first index into it.
                throw new InvalidOperationException(
                    $"GetOrUnpackVoxelArray: no denseVoxelData registered for chunk size {dataSizeKey}. " +
                    "Was RegisterVoxelVolume called for this volume's chunk size?");
            }

            return pool.GetOrUnpack(dataSizeKey, chunk, sparseData, isEdit);
        }

        internal void PackVoxelArray(int dataSizeKey, GeoChunk chunk, bool isEdit) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                // Same misuse case as GetOrUnpackVoxelArray - throw immediately rather than
                // falling through and dereferencing a null pool on the next line.
                throw new InvalidOperationException(
                    $"PackVoxelArray: no denseVoxelData registered for chunk size {dataSizeKey}. " +
                    "Was RegisterVoxelVolume called for this volume's chunk size?");
            }

            pool.Pack(chunk, isEdit);
        }

        internal void ReleaseVoxelArray(int dataSizeKey, GeoChunk chunk, bool isEdit) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                ConsoleLogger.PrintError(
                    $"MarchingCubes Manager does not have a denseVoxelData Array of this size");

                return;
            }

            pool.Release(chunk, isEdit);
        }

        // Read-only peek used by GeoChunk.GetVoxelData for an O(1) lookup when the chunk happens
        // to already be resident, instead of always paying for BinarySearchVoxelData over the
        // sparse array. See DenseVoxelDataPool.TryGetResident for the actual lookup and the
        // reasoning on why this is safe to call without a checkout.
        internal bool TryGetResidentVoxelArray(int dataSizeKey, GeoChunk chunk, out NativeArray<VoxelData> voxels) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var pool)) {
                voxels = default;

                return false;
            }

            return pool.TryGetResident(chunk, out voxels);
        }

        // One pool per chunk size, holding two independent slot lists (edit, validation) rather
        // than two separate dictionaries - keeps the "which pool for this chunk size" and "which
        // kind of checkout" concerns separately indexed instead of compounding them into one key.
        internal class DenseVoxelDataPool : IDisposable {
            private readonly int _chunkSize;
            private readonly List<DenseVoxelData> _editSlots;
            private readonly List<DenseVoxelData> _validationSlots;

            // Monotonic, not wall-clock - avoids Time.time precision/pausing weirdness and keeps
            // "longest since accessed" a pure ordering question. Shared across both slot lists
            // since eviction only ever compares slots within the same list (SlotsFor(isEdit)), so
            // there's no cross-pool contamination risk from sharing the counter.
            private long _accessCounter;

            internal DenseVoxelDataPool(int chunkSize, int initialEditPoolSize, int initialValidationPoolSize) {
                _chunkSize = chunkSize;

                _editSlots = new List<DenseVoxelData>();
                EnsureEditCapacity(initialEditPoolSize);

                _validationSlots = new List<DenseVoxelData>();
                EnsureValidationCapacity(initialValidationPoolSize);

                Debug.Log(
                    $"DenseVoxelDataPool [size {chunkSize}] constructed - edit slots: {_editSlots.Count}, " +
                    $"initial validation slots: {_validationSlots.Count}");
            }

            // Called from RegisterVoxelVolume whenever a volume registers at this chunk size whose
            // geometry (see ComputeEditPoolSize) demands more Edit-pool capacity than the pool
            // currently has. Grows only, never shrinks - same reasoning as EnsureValidationCapacity
            // below: a departing volume doesn't signal that the remaining volumes need less.
            // Appending new slots never touches any existing slot's identity, so this is safe to
            // call regardless of what's currently checked out.
            internal void EnsureEditCapacity(int minSize) {
                while (_editSlots.Count < minSize)
                    _editSlots.Add(new DenseVoxelData(_chunkSize));
            }

            // Called from RegisterVoxelVolume whenever a volume registers at this chunk size with
            // a larger ValidatesPerFrame than the pool currently supports. Grows only, never
            // shrinks - see the comment on RegisterVoxelVolume for why. Appending new slots never
            // touches any existing slot's identity (CurrentChunk/IsArrayInUse), so this is safe to
            // call regardless of what's currently checked out.
            internal void EnsureValidationCapacity(int minSize) {
                while (_validationSlots.Count < minSize)
                    _validationSlots.Add(new DenseVoxelData(_chunkSize));
            }

            private List<DenseVoxelData> SlotsFor(bool isEdit) => isEdit ? _editSlots : _validationSlots;

            internal NativeArray<VoxelData> GetOrUnpack(
                int dataSizeKey, GeoChunk chunk, NativeList<SparseVoxelData> sparseData, bool isEdit) {
                var slots = SlotsFor(isEdit);

                // Already resident for this exact chunk - hand it back without touching any other
                // slot. Still counts as an access for LRU purposes: a chunk being re-checked-out
                // while still resident is exactly the case LRU exists to protect from eviction.
                //
                // Correctness of this fast path depends on Pack keeping a chunk's residency in the
                // *other* pool in sync whenever it writes fresh sparse data for that chunk (see
                // SyncOtherPoolResidency below) - otherwise this would happily hand back a stale
                // pre-edit array for a chunk that's since been edited via the other pool.
                foreach (var slot in slots) {
                    if (slot.CurrentChunk != chunk)
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

                // Not resident anywhere in this pool - claim a slot and unpack into it. Two passes:
                // prefer a slot that's never held any chunk (grows the working set with zero
                // eviction), and only once every slot has been claimed at least once fall back to
                // reusing a free slot - specifically the one with the oldest LastAccessTick, so
                // whatever's been checked out most recently survives.
                var claimIndex = -1;

                for (var i = 0; i < slots.Count; i++) {
                    if (!slots[i].IsArrayInUse && slots[i].CurrentChunk == null) {
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
                    var evictedChunk = slot.CurrentChunk;
                    var wasNeverOccupied = evictedChunk == null;

                    slot.IsArrayInUse = true;
                    slot.CurrentChunk = chunk;
                    slot.LastAccessTick = ++_accessCounter;

                    ref var config = ref chunk.ParentGeoVolume.ConfigBlob.Value;

                    var unpackJob = new SparseToDenseVoxelDataJob {
                        ConfigBlob = chunk.ParentGeoVolume.ConfigBlob,
                        Voxels = slot.DenseVoxelArray,
                        SparseVoxels = sparseData
                    };
                    var jobHandle = unpackJob.Schedule(config.ChunkDataWidthSize, 1);
                    jobHandle.Complete();

                    if (isEdit) {
                        Log.Verbose(
                            $"DenseVoxelDataPool [size {dataSizeKey}, isEdit={isEdit}, slot {claimIndex}] " +
                            $"added chunk {chunk.ChunkCoord} - pool {(wasNeverOccupied ? "growing" : $"steady (evicted chunk {evictedChunk.ChunkCoord}, LRU)")}");
                    }

                    return slot.DenseVoxelArray;
                }

                // Every slot in use and none resident for this chunk - a genuine exhaustion, not
                // contention on a single shared slot. Both pools are sized per volume geometry at
                // RegisterVoxelVolume time, so this should be structurally impossible. Throw
                // instead of silently blocking, queuing, or handing back a different chunk's data -
                // if this ever fires, the caller (or the pool's sizing contract) has a real bug,
                // not something a bigger pool number papers over blindly.
                throw new InvalidOperationException(
                    $"DenseVoxelDataPool.GetOrUnpack (isEdit={isEdit}): all {slots.Count} slots in " +
                    $"use and none resident for chunk {chunk.ChunkCoord}. This should be structurally " +
                    "impossible under the current pool sizing - investigate the caller.");
            }

            internal void Pack(GeoChunk chunk, bool isEdit) {
                var slot = FindSlot(chunk, isEdit);

                if (slot == null) {
                    Debug.LogError(
                        $"PackVoxelArray - Trying to pack but chunk {chunk.ChunkCoord} has no resident slot (isEdit={isEdit})");

                    return;
                }

                if (!slot.IsArrayInUse) {
                    // Was falling through and packing anyway even though nothing currently has
                    // this array checked out - same "log then continue as if nothing happened"
                    // pattern the single-slot version had. Stop here instead.
                    Debug.LogError(
                        $"PackVoxelArray - Trying to pack but _isArrayInUse is false which is unexpected and bad");

                    return;
                }

                var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);

                // DensityRange is computed fresh here, single-threaded, from the dense array as it
                // currently stands - which already has any pending edits written into it by the
                // time Pack is called. This is the only place DensityRange gets computed.
                var packJob = new DenseToSparseVoxelDataJob {
                    Voxels = slot.DenseVoxelArray,
                    SparseVoxels = sparseData,
                    DensityRange = slot.DensityRange
                };
                var jobHandle = packJob.Schedule();
                jobHandle.Complete();

                slot.CurrentChunk.UpdateVoxelData(sparseData, slot.DensityRange[0]);
                sparseData.Dispose();

                SyncOtherPoolResidency(chunk, isEdit, slot.DenseVoxelArray);
            }

            // If `chunk` also happens to be resident (but not checked out) in the *other* pool -
            // e.g. it was LOD-validated a moment ago and its Validation-pool slot never got touched
            // since - that slot's dense array is now stale relative to the edit Pack just wrote.
            // Rather than invalidating it (CurrentChunk = null) and letting the next checkout there
            // pay for a full SparseToDenseVoxelDataJob re-unpack, copy the dense data we already
            // have in hand straight across - both pools' slots end up correct and still resident,
            // for the cost of one NativeArray.Copy instead of a job schedule+complete later.
            private void SyncOtherPoolResidency(GeoChunk chunk, bool packedIsEdit, NativeArray<VoxelData> freshVoxels) {
                foreach (var slot in SlotsFor(!packedIsEdit)) {
                    if (slot.CurrentChunk != chunk)
                        continue;

                    if (slot.IsArrayInUse) {
                        // Shouldn't be reachable under today's fully-synchronous checkout model -
                        // nothing else can be holding a checkout on this chunk in the other pool
                        // while an edit is being packed for it on the same thread. Logged rather
                        // than silently ignored in case that assumption stops holding later (e.g.
                        // once P1 makes checkouts span frames).
                        Debug.LogError(
                            $"SyncOtherPoolResidency: chunk {chunk.ChunkCoord} is checked out in " +
                            "the other pool while being packed - can't safely overwrite a slot " +
                            "that's in use.");

                        return;
                    }

                    NativeArray<VoxelData>.Copy(freshVoxels, slot.DenseVoxelArray);

                    return;
                }
            }

            internal void Release(GeoChunk chunk, bool isEdit) {
                var slot = FindSlot(chunk, isEdit);

                if (slot == null) {
                    ConsoleLogger.PrintError(
                        $"MarchingCubes Manager does not have a denseVoxelData Array for chunk {chunk.ChunkCoord} (isEdit={isEdit})");

                    return;
                }

                slot.IsArrayInUse = false;
            }

            // Read-only peek: does this pool currently have chunk resident, in either slot list,
            // regardless of IsArrayInUse? Not a checkout - doesn't touch IsArrayInUse or
            // LastAccessTick. Safe under today's synchronous Schedule()+Complete() model, where a
            // slot's CurrentChunk is only ever set once its data is fully unpacked/written; once
            // handles can live across frames (P1), this will need to additionally check that the
            // slot's job handle is complete before handing back its array.
            internal bool TryGetResident(GeoChunk chunk, out NativeArray<VoxelData> voxels) {
                foreach (var slot in _editSlots) {
                    if (slot.CurrentChunk != chunk)
                        continue;

                    voxels = slot.DenseVoxelArray;

                    return true;
                }

                foreach (var slot in _validationSlots) {
                    if (slot.CurrentChunk != chunk)
                        continue;

                    voxels = slot.DenseVoxelArray;

                    return true;
                }

                voxels = default;

                return false;
            }

            // Linear scan over a small list - cheaper and simpler than threading an opaque slot
            // handle back out through GetOrUnpackVoxelArray's callers to save what's noise-level
            // cost at this N.
            private DenseVoxelData FindSlot(GeoChunk chunk, bool isEdit) {
                foreach (var slot in SlotsFor(isEdit)) {
                    if (slot.CurrentChunk == chunk)
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
            internal GeoChunk CurrentChunk;
            internal long LastAccessTick;

            internal DenseVoxelData(int chunkSize, Allocator allocator = Allocator.Persistent) {
                var cs = chunkSize + 3;
                DenseVoxelArray = new NativeArray<VoxelData>(cs * cs * cs, allocator);
                DensityRange = new NativeArray<DensityRange>(1, allocator);
                IsArrayInUse = false;
                CurrentChunk = null;
            }

            internal DenseVoxelData() {
                DenseVoxelArray = default;
                DensityRange = default;
                IsArrayInUse = false;
                CurrentChunk = null;
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
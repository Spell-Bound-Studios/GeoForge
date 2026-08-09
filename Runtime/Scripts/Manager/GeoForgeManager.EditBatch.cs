// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager : MonoBehaviour {
        // Lets GeoChunk.HandleResolvedVoxelEdits know whether it's being invoked as part of a
        // DistributeVoxelEdits batch (see BeginEditBatch/EndEditBatch below) or as a standalone
        // edit outside that path. Batched chunks register themselves via
        // RegisterPendingEditRelease instead of completing/releasing immediately, so every
        // chunk's march jobs can be scheduled before any of them block on a shared Complete().
        internal bool IsBatchingEdits { get; private set; }

        private readonly List<GeoChunkEngine> _pendingEditReleases = new();

        // Call before scheduling a batch of chunk edits that should share one Complete() call.
        // Must be paired with EndEditBatch, wrapped in try/finally by the caller - an exception
        // mid-batch would otherwise leave IsBatchingEdits stuck true, silently breaking every
        // subsequent single-chunk edit until another DistributeVoxelEdits call reset it.
        internal void BeginEditBatch() {
            IsBatchingEdits = true;
            _pendingEditReleases.Clear();
        }

        // Called by GeoChunk.HandleResolvedVoxelEdits instead of completing/releasing immediately,
        // for any chunk whose edit was actually applied (ApplyVoxelEdits returned true) while a
        // batch is in progress. A chunk whose edit produced no real change never reaches this -
        // SimpleGeoEditStore.Delta doesn't fire OnGeoEditChanged for an empty changes list, so
        // HandleResolvedVoxelEdits is never even called for it.
        internal void RegisterPendingEditRelease(GeoChunkEngine chunkEngine) => _pendingEditReleases.Add(chunkEngine);

        // Completes every march/transition job scheduled by this batch's chunks in one shared
        // Complete() call, then releases each registered chunk's Edit-pool slot - only now that
        // nothing is left in flight reading from them. Safe to call even if the batch scheduled
        // zero chunks (e.g. every edit in it was a no-op).
        internal void EndEditBatch() {
            CompleteAndApplyMarchingCubesJobs();

            foreach (var chunk in _pendingEditReleases)
                ReleaseVoxelArray(chunk.ParentGeoVolume.ConfigBlob.Value.ChunkSize, chunk, isEdit: true);

            _pendingEditReleases.Clear();
            IsBatchingEdits = false;
        }
    }
}
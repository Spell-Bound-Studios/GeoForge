// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Simple implementation of IGeoEditStore for samples and small projects.
    /// </summary>
    public class SimpleGeoEditStore : IGeoEditStore {
        private readonly GeoForgeChunkData _chunkData;

        public SimpleGeoEditStore(GeoForgeChunkData chunkData = null) {
            chunkData ??= new GeoForgeChunkData();
            _chunkData = chunkData;
        }

        public event Action<List<(int, VoxelData)>> OnGeoEditChanged;
        public Func<int, VoxelData> DefaultVoxelDataFunc { get; set; }

        public bool TryRead(int idx, out VoxelData voxelData) {
            if (_chunkData.TryReadEdit(idx, out voxelData))
                return true;

            voxelData = DefaultVoxelDataFunc?.Invoke(idx) ?? new VoxelData();

            return false;
        }

        public void Write(List<(int, VoxelData)> voxelDatas) {
            var changes = new List<(int, VoxelData)>(voxelDatas.Count);

            foreach (var (idx, voxelData) in voxelDatas) {
                TryRead(idx, out var current);

                if (voxelData == current)
                    continue;

                _chunkData.WriteEdit(idx, voxelData);
                changes.Add((idx, voxelData));
            }

            NotifyGeoEditsChanged(changes);
        }

        public void PassVoxelEditOperation(VoxelEditOperation operation) {
            var changes = new List<(int, VoxelData)>(operation.Deltas.Length);

            foreach (var voxelDelta in operation.Deltas) {
                if (!_chunkData.TryReadEdit(voxelDelta.Index, out var voxelData))
                    voxelData = DefaultVoxelDataFunc(voxelDelta.Index);

                var wasFull = voxelData.Density >= 0;
                var wasMature = voxelData.IsMature();
                var existingMatIndex = voxelData.GetPlainMatIndex();

                // Gate: a voxel that's already full and whose current material this operation isn't
                // permitted to affect (e.g. Impervious, or below the calling tool's tier) rejects
                // ALL density changes outright — additions as well as subtractions. Additions onto
                // empty/Null voxels are never gated (wasFull is false there), and additions onto an
                // already-full, ALLOWED voxel still proceed normally below.
                if (wasFull && !operation.IsAllowed(existingMatIndex)) {
                    continue;
                }

                var density = (sbyte)Mathf.Clamp(
                    voxelData.Density + voxelDelta.DensityDelta,
                    sbyte.MinValue,
                    sbyte.MaxValue);

                var isFull = density >= 0;

                byte matIndex;
                bool isMature;

                if (!isFull) {
                    // Core invariant: any voxel ending below threshold is the null/sentinel
                    // material, no exceptions.
                    matIndex = VoxelData.NullSentinelValue;
                    isMature = false;
                }
                else if (!wasFull && isFull) {
                    // Material is only ever claimed at the empty -> full crossing - this is new
                    // growth, so it always starts immature regardless of anything about the
                    // previous (empty) voxel.
                    matIndex = operation.MaterialIndex;
                    isMature = false;
                }
                else {
                    // Already solid on both sides of this delta - material AND maturity persist
                    // unchanged. existingMatIndex is already demodulated (GetPlainMatIndex()
                    // strips the mature bit), so maturity has to be carried forward separately via
                    // wasMature or it silently gets lost on every solid-to-solid edit - e.g.
                    // AddSphere onto an already-full, already-mature region would otherwise flip
                    // everything it touches back to immature.
                    matIndex = existingMatIndex;
                    isMature = wasMature;
                }

                var resolved = isMature
                        ? VoxelData.CreateMature(density, matIndex)
                        : VoxelData.CreateImmature(density, matIndex);

                // If density didn't actually change (e.g. clamp saturated at the same extreme
                // despite a nonzero delta), and maturity/material also didn't change, resolved
                // comes out identical to voxelData - nothing new to write.
                if (resolved == voxelData)
                    continue;

                _chunkData.WriteEdit(voxelDelta.Index, resolved);
                changes.Add((voxelDelta.Index, resolved));
            }

            NotifyGeoEditsChanged(changes);
        }

        public IEnumerable<(int, VoxelData)> ReadAllEdits() {
            foreach (var (idx, voxelData) in _chunkData.Edits)
                yield return (idx, voxelData);
        }

        #region Notify Helpers

        private void NotifyGeoEditsChanged(List<(int, VoxelData)> changes) {
            if (changes.Count == 0)
                return;

            OnGeoEditChanged?.Invoke(changes);
        }

        #endregion Notify Helpers
    }
}
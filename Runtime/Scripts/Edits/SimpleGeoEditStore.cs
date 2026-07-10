// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge {
    public class SimpleGeoEditStore : IGeoEditStore {
        private GeoForgeChunkData _chunkData;

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

        public void Delta(List<VoxelDelta> newDeltas) {
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

                var resolved = Mathf.Abs(newDelta.densityDelta) > 0 ?
                        VoxelData.CreateImmature(density, matIndex) :
                        VoxelData.CreateMature(density, matIndex);

                if (resolved == voxelData)
                    continue;

                _chunkData.WriteEdit(newDelta.index, resolved);
                changes.Add((newDelta.index, resolved));
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
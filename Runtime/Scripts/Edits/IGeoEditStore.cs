// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Interface Contract for GeoForge Edits
    /// </summary>
    public interface IGeoEditStore {
        event Action<List<(int, VoxelData)>> OnGeoEditChanged;
        Func<int, VoxelData> DefaultVoxelDataFunc { get; set; }

        bool TryRead(int index, out VoxelData voxelData);

        void Write(List<(int, VoxelData)> voxelDatas);

        void Delta(List<VoxelDelta> voxelEdits);

        IEnumerable<(int, VoxelData)> ReadAllEdits();

        void ClearAllEdits();
    }
}
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

        bool TryRead(int idx, out VoxelData voxelData);

        void Write(List<(int, VoxelData)> voxelDatas);

        void Delta(VoxelEditOperation operation);

        IEnumerable<(int, VoxelData)> ReadAllEdits();
    }
}
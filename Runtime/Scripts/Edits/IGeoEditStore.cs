// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Interface Contract for GeoForge Edits
    /// </summary>
    public interface IGeoEditStore {
        event Action<List<int>> OnGeoEditChanged;
        Func<int, VoxelData> DefaultVoxelDataFunc { get; }
        
        bool TryRead(int index, out VoxelData voxelData);
        
        void Write(List<(int, VoxelData)> voxelDatas);

        void Delta(List<(VoxelEdit, VoxelData)> voxelEdits);

        IEnumerable<(int, VoxelData)> ReadAllEdits();

        void ClearAllEdits();

    }
}
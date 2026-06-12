// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;

namespace Spellbound.GeoForge {
    public class SimpleGeoEditStore : IGeoEditStore {
        public event Action<List<(int, VoxelData)>> OnGeoEditChanged;
        public Func<int, VoxelData> DefaultVoxelDataFunc { get; set; }
        public bool TryRead(int idx, out VoxelData voxelData) => throw new NotImplementedException();

        public void Write(List<(int, VoxelData)> voxelDatas) => throw new NotImplementedException();

        public void Delta(List<VoxelDelta> voxelEdits) => throw new NotImplementedException();

        public IEnumerable<(int, VoxelData)> ReadAllEdits() => throw new NotImplementedException();

        public void ClearAllEdits() => throw new NotImplementedException();
    }
}
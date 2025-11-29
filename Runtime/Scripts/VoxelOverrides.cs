// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;

namespace Spellbound.MarchingCubes {
    public class VoxelOverrides {
        private Dictionary<int, VoxelData> _xOverrides;
        private Dictionary<int, VoxelData> _yOverrides;
        private Dictionary<int, VoxelData> _zOverrides;
        private bool _hasOverrides;
        public bool HasAnyOverrides => _hasOverrides;

        public VoxelOverrides() {
            _hasOverrides = false;
        }

        public void AddOverride(Axis axis, int sliceIndex, VoxelData voxelData) {
            _hasOverrides = true;

            switch (axis) {
                case Axis.X:
                    _xOverrides ??= new Dictionary<int, VoxelData>();
                    _xOverrides[sliceIndex] = voxelData;

                    break;
                case Axis.Y:
                    _yOverrides ??= new Dictionary<int, VoxelData>();
                    _yOverrides[sliceIndex] = voxelData;

                    break;
                case Axis.Z:
                    _zOverrides ??= new Dictionary<int, VoxelData>();
                    _zOverrides[sliceIndex] = voxelData;

                    break;
            }
        }

        public bool HasOverride(int x, int y, int z) {
            if (!_hasOverrides) return false;

            return (_yOverrides?.ContainsKey(y) ?? false) ||
                   (_xOverrides?.ContainsKey(x) ?? false) ||
                   (_zOverrides?.ContainsKey(z) ?? false);
        }

        public bool TryGetOverride(int x, int y, int z, out VoxelData overrideVoxel) {
            if (!_hasOverrides) {
                overrideVoxel = default;

                return false;
            }

            if (_yOverrides?.TryGetValue(y, out overrideVoxel) ?? false)
                return true;

            if (_xOverrides?.TryGetValue(x, out overrideVoxel) ?? false)
                return true;

            if (_zOverrides?.TryGetValue(z, out overrideVoxel) ?? false)
                return true;

            overrideVoxel = default;

            return false;
        }

        public void Clear() {
            _xOverrides?.Clear();
            _yOverrides?.Clear();
            _zOverrides?.Clear();
            _hasOverrides = false;
        }

        public void CopyToNativeHashMaps(
            out NativeHashMap<int, VoxelData> xOverrides,
            out NativeHashMap<int, VoxelData> yOverrides,
            out NativeHashMap<int, VoxelData> zOverrides,
            Allocator allocator = Allocator.TempJob) {
            // Create hash maps with appropriate capacity
            xOverrides = new NativeHashMap<int, VoxelData>(
                _xOverrides?.Count ?? 0, allocator);

            yOverrides = new NativeHashMap<int, VoxelData>(
                _yOverrides?.Count ?? 0, allocator);

            zOverrides = new NativeHashMap<int, VoxelData>(
                _zOverrides?.Count ?? 0, allocator);

            // Copy data if dictionaries exist
            if (_xOverrides != null)
                foreach (var kvp in _xOverrides)
                    xOverrides.Add(kvp.Key, kvp.Value);

            if (_yOverrides != null)
                foreach (var kvp in _yOverrides)
                    yOverrides.Add(kvp.Key, kvp.Value);

            if (_zOverrides != null)
                foreach (var kvp in _zOverrides)
                    zOverrides.Add(kvp.Key, kvp.Value);
        }
    }
}
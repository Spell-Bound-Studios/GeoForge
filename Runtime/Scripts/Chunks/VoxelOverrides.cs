// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public class VoxelOverrides {
        private Dictionary<int, VoxelData> _xOverrides;
        private Dictionary<int, VoxelData> _yOverrides;
        private Dictionary<int, VoxelData> _zOverrides;
        private Dictionary<Vector3Int, VoxelData> _pointOverrides;
        private bool _hasOverrides;
        public bool HasAnyOverrides => _hasOverrides;

        public VoxelOverrides() {
            _hasOverrides = false;
        }

        public void AddPlaneOverride(Axis axis, int sliceIndex, VoxelData voxelData) {
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

        public void AddPointOverride(Vector3Int position, VoxelData voxelData) {
            _hasOverrides = true;
            _pointOverrides ??= new Dictionary<Vector3Int, VoxelData>();
            _pointOverrides[position] = voxelData;
        }

        public bool HasOverride(Vector3Int position) {
            if (!_hasOverrides) return false;

            return (_yOverrides?.ContainsKey(position.y) ?? false) ||
                   (_xOverrides?.ContainsKey(position.x) ?? false) ||
                   (_zOverrides?.ContainsKey(position.z) ?? false) ||
                   (_pointOverrides?.ContainsKey(position) ?? false);
        }

        public bool TryGetOverride(Vector3Int position, out VoxelData overrideVoxel) {
            if (!_hasOverrides) {
                overrideVoxel = default;

                return false;
            }

            if (_pointOverrides?.TryGetValue(position, out overrideVoxel) ?? false)
                return true;

            if (_yOverrides?.TryGetValue(position.y, out overrideVoxel) ?? false)
                return true;

            if (_xOverrides?.TryGetValue(position.x, out overrideVoxel) ?? false)
                return true;

            if (_zOverrides?.TryGetValue(position.z, out overrideVoxel) ?? false)
                return true;

            overrideVoxel = default;

            return false;
        }

        public void Clear() {
            _xOverrides?.Clear();
            _yOverrides?.Clear();
            _zOverrides?.Clear();
            _pointOverrides?.Clear();
            _hasOverrides = false;
        }

        public void CopyToNativeHashMaps(
            out NativeHashMap<int, VoxelData> xOverrides,
            out NativeHashMap<int, VoxelData> yOverrides,
            out NativeHashMap<int, VoxelData> zOverrides,
            out NativeHashMap<int3, VoxelData> pointOverrides,
            Allocator allocator = Allocator.Persistent) {
            // Create hash maps with appropriate capacity
            xOverrides = new NativeHashMap<int, VoxelData>(
                _xOverrides?.Count ?? 0, allocator);

            yOverrides = new NativeHashMap<int, VoxelData>(
                _yOverrides?.Count ?? 0, allocator);

            zOverrides = new NativeHashMap<int, VoxelData>(
                _zOverrides?.Count ?? 0, allocator);

            pointOverrides = new NativeHashMap<int3, VoxelData>(
                _pointOverrides?.Count ?? 0, allocator);

            // Copy data if dictionaries exist
            if (_xOverrides != null) {
                foreach (var kvp in _xOverrides)
                    xOverrides.Add(kvp.Key, kvp.Value);
            }

            if (_yOverrides != null) {
                foreach (var kvp in _yOverrides)
                    yOverrides.Add(kvp.Key, kvp.Value);
            }

            if (_zOverrides != null) {
                foreach (var kvp in _zOverrides)
                    zOverrides.Add(kvp.Key, kvp.Value);
            }

            if (_pointOverrides != null) {
                foreach (var kvp in _pointOverrides) {
                    var position = new int3(kvp.Key.x, kvp.Key.y, kvp.Key.z);
                    pointOverrides.Add(position, kvp.Value);
                }
            }
        }
    }
}
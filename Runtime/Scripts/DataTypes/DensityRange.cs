// Copyright 2026 Spellbound Studio Inc.

namespace Spellbound.GeoForge {
    /// <summary>
    /// Determines if a GeoForge Chunk can skip marching its cubes.
    /// If all the voxel densities are above zero or all are below zero, we don't need to march the cubes to know no
    /// mesh will be required.
    /// </summary>
    public struct DensityRange {
        private sbyte _min;
        private sbyte _max;
        private bool _isSkippable;

        public DensityRange(sbyte min, sbyte max) {
            _min = min;
            _max = max;
            _isSkippable = _min >= 0 || _max < 0;
        }

        public void Encapsulate(sbyte density) {
            if (!_isSkippable)
                return;

            if (density < _min) _min = density;
            if (density > _max) _max = density;
            _isSkippable = _min >= 0 || _max < 0;
        }

        public bool IsSkippable() => _isSkippable;
    }
}
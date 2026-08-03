// Copyright 2026 Spellbound Studio Inc.

namespace Spellbound.GeoForge {
    /// <summary>
    /// Indication of if a region of voxel data can skip marching its cubes or not
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
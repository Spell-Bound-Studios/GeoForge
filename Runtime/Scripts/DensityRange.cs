// Copyright 2025 Spellbound Studio Inc.

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Indication of if a region of voxel data can skip marching its cubes or not
    /// </summary>
    public struct DensityRange {
        private byte _min;
        private byte _max;
        private bool _isSkippable;

        public DensityRange(byte min, byte max) {
            _min = min;
            _max = max;
            _isSkippable = _min >= McStaticHelper.DensityThreshold || _max < McStaticHelper.DensityThreshold;
        }

        public void Encapsulate(byte density) {
            if (!_isSkippable)
                return;

            if (density < _min) _min = density;
            if (density > _max) _max = density;
            _isSkippable = _min >= McStaticHelper.DensityThreshold || _max < McStaticHelper.DensityThreshold;
        }

        public bool IsSkippable() => _isSkippable;
    }
}
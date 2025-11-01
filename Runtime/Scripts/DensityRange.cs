// Copyright 2025 Spellbound Studio Inc.

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Indication of if a region of voxel data can skip marching its cubes or not
    /// </summary>
    public struct DensityRange {
        private byte _min;
        private byte _max;
        private byte _densityThreshold;
        private bool _isSkippable;

        public DensityRange(byte min, byte max, byte densityThreshold) {
            _min = min;
            _max = max;
            _densityThreshold = densityThreshold;
            _isSkippable = _min >= _densityThreshold || _max < _densityThreshold;
        }

        public void Encapsulate(byte density) {
            if (!_isSkippable)
                return;

            if (density < _min) _min = density;
            if (density > _max) _max = density;
            _isSkippable = _min >= _densityThreshold || _max < _densityThreshold;
        }

        public bool IsSkippable() => _isSkippable;
    }
}
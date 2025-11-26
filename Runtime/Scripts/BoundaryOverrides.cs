using UnityEngine;

namespace Spellbound.MarchingCubes {
    [System.Serializable]
    public struct BoundaryOverrides {
        public enum Axis {X, Y, Z}
        public enum BoundaryDirection { Min, Max }
        
        public Axis axis;
        public BoundaryDirection boundaryDirection;
        public VoxelData VoxelData;  

    }
}


// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(menuName = "Spellbound/MarchingCubes/BoundaryOverrides")]
    public class BoundaryOverrides : ScriptableObject {
        [SerializeField] private List<BoundaryOverride> BoundaryOverridesList = new();

        public List<BoundaryOverrideRuntime> GetBoundaryOverrides() {
            var runtimeList = new List<BoundaryOverrideRuntime>();

            foreach (var bo in BoundaryOverridesList) {
                var voxelData = new VoxelData {
                    Density = bo.boundaryType == BoundaryType.Closed ? byte.MaxValue : byte.MinValue,
                    MaterialIndex = bo.materialType
                };

                runtimeList.Add(new BoundaryOverrideRuntime {
                    Axis = bo.axis,
                    Side = bo.side,
                    VoxelData = voxelData
                });
            }

            return runtimeList;
        }
    }

    public enum Axis {
        X,
        Y,
        Z
    }

    public enum Side {
        Min,
        Max
    }

    public enum BoundaryType {
        Closed,
        Open
    }

    [System.Serializable]
    public struct BoundaryOverride {
        public Axis axis;
        public Side side;
        public BoundaryType boundaryType;
        public byte materialType;
    }

    public struct BoundaryOverrideRuntime {
        public Axis Axis;
        public Side Side;
        public VoxelData VoxelData;
    }
}
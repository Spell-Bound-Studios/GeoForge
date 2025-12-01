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
        
        public VoxelOverrides BuildChunkOverrides(Vector3Int chunkCoord, Vector3Int sizeInChunks, int chunkSize) {
            var overrides = new VoxelOverrides();
    
            // Convert back to x,y,z indices for boundary logic
            var offset = new Vector3Int(sizeInChunks.x / 2, sizeInChunks.y / 2, sizeInChunks.z / 2);
            var indices = chunkCoord + offset;
    
            foreach (var boundary in GetBoundaryOverrides()) {
                var slices = new List<int>();
        
                switch (boundary.Axis) {
                    case Axis.X:
                        if (indices.x == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }
                        else if (indices.x == sizeInChunks.x - 1 && boundary.Side == Side.Max) {
                            slices.Add(chunkSize + 1);
                            slices.Add(chunkSize + 2);
                        }
                        break;
                
                    case Axis.Y:
                        if (indices.y == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }
                        else if (indices.y == sizeInChunks.y - 1 && boundary.Side == Side.Max) {
                            slices.Add(chunkSize + 1);
                            slices.Add(chunkSize + 2);
                        }
                        break;
                
                    case Axis.Z:
                        if (indices.z == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }
                        else if (indices.z == sizeInChunks.z - 1 && boundary.Side == Side.Max) {
                            slices.Add(chunkSize + 1);
                            slices.Add(chunkSize + 2);
                        }
                        break;
                }
        
                foreach (var slice in slices) {
                    overrides.AddPlaneOverride(boundary.Axis, slice, boundary.VoxelData);
                }
            }
    
            return overrides;
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
// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(menuName = "Spellbound/MarchingCubes/BoundaryOverrides")]
    public class BoundaryOverrides : ScriptableObject {
        [Tooltip("Full list of boundaries. Note 6 of them one on each face will fully constrain the volume boundaries"),
         SerializeField]
        private List<BoundaryOverride> BoundaryOverridesList = new();

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

        public VoxelOverrides BuildChunkOverrides(
            Vector3Int chunkCoord, BlobAssetReference<VolumeConfigBlobAsset> configBlob) {
            var overrides = new VoxelOverrides();

            // Convert back to x,y,z indices for boundary logic
            var offset = new Vector3Int(configBlob.Value.SizeInChunks.x / 2, configBlob.Value.SizeInChunks.y / 2,
                configBlob.Value.SizeInChunks.z / 2);
            var indices = chunkCoord + offset;

            foreach (var boundary in GetBoundaryOverrides()) {
                var slices = new List<int>();

                switch (boundary.Axis) {
                    case Axis.X:
                        if (indices.x == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }
                        else if (indices.x == configBlob.Value.SizeInChunks.x - 1 && boundary.Side == Side.Max) {
                            slices.Add(configBlob.Value.ChunkSize + 1);
                            slices.Add(configBlob.Value.ChunkSize + 2);
                        }

                        break;

                    case Axis.Y:
                        if (indices.y == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }
                        else if (indices.y == configBlob.Value.SizeInChunks.y - 1 && boundary.Side == Side.Max) {
                            slices.Add(configBlob.Value.ChunkSize + 1);
                            slices.Add(configBlob.Value.ChunkSize + 2);
                        }

                        break;

                    case Axis.Z:
                        if (indices.z == 0 && boundary.Side == Side.Min) {
                            slices.Add(0);
                            slices.Add(1);
                        }
                        else if (indices.z == configBlob.Value.SizeInChunks.z - 1 && boundary.Side == Side.Max) {
                            slices.Add(configBlob.Value.ChunkSize + 1);
                            slices.Add(configBlob.Value.ChunkSize + 2);
                        }

                        break;
                }

                foreach (var slice in slices) overrides.AddPlaneOverride(boundary.Axis, slice, boundary.VoxelData);
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
        [Tooltip("Boundary is in the direction of which axis")]
        public Axis axis;

        [Tooltip("Boundary is in the min or the max direction of the axis")]
        public Side side;

        [Tooltip("Open for empty/air this will be outside of the mesh. Closed for inside the mesh")]
        public BoundaryType boundaryType;

        [Tooltip("Material for the boundaries. " +
                 "Refer to MarchingCubeManager for what index corresponds to what material.")]
        public byte materialType;
    }

    public struct BoundaryOverrideRuntime {
        public Axis Axis;
        public Side Side;
        public VoxelData VoxelData;
    }
}
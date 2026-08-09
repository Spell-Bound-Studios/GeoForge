// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Defines the boundaries of a GeoForge Volume.
    /// This is useful/neccesary because if for example the volume is meant to be a closed shape or shapes,
    /// then the voxels on the outer boundaries of the volume must be kept "empty".
    /// </summary>
    [CreateAssetMenu(menuName = "Spellbound/GeoForge/BoundaryOverrides")]
    public class BoundaryOverrides : ScriptableObject {
        [Tooltip(
             "Full list of boundaries. Note 6 of them one on each face will fully constrain the geoVolume boundaries"),
         SerializeField]
        private List<BoundaryOverride> BoundaryOverridesList = new();

        private List<BoundaryOverrideRuntime> GetBoundaryOverrides() {
            var runtimeList = new List<BoundaryOverrideRuntime>();

            foreach (var bo in BoundaryOverridesList) {
                var closed = bo.boundaryType is BoundaryType.Closed or BoundaryType.MatureClosed;
                var mature = bo.boundaryType is BoundaryType.MatureClosed or BoundaryType.MatureOpen;
                
                var voxelData = mature
                    ? VoxelData.CreateMature(closed ? sbyte.MaxValue : sbyte.MinValue, bo.materialType)
                    : VoxelData.CreateImmature(closed ? sbyte.MaxValue : sbyte.MinValue, bo.materialType);

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

    /// <summary>
    /// Axis and Side identify a specific boundary of the Volume. There are 6 combinations in total, like the 6 faces
    /// of a cube.
    /// </summary>
    internal enum Axis {
        X,
        Y,
        Z
    }

    /// <summary>
    /// Axis and Side identify a specific boundary of the Volume. There are 6 combinations in total, like the 6 faces
    /// of a cube.
    /// </summary>
    internal enum Side {
        Min,
        Max
    }

    /// <summary>
    /// BoundaryType indicates what density and material index to set the boundary voxels to.
    /// Closed means the voxels are full, Open means the Voxels are empty.
    /// Mature Closed/Open means the material index should be flagged as Mature. 
    /// </summary>
    internal enum BoundaryType {
        Closed,
        Open,
        MatureClosed,
        MatureOpen
    }

    /// <summary>
    /// A single Boundary Override fully describes what to force one boundary of a GeoForgeVolume's voxels to.
    /// </summary>
    [System.Serializable]
    internal struct BoundaryOverride {
        [Tooltip("Boundary is in the direction of which axis")]
        internal Axis axis;

        [Tooltip("Boundary is in the min or the max direction of the axis")]
        internal Side side;

        [Tooltip("Open for empty/air this will be outside of the mesh. Closed for inside the mesh")]
        internal BoundaryType boundaryType;

        [Tooltip("Material for the boundaries. " +
                 "Refer to MarchingCubeManager for what index corresponds to what material.")]
        internal byte materialType;
    }

    /// <summary>
    /// Runtime representation of a BoundaryOverride.
    /// Has combined the rules into a specific VoxelData to be copied into all the Boundary Voxels.
    /// </summary>
    internal struct BoundaryOverrideRuntime {
        internal Axis Axis;
        internal Side Side;
        internal VoxelData VoxelData;
    }
}
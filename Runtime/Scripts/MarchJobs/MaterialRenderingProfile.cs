// Copyright 2026 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Defines how GeoForgeManager should render and mesh a set of materials: which render
    /// Material to use, and how to schedule the main-region and transition-region marching cubes
    /// jobs. Subclass this and implement both Schedule methods to plug in a specific meshing
    /// strategy (e.g. the existing blended matA/matB scheme, or a future hard-edge barycentric
    /// scheme) - GeoForgeManager calls through whichever profile is assigned without needing to
    /// know which concrete job struct is actually running.
    /// </summary>
    public abstract class MaterialRenderingProfile : ScriptableObject {
        [SerializeField] private Material material;

        public Material Material => material;

        public abstract JobHandle ScheduleMarchingCubes(
            BlobAssetReference<McTablesBlobAsset> tablesBlob,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> voxelArray,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeReference<Bounds> computedBounds,
            int lod,
            int3 start,
            JobHandle dependency = default);

        public abstract JobHandle ScheduleTransitionMarchingCubes(
            BlobAssetReference<McTablesBlobAsset> tablesBlob,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> voxelArray,
            NativeList<MeshingVertexData> transitionMeshingVertexData,
            NativeList<int> transitionTriangles,
            NativeArray<int2> transitionRanges,
            int lod,
            int3 start,
            JobHandle dependency = default);
    }
}
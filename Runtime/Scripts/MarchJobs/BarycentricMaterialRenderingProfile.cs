// Copyright 2026 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// MaterialRenderingProfile for the FlatShaded/Barycentric hard-edge material scheme.
    /// Schedules FlatBaryMarchJob and TransFlatBaryMarchJob, which always flat-shade and never
    /// blend materials - each vertex's material is simply the "full" voxel on the edge it sits on,
    /// packed per-triangle for the shader to select per-fragment with a hard boundary.
    /// </summary>
    [CreateAssetMenu(menuName = "Spellbound/GeoForge/Material Rendering Profile (Flat Bary)",
        fileName = "FlatBaryMaterialRenderingProfile")]
    public class BarycentricMaterialRenderingProfile : MaterialRenderingProfile {
        public override JobHandle ScheduleMarchingCubes(
            BlobAssetReference<McTablesBlobAsset> tablesBlob,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> voxelArray,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeReference<Bounds> computedBounds,
            int lod,
            int3 start,
            JobHandle dependency = default) {
            // isFlatShadedLookUp is unused here - this scheme is unconditionally flat-shaded for
            // every triangle, so there's no per-material "is this one flat" decision to make.
            var job = new MarchingCubeJob {
                TablesBlob = tablesBlob,
                ConfigBlob = configBlob,
                VoxelArray = voxelArray,
                Vertices = vertices,
                Triangles = triangles,
                ComputedBounds = computedBounds,
                Lod = lod,
                Start = start
            };

            return job.Schedule(dependency);
        }

        public override JobHandle ScheduleTransitionMarchingCubes(
            BlobAssetReference<McTablesBlobAsset> tablesBlob,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> voxelArray,
            NativeList<MeshingVertexData> transitionMeshingVertexData,
            NativeList<int> transitionTriangles,
            NativeArray<int2> transitionRanges,
            int lod,
            int3 start,
            JobHandle dependency = default) {
            var job = new TransitionMarchingCubeJob {
                TablesBlob = tablesBlob,
                ConfigBlob = configBlob,
                VoxelArray = voxelArray,
                TransitionMeshingVertexData = transitionMeshingVertexData,
                TransitionTriangles = transitionTriangles,
                TransitionRanges = transitionRanges,
                Lod = lod,
                Start = start
            };

            return job.Schedule(dependency);
        }
    }
}
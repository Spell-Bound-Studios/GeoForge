// Copyright 2026 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Default MaterialRenderingProfile - schedules the existing blended matA/matB
    /// MarchingCubeJob and TransitionMarchingCubeJob, unchanged. Use this unless a material set
    /// specifically needs a different meshing strategy.
    /// </summary>
    [CreateAssetMenu(menuName = "Spellbound/GeoForge/Material Rendering Profile (Default)", fileName = "DefaultMaterialRenderingProfile")]
    public class DefaultMaterialRenderingProfile : MaterialRenderingProfile {
        public override JobHandle ScheduleMarchingCubes(
            BlobAssetReference<McTablesBlobAsset> tablesBlob,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<bool> isFlatShadedLookUp,
            NativeArray<VoxelData> voxelArray,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            int lod,
            int3 start,
            JobHandle dependency = default) {
            var job = new MarchingCubeJob {
                TablesBlob = tablesBlob,
                ConfigBlob = configBlob,
                IsFlatShadedLookUp = isFlatShadedLookUp,
                VoxelArray = voxelArray,
                Vertices = vertices,
                Triangles = triangles,
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
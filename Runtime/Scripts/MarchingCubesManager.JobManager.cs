using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public partial class MarchingCubesManager : MonoBehaviour
    {
        private JobHandle _combinedJobHandle;
        private Dictionary<OctreeNode, MarchJobData> _pendingMarchJobData = new();
        private Dictionary<OctreeNode, TransitionMarchJobData> _pendingTransitionMarchJobData = new();

        private Dictionary<OctreeNode, Vector3Int> _nodeToChunkCoord = new();
        public void RegisterMarchJob(
            OctreeNode node,
            JobHandle jobHandle,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            Vector3Int chunkCoord) {
            _combinedJobHandle = JobHandle.CombineDependencies(_combinedJobHandle, jobHandle);

            _pendingMarchJobData[node] = new MarchJobData {
                Vertices = vertices,
                Triangles = triangles
            };

            _nodeToChunkCoord[node] = chunkCoord;
        }

        public void RegisterTransitionJob(
            OctreeNode node,
            JobHandle jobHandle,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeArray<int2> ranges,
            Vector3Int chunkCoord) {
            _combinedJobHandle = JobHandle.CombineDependencies(_combinedJobHandle, jobHandle);

            _pendingTransitionMarchJobData[node] = new TransitionMarchJobData {
                Vertices = vertices,
                Triangles = triangles,
                Ranges = ranges
            };

            if (!_nodeToChunkCoord.ContainsKey(node)) {
                _nodeToChunkCoord[node] = chunkCoord;
            }
        }

        public void CompleteAndApplyMarchingCubesJobs() {
            ReleaseVoxelArray();
            if (_pendingMarchJobData.Count == 0 && _pendingTransitionMarchJobData.Count == 0) {
                return;
            }
            
            _combinedJobHandle.Complete();

            foreach (var kvp in _pendingTransitionMarchJobData) {
                kvp.Key.ApplyTransitionMarchResults(kvp.Value.Vertices, kvp.Value.Triangles, kvp.Value.Ranges);
                kvp.Value.Dispose();
            }

            foreach (var kvp in _pendingMarchJobData) {
                kvp.Key.ApplyMarchResults(kvp.Value.Vertices, kvp.Value.Triangles);
                kvp.Value.Dispose();
            }

            _pendingMarchJobData.Clear();
            _pendingTransitionMarchJobData.Clear();
            _nodeToChunkCoord.Clear();
            _combinedJobHandle = default;
        }
    }
}



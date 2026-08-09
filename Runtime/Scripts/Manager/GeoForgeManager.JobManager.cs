// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager : MonoBehaviour {
        // Left public: this is the actual "plug in your own rendering strategy" knob
        // (MaterialRenderingProfile is deliberately a public extension point per its own doc
        // comment), and OctreeNode reads it every march via _gfManager.jobAndRenderProfile.
        // internal would still satisfy that same-assembly read - worth deciding deliberately
        // whether external code should also be able to swap profiles at runtime via script,
        // rather than only through the Inspector, before narrowing this one.
        [SerializeField] public MaterialRenderingProfile jobAndRenderProfile;

        private JobHandle _combinedJobHandle;
        private Dictionary<OctreeNode, MarchJobData> _pendingMarchJobData = new();
        private Dictionary<OctreeNode, TransitionMarchJobData> _pendingTransitionMarchJobData = new();

        private Dictionary<OctreeNode, Vector3Int> _nodeToChunkCoord = new();

        internal void RegisterMarchJob(
            OctreeNode node,
            JobHandle jobHandle,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeReference<Bounds> computedBounds,
            Vector3Int chunkCoord) {
            if (_pendingMarchJobData.ContainsKey(node)) {
                // Overwriting a pending entry would orphan the superseded job's NativeLists - that
                // job is still scheduled and actively writing into them via _combinedJobHandle, so
                // nothing can safely dispose them here without first isolating and completing that
                // one job's handle specifically, which this dictionary doesn't track (CombineDependencies
                // merges handles opaquely; there's no way to pull one back out). Fail loudly instead
                // of silently leaking - or, worse, inviting a future "helpful" naive dispose that
                // races the still-running job.
                throw new InvalidOperationException(
                    $"RegisterMarchJob: a march job is already pending for node {node} - " +
                    "the previous job must be completed and applied before registering another.");
            }

            _combinedJobHandle = JobHandle.CombineDependencies(_combinedJobHandle, jobHandle);

            _pendingMarchJobData[node] = new MarchJobData {
                Vertices = vertices,
                Triangles = triangles,
                ComputedBounds = computedBounds
            };

            _nodeToChunkCoord[node] = chunkCoord;
        }

        internal void RegisterTransitionJob(
            OctreeNode node,
            JobHandle jobHandle,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeArray<int2> ranges,
            Vector3Int chunkCoord) {
            if (_pendingTransitionMarchJobData.ContainsKey(node)) {
                // Same leak/race shape as RegisterMarchJob above.
                throw new InvalidOperationException(
                    $"RegisterTransitionJob: a transition job is already pending for node {node} - " +
                    "the previous job must be completed and applied before registering another.");
            }

            _combinedJobHandle = JobHandle.CombineDependencies(_combinedJobHandle, jobHandle);

            _pendingTransitionMarchJobData[node] = new TransitionMarchJobData {
                Vertices = vertices,
                Triangles = triangles,
                Ranges = ranges
            };

            _nodeToChunkCoord.TryAdd(node, chunkCoord);
        }

        internal void CompleteAndApplyMarchingCubesJobs() {
            if (_pendingMarchJobData.Count == 0 && _pendingTransitionMarchJobData.Count == 0) return;

            _combinedJobHandle.Complete();

            // Main leaf results MUST be applied before transition results: ApplyMarchResults is
            // what builds (or releases) each leaf's GameObject/Mesh/transition-Mesh in the first
            // place, and ApplyTransitionMarchResults needs those to already exist (or to already
            // know they don't) before it can safely write into them.
            foreach (var kvp in _pendingMarchJobData) {
                kvp.Key.ApplyMarchResults(kvp.Value.Vertices, kvp.Value.Triangles, kvp.Value.ComputedBounds.Value);
                ReturnMarchBuffers(kvp.Value);
            }

            foreach (var kvp in _pendingTransitionMarchJobData) {
                kvp.Key.ApplyTransitionMarchResults(kvp.Value.Vertices, kvp.Value.Triangles, kvp.Value.Ranges);
                ReturnTransitionBuffers(kvp.Value);
            }

            _pendingMarchJobData.Clear();
            _pendingTransitionMarchJobData.Clear();
            _nodeToChunkCoord.Clear();
            _combinedJobHandle = default;
        }

        // Returns each field to its pool instead of calling MarchJobData.Dispose()/
        // TransitionMarchJobData.Dispose(), which would actually free the underlying
        // NativeCollections - the whole point of the buffer pool (see GeoForgeManager.MarchBufferPool.cs)
        // is to avoid that allocate/dispose churn on the normal completion path. Those structs' own
        // Dispose() methods are left intact for any path that needs to genuinely drop a pending job
        // without applying it.
        private void ReturnMarchBuffers(MarchJobData data) {
            ReturnVertexBuffer(data.Vertices);
            ReturnTriangleBuffer(data.Triangles);
            ReturnBoundsBuffer(data.ComputedBounds);
        }

        private void ReturnTransitionBuffers(TransitionMarchJobData data) {
            ReturnVertexBuffer(data.Vertices);
            ReturnTriangleBuffer(data.Triangles);
            ReturnRangesBuffer(data.Ranges);
        }
    }
}
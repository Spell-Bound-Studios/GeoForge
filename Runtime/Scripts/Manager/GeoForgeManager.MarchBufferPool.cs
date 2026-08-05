// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager : MonoBehaviour {
        // Buffer pool for march job outputs. NativeList<MeshingVertexData>/NativeList<int> are
        // shared between the main leaf march and the transition march - both use the exact same
        // generic types, so there's no need for separate pools per job kind. Rent/Return instead of
        // allocate/Dispose avoids the per-march allocator churn from MarchAndMesh (up to 6 fresh
        // Allocator.Persistent NativeCollections per call previously). NativeList.Clear() resets
        // length without releasing capacity, so a buffer that's grown to fit a big leaf's mesh stays
        // that size for next time instead of being reallocated from scratch.
        private readonly Stack<NativeList<MeshingVertexData>> _vertexBufferPool = new();
        private readonly Stack<NativeList<int>> _triangleBufferPool = new();
        private readonly Stack<NativeReference<Bounds>> _boundsBufferPool = new();
        private readonly Stack<NativeArray<int2>> _rangesBufferPool = new();

        internal NativeList<MeshingVertexData> RentVertexBuffer() =>
                _vertexBufferPool.Count > 0
                        ? _vertexBufferPool.Pop()
                        : new NativeList<MeshingVertexData>(Allocator.Persistent);

        internal void ReturnVertexBuffer(NativeList<MeshingVertexData> buffer) {
            buffer.Clear();
            _vertexBufferPool.Push(buffer);
        }

        internal NativeList<int> RentTriangleBuffer() =>
                _triangleBufferPool.Count > 0
                        ? _triangleBufferPool.Pop()
                        : new NativeList<int>(Allocator.Persistent);

        internal void ReturnTriangleBuffer(NativeList<int> buffer) {
            buffer.Clear();
            _triangleBufferPool.Push(buffer);
        }

        internal NativeReference<Bounds> RentBoundsBuffer() =>
                _boundsBufferPool.Count > 0
                        ? _boundsBufferPool.Pop()
                        : new NativeReference<Bounds>(Allocator.Persistent);

        internal void ReturnBoundsBuffer(NativeReference<Bounds> buffer) {
            _boundsBufferPool.Push(buffer);
        }

        // TransitionRanges is always a fixed size of 6, and every job execution unconditionally
        // overwrites all 6 slots before anything reads from it - no need to clear between uses.
        internal NativeArray<int2> RentRangesBuffer() =>
                _rangesBufferPool.Count > 0
                        ? _rangesBufferPool.Pop()
                        : new NativeArray<int2>(6, Allocator.Persistent);

        internal void ReturnRangesBuffer(NativeArray<int2> buffer) {
            _rangesBufferPool.Push(buffer);
        }

        private void DisposeMarchBufferPools() {
            while (_vertexBufferPool.Count > 0) _vertexBufferPool.Pop().Dispose();
            while (_triangleBufferPool.Count > 0) _triangleBufferPool.Pop().Dispose();
            while (_boundsBufferPool.Count > 0) _boundsBufferPool.Pop().Dispose();
            while (_rangesBufferPool.Count > 0) _rangesBufferPool.Pop().Dispose();
        }
    }
}
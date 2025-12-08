// Copyright 2025 Spellbound Studio Inc.

using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Spellbound.MarchingCubes {
    public struct TransitionMarchJobData : IDisposable {
        public NativeList<MeshingVertexData> Vertices;
        public NativeList<int> Triangles;
        public NativeArray<int2> Ranges;

        public void Dispose() {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Triangles.IsCreated) Triangles.Dispose();
            if (Ranges.IsCreated) Ranges.Dispose();
        }
    }
}
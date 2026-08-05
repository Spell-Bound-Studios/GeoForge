// Copyright 2026 Spellbound Studio Inc.

using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    internal struct TransitionMarchJobData : IDisposable {
        internal NativeList<MeshingVertexData> Vertices;
        internal NativeList<int> Triangles;
        internal NativeArray<int2> Ranges;

        public void Dispose() {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Triangles.IsCreated) Triangles.Dispose();
            if (Ranges.IsCreated) Ranges.Dispose();
        }
    }
}
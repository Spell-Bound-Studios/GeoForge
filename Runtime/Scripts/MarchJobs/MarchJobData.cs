// Copyright 2025 Spellbound Studio Inc.

using System;
using Unity.Collections;

namespace Spellbound.GeoForge {
    public struct MarchJobData : IDisposable {
        public NativeList<MeshingVertexData> Vertices;
        public NativeList<int> Triangles;

        public void Dispose() {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Triangles.IsCreated) Triangles.Dispose();
        }
    }
}
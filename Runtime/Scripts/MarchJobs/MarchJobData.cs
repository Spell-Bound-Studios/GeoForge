// Copyright 2026 Spellbound Studio Inc.

using System;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge {
    internal struct MarchJobData : IDisposable {
        internal NativeList<MeshingVertexData> Vertices;
        internal NativeList<int> Triangles;
        internal NativeReference<Bounds> ComputedBounds;

        public void Dispose() {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Triangles.IsCreated) Triangles.Dispose();
            if (ComputedBounds.IsCreated) ComputedBounds.Dispose();
        }
    }
}
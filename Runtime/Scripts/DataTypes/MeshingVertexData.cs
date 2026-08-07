// Copyright 2026 Spellbound Studio Inc.

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Spellbound.GeoForge {
    /// <summary>
    /// A struct to hold the per-vertex data to be sent to the shader.
    /// TODO Shrink Memory without breaking stuff
    /// </summary>
    public struct MeshingVertexData {
        public float3 Position;
        public float3 Normal;
        public Color32 Materials;
        public Color32 Densities;

        public MeshingVertexData(float3 position, float3 normal, Color32 materials, Color32 densities) {
            Position = position;
            Normal = normal;
            Materials = materials;
            Densities = densities;
        }

        /// <summary>
        /// The memory layout of a single vertex in memory
        /// </summary>
        public static readonly VertexAttributeDescriptor[] VertexBufferMemoryLayout = {
            new(VertexAttribute.Position),
            new(VertexAttribute.Normal),
            new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm8, 4)
        };
    }
}
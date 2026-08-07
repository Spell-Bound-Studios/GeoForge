// Copyright 2026 Spellbound Studio Inc.

using System;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Thin wrapper around int3 so a chunk coordinate can be used as a NativeParallelMultiHashMap
    /// key with GetUniqueKeyArray - that extension requires TKey : IComparable&lt;TKey&gt; (to
    /// sort+dedupe keys) in addition to IEquatable&lt;TKey&gt;, and int3 itself implements neither
    /// ordering interface (there's no meaningful "less than" between two 3D vectors on its own).
    ///
    /// CompareTo is purely lexicographic on (x, y, z) - it doesn't need to mean anything spatially,
    /// only to be a total, consistent ordering so Sort()/Unique() produce correct results. Handles
    /// negative coordinates natively via int.CompareTo, unlike an earlier bit-packed-int approach
    /// that silently wrapped outside a fixed per-axis range.
    /// </summary>
    internal readonly struct ChunkCoordKey : IEquatable<ChunkCoordKey>, IComparable<ChunkCoordKey> {
        internal readonly int3 Value;

        internal ChunkCoordKey(int3 value) => Value = value;

        internal ChunkCoordKey(Vector3Int value) => Value = new int3(value.x, value.y, value.z);

        internal Vector3Int ToVector3Int() => new(Value.x, Value.y, Value.z);

        public bool Equals(ChunkCoordKey other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is ChunkCoordKey other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public int CompareTo(ChunkCoordKey other) {
            var xCompare = Value.x.CompareTo(other.Value.x);
            if (xCompare != 0) return xCompare;

            var yCompare = Value.y.CompareTo(other.Value.y);
            if (yCompare != 0) return yCompare;

            return Value.z.CompareTo(other.Value.z);
        }
    }
}
// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager {
        // Judgment call, leaning private: unlike materialDatabase, nothing external should need
        // to enumerate or mutate this list directly - GetMaterialData<TData> is the intended
        // single entry point, same way octreePrefab stays private plumbing behind GetPooledObject.
        [SerializeField] private List<MaterialSideTableBase> sideTables = new();

        // Lazily built on first GetMaterialData call rather than in Awake() - same pattern
        // VoxelMaterialDatabase uses for _nameToIndex - so this file doesn't need to touch (or
        // race with) the existing Awake() in GeoForgeManager.cs.
        private Dictionary<Type, MaterialSideTableBase> _sideTablesByType;

        /// <summary>
        /// Resolves the MaterialSideTable registered for TData and returns this material's data
        /// from it. Returns default(TData) and logs a warning if no table for TData is registered
        /// on this manager - a distinct problem from MaterialSideTable's own warning, which fires
        /// when a table exists but this specific material has no row in it (see MaterialSideTable.cs).
        /// </summary>
        public TData GetMaterialData<TData>(byte materialIndex) where TData : new() {
            _sideTablesByType ??= BuildSideTableCache();

            if (!_sideTablesByType.TryGetValue(typeof(TData), out var table)) {
                Debug.LogWarning(
                    $"{nameof(GeoForgeManager)}: no MaterialSideTable registered for type {typeof(TData)}. " +
                    $"Drag the relevant side table asset into {nameof(sideTables)} on this manager.", this);
                return default;
            }

            // Safe: table was only added to the dictionary keyed under its own DataType, and
            // MaterialSideTable<TData>.DataType is always typeof(TData) for that exact closed
            // generic - see MaterialSideTable.cs.
            return ((MaterialSideTable<TData>)table).GetData(materialIndex);
        }

        private Dictionary<Type, MaterialSideTableBase> BuildSideTableCache() {
            var dict = new Dictionary<Type, MaterialSideTableBase>();

            foreach (var table in sideTables) {
                if (table == null) continue;

                if (dict.TryGetValue(table.DataType, out var existing)) {
                    Debug.LogError(
                        $"{nameof(GeoForgeManager)}: multiple side tables registered for type " +
                        $"{table.DataType} - '{existing.name}' and '{table.name}'. Only '{existing.name}' will be used.",
                        this);
                    continue;
                }

                dict[table.DataType] = table;
            }

            return dict;
        }
    }
}
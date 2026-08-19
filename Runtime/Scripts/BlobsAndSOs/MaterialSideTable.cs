// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Non-generic marker so editor tooling (AssetDatabase.FindAssets, etc.) can discover
    /// every MaterialSideTable in a project regardless of its TData type. Don't derive from
    /// this directly - derive from MaterialSideTable&lt;TData&gt; instead.
    /// </summary>
    public abstract class MaterialSideTableBase : ScriptableObject {
        public abstract VoxelMaterialDatabase Database { get; }

        // Unguarded (not #if UNITY_EDITOR) because GeoForgeManager.GetMaterialData<TData> needs
        // this at runtime, in builds, to resolve which registered table serves a given TData -
        // see GeoForgeManager.MaterialSideTables.cs.
        public abstract Type DataType { get; }

#if UNITY_EDITOR
        /// <summary>Reconciles this table's entries against its VoxelMaterialDatabase's current material list.</summary>
        public abstract void Sync();

        /// <summary>Returns one description per problem found (missing/stale entries). Empty = in sync.</summary>
        public abstract List<string> Validate();
#endif
    }

    /// <summary>
    /// Generic base for a ScriptableObject that stores one TData per material, keyed by
    /// material name against a VoxelMaterialDatabase. Subclass this per concern in whichever
    /// project consumes GeoForge - e.g. a FootstepMaterialTable storing AudioClip[] per
    /// material. GeoForge itself never needs to know what TData is for any given subclass,
    /// and VoxelMaterialDatabase needs no changes to support this.
    ///
    /// Entries are keyed by material name rather than index so reordering the source
    /// material list doesn't silently misalign this table. Requires Unity 2020.1+ for
    /// generic type serialization.
    /// </summary>
    public abstract class MaterialSideTable<TData> : MaterialSideTableBase where TData : new() {
        [System.Serializable]
        public class Entry {
            public string materialName;
            public TData data;
        }

        [SerializeField] private VoxelMaterialDatabase materialDatabase;
        [SerializeField] private List<Entry> entries = new();

        public override VoxelMaterialDatabase Database => materialDatabase;
        public override Type DataType => typeof(TData);

        // Runtime lookup cache, same pattern as VoxelMaterialDatabase's _nameToIndex.
        private Dictionary<string, TData> _lookup;
        private bool _warnedThisSession; // avoid log spam if GetData is called every frame

        /// <summary>Look up this material's data by name. Logs a warning (once per session) and returns default if missing.</summary>
        public TData GetData(string materialName) {
            _lookup ??= BuildLookup();

            if (_lookup.TryGetValue(materialName, out var data)) return data;

            if (!_warnedThisSession) {
                Debug.LogWarning(
                    $"{name}: no entry for material '{materialName}'. Did you forget to run Sync " +
                    $"after adding it to {(materialDatabase != null ? materialDatabase.name : "<no database assigned>")}?",
                    this);
                _warnedThisSession = true;
            }

            return default;
        }

        /// <summary>Look up this material's data by index, resolving the name via the linked VoxelMaterialDatabase.</summary>
        public TData GetData(byte materialIndex) {
            if (materialDatabase == null) return default;
            var materialName = materialDatabase.GetMaterialName(materialIndex);
            return materialName == null ? default : GetData(materialName);
        }

        private Dictionary<string, TData> BuildLookup() {
            var dict = new Dictionary<string, TData>();
            foreach (var entry in entries) {
                if (!string.IsNullOrEmpty(entry.materialName)) dict[entry.materialName] = entry.data;
            }
            return dict;
        }

        private void OnValidate() {
            _lookup = null;
            _warnedThisSession = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reconciles entries against the database's current material list. If the entry
        /// count hasn't changed, this is treated as a rename pass and entries are realigned
        /// by position so existing data survives; otherwise entries are diffed by name,
        /// adding rows for new materials and dropping rows for removed ones.
        /// </summary>
        [ContextMenu("Sync With Material Database")]
        public override void Sync() {
            if (materialDatabase == null) {
                Debug.LogError($"{name}: no VoxelMaterialDatabase assigned - can't sync.", this);
                return;
            }

            var names = new List<string>(materialDatabase.GetAllMaterialNames());

            if (entries.Count == names.Count) {
                for (var i = 0; i < entries.Count; i++) entries[i].materialName = names[i];
            } else {
                var existingNames = new HashSet<string>();
                foreach (var entry in entries) existingNames.Add(entry.materialName);

                entries.RemoveAll(entry => !names.Contains(entry.materialName));

                foreach (var materialName in names) {
                    if (!existingNames.Contains(materialName))
                        entries.Add(new Entry { materialName = materialName, data = new TData() });
                }

                entries.Sort((a, b) => names.IndexOf(a.materialName) - names.IndexOf(b.materialName));
            }

            _lookup = null;
            EditorUtility.SetDirty(this);
        }

        public override List<string> Validate() {
            var problems = new List<string>();

            if (materialDatabase == null) {
                problems.Add("No VoxelMaterialDatabase assigned.");
                return problems;
            }

            var names = new HashSet<string>(materialDatabase.GetAllMaterialNames());
            var covered = new HashSet<string>();

            foreach (var entry in entries) {
                if (!names.Contains(entry.materialName))
                    problems.Add($"'{entry.materialName}' is stale - no longer in {materialDatabase.name}.");
                covered.Add(entry.materialName);
            }

            foreach (var materialName in names) {
                if (!covered.Contains(materialName))
                    problems.Add($"'{materialName}' is missing an entry.");
            }

            return problems;
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Project-wide maintenance for every MaterialSideTable, regardless of TData. Run
    /// "Validate All" before committing after editing a material list; run "Sync All" to
    /// fix whatever it flags.
    /// </summary>
    public static class MaterialSideTableUtility {
        [MenuItem("Spellbound/GeoForge/Validate All Material Side Tables")]
        private static void ValidateAll() {
            var tables = FindAllSideTables();
            var anyProblems = false;

            foreach (var table in tables) {
                var problems = table.Validate();
                if (problems.Count == 0) continue;

                anyProblems = true;
                Debug.LogError($"{table.name}:\n  " + string.Join("\n  ", problems), table);
            }

            if (!anyProblems) Debug.Log($"All {tables.Count} material side table(s) are in sync.");
        }

        [MenuItem("Spellbound/GeoForge/Sync All Material Side Tables")]
        private static void SyncAll() {
            var tables = FindAllSideTables();
            foreach (var table in tables) table.Sync();

            AssetDatabase.SaveAssets();
            Debug.Log($"Synced {tables.Count} material side table(s).");
        }

        private static List<MaterialSideTableBase> FindAllSideTables() {
            var result = new List<MaterialSideTableBase>();
            var guids = AssetDatabase.FindAssets("t:MaterialSideTableBase");

            foreach (var guid in guids) {
                var asset = AssetDatabase.LoadAssetAtPath<MaterialSideTableBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }

            return result;
        }
    }
#endif
}
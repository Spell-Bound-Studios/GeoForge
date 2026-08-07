// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Spellbound.GeoForge {
    [CreateAssetMenu(menuName = "Spellbound/GeoForge/VoxelMaterialDatabase")]
    public class VoxelMaterialDatabase : ScriptableObject {
        private const byte NotPresent = 255;

        [System.Serializable]
        public class MaterialEntry {
            public string materialName;

            [Tooltip("Albedo/Color texture")]
            public Texture2D albedoTexture;

            [Tooltip("Alt albedo texture (normal-aware/stratified variant, e.g. moss/snow/sand layers)")]
            public Texture2D altAlbedoTexture;

            public MaterialEntry(string name = "New Material") {
                materialName = name;
            }
        }

        [Header("Material Definitions")] public List<MaterialEntry> materials = new();

        [Header("Generated Content Arrays")]
        [Tooltip("One slice per material index - materialIndex indexes directly into this array.")]
        public Texture2DArray albedoTextureArray;

        [Tooltip("Same indexing as albedoTextureArray, built from altAlbedoTexture instead.")]
        public Texture2DArray altAlbedoTextureArray;

        [Header("Texture Array Settings")] public bool generateMipmaps = true;
        public FilterMode filterMode = FilterMode.Trilinear;
        public int anisoLevel = 8;

        [Header("Texture Type Settings")] public bool albedoIsLinear = false;

        // Runtime lookup cache
        private Dictionary<string, byte> _nameToIndex;

        /// <summary>
        /// Get the material index by name. Returns 255 if not found.
        /// </summary>
        public byte GetMaterialIndex(string materialName) {
            if (_nameToIndex == null) {
                _nameToIndex = new Dictionary<string, byte>();
                for (var i = 0; i < materials.Count; i++) {
                    if (!string.IsNullOrEmpty(materials[i].materialName))
                        _nameToIndex[materials[i].materialName] = (byte)i;
                }
            }

            if (_nameToIndex.TryGetValue(materialName, out var index)) return index;

            Debug.LogWarning(
                $"Material '{materialName}' not found in {name}. Available materials: {string.Join(", ", _nameToIndex.Keys)}");

            return NotPresent;
        }

        public string GetMaterialName(int index) {
            if (index >= 0 && index < materials.Count) return materials[index].materialName;
            return null;
        }

        public bool HasMaterial(string materialName) => GetMaterialIndex(materialName) != NotPresent;

        public IEnumerable<string> GetAllMaterialNames() {
            foreach (var mat in materials) {
                if (!string.IsNullOrEmpty(mat.materialName))
                    yield return mat.materialName;
            }
        }

        public int MaterialCount => materials.Count;

        private void OnValidate() => _nameToIndex = null;

#if UNITY_EDITOR
        [ContextMenu("Build Texture Arrays")]
        public void BuildTextureArrays() {
            if (materials == null || materials.Count == 0) {
                Debug.LogError("No materials defined!");
                return;
            }

            BuildDirectArray(ref albedoTextureArray, m => m.albedoTexture, "AlbedoArray");
            BuildDirectArray(ref altAlbedoTextureArray, m => m.altAlbedoTexture, "AltAlbedoArray");

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"Texture arrays built successfully with {materials.Count} materials!");
            Debug.Log($"Material order: {string.Join(", ", GetAllMaterialNames())}");
        }

        /// <summary>
        /// Builds one Texture2DArray with exactly materials.Count slices, one per material index
        /// directly (no compaction, no mapping table) - the shader just samples
        /// arrayName[materialIndex]. Every material is expected to have a real texture assigned;
        /// a missing texture logs an error and that slice is left blank rather than silently
        /// substituted, since there's no fallback color to generate one from anymore.
        /// </summary>
        private void BuildDirectArray(
            ref Texture2DArray textureArray,
            System.Func<MaterialEntry, Texture2D> textureSelector,
            string arrayName) {
            if (textureArray != null) {
                AssetDatabase.RemoveObjectFromAsset(textureArray);
                DestroyImmediate(textureArray);
                textureArray = null;
            }

            var width = 4;
            var height = 4;
            var format = TextureFormat.RGBA32;
            var foundRealTexture = false;

            foreach (var entry in materials) {
                var tex = textureSelector(entry);
                if (tex == null) continue;

                width = tex.width;
                height = tex.height;
                format = tex.format;
                foundRealTexture = true;

                break;
            }

            if (!foundRealTexture) {
                Debug.LogError($"{arrayName}: no materials have a texture assigned - array not built.");
                return;
            }

            foreach (var entry in materials) {
                var tex = textureSelector(entry);

                if (tex == null) {
                    Debug.LogError($"'{entry.materialName}': no texture assigned for {arrayName}!");
                    continue;
                }

                if (tex.width != width || tex.height != height) {
                    Debug.LogError(
                        $"Texture '{tex.name}' in {arrayName} has different dimensions! All textures must be {width}x{height}");
                    return;
                }

                var path = AssetDatabase.GetAssetPath(tex);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && !importer.isReadable) {
                    Debug.LogWarning($"Making texture '{tex.name}' readable...");
                    importer.isReadable = true;
                    AssetDatabase.ImportAsset(path);
                }
            }

            textureArray = new Texture2DArray(width, height, materials.Count, format, generateMipmaps, albedoIsLinear);
            textureArray.name = $"{name}_{arrayName}";
            textureArray.filterMode = filterMode;
            textureArray.anisoLevel = anisoLevel;
            textureArray.wrapMode = TextureWrapMode.Repeat;

            for (var i = 0; i < materials.Count; i++) {
                var tex = textureSelector(materials[i]);
                if (tex == null) continue; // already logged above; slice left blank

                var mipCount = generateMipmaps ? tex.mipmapCount : 1;
                for (var mip = 0; mip < mipCount; mip++)
                    Graphics.CopyTexture(tex, 0, mip, textureArray, i, mip);
            }

            // updateMipmaps = false: mips are already populated directly above via per-mip
            // Graphics.CopyTexture from each source texture's own pre-baked mip chain. Passing
            // true here would tell Unity to regenerate mips by downsampling mip 0, which isn't
            // possible for a compressed format and throws "Rebuilding mipmaps of compressed
            // 2DArray textures is not supported" - false skips that step entirely, which is
            // correct since there's nothing left for it to do.
            textureArray.Apply(false, false);

            if (!AssetDatabase.Contains(textureArray)) AssetDatabase.AddObjectToAsset(textureArray, this);

            Debug.Log($"{arrayName} created with {materials.Count} slices ({materials.Count} materials).");
        }

        [ContextMenu("Clear Generated Assets")]
        public void ClearGeneratedAssets() {
            var path = AssetDatabase.GetAssetPath(this);
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in allSubAssets) {
                if (asset == this) continue;
                if (asset is Texture2DArray) {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    DestroyImmediate(asset, true);
                }
            }

            albedoTextureArray = null;
            altAlbedoTextureArray = null;

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Cleared all generated texture arrays.");
        }

        [ContextMenu("Validate Material Names")]
        public void ValidateMaterialNames() {
            var uniqueNames = new HashSet<string>();
            var duplicates = new List<string>();
            var emptyIndices = new List<int>();

            for (var i = 0; i < materials.Count; i++) {
                var matName = materials[i].materialName;

                if (string.IsNullOrWhiteSpace(matName))
                    emptyIndices.Add(i);
                else if (!uniqueNames.Add(matName)) duplicates.Add(matName);
            }

            if (emptyIndices.Count > 0)
                Debug.LogWarning($"Materials at indices {string.Join(", ", emptyIndices)} have no name!");

            if (duplicates.Count > 0)
                Debug.LogError($"Duplicate material names found: {string.Join(", ", duplicates)}");

            if (emptyIndices.Count == 0 && duplicates.Count == 0) Debug.Log("All material names are valid and unique!");
        }
#endif
    }
}
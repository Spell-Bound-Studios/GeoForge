// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge {
    [CreateAssetMenu(menuName = "Spellbound/GeoForge/VoxelMaterialDatabase")]
    public class VoxelMaterialDatabase : ScriptableObject {
        public enum MapType { Albedo, MAS, Normal, AltAlbedo, AltMAS, AltNormal }
        private const int MapTypeCount = 6;
        private const byte NotPresent = 255;

        [System.Serializable]
        public class MaterialEntry {
            public string materialName;

            // ================= BASE =================

            // ---- Base color ----
            [Tooltip("Tint/base color when Use Gradient is false; gradient's low end when Use Gradient is true")]
            public Color flatColor = Color.white;
            
            // ---- Fallback PBR constants ----
            [Tooltip("Used when Has MAS Map is false")]
            [Range(0f, 1f)] public float fallbackMetallic;
            [Range(0f, 1f)] public float fallbackAO = 1f;
            [Range(0f, 1f)] public float fallbackSmoothness = 0.5f;
            
            [Tooltip("If true, blends between flatColor and gradientColor. If false, flatColor is used as a constant.")]
            public bool hasGradient;

            [Tooltip("Gradient's high end, used when Use Gradient is true")]
            public Color gradientColor = Color.white;
            
            [Tooltip("If true, samples albedoTexture and tints it by the color(s) below. If false, the color(s) below are the base color directly.")]
            public bool hasAlbedoTexture;

            [Tooltip("Albedo/Color texture, used when Has Albedo Texture is true")]
            public Texture2D albedoTexture;
            
            // ---- MAS (Metallic / AO / Smoothness) ----
            [Tooltip("If false, uses the fallback constants above instead of sampling a texture")]
            public bool hasMASMap;

            [Tooltip("Metallic (R), AO (G), Smoothness (B) packed texture")]
            public Texture2D masTexture;

            // ---- Normal map ----
            [Tooltip("If false, uses a flat-up normal instead of sampling a texture")]
            public bool hasNormalMap;

            [Tooltip("Normal map texture")]
            public Texture2D normalTexture;

            // ---- Flat shading ----
            [Tooltip("If you want every part of a given mesh triangle to share the same normal. Gives a 'crystalline' kind of look")]
            public bool isFlatShaded;

            // ================= ALT (normal-aware variant, e.g. moss/snow) =================

            [Header("Alt (undisturbed/normal-aware variant)")]
            
            [Tooltip("Tint/base color when Alt Use Gradient is false; gradient's low end when Alt Use Gradient is true")]
            public Color altFlatColor = Color.white;
            
            [Tooltip("Used when Alt Has MAS Map is false")]
            [Range(0f, 1f)] public float altFallbackMetallic;
            [Range(0f, 1f)] public float altFallbackAO = 1f;
            [Range(0f, 1f)] public float altFallbackSmoothness = 0.5f;
            
            [Tooltip("If true, blends between altFlatColor and altGradientColor. If false, altFlatColor is used as a constant.")]
            public bool altHasGradient;

            [Tooltip("Gradient's high end, used when Alt Use Gradient is true")]
            public Color altGradientColor = Color.white;
            
            [Tooltip("If true, samples altAlbedoTexture and tints it by the color(s) below. If false, the color(s) below are the Alt base color directly.")]
            public bool altHasAlbedoTexture;

            [Tooltip("Alt albedo texture, used when Alt Has Albedo Texture is true")]
            public Texture2D altAlbedoTexture;

            [Tooltip("If false, uses the alt fallback constants above instead of sampling a texture")]
            public bool altHasMASMap;

            [Tooltip("Alt MAS texture")]
            public Texture2D altMasTexture;

            [Tooltip("If false, uses a flat-up normal instead of sampling a texture")]
            public bool altHasNormalMap;

            [Tooltip("Alt normal texture")]
            public Texture2D altNormalTexture;

            // ---- Stratified / spatial variant (future) ----
            [Tooltip("Reserved for future height/position-based layering (e.g. sand strata)")]
            public bool hasStratifiedVariant;

            public MaterialEntry(string name = "New Material") {
                materialName = name;
            }

            /// <summary>
            /// Packs this material's per-pixel-relevant capability flags into a single byte
            /// for use in the vertex color B/A channel lookup. These bits gate whether the
            /// shader samples a texture array at all (0 vs 3 triplanar fetches) — NOT which
            /// data source to use, since that's resolved into the mapping table at build time.
            /// </summary>
            public byte GetFlagByte() {
                byte flags = 0;
                if (hasMASMap) flags |= 1 << 0;
                if (hasNormalMap) flags |= 1 << 1;
                if (altHasMASMap) flags |= 1 << 2;
                if (altHasNormalMap) flags |= 1 << 3;
                if (hasStratifiedVariant) flags |= 1 << 4;
                // bits 5-7 reserved
                return flags;
            }
        }

        [Header("Material Definitions")] public List<MaterialEntry> materials = new();

        [Header("Generated Content Arrays")]
        public Texture2DArray albedoTextureArray;
        public Texture2DArray masTextureArray;
        public Texture2DArray normalTextureArray;
        public Texture2DArray altAlbedoTextureArray;
        public Texture2DArray altMASTextureArray;
        public Texture2DArray altNormalTextureArray;

        [Header("Generated Mapping Table")]
        [Tooltip("256x1, 6 slices (one per MapType), R8. mappingTable[materialIndex] = compacted slot in the corresponding content array, or 255 if not present.")]
        public Texture2DArray mappingTable;

        [Header("Texture Array Settings")] public bool generateMipmaps = true;
        public FilterMode filterMode = FilterMode.Trilinear;
        public int anisoLevel = 8;

        [Header("Texture Type Settings")] public bool albedoIsLinear = false;
        public bool masIsLinear = true;    // MAS should typically be linear
        public bool normalIsLinear = true; // Normal maps should be linear

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

            return 255;
        }

        public string GetMaterialName(int index) {
            if (index >= 0 && index < materials.Count) return materials[index].materialName;
            return null;
        }

        public bool HasMaterial(string materialName) => GetMaterialIndex(materialName) >= 0;

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

            var mappings = new byte[MapTypeCount][];

            (albedoTextureArray, mappings[(int)MapType.Albedo]) = BuildCompactArray(
                m => m.hasAlbedoTexture ? m.albedoTexture : null,
                m => m.hasAlbedoTexture,
                "AlbedoArray", albedoIsLinear);

            (masTextureArray, mappings[(int)MapType.MAS]) = BuildCompactArray(
                m => m.masTexture,
                m => m.hasMASMap,
                "MASArray", masIsLinear);

            (normalTextureArray, mappings[(int)MapType.Normal]) = BuildCompactArray(
                m => m.normalTexture,
                m => m.hasNormalMap,
                "NormalArray", normalIsLinear);

            (altAlbedoTextureArray, mappings[(int)MapType.AltAlbedo]) = BuildCompactArray(
                m => m.altHasAlbedoTexture ? m.altAlbedoTexture : null,
                m => m.altHasAlbedoTexture,
                "AltAlbedoArray", albedoIsLinear);

            (altMASTextureArray, mappings[(int)MapType.AltMAS]) = BuildCompactArray(
                m => m.altMasTexture,
                m => m.altHasMASMap,
                "AltMASArray", masIsLinear);

            (altNormalTextureArray, mappings[(int)MapType.AltNormal]) = BuildCompactArray(
                m => m.altNormalTexture,
                m => m.altHasNormalMap,
                "AltNormalArray", normalIsLinear);

            BuildMappingTable(mappings);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"Texture arrays built successfully with {materials.Count} materials!");
            Debug.Log($"Material order: {string.Join(", ", GetAllMaterialNames())}");
        }

        /// <summary>
        /// Builds one compacted Texture2DArray for a given map type: only materials that pass
        /// hasFlag AND have a non-null texture get a slot. Everything else maps to 255 (not present)
        /// in the returned per-materialIndex mapping array. This keeps the array's slice count equal
        /// to real content only, instead of one slice per materialIndex regardless of whether it's used.
        /// </summary>
        private (Texture2DArray array, byte[] mapping) BuildCompactArray(
            System.Func<MaterialEntry, Texture2D> selector,
            System.Func<MaterialEntry, bool> hasFlag,
            string arrayName, bool isLinear) {

            var mapping = new byte[256];
            for (var i = 0; i < 256; i++) mapping[i] = NotPresent;

            var realTextures = new List<Texture2D>();

            for (var i = 0; i < materials.Count; i++) {
                var entry = materials[i];
                if (!hasFlag(entry)) continue; // stays NotPresent — shader branches around this material for this map

                var tex = selector(entry);
                if (tex == null) {
                    Debug.LogError($"'{entry.materialName}': flag set but texture missing for {arrayName}!");
                    continue; // stays NotPresent — logged as an error, not silently substituted
                }

                mapping[i] = (byte)realTextures.Count;
                realTextures.Add(tex);
            }

            Texture2DArray array = null;
            if (realTextures.Count > 0)
                BuildTextureArray(ref array, realTextures, arrayName, isLinear);
            else
                Debug.Log($"{arrayName}: no materials use this map, array not created.");

            return (array, mapping);
        }

        private void BuildTextureArray(
            ref Texture2DArray textureArray, List<Texture2D> sourceTextures, string arrayName, bool isLinear) {
            if (sourceTextures.Count == 0) {
                Debug.LogError($"No valid textures found for {arrayName}!");
                return;
            }

            if (textureArray != null) {
                AssetDatabase.RemoveObjectFromAsset(textureArray);
                DestroyImmediate(textureArray);
                textureArray = null;
            }

            var width = sourceTextures[0].width;
            var height = sourceTextures[0].height;
            var format = sourceTextures[0].format;

            for (var i = 0; i < sourceTextures.Count; i++) {
                if (sourceTextures[i].width != width || sourceTextures[i].height != height) {
                    Debug.LogError(
                        $"Texture '{sourceTextures[i].name}' in {arrayName} has different dimensions! All textures must be {width}x{height}");
                    return;
                }

                var path = AssetDatabase.GetAssetPath(sourceTextures[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && !importer.isReadable) {
                    Debug.LogWarning($"Making texture '{sourceTextures[i].name}' readable...");
                    importer.isReadable = true;
                    AssetDatabase.ImportAsset(path);
                }
            }

            textureArray = new Texture2DArray(width, height, sourceTextures.Count, format, generateMipmaps, isLinear);
            textureArray.name = $"{name}_{arrayName}";
            textureArray.filterMode = filterMode;
            textureArray.anisoLevel = anisoLevel;
            textureArray.wrapMode = TextureWrapMode.Repeat;

            for (var i = 0; i < sourceTextures.Count; i++) {
                var mipCount = generateMipmaps ? sourceTextures[i].mipmapCount : 1;
                for (var mip = 0; mip < mipCount; mip++)
                    Graphics.CopyTexture(sourceTextures[i], 0, mip, textureArray, i, mip);
            }

            textureArray.Apply(true, false);

            if (!AssetDatabase.Contains(textureArray)) AssetDatabase.AddObjectToAsset(textureArray, this);

            Debug.Log($"{arrayName} created with {sourceTextures.Count} textures, {textureArray.mipmapCount} mip levels!");
        }

        /// <summary>
        /// Packs all 6 per-materialIndex mapping arrays into one 256x1, 6-slice R8 Texture2DArray.
        /// Slice index = (int)MapType. Shader samples slice (mapType), U = materialIndex/255,
        /// multiplies result by 255 and rounds to recover the compacted slot (or 255 = not present).
        /// </summary>
        private void BuildMappingTable(byte[][] mappingsPerType) {
            if (mappingTable != null) {
                AssetDatabase.RemoveObjectFromAsset(mappingTable);
                DestroyImmediate(mappingTable);
                mappingTable = null;
            }

            mappingTable = new Texture2DArray(256, 1, MapTypeCount, TextureFormat.R8, false, true);
            mappingTable.name = $"{name}_MappingTable";
            mappingTable.filterMode = FilterMode.Point;
            mappingTable.wrapMode = TextureWrapMode.Clamp;

            for (var slice = 0; slice < MapTypeCount; slice++) {
                var tex = new Texture2D(256, 1, TextureFormat.R8, false, true);
                var pixels = new Color32[256];
                for (var i = 0; i < 256; i++) {
                    var v = mappingsPerType[slice][i];
                    pixels[i] = new Color32(v, 0, 0, 0);
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                Graphics.CopyTexture(tex, 0, 0, mappingTable, slice, 0);
                DestroyImmediate(tex);
            }

            mappingTable.Apply(false, false);

            if (!AssetDatabase.Contains(mappingTable)) AssetDatabase.AddObjectToAsset(mappingTable, this);

            Debug.Log("Mapping table built (256x1, 6 slices).");
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
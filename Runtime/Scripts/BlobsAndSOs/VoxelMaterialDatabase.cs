// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Spellbound.GeoForge {
    [CreateAssetMenu(menuName = "Spellbound/GeoForge/VoxelMaterialDatabase")]
    public class VoxelMaterialDatabase : ScriptableObject {
        // Slice 0 of MappingTable: R = Albedo slot, G = MAS slot, B = Normal slot
        // Slice 1 of MappingTable: R = AltAlbedo slot, G = AltMAS slot, B = AltNormal slot
        public enum MapType { Albedo, MAS, Normal, AltAlbedo, AltMAS, AltNormal }
        private const int MapTypeCount = 6;
        private const int MappingSliceCount = 2;

        public enum ConstantType { FlatColor, FallbackMAS, AltFlatColor, AltFallbackMAS }
        private const int ConstantTypeCount = 4;

        private const byte NotPresent = 255;

        [System.Serializable]
        public class MaterialEntry {
            public string materialName;
            
            // ---- Flat shading ----
            [Tooltip("If you want every part of a given mesh triangle to share the same normal. Gives a 'crystalline' kind of look")]
            public bool isFlatShaded;

            // ================= BASE =================
            
            [Header("Main Textures/Fallbacks")]

            // ---- Base color ----
            [Tooltip("Tint/base color. Used directly if Has Albedo Texture is false, or as a multiplicative tint over the texture if true.")]
            public Color flatColor = Color.white;

            // ---- Fallback PBR constants ----
            [Tooltip("Used when Has MAS Map is false")]
            [Range(0f, 1f)] public float fallbackMetallic;
            [Range(0f, 1f)] public float fallbackAO = 1f;
            [Range(0f, 1f)] public float fallbackSmoothness = 0.5f;

            [Tooltip("If true, samples albedoTexture and tints it by flatColor. If false, flatColor is the base color directly.")]
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

            // ================= ALT (normal-aware / stratified variant, e.g. moss/snow/sand layers) =================

            [Header("Alt Textures/Fallbacks")]

            [Tooltip("Tint/base color for the Alt variant. Used directly if Alt Has Albedo Texture is false, or as a multiplicative tint over the alt texture if true.")]
            public Color altFlatColor = Color.white;

            [Tooltip("Used when Alt Has MAS Map is false")]
            [Range(0f, 1f)] public float altFallbackMetallic;
            [Range(0f, 1f)] public float altFallbackAO = 1f;
            [Range(0f, 1f)] public float altFallbackSmoothness = 0.5f;

            [Tooltip("If true, samples altAlbedoTexture and tints it by altFlatColor. If false, altFlatColor is the Alt base color directly.")]
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

            public MaterialEntry(string name = "New Material") {
                materialName = name;
            }

            // NOTE: material-capability flags (hasMASMap, hasNormalMap, etc.) used to be packed
            // into a GetFlagByte() for the shader to read from vertex colors. That's gone —
            // MappingTable already answers "does this material have this map" via its 255
            // sentinel, so baking the same answer into vertex colors was pure duplication.
            // Vertex color B/A channels are free for genuine per-vertex runtime state instead
            // (e.g. "is this voxel undisturbed"), which MappingTable can't express since it's
            // static per-materialIndex, not per-voxel.
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
        [Tooltip("256x1, 2 slices, RGBA32. Slice 0: R=Albedo slot, G=MAS slot, B=Normal slot. Slice 1: R=AltAlbedo slot, G=AltMAS slot, B=AltNormal slot. 255 = not present.")]
        public Texture2DArray mappingTable;

        [Header("Generated Material Constants")]
        [Tooltip("256x1, 4 slices (one per ConstantType), RGBA32. Holds flatColor and fallback MAS constants per materialIndex, for when no texture is used.")]
        public Texture2DArray materialConstantsArray;

        [Header("Fallback")]
        [Tooltip("1x1 dummy array bound to the shader whenever a content array is null (no materials use that map), so the sampler always has something valid bound.")]
        public Texture2DArray dummyTextureArray;

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

            EnsureDummyArray();

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
            BuildMaterialConstantsArray();

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
        /// Packs all 6 per-materialIndex mapping arrays into a single 256x1, 2-slice RGBA32
        /// Texture2DArray. Slice 0 channels: R=Albedo, G=MAS, B=Normal. Slice 1 channels:
        /// R=AltAlbedo, G=AltMAS, B=AltNormal. This gives the shader 3 map types per sample
        /// instead of 1, so resolving all 6 map types costs 2 samples instead of 6.
        /// Shader: sample slice, split channel, multiply by 255 and round to recover the
        /// compacted slot (or 255 = not present).
        /// </summary>
        private void BuildMappingTable(byte[][] mappingsPerType) {
            if (mappingTable != null) {
                AssetDatabase.RemoveObjectFromAsset(mappingTable);
                DestroyImmediate(mappingTable);
                mappingTable = null;
            }

            mappingTable = new Texture2DArray(256, 1, MappingSliceCount, TextureFormat.RGBA32, false, true);
            mappingTable.name = $"{name}_MappingTable";
            mappingTable.filterMode = FilterMode.Point;
            mappingTable.wrapMode = TextureWrapMode.Clamp;

            WriteMappingSlice(0,
                mappingsPerType[(int)MapType.Albedo],
                mappingsPerType[(int)MapType.MAS],
                mappingsPerType[(int)MapType.Normal]);

            WriteMappingSlice(1,
                mappingsPerType[(int)MapType.AltAlbedo],
                mappingsPerType[(int)MapType.AltMAS],
                mappingsPerType[(int)MapType.AltNormal]);

            mappingTable.Apply(false, false);

            if (!AssetDatabase.Contains(mappingTable)) AssetDatabase.AddObjectToAsset(mappingTable, this);

            Debug.Log("Mapping table built (256x1, 2 slices, RGB-packed).");
        }

        private void WriteMappingSlice(int slice, byte[] rMap, byte[] gMap, byte[] bMap) {
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[256];
            for (var i = 0; i < 256; i++)
                pixels[i] = new Color32(rMap[i], gMap[i], bMap[i], 0);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Graphics.CopyTexture(tex, 0, 0, mappingTable, slice, 0);
            DestroyImmediate(tex);
        }

        /// <summary>
        /// Packs per-materialIndex fallback constants (flatColor, fallback MAS, and Alt equivalents)
        /// into one 256x1, 4-slice RGBA32 Texture2DArray. Slice index = (int)ConstantType.
        /// Used by the shader when the corresponding mapping slot is 255 (not present), so no
        /// separate branch data is needed to pick "sample a texture" vs "read a constant" —
        /// both are just array samples resolved from the same materialIndex.
        /// </summary>
        private void BuildMaterialConstantsArray() {
            if (materialConstantsArray != null) {
                AssetDatabase.RemoveObjectFromAsset(materialConstantsArray);
                DestroyImmediate(materialConstantsArray);
                materialConstantsArray = null;
            }

            materialConstantsArray = new Texture2DArray(256, 1, ConstantTypeCount, TextureFormat.RGBA32, false, true);
            materialConstantsArray.name = $"{name}_MaterialConstants";
            materialConstantsArray.filterMode = FilterMode.Point;
            materialConstantsArray.wrapMode = TextureWrapMode.Clamp;

            WriteConstantSlice((int)ConstantType.FlatColor, m => m.flatColor);
            WriteConstantSlice((int)ConstantType.FallbackMAS, m => new Color(m.fallbackMetallic, m.fallbackAO, m.fallbackSmoothness));
            WriteConstantSlice((int)ConstantType.AltFlatColor, m => m.altFlatColor);
            WriteConstantSlice((int)ConstantType.AltFallbackMAS, m => new Color(m.altFallbackMetallic, m.altFallbackAO, m.altFallbackSmoothness));

            materialConstantsArray.Apply(false, false);

            if (!AssetDatabase.Contains(materialConstantsArray)) AssetDatabase.AddObjectToAsset(materialConstantsArray, this);

            Debug.Log("Material constants array built (256x1, 4 slices).");
        }

        private void WriteConstantSlice(int slice, System.Func<MaterialEntry, Color> selector) {
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[256];
            for (var i = 0; i < 256; i++)
                pixels[i] = i < materials.Count ? (Color32)selector(materials[i]) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Graphics.CopyTexture(tex, 0, 0, materialConstantsArray, slice, 0);
            DestroyImmediate(tex);
        }

        /// <summary>
        /// Ensures a 1x1, 1-slice dummy Texture2DArray exists. Bind this to the shader in place
        /// of any content array that came back null (no materials use that map) — MappingTable
        /// will only ever return 255 for that map type in that case, so the real content
        /// branch is provably dead code and the dummy's actual content is irrelevant.
        /// </summary>
        private void EnsureDummyArray() {
            if (dummyTextureArray != null) return;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply(false, false);

            dummyTextureArray = new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, false, true);
            dummyTextureArray.name = $"{name}_DummyArray";
            Graphics.CopyTexture(tex, 0, 0, dummyTextureArray, 0, 0);
            dummyTextureArray.Apply(false, false);
            DestroyImmediate(tex);

            if (!AssetDatabase.Contains(dummyTextureArray)) AssetDatabase.AddObjectToAsset(dummyTextureArray, this);
        }

        [ContextMenu("Clear Generated Assets")]
        public void ClearGeneratedAssets() {
            var path = AssetDatabase.GetAssetPath(this);
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in allSubAssets) {
                if (asset == this) continue; // don't delete the database itself
                if (asset is Texture2DArray) {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    DestroyImmediate(asset, true);
                }
            }

            albedoTextureArray = null;
            masTextureArray = null;
            normalTextureArray = null;
            altAlbedoTextureArray = null;
            altMASTextureArray = null;
            altNormalTextureArray = null;
            mappingTable = null;
            materialConstantsArray = null;
            dummyTextureArray = null;

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
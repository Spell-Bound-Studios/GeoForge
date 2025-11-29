// Copyright 2025 Spellbound Studio Inc.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(fileName = "VoxelMaterialDatabase", menuName = "Voxel/Material Database")]
    public class VoxelMaterialDatabase : ScriptableObject {
        [System.Serializable]
        public class MaterialEntry {
            public string materialName;
            [Tooltip("Albedo/Color texture")] public Texture2D albedoTexture;

            [Tooltip("Metallic (R), AO (G), Smoothness (B) packed texture")]
            public Texture2D masTexture;

            public MaterialEntry(string name = "New Material") {
                materialName = name;
            }
        }

        [Header("Material Definitions")] public List<MaterialEntry> materials = new();

        [Header("Generated Assets")] public Texture2DArray albedoTextureArray;
        public Texture2DArray masTextureArray;

        [Header("Texture Array Settings")] public bool generateMipmaps = true;
        public FilterMode filterMode = FilterMode.Trilinear;
        public int anisoLevel = 8;

        [Header("Texture Type Settings")] public bool albedoIsLinear = false;
        public bool masIsLinear = true; // MAS should typically be linear

        // Runtime lookup cache
        private Dictionary<string, byte> _nameToIndex;

        /// <summary>
        /// Get the material index by name. Returns -1 if not found.
        /// </summary>
        public byte GetMaterialIndex(string materialName) {
            // Build cache on first access
            if (_nameToIndex == null) {
                _nameToIndex = new Dictionary<string, byte>();

                for (var i = 0; i < materials.Count; i++)
                    if (!string.IsNullOrEmpty(materials[i].materialName))
                        _nameToIndex[materials[i].materialName] = (byte)i;
            }

            if (_nameToIndex.TryGetValue(materialName, out var index)) return index;

            Debug.LogWarning(
                $"Material '{materialName}' not found in {name}. Available materials: {string.Join(", ", _nameToIndex.Keys)}");

            return 255;
        }

        /// <summary>
        /// Get material name by index.
        /// </summary>
        public string GetMaterialName(int index) {
            if (index >= 0 && index < materials.Count) return materials[index].materialName;

            return null;
        }

        /// <summary>
        /// Check if a material exists in the database.
        /// </summary>
        public bool HasMaterial(string materialName) => GetMaterialIndex(materialName) >= 0;

        /// <summary>
        /// Get all material names.
        /// </summary>
        public IEnumerable<string> GetAllMaterialNames() {
            foreach (var mat in materials)
                if (!string.IsNullOrEmpty(mat.materialName))
                    yield return mat.materialName;
        }

        /// <summary>
        /// Get the number of materials in the database.
        /// </summary>
        public int MaterialCount => materials.Count;

        // Clear cache when modified in editor
        private void OnValidate() => _nameToIndex = null;

#if UNITY_EDITOR
        [ContextMenu("Build Texture Arrays")]
        public void BuildTextureArrays() {
            if (materials == null || materials.Count == 0) {
                Debug.LogError("No materials defined!");

                return;
            }

            // Validate and collect textures
            var validAlbedoTextures = new List<Texture2D>();
            var validMasTextures = new List<Texture2D>();
            var missingAlbedoNames = new List<string>();
            var missingMasNames = new List<string>();

            for (var i = 0; i < materials.Count; i++) {
                if (materials[i].albedoTexture == null)
                    missingAlbedoNames.Add(materials[i].materialName);
                else
                    validAlbedoTextures.Add(materials[i].albedoTexture);

                if (materials[i].masTexture == null)
                    missingMasNames.Add(materials[i].materialName);
                else
                    validMasTextures.Add(materials[i].masTexture);
            }

            if (missingAlbedoNames.Count > 0) {
                Debug.LogError($"Missing albedo textures for materials: {string.Join(", ", missingAlbedoNames)}");

                return;
            }

            if (missingMasNames.Count > 0) {
                Debug.LogError($"Missing MAS textures for materials: {string.Join(", ", missingMasNames)}");

                return;
            }

            // Build albedo texture array
            BuildTextureArray(
                ref albedoTextureArray,
                validAlbedoTextures,
                "AlbedoArray",
                albedoIsLinear
            );

            // Build MAS texture array
            BuildTextureArray(
                ref masTextureArray,
                validMasTextures,
                "MASArray",
                masIsLinear
            );

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"Texture arrays built successfully with {materials.Count} materials!");
            Debug.Log($"Material order: {string.Join(", ", GetAllMaterialNames())}");
        }

        private void BuildTextureArray(
            ref Texture2DArray textureArray, List<Texture2D> sourceTextures, string arrayName, bool isLinear) {
            if (sourceTextures.Count == 0) {
                Debug.LogError($"No valid textures found for {arrayName}!");

                return;
            }

            // Delete old texture array if it exists
            if (textureArray != null) {
                AssetDatabase.RemoveObjectFromAsset(textureArray);
                DestroyImmediate(textureArray);
                textureArray = null;
            }

            // Validate all textures have same dimensions
            var width = sourceTextures[0].width;
            var height = sourceTextures[0].height;
            var format = sourceTextures[0].format;

            for (var i = 0; i < sourceTextures.Count; i++) {
                if (sourceTextures[i].width != width || sourceTextures[i].height != height) {
                    Debug.LogError(
                        $"Material '{materials[i].materialName}' texture has different dimensions! All textures must be {width}x{height}");

                    return;
                }

                // Ensure source textures are readable
                var path = AssetDatabase.GetAssetPath(sourceTextures[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && !importer.isReadable) {
                    Debug.LogWarning($"Making texture for '{materials[i].materialName}' readable...");
                    importer.isReadable = true;
                    AssetDatabase.ImportAsset(path);
                }
            }

            // Create the texture array
            textureArray = new Texture2DArray(
                width,
                height,
                sourceTextures.Count,
                format,
                generateMipmaps,
                isLinear
            );

            textureArray.name = $"{name}_{arrayName}";
            textureArray.filterMode = filterMode;
            textureArray.anisoLevel = anisoLevel;
            textureArray.wrapMode = TextureWrapMode.Repeat;

            // Copy textures with all mip levels
            for (var i = 0; i < sourceTextures.Count; i++) {
                var mipCount = generateMipmaps ? sourceTextures[i].mipmapCount : 1;

                for (var mip = 0; mip < mipCount; mip++)
                    Graphics.CopyTexture(sourceTextures[i], 0, mip, textureArray, i, mip);
            }

            textureArray.Apply(true, false);

            // Save as a sub-asset
            if (!AssetDatabase.Contains(textureArray)) AssetDatabase.AddObjectToAsset(textureArray, this);

            Debug.Log(
                $"{arrayName} created with {sourceTextures.Count} textures, {textureArray.mipmapCount} mip levels!");
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
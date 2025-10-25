// Copyright 2025 Spellbound Studio Inc.

using UnityEditor;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(fileName = "TextureArrayBuilder", menuName = "Textures/Texture Array Builder")]
    public class TextureArrayBuilder : ScriptableObject {
        [Header("Source Textures")] public Texture2D[] sourceTextures;

        [Header("Output")] public Texture2DArray textureArray;

        [Header("Settings")] public bool generateMipmaps = true;
        public bool isLinear = false;
        public FilterMode filterMode = FilterMode.Trilinear;
        public int anisoLevel = 8;

#if UNITY_EDITOR
        [ContextMenu("Build Texture Array")]
        public void BuildTextureArray() {
            if (sourceTextures == null || sourceTextures.Length == 0) {
                Debug.LogError("No source textures assigned!");

                return;
            }

            // Delete old texture array if it exists
            if (textureArray != null) {
                AssetDatabase.RemoveObjectFromAsset(textureArray);
                DestroyImmediate(textureArray);
                textureArray = null;
            }

            // Validate all textures
            var width = sourceTextures[0].width;
            var height = sourceTextures[0].height;
            var format = sourceTextures[0].format;

            for (var i = 0; i < sourceTextures.Length; i++) {
                if (sourceTextures[i] == null) {
                    Debug.LogError($"Texture at index {i} is null!");

                    return;
                }

                if (sourceTextures[i].width != width || sourceTextures[i].height != height) {
                    Debug.LogError(
                        $"Texture {sourceTextures[i].name} has different dimensions! All textures must be {width}x{height}");

                    return;
                }

                // Ensure source textures are readable
                var path = AssetDatabase.GetAssetPath(sourceTextures[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && !importer.isReadable) {
                    Debug.LogWarning($"Making texture {sourceTextures[i].name} readable...");
                    importer.isReadable = true;
                    AssetDatabase.ImportAsset(path);
                }
            }

            // Create the texture array
            textureArray = new Texture2DArray(
                width,
                height,
                sourceTextures.Length,
                format,
                generateMipmaps,
                isLinear
            );

            textureArray.name = "TerrainTextureArray";
            textureArray.filterMode = filterMode;
            textureArray.anisoLevel = anisoLevel;
            textureArray.wrapMode = TextureWrapMode.Repeat;

            // Copy textures - METHOD 1: Copy all mip levels
            for (var i = 0; i < sourceTextures.Length; i++) {
                var mipCount = generateMipmaps ? sourceTextures[i].mipmapCount : 1;

                for (var mip = 0; mip < mipCount; mip++)
                    Graphics.CopyTexture(sourceTextures[i], 0, mip, textureArray, i, mip);
            }

            // Alternative METHOD 2: Copy base level and regenerate mipmaps
            // Uncomment this and comment out METHOD 1 if source textures don't have mipmaps
            /*
            for (int i = 0; i < sourceTextures.Length; i++)
            {
                Color[] pixels = sourceTextures[i].GetPixels();
                textureArray.SetPixels(pixels, i, 0);
            }
            */

            textureArray.Apply(true, false); // updateMipmaps=true, makeNoLongerReadable=false for debugging

            // Save as a sub-asset
            if (!AssetDatabase.Contains(textureArray)) AssetDatabase.AddObjectToAsset(textureArray, this);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Texture array created with {sourceTextures.Length} textures, {textureArray.mipmapCount} mip levels!");
        }
#endif
    }
}
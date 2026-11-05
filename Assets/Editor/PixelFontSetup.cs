using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Aeternal.EditorTools
{
    /// <summary>
    /// Builds the game's TextMeshPro font asset from Jersey 10 (SIL OFL, licence next to the
    /// font), a chunky condensed pixel font that matches the card art's lettering and keeps
    /// 2, 5 and G distinct at card sizes (Pixelify Sans, tried first, closes them into 8, S
    /// and 6). Dynamic, so any character used at runtime is added; Liberation Sans stays as the
    /// fallback for symbols the pixel font lacks (arrows, stars). Run once: Aeternal > Build
    /// Pixel Font, or in batch mode with -executeMethod Aeternal.EditorTools.PixelFontSetup.BuildAndExit.
    /// </summary>
    public static class PixelFontSetup
    {
        const string FontPath = "Assets/Fonts/Jersey10/Jersey10-Regular.ttf";
        const string AssetPath = "Assets/Fonts/Jersey10/Jersey10 SDF.asset";
        const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Aeternal/Build Pixel Font")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(FontPath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError($"[PixelFontSetup] No font at {FontPath}");
                return;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = "Jersey10 SDF";
            AssetDatabase.CreateAsset(asset, AssetPath);
            var atlas = asset.atlasTextures[0];
            atlas.name = "Jersey10 SDF Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = "Jersey10 SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            // Printable ASCII up front, so text shows in the editor before anything runs.
            var ascii = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++) ascii.Append(c);
            asset.TryAddCharacters(ascii.ToString());

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            if (fallback != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PixelFontSetup] Wrote {AssetPath}");
        }

        public static void BuildAndExit()
        {
            Build();
            EditorApplication.Exit(0);
        }
    }
}

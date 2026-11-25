using UnityEditor;
using UnityEngine;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Menu-only import preset: handoff art, Polish skins and V4 borders. No global texture changes.</summary>
    public sealed class TrainerMenuImportPostprocessor : AssetPostprocessor {
        public const string Root = "Assets/ThangTest/UIThang/GUIArt/Resources/UI/TrainerMenu/";
        void OnPreprocessTexture() {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Sprite;
            texture.spriteImportMode = SpriteImportMode.Single;
            texture.spritePixelsPerUnit = 100;
            texture.alphaIsTransparency = true; texture.sRGBTexture = true;
            texture.mipmapEnabled = false; texture.isReadable = false;
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            texture.maxTextureSize = assetPath.Contains("frame") || assetPath.Contains("/V4/") ? 1024 : assetPath.Contains("icon-") || assetPath.Contains("/Polish/surface") || assetPath.Contains("/Polish/focus") ? 128 : 512;
            texture.spriteBorder = assetPath.EndsWith("07-secondary-frame.png", System.StringComparison.Ordinal) ? new Vector4(320, 180, 320, 180)
                : assetPath.EndsWith("/surface.png", System.StringComparison.Ordinal) || assetPath.EndsWith("/focus.png", System.StringComparison.Ordinal) ? Vector4.one * 16 : Vector4.zero;
            var settings = new TextureImporterSettings(); texture.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; texture.SetTextureSettings(settings);
        }
    }
}

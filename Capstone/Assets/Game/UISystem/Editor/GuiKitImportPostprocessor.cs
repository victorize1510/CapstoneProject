using UnityEditor;
using UnityEngine;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Scoped to generated GUI art; never changes existing item/pet imports.</summary>
    public sealed class GuiKitImportPostprocessor : AssetPostprocessor {
        const string Root = "Assets/ThangTest/UIThang/GUIArt/Resources/UI/GuiKit/";
        void OnPreprocessTexture() {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Sprite;
            texture.spriteImportMode = SpriteImportMode.Single;
            texture.spritePixelsPerUnit = 100;
            texture.alphaIsTransparency = true;
            texture.sRGBTexture = true;
            texture.mipmapEnabled = false;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.maxTextureSize = assetPath.Contains("/Backgrounds/") ? 2048 : 256;
            texture.isReadable = false;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool sliced = assetPath.Contains("/Frames/") && name != "avatar_ring";
            texture.spriteBorder = !sliced ? Vector4.zero : name == "progress_track" ? new Vector4(12, 10, 12, 10) : new Vector4(16, 16, 16, 16);
            var settings = new TextureImporterSettings();
            texture.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            texture.SetTextureSettings(settings);
        }
    }
}

using UnityEditor;
using UnityEngine;
namespace Capstone.Game.UISystem.Editor {
    public sealed class PanelMaterialImporter:AssetPostprocessor {
        void OnPreprocessTexture() {
            if(!assetPath.StartsWith("Assets/Game/UISystem/Resources/UI/Backgrounds20261023/")||!assetPath.EndsWith(".png"))return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Default;t.npotScale=TextureImporterNPOTScale.None;
            t.mipmapEnabled=false;t.isReadable=false;t.sRGBTexture=true;t.alphaIsTransparency=true;
            t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.maxTextureSize=2048;
            t.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}

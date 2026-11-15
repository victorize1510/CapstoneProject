using UnityEditor;
using UnityEngine;
namespace Capstone.Game.ShopSystem.Editor {
    public sealed class ShopAAssetImporter:AssetPostprocessor {
        void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Game/ShopSystem/Resources/UI/ShopA20261104/")||!assetPath.EndsWith(".png"))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.sRGBTexture=true;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.npotScale=TextureImporterNPOTScale.None;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;}
    }
}

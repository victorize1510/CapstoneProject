using UnityEditor;
using UnityEngine;
namespace Capstone.Game.Inventory.Bag.Editor {
    sealed class BagAssetImporter:AssetPostprocessor {
        void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Game/Inventory/Bag/Resources/"))return;var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.isReadable=false;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.maxTextureSize=2048;t.npotScale=TextureImporterNPOTScale.None;t.textureCompression=TextureImporterCompression.CompressedHQ;}
    }
}

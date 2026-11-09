using UnityEditor;
using UnityEngine;

namespace Capstone.Game.UISystem.Editor {
    public sealed class ElementsR2AssetImporter:AssetPostprocessor {
        void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Game/UISystem/Resources/UI/ElementsR2/",System.StringComparison.Ordinal))return;var importer=(TextureImporter)assetImporter;importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=256;}
        public override uint GetVersion()=>1;
    }
}

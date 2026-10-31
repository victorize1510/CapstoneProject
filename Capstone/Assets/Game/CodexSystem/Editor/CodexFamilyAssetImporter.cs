using UnityEditor;
using UnityEngine;

namespace Capstone.Game.CodexSystem.Editor {
    /// <summary>Only the Codex family assets; retains their native dimensions and alpha.</summary>
    public sealed class CodexFamilyAssetImporter : AssetPostprocessor {
        void OnPreprocessTexture(){
            if(!assetPath.StartsWith("Assets/Game/CodexSystem/Resources/UI/Codex20261026/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.alphaIsTransparency=true;importer.maxTextureSize=assetPath.Contains("/Art/")?2048:512;
            importer.textureCompression=assetPath.Contains("/Art/")?TextureImporterCompression.CompressedHQ:TextureImporterCompression.Uncompressed;
        }
    }
}

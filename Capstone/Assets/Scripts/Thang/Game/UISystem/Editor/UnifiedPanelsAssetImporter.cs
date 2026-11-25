using UnityEditor;
using UnityEngine;
namespace Capstone.Game.UISystem.Editor {
    public static class UnifiedPanelsAssetImporter {
        public static void Configure(){foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Game/UISystem/Resources/UI/Unified20261024"})){var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;if(importer==null)continue;if(importer.textureType==TextureImporterType.Default&&!importer.mipmapEnabled&&!importer.isReadable&&importer.alphaIsTransparency&&importer.npotScale==TextureImporterNPOTScale.None&&importer.wrapMode==TextureWrapMode.Clamp&&importer.filterMode==FilterMode.Bilinear&&importer.maxTextureSize==2048&&importer.textureCompression==TextureImporterCompression.Uncompressed)continue;importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}}
    }
}

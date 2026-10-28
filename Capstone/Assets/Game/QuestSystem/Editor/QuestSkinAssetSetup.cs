using System.IO;
using UnityEditor;
using UnityEngine;
namespace Capstone.Game.QuestSystem.Editor {
    public static class QuestSkinAssetSetup {
        public const string Root="Assets/Game/QuestSystem/UI/Resources/UI/Quest20261023/";
        [MenuItem("Tools/Capstone/Quest Skin/Configure Runtime Textures")]
        public static void Configure(){if(!Directory.Exists(Root))return;foreach(var file in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories)){
                string path=file.Replace('\\','/');var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);importer=AssetImporter.GetAtPath(path) as TextureImporter;}if(importer==null)continue;
                bool change=importer.textureType!=TextureImporterType.Default||importer.mipmapEnabled||importer.isReadable||!importer.alphaIsTransparency||importer.npotScale!=TextureImporterNPOTScale.None||importer.wrapMode!=TextureWrapMode.Clamp||importer.filterMode!=FilterMode.Bilinear||importer.maxTextureSize!=2048||importer.textureCompression!=TextureImporterCompression.Uncompressed;
                if(!change)continue;importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }}
    }
}

using UnityEditor;
using UnityEngine;
namespace Capstone.Game.HudSystem.Editor {
    public sealed class HubR11AssetImporter:AssetPostprocessor {
        const string Root="Assets/Game/HudSystem/Resources/UI/HubR11/";
        public override uint GetVersion()=>1;
        void OnPreprocessTexture(){if(!assetPath.StartsWith(Root))return;Configure((TextureImporter)assetImporter);}
        static void Configure(TextureImporter t){t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=100;t.alphaIsTransparency=true;t.sRGBTexture=true;t.mipmapEnabled=false;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=1024;t.npotScale=TextureImporterNPOTScale.None;t.isReadable=false;var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);}
        [MenuItem("Game Tools/GameToolThang/HUD/R11/Reimport separated HUD assets")]
        public static void Reimport(){foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Root.TrimEnd('/')})){string path=AssetDatabase.GUIDToAssetPath(guid);if(AssetImporter.GetAtPath(path) is TextureImporter t){Configure(t);t.SaveAndReimport();}}}
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Capstone.Game.HudSystem.Editor {
    public sealed class CompanionAssetImporter : AssetPostprocessor {
        public const string Root="Assets/ThangTest/UIThang/GUIArt/Resources/UI/Companion/";
        [Serializable]class Manifest{public Entry[] components;}
        [Serializable]class Entry{public string id;public Slice slice;}
        [Serializable]class Slice{public int left,top,right,bottom;}
        void OnPreprocessTexture(){
            if(!assetPath.StartsWith(Root,StringComparison.Ordinal)||!assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return;
            var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
            importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.maxTextureSize=assetPath.Contains("/Art/")?1024:256;
            importer.textureCompression=assetPath.Contains("/Art/")?TextureImporterCompression.CompressedHQ:TextureImporterCompression.Uncompressed;
            importer.spriteBorder=Vector4.zero;
            if(assetPath.Contains("/Components/")){
                var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"Metadata/component-manifest.json"));
                var entry=Array.Find(manifest.components,e=>e.id==Path.GetFileNameWithoutExtension(assetPath));
                if(entry!=null)importer.spriteBorder=new Vector4(entry.slice.left,entry.slice.bottom,entry.slice.right,entry.slice.top);
            }
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        }
    }
}

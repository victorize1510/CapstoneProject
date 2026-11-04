using System;
using System.IO;
using Capstone.Game.MapSystem.UI;
using Capstone.Game.UISystem;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Capstone.Game.MapSystem.Editor {
    public static class MapR5AssetImporter {
        internal const string Root="Assets/Game/MapSystem/Resources/UI/MapR5/";
        internal static void Configure(TextureImporter importer) {
            bool sprite=importer.assetPath.Contains("/Icons/")||importer.assetPath.Contains("/Effects/")||importer.assetPath.EndsWith("__mask.png");
            importer.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.spritePivot=new Vector2(.5f,.5f);importer.spriteBorder=Vector4.zero;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.maxTextureSize=sprite&&!importer.assetPath.Contains("/Components/")?128:2048;
            importer.textureCompression=sprite?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
        }
    }
    [InitializeOnLoad]
    public static class MapR5AssetSetup {
        const string ThemePath="Assets/Game/MapSystem/Generated/UI/WorldMapUITheme.asset";
        static MapR5AssetSetup() {EditorApplication.delayCall+=Install;}
        [MenuItem("Game Tools/GameToolThang/Map/Install Map R5 Assets")]
        public static void Install() {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Install;return;}
            var theme=AssetDatabase.LoadAssetAtPath<WorldMapUITheme>(ThemePath);if(theme==null||theme.presentationRevision==5)return;
            foreach(string path in Directory.GetFiles(MapR5AssetImporter.Root,"*.png",SearchOption.AllDirectories)) {
                var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;
                if(importer==null)continue;MapR5AssetImporter.Configure(importer);importer.SaveAndReimport();
            }
            theme.font=FontAsset("MapR5Body",MenuTypography.Font);
            theme.titleFont=FontAsset("MapR5Title",TrainerMenuArt.Serif);
            theme.background=GreenAngularGeometry.Shell;theme.text=GreenAngularGeometry.Ink;theme.border=GreenAngularGeometry.Edge;theme.accent=GreenAngularGeometry.Primary;theme.gold=GreenAngularGeometry.Gold;
            theme.playerIcon=Icon("player");theme.waypointIcon=Icon("waypoint");theme.emblem=Icon("compass");
            string[] groups={"npc","pet","enemy","boss","quest","shop","travel"};
            for(int i=0;i<7;i++)theme.markers[i]=new WorldMapUITheme.MarkerStyle{icon=Icon(groups[i]),color=Color.white};
            theme.presentationRevision=5;EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
            Debug.Log("Map R5 assets installed: 24 original-colour glyphs, polygon viewport mask and Map-only TMP fonts. Scene/provider unchanged.");
        }
        static Sprite Icon(string id)=>AssetDatabase.LoadAssetAtPath<Sprite>(MapR5AssetImporter.Root+"Icons/"+id+".png");
        static TMP_FontAsset FontAsset(string name,Font source) {
            string path="Assets/Game/MapSystem/Generated/UI/"+name+".asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font!=null)return font;
            if(source==null)throw new InvalidOperationException("Missing shared font for Map R5 "+name);
            font=TMP_FontAsset.CreateFontAsset(source,90,9,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);font.name=name;
            AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
            font.TryAddCharacters("BẢN ĐỒ Khám phá Điểm đánh dấu VỊ TRÍ NGƯỜI CHƠI Trở lại Nhân vật Thú Kẻ địch Trùm Nhiệm vụ Cửa hàng Dịch chuyển Bộ lọc nhóm Xóa dấu Chọn điểm Chưa có dữ liệu bản đồ Bạn ĐANG THEO DÕI ĐIỂM ĐÃ CHỌN 0123456789 X Z N E S W · — + −");
            EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);return font;
        }
    }
}

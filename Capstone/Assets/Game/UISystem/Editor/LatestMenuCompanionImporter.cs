using UnityEditor;
using UnityEngine;
namespace Capstone.Game.UISystem.Editor {
    public sealed class LatestMenuCompanionImporter : AssetPostprocessor {
        void OnPreprocessTexture(){
            if((!assetPath.StartsWith("Assets/Game/UISystem/Resources/UI/Latest20261020/")&&!assetPath.StartsWith("Assets/Game/UISystem/Resources/UI/InteractionR3/"))||!assetPath.EndsWith(".png"))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;
            t.mipmapEnabled=false;t.isReadable=false;t.alphaIsTransparency=true;t.sRGBTexture=true;t.wrapMode=TextureWrapMode.Clamp;
            t.filterMode=FilterMode.Bilinear;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;t.spriteBorder=Vector4.zero;
            var s=new TextureImporterSettings();t.ReadTextureSettings(s);s.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(s);
        }
    }
}

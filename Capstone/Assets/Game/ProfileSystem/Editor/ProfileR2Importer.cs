using UnityEditor;
using UnityEngine;
namespace Capstone.Game.ProfileSystem.Editor {
    public sealed class ProfileR2Importer:AssetPostprocessor {
        void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Game/ProfileSystem/Resources/UI/ProfileR2/")||!assetPath.EndsWith(".png"))return;
            var t=(TextureImporter)assetImporter;t.textureType=assetPath.Contains("/Icons/")?TextureImporterType.Sprite:TextureImporterType.Default;t.spriteImportMode=SpriteImportMode.Single;
            t.npotScale=TextureImporterNPOTScale.None;t.mipmapEnabled=false;t.isReadable=false;t.alphaIsTransparency=true;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.Uncompressed;t.spriteBorder=Vector4.zero;
        }
    }
}

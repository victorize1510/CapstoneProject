using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.HudSystem {
    /// <summary>Presentation only: large illustration is deliberately separate from the HUD avatar. Never saved in gameplay.</summary>
    [CreateAssetMenu(menuName="Capstone/UI/Companion portrait catalog")]
    public sealed class PetCompanionArtCatalog : ScriptableObject {
        [Serializable] public sealed class Entry {
            public string definitionId;
            public string formId;
            public Sprite largeIllustration;
            public Sprite avatarCrop;
        }
        public List<Entry> entries=new List<Entry>();
        public Entry Find(PetCollectionMetadata metadata) {
            if(metadata==null)return null;
            return entries.Find(e=>e!=null&&string.Equals(e.definitionId,metadata.DefinitionId,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(e.formId,metadata.CurrentFormId,StringComparison.OrdinalIgnoreCase));
        }
    }
    internal static class CompanionArt {
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        // Neutral UI fallback: never substitutes invented artwork or a letter for a missing skill sprite.
        public static void SetSkillImage(VisualElement icon,Label fallback,Sprite sprite,bool locked=false){
            if(icon==null)return;
            icon.AddToClassList("companion-skill-art");
            icon.style.backgroundImage=StyleKeyword.None;
            var surface=icon.Q<Capstone.Game.UISystem.AngularSurface>("latest-skill-surface");
            if(surface==null){surface=new Capstone.Game.UISystem.AngularSurface{name="latest-skill-surface",Shape="skill-oct",Role="primary",ArtCrop=false,ArtInset=8};surface.style.position=Position.Absolute;surface.style.left=surface.style.top=surface.style.right=surface.style.bottom=0;icon.Insert(0,surface);}
            Capstone.Game.UISystem.AngularSurface.SetArt(surface,sprite);
            if(fallback==null)return;
            fallback.text=string.Empty;fallback.AddToClassList("companion-skill-fallback");
            fallback.style.backgroundImage=new StyleBackground(Capstone.Game.UISystem.LatestMenuCompanionAssets.Load(locked?"Masks/lock":"Masks/skills"));
            fallback.style.display=sprite!=null?DisplayStyle.None:DisplayStyle.Flex;
        }
        public static Sprite Load(string name){
            if(sprites.TryGetValue(name,out var result))return result;
            var source=Resources.Load<Sprite>("UI/Companion/"+name);if(source==null)return null;
            Vector4 crop; Vector2 size;
            switch(name){
                case "Art/champagne-leaf-corner":size=new Vector2(1254,1254);crop=new Vector4(220,178,880,824);break;
                case "Art/companion-title-emblem":size=new Vector2(1254,1254);crop=new Vector4(135,110,983,1016);break;
                default:sprites[name]=source;return source;
            }
            var texture=source.texture;
            result=Sprite.Create(texture,new Rect(crop.x/size.x*texture.width,(1-(crop.y+crop.w)/size.y)*texture.height,crop.z/size.x*texture.width,crop.w/size.y*texture.height),Vector2.one*.5f,100);
            result.name="CompanionCrop_"+name;result.hideFlags=HideFlags.HideAndDontSave;sprites[name]=result;return result;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){
            foreach(var sprite in sprites.Values)if(sprite!=null&&sprite.name.StartsWith("CompanionCrop_")){if(Application.isPlaying)UnityEngine.Object.Destroy(sprite);else UnityEngine.Object.DestroyImmediate(sprite);}sprites.Clear();
        }
    }
}


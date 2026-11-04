using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.SettingsSystem {
    /// <summary>FINAL artwork only. Does not own settings or pet data.</summary>
    public static class SettingsFinalArt {
        const string Root="UI/SettingsFinal20261102/";
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        public static Texture2D Texture(string key){if(!textures.TryGetValue(key,out var value)){value=Resources.Load<Texture2D>(Root+key);textures[key]=value;}return value;}
        public static Sprite Sprite(string key){if(!sprites.TryGetValue(key,out var value)){value=Resources.Load<Sprite>(Root+key);sprites[key]=value;}return value;}
        public static void Icon(VisualElement element,string id){if(element==null)return;element.style.backgroundImage=new StyleBackground(Texture("Icons/"+id));element.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);element.style.unityBackgroundImageTintColor=Color.white;element.pickingMode=PickingMode.Ignore;}
        public static string StatKey(string key){switch(key){case "hp":return "hp";case "atk":case "attack":return "attack";case "special_attack":case "magic-atk":case "magic-attack":return "special_attack";case "def":case "defense":return "defense";case "special_defense":case "magic-def":case "magic-defense":return "special_defense";case "spd":case "speed":return "speed";case "crit_rate":case "crit-rate":case "critical-rate":return "crit_rate";case "crit_damage":case "crit-dmg":case "critical-damage":return "crit_damage";default:return null;}}
        public static bool StatIcon(VisualElement element,string key,bool coral=false){var id=StatKey(key);if(id==null)return false;Icon(element,coral&&id=="hp"?"hp-coral":id);return true;}
        public static void BindStatRow(VisualElement row,string key,string labelClass){
            if(row==null||StatKey(key)==null)return;
            var icon=row.Q("final-stat-icon");if(icon==null){icon=new VisualElement{name="final-stat-icon",pickingMode=PickingMode.Ignore};icon.style.position=Position.Absolute;icon.style.left=0;icon.style.top=2;icon.style.width=icon.style.height=20;row.Add(icon);}
            StatIcon(icon,key);var label=row.Q<Label>(className:labelClass);if(label!=null){label.style.paddingLeft=28;label.style.unityTextAlign=TextAnchor.MiddleLeft;}
        }
    }
}

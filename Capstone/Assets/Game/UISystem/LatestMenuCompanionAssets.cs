using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.UISystem {
    /// <summary>Loose current-design components, cached on first use. No screen images or game data.</summary>
    public static class LatestMenuCompanionAssets {
        public const string Root="UI/Latest20261020/";
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        public static Sprite Load(string path){if(!cache.TryGetValue(path,out var value)){value=Resources.Load<Sprite>(Root+path);cache[path]=value;}return value;}
        public static Sprite Polish(string name){string key="polish/"+name;if(!cache.TryGetValue(key,out var value)||value==null){value=Resources.Load<Sprite>("UI/Polish20261022/"+name);cache[key]=value;}return value;}
        public static Texture Surface(string shape,string role,float width,float height,int tab=0){
            int w=Mathf.RoundToInt(width),h=Mathf.RoundToInt(height);
            if(shape=="union")return Load("Surfaces/union_panel_864_620_"+tab)?.texture;
            if(role=="track"||role=="hp"||role=="exp"||w<36||h<36)return null;
            string key="Surfaces/"+shape+"_"+role+"_"+w+"_"+h;
            var exact=Load(key);if(exact!=null)return exact.texture;
            if(role=="panel"){role="raised";exact=Load("Surfaces/"+shape+"_"+role+"_"+w+"_"+h);if(exact!=null)return exact.texture;}
            // Yoga may round a logical edge by one pixel at fractional scaling.
            foreach(int dw in new[]{-1,1}){exact=Load("Surfaces/"+shape+"_"+role+"_"+(w+dw)+"_"+h);if(exact!=null)return exact.texture;}
            return null;
        }
    }
}

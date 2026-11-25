using System.Collections.Generic;
using UnityEngine;
namespace Capstone.Game.UISystem {
    /// <summary>R3 component art only; all text and game data remain live UI.</summary>
    public static class InteractionR3Assets {
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        public static Sprite Load(string path){if(!cache.TryGetValue(path,out var sprite)){sprite=Resources.Load<Sprite>("UI/InteractionR3/"+path);cache[path]=sprite;}return sprite;}
        public static Texture Texture(string path){var sprite=Load(path);return sprite!=null?sprite.texture:null;}
    }
}

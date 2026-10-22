using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.UISystem {
    /// <summary>Shared art references for uGUI. UI Toolkit uses the same PNGs in GuiKit.uss.</summary>
    public static class GuiKitAssets {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite Icon(string id) => Load("Icons/" + id);
        public static Sprite Frame(string id) => Load("Frames/" + id);
        public static Sprite Decoration(string id) => Load("Decor/" + id);
        public static Sprite Background(string id = "menu_canvas") => Load("Backgrounds/" + id);
        static Sprite Load(string key) {
            if (!sprites.TryGetValue(key, out var sprite)) {
                sprite = Resources.Load<Sprite>("UI/GuiKit/" + key);
                if (sprite == null) Debug.LogWarning("Thiếu tài nguyên GUI: " + key);
                sprites[key] = sprite;
            }
            return sprite;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => sprites.Clear();
    }
}

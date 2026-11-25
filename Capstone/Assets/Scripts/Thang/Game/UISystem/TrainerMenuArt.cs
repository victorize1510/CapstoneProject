using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.UISystem {
    /// <summary>Menu-only art/font. Crops use the supplied alpha bounds, never gameplay data.</summary>
    public static class TrainerMenuArt {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Font serif;
        public static Font Serif => serif != null ? serif : serif = Resources.Load<Font>("UI/TrainerMenu/Polish/Fonts/NotoSerif-Regular");
        public static Sprite Polish(string name) => Load("Polish/" + name);
        public static Sprite Load(string name) {
            if (cache.TryGetValue(name, out var cached)) return cached;
            var source = Resources.Load<Sprite>("UI/TrainerMenu/" + name);
            if (source == null) { Debug.LogWarning("Thiếu asset Menu: " + name); return null; }
            // x/y from the top-left; the dimensions refer to untouched source PNGs.
            Vector4 bounds; Vector2 size; float corner = 0;
            switch (name) {
                case "01-backpack": size = new Vector2(1254,1254); bounds = new Vector4(48,18,1189,1214); break;
                case "02-profile-corner": size = new Vector2(1536,1024); bounds = new Vector4(389,4,1118,1009); break;
                case "03-quest-board-corner": size = new Vector2(1312,1199); bounds = new Vector4(176,81,1073,1054); break;
                case "04-terrain-map-corner": size = new Vector2(1536,1024); bounds = new Vector4(171,68,1356,807); break;
                case "05-codex-book-corner": size = new Vector2(1536,1024); bounds = new Vector4(143,1,1393,951); break;
                case "Polish/06-primary-frame-light": size = new Vector2(1448,1086); bounds = new Vector4(25,68,1398,952); corner = 142; break;
                case "Polish/07-secondary-frame-light": size = new Vector2(2073,758); bounds = new Vector4(22,71,2025,610); corner = 160; break;
                case "V4/10-v4-unified-shell-border": size = new Vector2(1231,1277); bounds = new Vector4(29,16,1173,1224); corner = 180; break;
                case "V4/11-v4-navigation-border": size = new Vector2(2018,779); bounds = new Vector4(81,123,1857,497); corner = 180; break;
                default: cache[name] = source; return source;
            }
            var texture = source.texture;
            var crop = new Rect(bounds.x / size.x * texture.width,
                (1 - (bounds.y + bounds.w) / size.y) * texture.height,
                bounds.z / size.x * texture.width, bounds.w / size.y * texture.height);
            float edge = corner / size.x * texture.width;
            var sprite = Sprite.Create(texture, crop, new Vector2(.5f,.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * edge);
            sprite.name = "TrainerMenuCrop_" + name; sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[name] = sprite; return sprite;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() {
            foreach (var sprite in cache.Values) if (sprite != null && sprite.name.StartsWith("TrainerMenuCrop_")) {
                if (Application.isPlaying) Object.Destroy(sprite); else Object.DestroyImmediate(sprite);
            }
            cache.Clear(); serif = null;
        }
    }
}

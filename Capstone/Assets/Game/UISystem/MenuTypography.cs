using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>uGUI equivalents of MenuTheme.uss screen colors.</summary>
    public static class MenuPalette {
        public static readonly Color Background = new Color32(248,244,235,255);
        public static readonly Color Surface = Color.white;
        public static readonly Color HeadingAccent = new Color32(84,130,91,255);
    }

    /// <summary>References the existing font; no duplicate font or runtime OS dependency.</summary>
    public sealed class MenuTypography : ScriptableObject {
        public Font font;
        static Font cached;
        public const int TitleSize = 38;
        public const int BackSize = 15;
        public const int HeaderHeight = 76;
        public const int BackWidth = 146;
        public const int BackHeight = 42;
        public static void StyleBack(Button button) {
            button.targetGraphic.color = new Color(1f, .992f, .965f);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.894f, .929f, .855f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.82f, .88f, .78f);
            button.colors = colors;
            if (button.TryGetComponent(out Outline border)) {
                border.effectColor = new Color(.663f, .741f, .612f);
                border.effectDistance = new Vector2(1, -1);
                if (!button.TryGetComponent(out MenuBackFocus focus)) focus = button.gameObject.AddComponent<MenuBackFocus>();
                focus.Configure(border,border.effectColor);
            }
            var label = button.GetComponentInChildren<Text>();
            if (label != null) {
                label.font = Font; label.text = VietnameseUI.Back; label.fontSize = BackSize;
                label.color = new Color(.192f, .341f, .239f); label.fontStyle = FontStyle.Bold;
            }
            var tmp = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) { tmp.text = VietnameseUI.Back; tmp.fontSize = BackSize; tmp.color = new Color(.192f, .341f, .239f); }
        }
        public static Font Font {
            get {
                if (cached != null) return cached;
                var settings = Resources.Load<MenuTypography>("UI/MenuTypography");
                cached = settings != null ? settings.font : null;
                if (cached == null) {
                    Debug.LogWarning("MenuTypography thiếu font dùng chung; dùng font dự phòng của Unity.");
                    cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return cached;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cached = null;
    }
}

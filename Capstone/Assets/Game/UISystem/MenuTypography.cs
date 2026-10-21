using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>uGUI equivalents of MenuTheme.uss screen colors.</summary>
    public static class MenuPalette {
        public static readonly Color Background = GuiKitPalette.Background;
        public static readonly Color Surface = GuiKitPalette.Surface;
        public static readonly Color HeadingAccent = GuiKitPalette.Accent;
    }

    /// <summary>References the existing font; no duplicate font or runtime OS dependency.</summary>
    public sealed class MenuTypography : ScriptableObject {
        public Font font;
        static Font cached;
        public const int TitleSize = GuiKitPalette.TitleSize;
        public const int BackSize = 15;
        public const int HeaderHeight = GuiKitPalette.HeaderHeight;
        public const int BackWidth = GuiKitPalette.BackWidth;
        public const int BackHeight = GuiKitPalette.BackHeight;
        public static void StyleBack(Button button) {
            button.targetGraphic.color = GuiKitPalette.Surface;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = GuiKitPalette.Hover;
            colors.selectedColor = Color.white;
            colors.pressedColor = GuiKitPalette.Pressed;
            button.colors = colors;
            if (button.TryGetComponent(out Outline border)) {
                border.effectColor = GuiKitPalette.Border;
                border.effectDistance = new Vector2(1, -1);
                if (!button.TryGetComponent(out MenuBackFocus focus)) focus = button.gameObject.AddComponent<MenuBackFocus>();
                focus.Configure(border,border.effectColor);
            }
            var label = button.GetComponentInChildren<Text>();
            if (label != null) {
                label.font = Font; label.text = VietnameseUI.Back; label.fontSize = BackSize;
                label.color = GuiKitPalette.Text; label.fontStyle = FontStyle.Bold;
            }
            var tmp = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) { tmp.text = VietnameseUI.Back; tmp.fontSize = BackSize; tmp.color = GuiKitPalette.Text; }
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

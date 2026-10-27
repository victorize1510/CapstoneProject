using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.HudSystem {
    internal static class GameplayHudVisuals {
        public static readonly Color Paper = new Color(.68f, .72f, .80f, .94f);
        public static readonly Color Forest = Capstone.Game.UISystem.GreenAngularGeometry.Ink;
        public static readonly Color Sage = Capstone.Game.UISystem.GreenAngularGeometry.Edge;
        public static readonly Color Line = new Color(.40f, .45f, .56f, .85f);
        public static readonly Color Gold = Capstone.Game.UISystem.GreenAngularGeometry.Gold;
        public const float PetStatusScale = .65f;
        public const float PetRowWidth = 680f * PetStatusScale;
        public static Font Font;
        public static Sprite CircleMaskOverride;
        static Sprite circle;
        public static RectTransform Rect(Transform parent, string name) {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false); return (RectTransform)obj.transform;
        }
        public static HudFrameGraphic Frame(Transform parent, string name, HudFrameGraphic.Shape shape,
            Color fill, Color border, float width = 1.4f, float cut = 14f) {
            RectTransform rect = Rect(parent, name);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var graphic = rect.gameObject.AddComponent<HudFrameGraphic>();
            graphic.raycastTarget = false; graphic.Configure(shape, fill, border, width, cut); return graphic;
        }
        public static Button Button(Transform parent, string name, HudFrameGraphic.Shape shape, float cut = 14f) {
            var frame = Frame(parent, name, shape, Paper, Line, 1.4f, cut);
            frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame; button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.05f, 1.05f, 1.01f);
            colors.pressedColor = new Color(.84f, .89f, .85f); colors.disabledColor = new Color(1f, 1f, 1f, .5f);
            button.colors = colors; return button;
        }
        public static Text Text(Transform parent, string name, string value, int size, TextAnchor align = TextAnchor.MiddleLeft) {
            RectTransform rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font != null ? Font : Capstone.Game.UISystem.MenuTypography.Font;
            text.text = value; text.fontSize = size;var frame=parent.GetComponentInParent<HudFrameGraphic>();text.color=frame!=null&&frame.IsDarkReading?Capstone.Game.UISystem.GreenAngularGeometry.Light:Forest; text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false; return text;
        }
        public static Image Image(Transform parent, string name) {
            RectTransform rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
            image.preserveAspect = true; return image;
        }
        public static void Icon(Image image, Sprite sprite) {
            if (image.sprite != sprite) image.sprite = sprite;
            image.enabled = sprite != null; image.color = Color.white;
        }
        public static Text Keycap(Transform parent, string key, float x, float y, float w = 48f, float h = 38f) {
            var frame = Frame(parent, "Keycap", HudFrameGraphic.Shape.Chamfered, Paper, Forest, 1.3f, 7f);
            Place(frame.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, y), new Vector2(w, h));
            Text label = Text(frame.transform, "Binding", key, 24, TextAnchor.MiddleCenter); Stretch(label.rectTransform);
            return label;
        }
        public static Image Bar(Transform parent, string name, Color fillColor, float x, float y, float w) {
            var frame = Frame(parent, name, HudFrameGraphic.Shape.Chamfered, new Color(.85f, .89f, .86f, 1f), Sage, 1f, 6f);
            Top(frame.rectTransform, x, y, w, 18f);
            var fill = Image(frame.transform, "Fill"); fill.preserveAspect = false; fill.color = fillColor;
            Stretch(fill.rectTransform, 2f); return fill;
        }
        public static void Fill(Image image, float amount) {
            image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
            image.rectTransform.offsetMax = new Vector2(-2f, -2f);
            image.enabled = amount > .001f;
        }
        public static void Diamond(Transform parent, string name, float x, float y, Color line) {
            var frame = Frame(parent, name, HudFrameGraphic.Shape.Diamond, Paper, line, 1.7f);
            Place(frame.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, y), new Vector2(13f, 18f));
        }
        public static void Stretch(RectTransform rect, float inset = 0f) {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
        }
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = pos;
            rect.sizeDelta = size; rect.localScale = Vector3.one;
        }
        public static void Top(RectTransform rect, float x, float y, float w, float h) {
            Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
        }
        public static void Bottom(RectTransform rect, float x, float y, float w, float h) {
            Place(rect, Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(w, h));
        }
        public static void Clear(Transform parent) {
            for (int i = parent.childCount - 1; i >= 0; i--) {
                GameObject obj = parent.GetChild(i).gameObject; obj.SetActive(false);
                if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
            }
        }
        public static Sprite CircleMask {
            get {
                if (CircleMaskOverride != null) return CircleMaskOverride;
                if (circle != null) return circle;
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                    name = "HudCircleMask", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(127.5f, 127.5f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(127.5f - distance) * 255));
                }
                texture.SetPixels32(pixels); texture.Apply();
                circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
                circle.hideFlags = HideFlags.HideAndDontSave; return circle;
            }
        }
    }
}

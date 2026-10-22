using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    public enum GuiButtonKind { Primary, Secondary, Danger }

    /// <summary>Creates presentation only; callers own layout and action subscriptions.</summary>
    public static class GuiKitControls {
        public static Image Background(Transform parent, string name = "MenuBackground") {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.transform.SetAsFirstSibling();
            image.sprite = GuiKitAssets.Background();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            if (image.sprite != null) {
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }
            return image;
        }
        public static Image Frame(Transform parent, string name, string frameId = "panel") {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = GuiKitAssets.Frame(frameId);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }
        public static Image Icon(Transform parent, string name, string iconId) {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = GuiKitAssets.Icon(iconId);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = GuiKitPalette.Title;
            image.raycastTarget = false;
            return image;
        }
        public static Text Label(Transform parent, string name, string text, int size = 17) {
            var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = MenuTypography.Font;
            label.text = text;
            label.fontSize = size;
            label.color = GuiKitPalette.Text;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        public static GuiKitButton Button(Transform parent, string name, string text, GuiButtonKind kind = GuiButtonKind.Secondary, string iconId = null) {
            var frame = Frame(parent, name, kind == GuiButtonKind.Primary ? "button_primary" : kind == GuiButtonKind.Danger ? "button_danger" : "button_secondary");
            frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<GuiKitButton>();
            button.targetGraphic = frame;
            var label = Label(frame.transform, "Label", text);
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, iconId == null ? 12 : 44, 8, 12, 8);
            Image icon = null;
            if (iconId != null) {
                icon = Icon(frame.transform, "Icon", iconId);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
                icon.rectTransform.anchoredPosition = new Vector2(28, 0);
                icon.rectTransform.sizeDelta = new Vector2(24, 24);
                if (string.IsNullOrEmpty(text)) {
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    icon.rectTransform.anchoredPosition = Vector2.zero;
                }
            }
            var focus = Frame(frame.transform, "Focus", "focus");
            Stretch(focus.rectTransform);
            focus.enabled = false;
            button.Configure(kind, label, icon, focus);
            return button;
        }
        public static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0) {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }
    }

}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    internal static class MapUIFactory {
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h) {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        public static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
        public static Image Panel(RectTransform rect, WorldMapUITheme theme, Color color, bool interactive = false, bool border = true) {
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = theme.roundedPanel;
            image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = interactive;
            if (border) { var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = theme.border; outline.effectDistance = new Vector2(1, -1); outline.useGraphicAlpha = false; }
            if(!interactive&&(rect.name=="SelectedMarker"||rect.name=="MarkerTooltip"||rect.name=="BottomFilterBar"||rect.name=="ZoomButtonGroup"||rect.name=="RegionPanel"||rect.name=="TrackedQuestOverlay")){
                image.color=Color.clear;var outline=rect.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
                bool onMap=rect.name=="SelectedMarker"||rect.name=="MarkerTooltip"||rect.name=="RegionPanel"||rect.name=="TrackedQuestOverlay";
                Capstone.Game.UISystem.UiSectionGraphic.Attach(rect,onMap,1.2f);
            }
            return image;
        }
        public static TextMeshProUGUI Text(Transform parent, WorldMapUITheme theme, string name, string value,
            float x, float y, float w, float h, float size, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft) {
            var rect = Rect(parent, name, x, y, w, h); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = theme.font; text.text = value; text.color = theme.text; text.fontSize = size;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            var reading=parent.GetComponentInChildren<Capstone.Game.UISystem.SoftReadingGraphic>();if(reading!=null&&reading.Dark)text.color=Capstone.Game.UISystem.GreenAngularGeometry.Light;
            var section=parent.GetComponentInChildren<Capstone.Game.UISystem.UiSectionGraphic>();
            if(section!=null)text.color=section.Dark?Capstone.Game.UISystem.GreenAngularGeometry.Light:Capstone.Game.UISystem.GreenAngularGeometry.Ink;
            text.raycastTarget = false; return text;
        }
        public static Image Icon(Transform parent, string name, Sprite sprite, float x, float y, float size, Color color) {
            var icon = Rect(parent, name, x, y, size, size).gameObject.AddComponent<Image>();
            icon.sprite = sprite; icon.color = color; icon.preserveAspect = true; icon.raycastTarget = false; return icon;
        }
        public static Button Button(RectTransform rect, WorldMapUITheme theme, string caption, int fontSize = 20) {
            var image = Panel(rect, theme, theme.background, true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.87f, .94f, .84f); colors.pressedColor = new Color(.74f, .87f, .70f);
            colors.selectedColor = colors.highlightedColor; button.colors = colors;
            Text(rect, theme, "Label", caption, 0, 0, rect.sizeDelta.x, rect.sizeDelta.y, fontSize, true, TextAlignmentOptions.Center);
            return button;
        }
    }
}

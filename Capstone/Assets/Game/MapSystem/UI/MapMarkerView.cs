using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    [RequireComponent(typeof(Button))]
    public sealed class MapMarkerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        MapMarkerData data;
        Image selection;
        RectTransform rect;
        Transform icon;
        bool hovered;
        bool visible, clustered;
        Action<MapMarkerData> selected;
        Action<MapMarkerData, bool> tooltip;
        public MapMarkerData Data => data;
        public bool IsVisible => visible;
        public void SetData(MapMarkerData value) { data = value; UpdateHighlight(); }
        public void Bind(MapMarkerData value, Image halo, Action<MapMarkerData> callback, Action<MapMarkerData, bool> hover = null) {
            data = value; selection = halo; selected = callback; tooltip = hover; rect = (RectTransform)transform;
            visible = gameObject.activeSelf; clustered = false;
            icon = transform.Find("Icon");
            var button = GetComponent<Button>(); button.onClick.RemoveListener(Select); button.onClick.AddListener(Select);
        }
        public void SetPosition(Vector2 normalized, bool visible) {
            this.visible = visible;
            data.normalizedPosition = normalized;
            rect.anchorMin = rect.anchorMax = normalized; rect.anchoredPosition = Vector2.zero;
            ApplyVisibility();
        }
        public void SetClustered(bool value) { clustered = value; ApplyVisibility(); }
        void ApplyVisibility() {
            bool shown = visible && !clustered;
            if (gameObject.activeSelf != shown) gameObject.SetActive(shown);
        }
        public void SetSelected(bool value) { data.isSelected = value; UpdateHighlight(); }
        public void SetDirection(float angle) { if (icon != null) icon.localRotation = Quaternion.Euler(0, 0, angle); }
        void Select() => selected?.Invoke(data);
        public void OnPointerEnter(PointerEventData eventData) { hovered = true; UpdateHighlight(); tooltip?.Invoke(data, true); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; UpdateHighlight(); tooltip?.Invoke(data, false); }
        void OnDisable() { if (hovered) tooltip?.Invoke(data, false); hovered = false; UpdateHighlight(); }
        void UpdateHighlight() {
            if (selection != null) selection.enabled = hovered || (data != null && data.isSelected);
            transform.localScale = Vector3.one * (hovered || (data != null && data.isSelected) ? 1.12f : 1f);
        }
    }
}

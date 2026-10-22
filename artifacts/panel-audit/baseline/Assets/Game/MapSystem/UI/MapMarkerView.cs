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
        bool hovered;
        Action<MapMarkerData> selected;
        public MapMarkerData Data => data;
        public void SetData(MapMarkerData value) { data = value; UpdateHighlight(); }
        public void Bind(MapMarkerData value, Image halo, Action<MapMarkerData> callback) {
            data = value; selection = halo; selected = callback; rect = (RectTransform)transform;
            var button = GetComponent<Button>(); button.onClick.RemoveListener(Select); button.onClick.AddListener(Select);
        }
        public void SetPosition(Vector2 normalized, bool visible) {
            data.normalizedPosition = normalized;
            rect.anchorMin = rect.anchorMax = normalized; rect.anchoredPosition = Vector2.zero;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
        public void SetSelected(bool value) { data.isSelected = value; UpdateHighlight(); }
        public void SetDirection(float angle) { transform.Find("Icon").localRotation = Quaternion.Euler(0, 0, angle); }
        void Select() => selected?.Invoke(data);
        public void OnPointerEnter(PointerEventData eventData) { hovered = true; UpdateHighlight(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; UpdateHighlight(); }
        void OnDisable() { hovered = false; UpdateHighlight(); }
        void UpdateHighlight() {
            if (selection != null) selection.enabled = hovered || (data != null && data.isSelected);
            transform.localScale = Vector3.one * (hovered || (data != null && data.isSelected) ? 1.12f : 1f);
        }
    }
}

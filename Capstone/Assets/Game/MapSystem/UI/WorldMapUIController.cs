using System;
using System.Collections.Generic;
using Capstone.Game.QuestSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class WorldMapUIController : MonoBehaviour {
        [SerializeField] WorldMapController worldMap;
        [SerializeField] WorldMapUITheme theme;
        [SerializeField] RectTransform overlayRoot;
        [SerializeField] RectTransform mapViewport;
        [SerializeField] MapMarkerManager markerManager;
        [SerializeField] QuestManager quests;
        RectTransform screen, markerContainer;
        MapSystemController mapSystem;
        Vector2 fittedViewportSize;
        RegionDisplayPanel region;
        TrackedQuestBanner questBanner;
        TMP_Text selectedLabel;
        RectTransform markerTooltip;
        TMP_Text markerTooltipText;
        MapMarkerData hoveredMarker;
        readonly Dictionary<MapMarker, MapMarkerView> sourceViews = new Dictionary<MapMarker, MapMarkerView>();
        readonly Dictionary<string, MapMarkerView> dataViews = new Dictionary<string, MapMarkerView>();
        readonly List<MapMarker> markers = new List<MapMarker>();
        readonly List<MapMarker> removed = new List<MapMarker>();
        readonly HashSet<MapMarker> registeredMarkers = new HashSet<MapMarker>();
        readonly List<Renderer> iconRenderers = new List<Renderer>();
        readonly List<bool> rendererStates = new List<bool>();
        readonly bool[] visibleGroups = { true, true, true, true, true, true, true };
        readonly Toggle[] filterToggles = new Toggle[7];
        MapMarkerClusters clusters;
        readonly List<MapMarkerView> clusterViews = new List<MapMarkerView>();
        float nextMarkerRefresh;
        bool iconsSuppressed;
        string selectedId;

        public event Action<MapMarkerData> MarkerSelected;
        public event Action<MapFilterGroup, bool> FilterChanged;
        public RectTransform MarkerContainer => markerContainer;
        public Transform NavigationRoot => clusters?.IsPopupOpen == true ? clusters.NavigationRoot : screen;
        public WorldMapUITheme Theme => theme;
        public float ViewportAspect => 1832f / 818f;

        void Awake() { mapSystem = GetComponent<MapSystemController>(); EnsureBuilt(); }
        void OnEnable() {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        void OnDisable() {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            RestoreIcons();
            clusters?.Close();
            HideMarkerTooltip();
        }
        public void Configure(WorldMapController controller, RectTransform overlay, RectTransform viewport, WorldMapUITheme assets) {
            worldMap = controller; overlayRoot = overlay; mapViewport = viewport; theme = assets;
            markerManager = GetComponent<MapMarkerManager>(); quests = FindFirstObjectByType<QuestManager>();
        }
        public void EnsureBuilt() { if (screen == null && theme != null && overlayRoot != null && mapViewport != null) Rebuild(); }

        [ContextMenu("Rebuild World Map UI")]
        public void Rebuild() {
            if (theme == null || worldMap == null || overlayRoot == null || mapViewport == null) return;
            RestoreIcons(); sourceViews.Clear(); dataViews.Clear(); selectedId = null;
            fittedViewportSize = Vector2.zero;
            for (int i = overlayRoot.childCount - 1; i >= 0; i--) {
                var child = overlayRoot.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            if (overlayRoot.TryGetComponent(out Image surface)) { surface.color = Color.clear; surface.raycastTarget = false; }
            if (overlayRoot.TryGetComponent(out Outline oldBorder)) oldBorder.enabled = false;
            MapUIFactory.Stretch(overlayRoot);
            screen = MapUIFactory.Rect(overlayRoot, "MapScreen", 0, 0, 1920, 1080);
            screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(.5f, .5f); screen.anchoredPosition = Vector2.zero;
            var header = MapUIFactory.Rect(screen, "TopHeader", 0, 0, 1920, 76);
            var emblem = MapUIFactory.Rect(header, "Emblem", 24, 26, 24, 24);
            MapUIFactory.Panel(emblem, theme, Capstone.Game.UISystem.MenuPalette.HeadingAccent, false, false).sprite = null;
            emblem.pivot = new Vector2(.5f,.5f);
            emblem.anchoredPosition = new Vector2(36,-38);
            emblem.localRotation = Quaternion.Euler(0,0,45);
            MapUIFactory.Text(header, theme, "Title", "BẢN ĐỒ", 66, 0, 300, 76, 38, true);
            MapUIFactory.Text(header, theme, "Subtitle", "Xem khu vực và các điểm đánh dấu quan trọng", 370, 0, 1060, 76, 18);
            var back = MapUIFactory.Button(MapUIFactory.Rect(header, "BackButton", 1752, 17, 146, 42), theme, Capstone.Game.UISystem.VietnameseUI.Back, 15);
            Capstone.Game.UISystem.MenuTypography.StyleBack(back);
            back.onClick.AddListener(worldMap.RequestBackFromUI);
            var divider = MapUIFactory.Rect(screen, "HeaderDivider", 24, 75, 1872, 1);
            MapUIFactory.Panel(divider, theme, theme.border, false, false);
            var frame = MapUIFactory.Rect(screen, "MapFrame", 32, 112, 1856, 842);
            var frameImage = MapUIFactory.Panel(frame, theme, theme.border, false, false);
            frameImage.sprite = theme.roundedBorder;
            Compass("N", 942, 94); Compass("E", 1870, 515); Compass("S", 942, 936); Compass("W", 14, 515);

            markerContainer = MapUIFactory.Rect(screen, "MarkerContainer", 44, 124, 1832, 818);
            markerContainer.gameObject.AddComponent<RectMask2D>();
            clusters = new MapMarkerClusters(markerContainer, theme, SelectMarker);
            var questRect = MapUIFactory.Rect(screen, "TrackedQuestOverlay", 64, 146, 360, 56);
            MapUIFactory.Panel(questRect, theme, new Color(1, 1, 1, .83f));
            MapUIFactory.Icon(questRect, "QuestIcon", theme.markers[(int)MapFilterGroup.Quest].icon, 12, 10, 36, Color.white);
            var questText = MapUIFactory.Text(questRect, theme, "TrackedQuestTitle", "", 62, 0, 284, 56, 22, true);
            questBanner = questRect.gameObject.AddComponent<TrackedQuestBanner>(); questBanner.Bind(quests, questText);
            var regionRect = MapUIFactory.Rect(screen, "RegionPanel", 764, 146, 392, 56);
            MapUIFactory.Panel(regionRect, theme, new Color(1, 1, 1, .83f));
            MapUIFactory.Icon(regionRect, "LocationIcon", theme.waypointIcon, 12, 12, 32, Color.white);
            region = regionRect.gameObject.AddComponent<RegionDisplayPanel>();
            region.Bind(MapUIFactory.Text(regionRect, theme, "Region", "Tọa độ: X 0, Z 0", 60, 0, 316, 56, 21, true));
            var zoom = MapUIFactory.Rect(screen, "ZoomButtonGroup", 1788, 612, 72, 246);
            MapUIFactory.Panel(zoom, theme, new Color(1, 1, 1, .75f));
            ActionButton(zoom, "ZoomIn", "+", 4, 8, "map-zoom-in", () => worldMap.Zoom(1));
            ActionButton(zoom, "ZoomOut", "−", 4, 88, "map-zoom-out", () => worldMap.Zoom(-1));
            ActionButton(zoom, "Center", "⊙", 4, 168, "map-center", worldMap.CenterOnPlayer);

            var filters = MapUIFactory.Rect(screen, "BottomFilterBar", 218, 978, 1320, 64);
            MapUIFactory.Panel(filters, theme, new Color(1, 1, 1, .9f));
            for (int i = 0; i < 7; i++) BuildFilter(filters, (MapFilterGroup)i, i);
            var clear = MapUIFactory.Button(MapUIFactory.Rect(screen, "ClearWaypointButton", 1570, 978, 318, 64), theme, "Xóa điểm đánh dấu", 22);
            clear.gameObject.AddComponent<MapActionButton>().Bind(this, "map-clear", worldMap.ClearWaypoint);
            var selected = MapUIFactory.Rect(screen, "SelectedMarker", 64, 860, 360, 56);
            MapUIFactory.Panel(selected, theme, new Color(1, 1, 1, .85f));
            selectedLabel = MapUIFactory.Text(selected, theme, "Name", "", 16, 0, 328, 56, 22, true); selected.gameObject.SetActive(false);
            markerTooltip = MapUIFactory.Rect(screen, "MarkerTooltip", 0, 0, 340, 72);
            markerTooltip.anchorMin = markerTooltip.anchorMax = new Vector2(.5f, .5f);
            markerTooltip.pivot = new Vector2(0, 1);
            MapUIFactory.Panel(markerTooltip, theme, theme.background, false);
            markerTooltipText = MapUIFactory.Text(markerTooltip, theme, "Text", "", 12, 6, 316, 60, 18);
            markerTooltipText.raycastTarget = false;
            markerTooltipText.overflowMode = TextOverflowModes.Ellipsis;
            HideMarkerTooltip();
            worldMap.UsePresentation(this, overlayRoot);
            FitViewport(); nextMarkerRefresh = 0;
        }
        void Compass(string caption, float x, float y) {
            var rect = MapUIFactory.Rect(screen, "Compass" + caption, x, y, 36, 36);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition += new Vector2(18, -18);
            var diamond = MapUIFactory.Panel(rect, theme, theme.background);
            diamond.sprite = null; diamond.type = Image.Type.Simple;
            rect.localRotation = Quaternion.Euler(0, 0, 45);
            var label = MapUIFactory.Text(rect, theme, "Direction", caption, 0, 0, 36, 36, 22, true, TextAlignmentOptions.Center);
            label.rectTransform.pivot = new Vector2(.5f, .5f); label.rectTransform.anchoredPosition = new Vector2(18,-18);
            label.transform.localRotation = Quaternion.Euler(0, 0, -45);
        }
        void ActionButton(Transform parent, string name, string caption, float x, float y, string key, Action action) {
            var button = MapUIFactory.Button(MapUIFactory.Rect(parent, name, x, y, 64, 64), theme, caption, 36);
            button.image.sprite = theme.circle; button.image.type = Image.Type.Simple;
            if (name == "Center") {
                button.GetComponentInChildren<TMP_Text>().text = string.Empty;
                MapUIFactory.Icon(button.transform, "CenterTarget", theme.circleBorder, 18,18,28,theme.text);
                MapUIFactory.Panel(MapUIFactory.Rect(button.transform,"Horizontal",12,30,40,4),theme,theme.text,false,false);
                MapUIFactory.Panel(MapUIFactory.Rect(button.transform,"Vertical",30,12,4,40),theme,theme.text,false,false);
            }
            button.gameObject.AddComponent<MapActionButton>().Bind(this, key, action);
        }
        void BuildFilter(Transform parent, MapFilterGroup group, int index) {
            var rect = MapUIFactory.Rect(parent, "Filter" + group, 22 + index * 184, 12, 174, 40);
            var hit = MapUIFactory.Panel(rect, theme, Color.clear, true, false);
            var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = hit;
            filterToggles[index] = toggle;
            var checkbox = MapUIFactory.Rect(rect, "Checkbox", 0, 8, 24, 24);
            MapUIFactory.Panel(checkbox, theme, theme.background);
            var check = MapUIFactory.Panel(MapUIFactory.Rect(checkbox, "Checkmark", 4, 4, 16, 16), theme, theme.accent, false, false);
            toggle.graphic = check;
            MapUIFactory.Icon(rect, "Icon", theme.markers[index].icon, 34, 4, 32, Color.white);
            MapUIFactory.Text(rect, theme, "Label", FilterName(group), 76, 0, 96, 40, 17);
            rect.gameObject.AddComponent<MapFilterToggle>().Bind(group, visibleGroups[index], SetFilter);
        }
        public void FitViewport() {
            if (screen == null || mapViewport == null) return;
            Vector2 size = overlayRoot.rect.size;
            if (size == fittedViewportSize) return;
            float scale = Mathf.Min(overlayRoot.rect.width / 1920f, overlayRoot.rect.height / 1080f);
            if (scale <= 0) return;
            fittedViewportSize = size;
            screen.localScale = Vector3.one * scale;
            mapViewport.anchorMin = mapViewport.anchorMax = mapViewport.pivot = new Vector2(.5f, .5f);
            mapViewport.sizeDelta = new Vector2(1832, 818) * scale; mapViewport.anchoredPosition = new Vector2(0, 7) * scale;
            mapViewport.localScale = Vector3.one;
            if (mapViewport.TryGetComponent(out Image mask)) { mask.sprite = theme.roundedPanel; mask.type = Image.Type.Sliced; }
        }
        static string FilterName(MapFilterGroup group) {
            switch (group) {
                case MapFilterGroup.NPC: return "Nhân vật";
                case MapFilterGroup.Pet: return "Thú";
                case MapFilterGroup.Enemy: return "Kẻ địch";
                case MapFilterGroup.Boss: return "Trùm";
                case MapFilterGroup.Quest: return "Nhiệm vụ";
                case MapFilterGroup.Shop: return "Cửa hàng";
                default: return "Dịch chuyển";
            }
        }
        public bool TryCloseMarkerGroup() { if (clusters?.IsPopupOpen != true) return false; clusters.Close(); return true; }
        public void SetFilter(MapFilterGroup group, bool visible) {
            clusters?.Close();
            HideMarkerTooltip();
            visibleGroups[(int)group] = visible;
            if (filterToggles[(int)group] != null) filterToggles[(int)group].SetIsOnWithoutNotify(visible);
            foreach (MapMarkerType type in Enum.GetValues(typeof(MapMarkerType)))
                if (WorldMapUITheme.Group(type) == group) worldMap.SetFilter(type, visible);
            foreach (var view in dataViews.Values) UpdateDataVisibility(view);
            FilterChanged?.Invoke(group, visible);
        }
        public bool IsFilterVisible(MapFilterGroup group) => visibleGroups[(int)group];
        public void SetTrackedQuestTitle(string title) => questBanner?.SetTitle(title);
        public void UpdateRegion(Vector3 position) => region?.SetPosition(position);

        public MapMarkerView RenderMarker(MapMarkerData data) {
            if (data == null || string.IsNullOrWhiteSpace(data.id)) throw new ArgumentException("Marker requires an id.");
            EnsureBuilt();
            if (!dataViews.TryGetValue(data.id, out var view)) { view = CreateMarker(data); dataViews.Add(data.id, view); }
            else { view.SetData(data); ApplyMarkerAppearance(view); }
            UpdateDataVisibility(view); return view;
        }
        public bool RemoveMarker(string id) {
            if (!dataViews.TryGetValue(id, out var view)) return false;
            dataViews.Remove(id);
            if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            if (selectedId == id) { selectedId = null; selectedLabel.transform.parent.gameObject.SetActive(false); }
            return true;
        }
        void UpdateDataVisibility(MapMarkerView view) {
            var data = view.Data; var group = WorldMapUITheme.Group(data.type);
            view.SetPosition(data.normalizedPosition, data.isVisible && (!group.HasValue || visibleGroups[(int)group.Value]));
        }
        MapMarkerView CreateMarker(MapMarkerData data) {
            var rect = MapUIFactory.Rect(markerContainer, "Marker_" + data.id, 0, 0, 52, 52); rect.pivot = new Vector2(.5f, .5f);
            var group = WorldMapUITheme.Group(data.type);
            var color = group.HasValue ? theme.markers[(int)group.Value].color : theme.accent;
            var image = MapUIFactory.Panel(rect, theme, color, true); image.sprite = theme.circle; image.type = Image.Type.Simple;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var halo = MapUIFactory.Icon(rect, "Selection", theme.circleBorder, -5, -5, 62, theme.gold);
            halo.transform.SetAsFirstSibling(); halo.enabled = false;
            Sprite sprite = group.HasValue ? theme.markers[(int)group.Value].icon
                : data.type == MapMarkerType.Player ? theme.playerIcon : theme.waypointIcon;
            MapUIFactory.Icon(rect, "Icon", sprite, 4, 4, 44, Color.white);
            var view = rect.gameObject.AddComponent<MapMarkerView>(); view.Bind(data, halo, SelectMarker, HoverMarker); return view;
        }
        void ApplyMarkerAppearance(MapMarkerView view) {
            var group = WorldMapUITheme.Group(view.Data.type);
            view.GetComponent<Image>().color = group.HasValue ? theme.markers[(int)group.Value].color : theme.accent;
            view.transform.Find("Icon").GetComponent<Image>().sprite = group.HasValue ? theme.markers[(int)group.Value].icon
                : view.Data.type == MapMarkerType.Player ? theme.playerIcon : theme.waypointIcon;
        }
        void SelectMarker(MapMarkerData data) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "select-map-marker")) return;
            selectedId = data.id;
            foreach (var view in sourceViews.Values) view.SetSelected(view.Data.id == selectedId);
            foreach (var view in dataViews.Values) view.SetSelected(view.Data.id == selectedId);
            selectedLabel.transform.parent.gameObject.SetActive(true); selectedLabel.text = data.displayName;
            MarkerSelected?.Invoke(data);
        }
        void HoverMarker(MapMarkerData data, bool visible) {
            if (!visible) { if (hoveredMarker == data) HideMarkerTooltip(); return; }
            if (markerTooltip == null) return;
            hoveredMarker = data;
            string name = string.IsNullOrWhiteSpace(data.displayName) ? "Điểm đánh dấu" : data.displayName;
            var group = WorldMapUITheme.Group(data.type);
            string category = group.HasValue ? FilterName(group.Value) : data.type == MapMarkerType.Player ? "Bạn" : "Điểm đánh dấu";
            markerTooltipText.text = name + "\n" + category + " • Nhấp để chọn";
            markerTooltip.gameObject.SetActive(true);
            markerTooltip.SetAsLastSibling();
        }
        void HideMarkerTooltip() {
            hoveredMarker = null;
            if (markerTooltip != null) markerTooltip.gameObject.SetActive(false);
        }
        void UpdateMarkerTooltipPosition() {
            if (hoveredMarker == null || markerTooltip == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(screen, Input.mousePosition, null, out var point)) return;
            point += new Vector2(18, -18);
            point.x = Mathf.Clamp(point.x, screen.rect.xMin + 8, screen.rect.xMax - markerTooltip.rect.width - 8);
            point.y = Mathf.Clamp(point.y, screen.rect.yMin + markerTooltip.rect.height + 8, screen.rect.yMax - 8);
            markerTooltip.anchoredPosition = point;
        }
        void LateUpdate() {
            if (worldMap == null || !worldMap.IsOpen) { clusters?.Close(); HideMarkerTooltip(); return; }
            EnsureBuilt(); if (screen == null) return;
            FitViewport();
            UpdateMarkerTooltipPosition();
            if (worldMap.MapManager != null) UpdateRegion(worldMap.MapManager.GetCameraPosition());
            if (Time.unscaledTime >= nextMarkerRefresh) { RefreshSources(); nextMarkerRefresh = Time.unscaledTime + .5f; }
            Camera camera = worldMap.MapCamera; if (camera == null) return;
            Transform viewer = mapSystem != null ? mapSystem.PlayerTarget : null;
            bool selectedVisible = false;
            foreach (var pair in sourceViews) {
                var source = pair.Key; var view = pair.Value;
                if (source == null) { view.SetPosition(view.Data.normalizedPosition, false); continue; }
                if (view.Data.type != source.MarkerType) { view.Data.type = source.MarkerType; ApplyMarkerAppearance(view); }
                view.Data.displayName = source.DisplayName;
                var position = camera.WorldToViewportPoint(source.transform.position);
                var group = WorldMapUITheme.Group(source.MarkerType);
                bool shown = source.ShouldRender(viewer, false) && source.MarkerType != MapMarkerType.CoopPlayer
                    && (!group.HasValue || visibleGroups[(int)group.Value])
                    && (markerManager == null || markerManager.IsMarkerTypeVisible(source.MarkerType))
                    && position.z > 0 && position.x >= 0 && position.x <= 1 && position.y >= 0 && position.y <= 1;
                view.SetPosition(position, shown);
                if (shown && view.Data.id == selectedId) {
                    selectedVisible = true;
                    selectedLabel.text = view.Data.displayName;
                }
                if (shown && source.MarkerType == MapMarkerType.Player) view.SetDirection(-source.transform.eulerAngles.y);
            }
            foreach (var view in dataViews.Values) UpdateDataVisibility(view);
            if (selectedId != null && dataViews.TryGetValue(selectedId, out var selectedData)) {
                selectedVisible |= selectedData.IsVisible;
                if (selectedData.IsVisible) selectedLabel.text = selectedData.Data.displayName;
            }
            selectedLabel.transform.parent.gameObject.SetActive(selectedVisible);
            clusterViews.Clear();
            foreach (var view in sourceViews.Values) clusterViews.Add(view);
            foreach (var view in dataViews.Values) clusterViews.Add(view);
            clusters?.Update(clusterViews);
        }
        void RefreshSources() {
            if (markerManager == null) markerManager = GetComponent<MapMarkerManager>();
            if (markerManager == null) return;
            markerManager.CopyMarkersTo(markers); markerManager.CopyIconRenderersTo(iconRenderers);
            registeredMarkers.Clear(); foreach (var marker in markers) registeredMarkers.Add(marker);
            removed.Clear(); foreach (var pair in sourceViews) if (pair.Key == null || !registeredMarkers.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var source in removed) { Destroy(sourceViews[source].gameObject); sourceViews.Remove(source); }
            foreach (var source in markers) {
                if (source == null || sourceViews.ContainsKey(source)) continue;
                sourceViews.Add(source, CreateMarker(new MapMarkerData { id = source.MarkerId, displayName = source.DisplayName, type = source.MarkerType }));
            }
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera) {
            if (worldMap == null || !worldMap.IsOpen || camera != worldMap.MapCamera || iconsSuppressed) return;
            rendererStates.Clear();
            foreach (var renderer in iconRenderers) { rendererStates.Add(renderer != null && renderer.forceRenderingOff); if (renderer != null) renderer.forceRenderingOff = true; }
            iconsSuppressed = true;
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera) { if (worldMap != null && camera == worldMap.MapCamera) RestoreIcons(); }
        void RestoreIcons() {
            if (!iconsSuppressed) return;
            for (int i = 0; i < rendererStates.Count && i < iconRenderers.Count; i++) if (iconRenderers[i] != null) iconRenderers[i].forceRenderingOff = rendererStates[i];
            iconsSuppressed = false; rendererStates.Clear();
        }
    }
}

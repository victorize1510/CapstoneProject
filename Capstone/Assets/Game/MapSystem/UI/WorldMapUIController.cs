using System;
using System.Collections.Generic;
using Capstone.Game.QuestSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed partial class WorldMapUIController : MonoBehaviour {
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
        public Transform NavigationRoot => clusters?.IsPopupOpen == true ? clusters.NavigationRoot : overlayRoot?.parent;
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
        public void Rebuild() => BuildR5();
        public void FitViewport() => FitR5Viewport();
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
            if (filterToggles[(int)group] != null) {
                filterToggles[(int)group].SetIsOnWithoutNotify(visible);
                filterToggles[(int)group].GetComponent<MapR5Feedback>()?.Refresh();
                if(r5Compact!=null)r5Compact.Find("Compact_"+(int)group).GetComponent<Image>().color=Capstone.Game.UISystem.GreenAngularGeometry.FilterGlyph(visible);
            }
            foreach (MapMarkerType type in Enum.GetValues(typeof(MapMarkerType)))
                if (WorldMapUITheme.Group(type) == group) worldMap.SetFilter(type, visible);
            foreach (var view in dataViews.Values) UpdateDataVisibility(view);
            FilterChanged?.Invoke(group, visible);
        }
        public bool IsFilterVisible(MapFilterGroup group) => visibleGroups[(int)group];
        public void SetTrackedQuestTitle(string title) => questBanner?.SetTitle(title);
        public void UpdateRegion(Vector3 position) {if(mapSystem!=null&&mapSystem.PlayerTarget!=null)region?.SetPosition(mapSystem.PlayerTarget.position);}

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
            view.SetPosition(data.normalizedPosition, data.isVisible && MapR5Contour.ContainsNormalized(data.normalizedPosition) && (!group.HasValue || visibleGroups[(int)group.Value]));
        }
        MapMarkerView CreateMarker(MapMarkerData data) => CreateR5Marker(data);
        void ApplyMarkerAppearance(MapMarkerView view) => ApplyR5MarkerAppearance(view);
        void SelectMarker(MapMarkerData data) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "select-map-marker")) return;
            selectedId = data.id;
            foreach (var view in sourceViews.Values) view.SetSelected(view.Data.id == selectedId);
            foreach (var view in dataViews.Values) view.SetSelected(view.Data.id == selectedId);
            selectedLabel.transform.parent.gameObject.SetActive(true); selectedLabel.text = data.displayName; UpdateSelectedCard(data);
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
            if (clusters?.IsPopupOpen == true) { HideMarkerTooltip(); return; }
            var rect=mapViewport.rect;
            var centre=screen.InverseTransformPoint(mapViewport.TransformPoint(new Vector3(rect.xMin+hoveredMarker.normalizedPosition.x*rect.width,rect.yMin+hoveredMarker.normalizedPosition.y*rect.height)));
            var lower=screen.InverseTransformPoint(mapViewport.TransformPoint(new Vector3(rect.xMin,rect.yMin)));
            var upper=screen.InverseTransformPoint(mapViewport.TransformPoint(new Vector3(rect.xMax,rect.yMax)));
            float inset=20*MapR5View.Native, gap=(MapR5View.MarkerSize(hoveredMarker.type)/2+12)*MapR5View.Native;
            var size=markerTooltip.rect.size;
            var point=new Vector2(centre.x-size.x/2,centre.y+gap+size.y);
            if(point.y>upper.y-inset)point.y=centre.y-gap;
            point.x=Mathf.Clamp(point.x,lower.x+inset,upper.x-inset-size.x);
            point.y=Mathf.Clamp(point.y,lower.y+inset+size.y,upper.y-inset);
            markerTooltip.anchoredPosition=point;
        }
        void LateUpdate() {
            if (worldMap == null || !worldMap.IsOpen) { clusters?.Close(); HideMarkerTooltip(); return; }
            EnsureBuilt(); if (screen == null) return;
            RecoverR5KeyboardSubmit();if(!worldMap.IsOpen)return;
            FitViewport();
            if (!UpdateR5Availability()) return;
            UpdateMarkerTooltipPosition();
            if (worldMap.MapManager != null) UpdateRegion(worldMap.MapManager.GetCameraPosition());
            if (Time.unscaledTime >= nextMarkerRefresh) { RefreshSources(); nextMarkerRefresh = Time.unscaledTime + .5f; }
            Camera camera = worldMap.MapCamera; if (camera == null) return;
            Transform viewer = mapSystem != null ? mapSystem.PlayerTarget : null;
            bool selectedVisible = false;
            foreach (var pair in sourceViews) {
                var source = pair.Key; var view = pair.Value;
                if (source == null) { view.SetPosition(view.Data.normalizedPosition, false); continue; }
                var displayType = worldMap.IsWaypointMarker(source) ? MapMarkerType.Waypoint : source.MarkerType;
                if (view.Data.type != displayType) { view.Data.type = displayType; ApplyMarkerAppearance(view); }
                view.Data.displayName = worldMap.IsWaypointMarker(source) ? "Điểm đánh dấu" : source.DisplayName;
                var position = camera.WorldToViewportPoint(source.transform.position);
                var group = WorldMapUITheme.Group(source.MarkerType);
                bool shown = source.ShouldRender(viewer, false) && source.MarkerType != MapMarkerType.CoopPlayer
                    && (!group.HasValue || visibleGroups[(int)group.Value])
                    && (markerManager == null || markerManager.IsMarkerTypeVisible(source.MarkerType))
                    && position.z > 0 && MapR5Contour.ContainsNormalized(position);
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
                sourceViews.Add(source, CreateMarker(new MapMarkerData { id = source.MarkerId, displayName = source.DisplayName, type = worldMap.IsWaypointMarker(source) ? MapMarkerType.Waypoint : source.MarkerType }));
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

using UnityEngine;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class MapIconRegistry : MonoBehaviour {
        [SerializeField] MapMarkerManager markerManager = null;

        public MapMarkerManager MarkerManager => markerManager;

        void Awake() {
            ResolveReferences();
        }

        void Start() {
            ResolveReferences();
            markerManager?.ScanMarkers();
        }

        public void SetMarkerManager(MapMarkerManager manager) {
            markerManager = manager != null ? manager : markerManager;
        }

        public MapMarker Register(GameObject source, MapMarkerType type, string markerId, string title, Texture icon, Color color) {
            if (source == null) return null;
            ResolveReferences();

            MapMarker marker = source.GetComponent<MapMarker>();
            if (marker == null) marker = source.AddComponent<MapIcon>();
            marker.ConfigureRuntime(type, markerId, title, icon, color, true, true);
            markerManager?.RegisterMarker(marker);
            return marker;
        }

        public MapMarker Register(GameObject source, MapMarkerType type, string markerId, string title) {
            if (markerManager != null) return markerManager.EnsureRuntimeMarker(source, type, markerId, title);
            return Register(source, type, markerId, title, null, Color.white);
        }

        public void Refresh() {
            ResolveReferences();
            markerManager?.ScanMarkers();
        }

        public void SetTypeVisible(MapMarkerType type, bool visible) {
            ResolveReferences();
            markerManager?.SetMarkerTypeVisible(type, visible);
        }

        public bool IsTypeVisible(MapMarkerType type) {
            ResolveReferences();
            return markerManager == null || markerManager.IsMarkerTypeVisible(type);
        }

        void ResolveReferences() {
            if (markerManager == null) markerManager = GetComponent<MapMarkerManager>();
            if (markerManager == null) markerManager = FindFirstObjectByType<MapMarkerManager>();
        }
    }
}

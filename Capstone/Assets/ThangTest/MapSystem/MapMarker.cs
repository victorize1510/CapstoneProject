using System;
using System.Reflection;
using UnityEngine;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public class MapMarker : MonoBehaviour {
        [Header("Identity")]
        [SerializeField] string markerId = string.Empty;
        [SerializeField] MapMarkerType markerType = MapMarkerType.Custom;
        [SerializeField] string displayName = string.Empty;

        [Header("Icon")]
        [SerializeField] Texture iconTexture = null;
        [SerializeField] Color iconColor = Color.white;
        [SerializeField] Vector3 iconOffset = new Vector3(0f, 100f, 0f);
        [SerializeField] Vector3 iconScale = new Vector3(3f, 1f, 3f);
        [SerializeField] float iconRotation = 0f;

        [Header("Visibility")]
        [SerializeField] bool visible = true;
        [SerializeField] bool showOnMinimap = true;
        [SerializeField] bool showOnWorldMap = true;
        [SerializeField, Min(0f)] float minimapVisibleDistance = 0f;
        [SerializeField] bool hideWhenDead = true;
        [SerializeField] string aliveMemberName = "IsAlive";

        [Header("Rotation")]
        [SerializeField] bool rotateWithMinimapCamera = true;
        [SerializeField] bool useMapCameraRotation = true;
        [SerializeField] float mapCameraRotation = 0f;

        MonoBehaviour cachedAliveSource;
        bool aliveSourceResolved;

        public static event Action<MapMarker> MarkerEnabled;
        public static event Action<MapMarker> MarkerDisabled;
        public static event Action<MapMarker> MarkerChanged;

        public string MarkerId => string.IsNullOrWhiteSpace(markerId) ? gameObject.name : markerId;
        public MapMarkerType MarkerType => markerType;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public Texture IconTexture => iconTexture;
        public Color IconColor => iconColor;
        public Vector3 IconOffset => iconOffset;
        public Vector3 IconScale => iconScale;
        public float IconRotation => iconRotation;
        public bool RotateWithMinimapCamera => rotateWithMinimapCamera;
        public bool UseMapCameraRotation => useMapCameraRotation;
        public float MapCameraRotation => mapCameraRotation;

        void OnEnable() {
            MarkerEnabled?.Invoke(this);
        }

        void OnDisable() {
            MarkerDisabled?.Invoke(this);
        }

        void OnValidate() {
            iconScale.x = Mathf.Max(0.01f, iconScale.x);
            iconScale.y = Mathf.Max(0.01f, iconScale.y);
            iconScale.z = Mathf.Max(0.01f, iconScale.z);
            minimapVisibleDistance = Mathf.Max(0f, minimapVisibleDistance);
            MarkerChanged?.Invoke(this);
        }

        public void ConfigureRuntime(
            MapMarkerType type,
            string id,
            string title,
            Texture texture,
            Color color,
            bool minimap,
            bool worldMap) {
            markerType = type;
            markerId = id ?? string.Empty;
            displayName = title ?? string.Empty;
            iconTexture = texture;
            iconColor = color;
            showOnMinimap = minimap;
            showOnWorldMap = worldMap;
            MarkerChanged?.Invoke(this);
        }

        public void ConfigureVisibility(bool minimap, bool worldMap, float minimapDistance) {
            showOnMinimap = minimap;
            showOnWorldMap = worldMap;
            minimapVisibleDistance = Mathf.Max(0f, minimapDistance);
            MarkerChanged?.Invoke(this);
        }

        public void ConfigureRotation(bool rotateWithMinimap, bool customMapRotation, float mapRotation) {
            rotateWithMinimapCamera = rotateWithMinimap;
            useMapCameraRotation = customMapRotation;
            mapCameraRotation = mapRotation;
            MarkerChanged?.Invoke(this);
        }

        public void SetVisible(bool value) {
            if (visible == value) return;
            visible = value;
            MarkerChanged?.Invoke(this);
        }

        public bool ShouldRender(Transform viewer, bool forMinimap) {
            if (!visible || !isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
            if (forMinimap && !showOnMinimap) return false;
            if (!forMinimap && !showOnWorldMap) return false;
            if (hideWhenDead && IsKnownDead()) return false;

            if (forMinimap && minimapVisibleDistance > 0f && viewer != null) {
                Vector3 offset = transform.position - viewer.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > minimapVisibleDistance * minimapVisibleDistance) return false;
            }

            return true;
        }

        bool IsKnownDead() {
            if (string.IsNullOrWhiteSpace(aliveMemberName)) return false;

            if (!aliveSourceResolved || cachedAliveSource == null) {
                ResolveAliveSource();
            }

            if (cachedAliveSource == null) return false;
            return TryReadBoolMember(cachedAliveSource, aliveMemberName, out bool alive) && !alive;
        }

        void ResolveAliveSource() {
            aliveSourceResolved = true;
            cachedAliveSource = null;

            foreach (var behaviour in GetComponentsInParent<MonoBehaviour>(true)) {
                if (behaviour != null && TryReadBoolMember(behaviour, aliveMemberName, out _)) {
                    cachedAliveSource = behaviour;
                    return;
                }
            }

            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour != null && TryReadBoolMember(behaviour, aliveMemberName, out _)) {
                    cachedAliveSource = behaviour;
                    return;
                }
            }
        }

        static bool TryReadBoolMember(object source, string memberName, out bool value) {
            value = false;
            if (source == null || string.IsNullOrWhiteSpace(memberName)) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = source.GetType();

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0) {
                value = (bool)property.GetValue(source);
                return true;
            }

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null && field.FieldType == typeof(bool)) {
                value = (bool)field.GetValue(source);
                return true;
            }

            return false;
        }
    }
}

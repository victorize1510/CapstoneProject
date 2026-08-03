using System;
using System.Collections.Generic;
using AAMAP;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class WorldMapController : MonoBehaviour {
        [Serializable]
        public struct MapFilterBinding {
            public Toggle toggle;
            public MapMarkerType markerType;
        }

        [Header("References")]
        [SerializeField] MapManager mapManager = null;
        [SerializeField] Camera mapCamera = null;
        [SerializeField] GameObject minimapRoot = null;
        [SerializeField] RectTransform mapInteractionRect = null;
        [SerializeField] RectTransform overlayRoot = null;
        [SerializeField] Text regionNameText = null;
        [SerializeField] Button closeButton = null;
        [SerializeField] Button zoomInButton = null;
        [SerializeField] Button zoomOutButton = null;
        [SerializeField] Button clearWaypointButton = null;
        [SerializeField] MapIconRegistry iconRegistry = null;
        [SerializeField] Transform playerTarget = null;
        [SerializeField] MapMarker waypointMarker = null;

        [Header("Camera")]
        [SerializeField] bool startClosed = true;
        [SerializeField, Min(1f)] float cameraHeight = 180f;
        [SerializeField, Min(10f)] float defaultOrthographicSize = 420f;
        [SerializeField, Min(5f)] float minOrthographicSize = 70f;
        [SerializeField, Min(10f)] float maxOrthographicSize = 900f;
        [SerializeField, Min(0.01f)] float wheelZoomStep = 0.08f;
        [SerializeField, Min(0.01f)] float panSpeed = 1f;
        [SerializeField] bool openFullMapWhenMapSetExists = true;
        [SerializeField] string mapSetObjectName = "MapSet";
        [SerializeField, Min(1f)] float mapFitPadding = 1.02f;

        [Header("World Bounds")]
        [SerializeField] bool clampToWorldBounds = true;
        [SerializeField] Vector2 worldCenter = Vector2.zero;
        [SerializeField] Vector2 worldSize = new Vector2(120f, 120f);

        [Header("Waypoint")]
        [SerializeField] string waypointMarkerId = "custom-waypoint";
        [SerializeField] string waypointDisplayName = "Waypoint";
        [SerializeField] Color waypointColor = new Color(0.25f, 0.95f, 1f, 1f);

        [Header("Filters")]
        [SerializeField] List<MapFilterBinding> filterBindings = new List<MapFilterBinding>();

        bool buttonsWired;
        bool cachedMinimapActive;
        bool hasCachedMinimapActive;
        bool hasMapSetBounds;
        Bounds mapSetBounds;
        RenderTexture runtimeWorldMapTexture;

        public event Action<Vector3> WaypointChanged;
        public event Action WaypointCleared;

        public MapManager MapManager => mapManager;
        public Camera MapCamera => mapCamera;
        public bool IsOpen => mapManager != null && mapManager.IsMapEnabled();

        void Awake() {
            ResolveReferences();
            WireButtons();
            EnsureOpaqueMapVisuals();
            TryResolveMapSetBounds();
        }

        void Start() {
            ResolveReferences();
            WireButtons();
            EnsureOpaqueMapVisuals();
            TryResolveMapSetBounds();
            ApplyCameraDefaults();
            if (startClosed) CloseMap();
        }

        void OnDestroy() {
            if (runtimeWorldMapTexture == null) return;
            runtimeWorldMapTexture.Release();
            Destroy(runtimeWorldMapTexture);
        }

        public void SetTarget(Transform target) {
            playerTarget = target;
        }

        public void SetWorldBounds(Vector2 center, Vector2 size) {
            worldCenter = center;
            worldSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            ApplyCameraDefaults();
        }

        public void SetReferences(MapManager manager, Camera camera, RectTransform interactionRect, RectTransform overlay, Transform target) {
            mapManager = manager != null ? manager : mapManager;
            mapCamera = camera != null ? camera : mapCamera;
            mapInteractionRect = interactionRect != null ? interactionRect : mapInteractionRect;
            overlayRoot = overlay != null ? overlay : overlayRoot;
            playerTarget = target != null ? target : playerTarget;
            ApplyCameraDefaults();
        }

        public void OpenMap(bool focusPlayer) {
            ResolveReferences();
            if (mapManager == null) return;

            bool openedFullMap = openFullMapWhenMapSetExists && TryFitToMapSet(false);
            if (!openedFullMap && focusPlayer && playerTarget != null) Focus(playerTarget.position, false);
            EnsureOpaqueMapVisuals();
            SetMinimapVisible(false);
            SetOverlayVisible(true);
            mapManager.EnableMap();
            EnsureOpaqueMapVisuals();
            UpdateRegionName();
        }

        public void CloseMap() {
            ResolveReferences();
            if (mapManager != null) mapManager.DisableMap();
            SetOverlayVisible(false);
            RestoreMinimapVisible();
        }

        public void ToggleMap() {
            if (IsOpen) CloseMap();
            else OpenMap(true);
        }

        public void Focus(Vector3 worldPosition, bool openMap) {
            ResolveReferences();
            if (mapManager == null) return;

            float size = Mathf.Clamp(defaultOrthographicSize, minOrthographicSize, maxOrthographicSize);
            Vector3 cameraPosition = ClampCameraPosition(new Vector3(worldPosition.x, cameraHeight, worldPosition.z), size);
            mapManager.SetCameraPosition(cameraPosition);
            mapManager.SetCameraOrthograpicSize(size);
            if (openMap) OpenMap(false);
            UpdateRegionName();
        }

        public void Zoom(float scrollDelta) {
            ResolveReferences();
            if (mapManager == null) return;

            float current = mapManager.GetCameraOrthograpicSize();
            if (current <= 0f) current = defaultOrthographicSize;

            float direction = Mathf.Sign(scrollDelta);
            float multiplier = 1f - direction * wheelZoomStep;
            float next = Mathf.Clamp(current * multiplier, minOrthographicSize, maxOrthographicSize);
            mapManager.SetCameraOrthograpicSize(next);
            mapManager.SetCameraPosition(ClampCameraPosition(mapManager.GetCameraPosition(), next));
            UpdateRegionName();
        }

        public void Pan(Vector2 screenDelta) {
            ResolveReferences();
            if (mapManager == null || mapCamera == null) return;

            float rectHeight = mapInteractionRect != null ? Mathf.Max(1f, mapInteractionRect.rect.height) : Mathf.Max(1f, Screen.height);
            float worldUnitsPerPixel = mapManager.GetCameraOrthograpicSize() * 2f / rectHeight;
            Vector3 position = mapManager.GetCameraPosition();
            Vector3 pan = new Vector3(-screenDelta.x, 0f, -screenDelta.y) * worldUnitsPerPixel * panSpeed;
            mapManager.SetCameraPosition(ClampCameraPosition(position + pan, mapManager.GetCameraOrthograpicSize()));
            UpdateRegionName();
        }

        public bool TrySetWaypoint(Vector2 screenPosition) {
            ResolveReferences();
            if (!ScreenPointToWorld(screenPosition, out Vector3 worldPosition)) return false;

            EnsureWaypointMarker();
            if (waypointMarker == null) return false;

            waypointMarker.transform.position = ClampWorldPosition(worldPosition);
            waypointMarker.ConfigureRuntime(MapMarkerType.Custom, waypointMarkerId, waypointDisplayName, null, waypointColor, true, true);
            waypointMarker.SetVisible(true);
            WaypointChanged?.Invoke(waypointMarker.transform.position);
            return true;
        }

        public void ClearWaypoint() {
            EnsureWaypointMarker();
            if (waypointMarker != null) waypointMarker.SetVisible(false);
            WaypointCleared?.Invoke();
        }

        public void SetFilter(MapMarkerType markerType, bool visible) {
            ResolveReferences();
            iconRegistry?.SetTypeVisible(markerType, visible);
        }

        void ResolveReferences() {
            if (mapManager == null) mapManager = FindFirst<MapManager>();
            if (mapCamera == null && mapManager != null && mapManager.GetMapCamera() != null) mapCamera = mapManager.GetMapCamera().GetComponent<Camera>();
            if (mapCamera == null) {
                GameObject cameraObject = GameObject.Find("Map Camera");
                if (cameraObject != null) mapCamera = cameraObject.GetComponent<Camera>();
            }
            if (mapInteractionRect == null && mapManager != null) {
                Transform mask = mapManager.transform.Find("Map Mask");
                if (mask != null) mapInteractionRect = mask.GetComponent<RectTransform>();
            }
            if (iconRegistry == null) iconRegistry = FindFirst<MapIconRegistry>();
            if (playerTarget == null) playerTarget = AAMapRuntimeBinder.FindPlayerTarget();
            if (minimapRoot == null) {
                MinimapManager minimap = FindFirst<MinimapManager>();
                if (minimap != null) minimapRoot = minimap.gameObject;
            }
        }

        void ApplyCameraDefaults() {
            if (mapManager != null) {
                mapManager.enablingShortcut = KeyCode.None;
                mapManager.disablingShortcut = KeyCode.None;
                if (mapCamera != null) mapManager.SetMapCamera(mapCamera.gameObject);
                float size = Mathf.Clamp(mapManager.GetCameraOrthograpicSize(), minOrthographicSize, maxOrthographicSize);
                if (size <= 0f) size = Mathf.Clamp(defaultOrthographicSize, minOrthographicSize, maxOrthographicSize);
                if (mapCamera != null) {
                    mapManager.SetCameraPosition(ClampCameraPosition(
                        new Vector3(mapCamera.transform.position.x, cameraHeight, mapCamera.transform.position.z),
                        size));
                }
                mapManager.SetCameraOrthograpicSize(size);
            }

            if (mapCamera != null) {
                mapCamera.orthographic = true;
                mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                mapCamera.nearClipPlane = 0.1f;
                mapCamera.farClipPlane = Mathf.Max(cameraHeight + 500f, 1000f);
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
                mapCamera.aspect = GetWorldMapAspect();
                mapCamera.transform.position = ClampCameraPosition(
                    new Vector3(mapCamera.transform.position.x, cameraHeight, mapCamera.transform.position.z),
                    mapCamera.orthographicSize);
            }
        }

        bool TryFitToMapSet(bool forceRefresh) {
            if (forceRefresh || !hasMapSetBounds) TryResolveMapSetBounds();
            if (!hasMapSetBounds || mapManager == null || mapCamera == null) return false;

            Vector3 center = mapSetBounds.center;
            float fitSize = CalculateFitOrthographicSize(mapSetBounds, GetWorldMapAspect(), mapFitPadding);
            defaultOrthographicSize = fitSize;
            maxOrthographicSize = Mathf.Max(fitSize * 1.25f, fitSize + 1f);
            minOrthographicSize = Mathf.Min(minOrthographicSize, fitSize * 0.35f);
            worldCenter = new Vector2(center.x, center.z);
            worldSize = new Vector2(Mathf.Max(1f, mapSetBounds.size.x), Mathf.Max(1f, mapSetBounds.size.z));

            Vector3 cameraPosition = ClampCameraPosition(new Vector3(center.x, cameraHeight, center.z), fitSize);
            mapManager.SetCameraPosition(cameraPosition);
            mapManager.SetCameraOrthograpicSize(fitSize);
            mapCamera.transform.position = cameraPosition;
            mapCamera.orthographicSize = fitSize;
            return true;
        }

        bool TryResolveMapSetBounds() {
            hasMapSetBounds = false;
            Transform mapSet = FindNamedTransform(mapSetObjectName);
            if (mapSet == null) mapSet = FindLikelyMapContentTransform();
            if (mapSet == null) return false;

            Renderer[] renderers = mapSet.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.GetComponentInParent<Canvas>() != null) continue;

                if (!hasMapSetBounds) {
                    mapSetBounds = renderer.bounds;
                    hasMapSetBounds = true;
                } else {
                    mapSetBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasMapSetBounds;
        }

        float GetWorldMapAspect() {
            if (mapManager != null && mapManager.renderTexture != null && mapManager.renderTexture.height > 0) {
                return Mathf.Max(0.01f, (float)mapManager.renderTexture.width / mapManager.renderTexture.height);
            }
            if (mapCamera != null) return Mathf.Max(0.01f, mapCamera.aspect);
            return 16f / 9f;
        }

        static float CalculateFitOrthographicSize(Bounds bounds, float aspect, float padding) {
            float halfHeight = Mathf.Max(1f, bounds.size.z * 0.5f);
            float halfWidth = Mathf.Max(1f, bounds.size.x * 0.5f / Mathf.Max(0.01f, aspect));
            return Mathf.Max(halfHeight, halfWidth) * Mathf.Max(1f, padding);
        }

        void EnsureOpaqueMapVisuals() {
            Color backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
            RenderTexture renderTexture = EnsureWorldMapRenderTexture();

            if (mapManager != null) {
                mapManager.mapOpacity = 1f;
                mapManager.mapColor = Color.white;
                mapManager.clearFlags = MapClearFlags.SolidColor;
                mapManager.cameraBGColor = backgroundColor;
                mapManager.haveBackgroundImage = false;
                mapManager.haveZoomButtons = false;
                mapManager.haveExitButton = false;
                mapManager.disableMinimap = false;
            }

            if (mapCamera != null) {
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = backgroundColor;
                if (renderTexture != null) mapCamera.targetTexture = renderTexture;
            }

            Transform mapRoot = mapManager != null ? mapManager.transform : null;
            Transform mask = mapRoot != null ? mapRoot.Find("Map Mask") : null;
            if (mask == null) return;

            Image maskImage = mask.GetComponent<Image>();
            if (maskImage != null) {
                maskImage.color = Color.white;
                maskImage.raycastTarget = false;
            }

            Mask maskComponent = mask.GetComponent<Mask>();
            if (maskComponent != null) maskComponent.showMaskGraphic = false;

            Image filler = EnsureRuntimeImage(mask, "Map Background Filler", backgroundColor);
            if (filler != null) filler.transform.SetAsFirstSibling();

            Image background = EnsureRuntimeImage(mask, "Map Background", Color.clear);
            if (background != null) background.gameObject.SetActive(false);

            Image grid = EnsureRuntimeImage(mask, "Map Grid", Color.clear);
            if (grid != null) grid.gameObject.SetActive(false);

            Transform displayTransform = mask.Find("Map Display");
            RawImage display = displayTransform != null ? displayTransform.GetComponent<RawImage>() : null;
            if (display != null) {
                display.color = Color.white;
                display.raycastTarget = false;
                if (renderTexture != null) display.texture = renderTexture;
                Stretch(display.rectTransform);
                display.transform.SetSiblingIndex(1);
            }

            Transform zoomButtons = mapRoot.Find("Map Zoom Buttons");
            if (zoomButtons != null) zoomButtons.gameObject.SetActive(false);

            Transform exitButton = mapRoot.Find("Map Exit Button");
            if (exitButton != null) exitButton.gameObject.SetActive(false);
        }

        RenderTexture EnsureWorldMapRenderTexture() {
            const int width = 2048;
            const int height = 1152;
            const float targetAspect = 16f / 9f;

            RenderTexture current = mapManager != null ? mapManager.renderTexture : null;
            bool valid = current != null
                         && current.height > 0
                         && Mathf.Abs(((float)current.width / current.height) - targetAspect) < 0.02f;

            RenderTexture texture = valid ? current : runtimeWorldMapTexture;
            if (texture == null) {
                runtimeWorldMapTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) {
                    name = "WorldMap_RT_Runtime",
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                runtimeWorldMapTexture.Create();
                texture = runtimeWorldMapTexture;
            }

            if (mapManager != null) mapManager.renderTexture = texture;
            if (mapCamera != null) {
                mapCamera.targetTexture = texture;
                mapCamera.aspect = targetAspect;
            }
            return texture;
        }

        void SetMinimapVisible(bool visible) {
            if (minimapRoot == null) return;
            if (!hasCachedMinimapActive) {
                cachedMinimapActive = minimapRoot.activeSelf;
                hasCachedMinimapActive = true;
            }
            minimapRoot.SetActive(visible);
        }

        void RestoreMinimapVisible() {
            if (minimapRoot == null || !hasCachedMinimapActive) return;
            minimapRoot.SetActive(cachedMinimapActive);
            hasCachedMinimapActive = false;
        }

        static Image EnsureRuntimeImage(Transform parent, string name, Color color) {
            Transform child = parent.Find(name);
            if (child == null) {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
                obj.transform.SetParent(parent, false);
                child = obj.transform;
            }

            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null) Stretch(rect);

            Image image = child.GetComponent<Image>();
            if (image == null) image = child.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect) {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static Transform FindNamedTransform(string objectName) {
            if (string.IsNullOrWhiteSpace(objectName)) return null;

            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++) {
                Transform item = transforms[i];
                if (item != null && item.name == objectName) return item;
            }
            return null;
        }

        static Transform FindLikelyMapContentTransform() {
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform best = null;
            float bestArea = 0f;

            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Transform candidate = GetMapContentRoot(renderer.transform);
                if (candidate == null) continue;

                Bounds bounds = renderer.bounds;
                float area = Mathf.Max(0f, bounds.size.x) * Mathf.Max(0f, bounds.size.z);
                if (area <= bestArea) continue;

                bestArea = area;
                best = candidate;
            }

            return best;
        }

        static Transform GetMapContentRoot(Transform transform) {
            if (transform == null) return null;
            if (transform.GetComponentInParent<Canvas>() != null) return null;
            if (transform.GetComponentInParent<Camera>() != null) return null;
            if (transform.GetComponentInParent<MapMarker>() != null) return null;

            Transform current = transform;
            while (current != null) {
                string name = current.name;
                if (name.Contains("Map Icon") || name.Contains("Minimap") || name.Contains("WorldMap") || name.Contains("MapSystem")) return null;
                if (name.StartsWith("Map") || name.Contains("MapSet") || name.Contains("Map Set")) return current;
                current = current.parent;
            }

            return null;
        }

        void WireButtons() {
            if (buttonsWired) return;
            buttonsWired = true;

            if (closeButton != null) closeButton.onClick.AddListener(CloseMap);
            if (zoomInButton != null) zoomInButton.onClick.AddListener(() => Zoom(1f));
            if (zoomOutButton != null) zoomOutButton.onClick.AddListener(() => Zoom(-1f));
            if (clearWaypointButton != null) clearWaypointButton.onClick.AddListener(ClearWaypoint);

            for (int i = 0; i < filterBindings.Count; i++) {
                Toggle toggle = filterBindings[i].toggle;
                MapMarkerType type = filterBindings[i].markerType;
                if (toggle == null) continue;
                toggle.onValueChanged.AddListener(value => SetFilter(type, value));
            }
        }

        void SetOverlayVisible(bool visible) {
            if (overlayRoot != null) overlayRoot.gameObject.SetActive(visible);
        }

        void UpdateRegionName() {
            if (regionNameText == null || mapManager == null) return;
            Vector3 position = mapManager.GetCameraPosition();
            regionNameText.text = string.Format("Region: X {0:0}, Z {1:0}", position.x, position.z);
        }

        bool ScreenPointToWorld(Vector2 screenPosition, out Vector3 worldPosition) {
            worldPosition = Vector3.zero;
            if (mapCamera == null) return false;

            RectTransform rect = mapInteractionRect;
            if (rect != null) {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out Vector2 local)) return false;
                Rect bounds = rect.rect;
                if (!bounds.Contains(local)) return false;

                float viewportX = Mathf.InverseLerp(bounds.xMin, bounds.xMax, local.x);
                float viewportY = Mathf.InverseLerp(bounds.yMin, bounds.yMax, local.y);
                Ray ray = mapCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
                if (!IntersectGround(ray, out worldPosition)) return false;
                worldPosition = ClampWorldPosition(worldPosition);
                return true;
            }

            if (!IntersectGround(mapCamera.ScreenPointToRay(screenPosition), out worldPosition)) return false;
            worldPosition = ClampWorldPosition(worldPosition);
            return true;
        }

        Vector3 ClampCameraPosition(Vector3 position, float orthographicSize) {
            position.y = cameraHeight;
            if (!clampToWorldBounds || mapCamera == null) return position;

            float halfHeight = Mathf.Max(1f, orthographicSize);
            float halfWidth = halfHeight * Mathf.Max(0.01f, mapCamera.aspect);
            float halfWorldX = Mathf.Max(0.5f, worldSize.x * 0.5f);
            float halfWorldZ = Mathf.Max(0.5f, worldSize.y * 0.5f);

            float minX = worldCenter.x - halfWorldX + halfWidth;
            float maxX = worldCenter.x + halfWorldX - halfWidth;
            float minZ = worldCenter.y - halfWorldZ + halfHeight;
            float maxZ = worldCenter.y + halfWorldZ - halfHeight;

            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : worldCenter.x;
            position.z = minZ <= maxZ ? Mathf.Clamp(position.z, minZ, maxZ) : worldCenter.y;
            return position;
        }

        Vector3 ClampWorldPosition(Vector3 position) {
            if (!clampToWorldBounds) return position;

            float halfWorldX = Mathf.Max(0.5f, worldSize.x * 0.5f);
            float halfWorldZ = Mathf.Max(0.5f, worldSize.y * 0.5f);
            position.x = Mathf.Clamp(position.x, worldCenter.x - halfWorldX, worldCenter.x + halfWorldX);
            position.z = Mathf.Clamp(position.z, worldCenter.y - halfWorldZ, worldCenter.y + halfWorldZ);
            return position;
        }

        void OnValidate() {
            worldSize = new Vector2(Mathf.Max(1f, worldSize.x), Mathf.Max(1f, worldSize.y));
        }

        static bool IntersectGround(Ray ray, out Vector3 worldPosition) {
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter)) {
                worldPosition = ray.GetPoint(enter);
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        void EnsureWaypointMarker() {
            if (waypointMarker != null) return;

            Transform markerTransform = transform.Find("Custom Waypoint Marker");
            GameObject markerObject = markerTransform != null ? markerTransform.gameObject : new GameObject("Custom Waypoint Marker");
            markerObject.transform.SetParent(transform, false);
            waypointMarker = markerObject.GetComponent<MapMarker>();
            if (waypointMarker == null) waypointMarker = markerObject.AddComponent<MapMarker>();
            waypointMarker.SetVisible(false);
        }

        static T FindFirst<T>() where T : UnityEngine.Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}

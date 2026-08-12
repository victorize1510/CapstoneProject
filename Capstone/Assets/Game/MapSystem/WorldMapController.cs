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


        struct RendererState {
            public readonly Renderer renderer;
            public readonly bool wasEnabled;

            public RendererState(Renderer renderer, bool wasEnabled) {
                this.renderer = renderer;
                this.wasEnabled = wasEnabled;
            }
        }

        struct TerrainTreeState {
            public readonly Terrain terrain;
            public readonly bool drewTreesAndFoliage;

            public TerrainTreeState(Terrain terrain, bool drewTreesAndFoliage) {
                this.terrain = terrain;
                this.drewTreesAndFoliage = drewTreesAndFoliage;
            }
        }
        struct HudRootState {
            public readonly GameObject root;
            public readonly bool wasActive;

            public HudRootState(GameObject root, bool wasActive) {
                this.root = root;
                this.wasActive = wasActive;
            }
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
        [SerializeField] Button centerOnPlayerButton = null;
        [SerializeField] Button clearWaypointButton = null;
        [SerializeField] MapIconRegistry iconRegistry = null;
        [SerializeField] Transform playerTarget = null;
        [SerializeField] MapMarker waypointMarker = null;
        [SerializeField] RectTransform customFrame = null;

        [Header("Camera")]
        [SerializeField] bool startClosed = true;
        [SerializeField, Min(1f)] float cameraHeight = 180f;
        [SerializeField, Range(0.01f, 1f)] float minimumVisiblePercent = 0.05f;
        [SerializeField, Range(0.01f, 1f)] float initialVisiblePercent = 0.12f;
        [SerializeField, Range(0.01f, 1f)] float maximumVisiblePercent = 0.30f;
        [SerializeField, Min(0.01f)] float wheelZoomStep = 0.08f;
        [SerializeField, Min(0.01f)] float panSpeed = 1f;
        [SerializeField] bool openMapCenteredOnPlayer = true;

        [Header("Viewport")]
        [SerializeField] bool enforceRuntimeViewport = true;
        [SerializeField] Vector2 viewportSize = new Vector2(1280f, 720f);
        [SerializeField] Vector2 viewportOffset = Vector2.zero;
        [SerializeField, Min(0f)] float frameOverscan = 128f;

        [Header("HUD Layering")]
        [SerializeField] bool hideGameplayHudWhileOpen = true;
        [SerializeField] List<GameObject> hudRootsToHide = new List<GameObject>();

        [Header("Map Bounds Source")]
        [SerializeField] bool useMapSetBounds = true;
        [SerializeField] string mapSetObjectName = "MapSet";

        [Header("Map Camera Cleanup")]
        [SerializeField] bool hideTreesWaterAndEffectsOnMap = true;
        [SerializeField] string[] mapExcludedLayerNames = { "Water", "Suimono_Water", "Suimono_Depth", "Suimono_Screen", "TransparentFX", "SmallVFX", "MedVFX", "LargeVFX" };
        [SerializeField] string[] mapHiddenNameKeywords = { "Water", "Lake", "Tree", "Trees", "Foliage", "Grass", "VFX", "FX", "Effect", "Particle", "Splash", "Foam", "Mist", "Fog" };

        [Header("World Bounds")]
        [SerializeField] bool clampToWorldBounds = true;
        [SerializeField] Vector2 worldCenter = Vector2.zero;
        [SerializeField] Vector2 worldSize = new Vector2(120f, 120f);
        [SerializeField] Vector4 worldBoundsInsetPercent = new Vector4(0.07f, 0f, 0.03f, 0.16f);

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
        bool worldMapOpen;
        Bounds mapSetBounds;
        RenderTexture runtimeWorldMapTexture;
        readonly List<HudRootState> hiddenHudRoots = new List<HudRootState>();
        readonly List<RendererState> hiddenMapRenderers = new List<RendererState>();
        readonly List<TerrainTreeState> hiddenMapTerrains = new List<TerrainTreeState>();
        bool mapSceneVisualsHidden;

        public event Action<Vector3> WaypointChanged;
        public event Action WaypointCleared;

        public MapManager MapManager => mapManager;
        public Camera MapCamera => mapCamera;
        public RectTransform MapInteractionRect => mapInteractionRect;
        public Vector2 WorldSize => worldSize;
        public float MinimumVisiblePercent => minimumVisiblePercent;
        public float InitialVisiblePercent => initialVisiblePercent;
        public float MaximumVisiblePercent => maximumVisiblePercent;
        public bool IsOpen => worldMapOpen;

        void Awake() {
            ResolveReferences();
            WireButtons();
            NormalizeVisiblePercents();
            EnsureOpaqueMapVisuals();
            ApplyMapSetBoundsIfAvailable();
        }

        void OnEnable() {
            ResolveReferences();
            WireButtons();
            NormalizeVisiblePercents();
            EnsureOpaqueMapVisuals();
        }

        void Start() {
            ResolveReferences();
            WireButtons();
            NormalizeVisiblePercents();
            EnsureOpaqueMapVisuals();
            ApplyMapSetBoundsIfAvailable();
            ApplyCameraDefaults();
            if (startClosed) CloseMap();
        }

        void LateUpdate() {
            if (!IsOpen) return;

            EnsureOpaqueMapVisuals();
            UpdateRegionName();
        }

        void OnDestroy() {
            RestoreHiddenHudRoots();

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

            worldMapOpen = true;
            SetWorldMapRootVisible(true);
            NormalizeVisiblePercents();
            ApplyMapSetBoundsIfAvailable();
            if (focusPlayer) {
                if (openMapCenteredOnPlayer && playerTarget != null) Focus(playerTarget.position, false);
                else Focus(new Vector3(worldCenter.x, 0f, worldCenter.y), false);
            } else {
                ApplyCameraDefaults();
            }
            EnsureOpaqueMapVisuals();
            SetMinimapVisible(false);
            SetOverlayVisible(true);
            SetCustomFrameVisible(false);
            SetHudRootsVisibleForMap(true);
            EnableMapSafely();
            BringWorldMapToFront();
            EnsureOpaqueMapVisuals();
            UpdateRegionName();
        }

        public void CloseMap() {
            ResolveReferences();
            worldMapOpen = false;
            DisableMapSafely();
            SetOverlayVisible(false);
            SetCustomFrameVisible(false);
            SetWorldMapRootVisible(false);
            SetMinimapVisible(false);
            SetHudRootsVisibleForMap(false);
        }

        public void ToggleMap() {
            if (IsOpen) CloseMap();
            else OpenMap(true);
        }

        public void Focus(Vector3 worldPosition, bool openMap) {
            ResolveReferences();
            if (mapManager == null) return;

            NormalizeVisiblePercents();
            float size = ClampOrthographicSize(CalculateOrthographicSizeForVisiblePercent(initialVisiblePercent));
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
            if (current <= 0f) current = CalculateOrthographicSizeForVisiblePercent(initialVisiblePercent);

            float direction = Mathf.Sign(scrollDelta);
            float multiplier = 1f - direction * wheelZoomStep;
            float next = ClampOrthographicSize(current * multiplier);
            mapManager.SetCameraOrthograpicSize(next);
            mapManager.SetCameraPosition(ClampCameraPosition(mapManager.GetCameraPosition(), next));
            UpdateRegionName();
        }

        public void CenterOnPlayer() {
            ResolveReferences();
            if (playerTarget != null) Focus(playerTarget.position, false);
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

        void ApplyMapCameraCullingMask() {
            if (mapCamera == null || mapExcludedLayerNames == null) return;

            int mask = mapCamera.cullingMask;
            for (int i = 0; i < mapExcludedLayerNames.Length; i++) {
                int layer = LayerMask.NameToLayer(mapExcludedLayerNames[i]);
                if (layer >= 0) mask &= ~(1 << layer);
            }

            mapCamera.cullingMask = mask;
        }

        void SetMapOnlySceneVisualsHidden(bool hidden) {
            if (!hideTreesWaterAndEffectsOnMap) return;
            if (hidden == mapSceneVisualsHidden) return;

            if (!hidden) {
                RestoreMapHiddenSceneVisuals();
                return;
            }

            mapSceneVisualsHidden = true;
            ApplyMapCameraCullingMask();
            HideTerrainTreesForMap();
            HideNamedRenderersForMap();
        }

        void HideTerrainTreesForMap() {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < terrains.Length; i++) {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;
                hiddenMapTerrains.Add(new TerrainTreeState(terrain, terrain.drawTreesAndFoliage));
                terrain.drawTreesAndFoliage = false;
            }
        }

        void HideNamedRenderersForMap() {
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || IsMapOwnedRenderer(renderer) || !ShouldHideRendererForMap(renderer)) continue;
                hiddenMapRenderers.Add(new RendererState(renderer, renderer.enabled));
                renderer.enabled = false;
            }
        }

        void RestoreMapHiddenSceneVisuals() {
            for (int i = 0; i < hiddenMapRenderers.Count; i++) {
                RendererState state = hiddenMapRenderers[i];
                if (state.renderer != null) state.renderer.enabled = state.wasEnabled;
            }

            for (int i = 0; i < hiddenMapTerrains.Count; i++) {
                TerrainTreeState state = hiddenMapTerrains[i];
                if (state.terrain != null) state.terrain.drawTreesAndFoliage = state.drewTreesAndFoliage;
            }

            hiddenMapRenderers.Clear();
            hiddenMapTerrains.Clear();
            mapSceneVisualsHidden = false;
        }

        bool ShouldHideRendererForMap(Renderer renderer) {
            if (renderer is ParticleSystemRenderer || renderer.GetComponentInParent<ParticleSystem>() != null) return true;

            string layerName = LayerMask.LayerToName(renderer.gameObject.layer);
            if (ContainsText(layerName, mapExcludedLayerNames)) return true;
            if (TransformNameContains(renderer.transform, mapHiddenNameKeywords)) return true;

            Material material = renderer.sharedMaterial;
            if (material != null) {
                if (ContainsText(material.name, mapHiddenNameKeywords)) return true;
                Shader shader = material.shader;
                if (shader != null && ContainsText(shader.name, mapHiddenNameKeywords)) return true;
            }

            return false;
        }

        bool IsMapOwnedRenderer(Renderer renderer) {
            if (renderer == null) return true;
            Transform target = renderer.transform;
            if (target.GetComponentInParent<Canvas>() != null) return true;
            if (target.GetComponentInParent<MapMarker>() != null) return true;
            if (mapManager != null && target.IsChildOf(mapManager.transform)) return true;
            if (overlayRoot != null && target.IsChildOf(overlayRoot)) return true;
            if (customFrame != null && target.IsChildOf(customFrame)) return true;
            return false;
        }

        static bool TransformNameContains(Transform transform, string[] keywords) {
            Transform current = transform;
            int depth = 0;
            while (current != null && depth < 6) {
                if (ContainsText(current.name, keywords)) return true;
                current = current.parent;
                depth++;
            }
            return false;
        }

        static bool ContainsText(string value, string[] keywords) {
            if (string.IsNullOrWhiteSpace(value) || keywords == null) return false;
            for (int i = 0; i < keywords.Length; i++) {
                string keyword = keywords[i];
                if (!string.IsNullOrWhiteSpace(keyword) && value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
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
            if (customFrame == null) {
                Transform frame = overlayRoot != null ? overlayRoot.Find("Thang World Map Frame") : null;
                if (frame == null && mapManager != null) frame = mapManager.transform.Find("Thang World Map Frame");
                if (frame != null) customFrame = frame.GetComponent<RectTransform>();
            }
            if (iconRegistry == null) iconRegistry = FindFirst<MapIconRegistry>();
            if (playerTarget == null) playerTarget = AAMapRuntimeBinder.FindPlayerTarget();
            if (minimapRoot == null) {
                MinimapManager minimap = FindFirst<MinimapManager>();
                if (minimap != null) minimapRoot = minimap.gameObject;
            }
        }

        void ApplyCameraDefaults() {
            NormalizeVisiblePercents();
            if (mapManager != null) {
                mapManager.enablingShortcut = KeyCode.None;
                mapManager.disablingShortcut = KeyCode.None;
                if (mapCamera != null) mapManager.SetMapCamera(mapCamera.gameObject);
                float currentSize = mapManager.GetCameraOrthograpicSize();
                float size = currentSize > 0f ? ClampOrthographicSize(currentSize) : CalculateOrthographicSizeForVisiblePercent(initialVisiblePercent);
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

        bool ApplyMapSetBoundsIfAvailable() {
            if (!useMapSetBounds) return false;
            if (!TryResolveMapSetBounds()) return false;

            Vector3 center = mapSetBounds.center;
            worldCenter = new Vector2(center.x, center.z);
            worldSize = new Vector2(Mathf.Max(1f, mapSetBounds.size.x), Mathf.Max(1f, mapSetBounds.size.z));
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
            if (mapInteractionRect != null) {
                Rect rect = mapInteractionRect.rect;
                if (rect.width > 1f && rect.height > 1f) return Mathf.Max(0.01f, rect.width / rect.height);
            }
            if (mapManager != null && mapManager.renderTexture != null && mapManager.renderTexture.height > 0) {
                return Mathf.Max(0.01f, (float)mapManager.renderTexture.width / mapManager.renderTexture.height);
            }
            if (mapCamera != null) return Mathf.Max(0.01f, mapCamera.aspect);
            return 16f / 9f;
        }

        float CalculateOrthographicSizeForVisiblePercent(float visiblePercent) {
            float normalizedPercent = Mathf.Clamp(visiblePercent, 0.01f, 1f);
            float referenceLength = Mathf.Max(1f, Mathf.Min(worldSize.x, worldSize.y));
            float maxVisibleLongSide = referenceLength * normalizedPercent;
            float aspect = Mathf.Max(0.01f, GetWorldMapAspect());

            // Orthographic size is half vertical height; keep the viewport's longest visible side within the percent cap.
            return aspect >= 1f ? maxVisibleLongSide / (2f * aspect) : maxVisibleLongSide * 0.5f;
        }

        float ClampOrthographicSize(float size) {
            float closeSize = CalculateOrthographicSizeForVisiblePercent(minimumVisiblePercent);
            float farSize = CalculateOrthographicSizeForVisiblePercent(maximumVisiblePercent);
            float lower = Mathf.Min(closeSize, farSize);
            float upper = Mathf.Max(closeSize, farSize);
            return Mathf.Clamp(size, lower, upper);
        }

        void NormalizeVisiblePercents() {
            minimumVisiblePercent = Mathf.Clamp(minimumVisiblePercent, 0.01f, 1f);
            maximumVisiblePercent = Mathf.Clamp(maximumVisiblePercent, minimumVisiblePercent, 1f);
            initialVisiblePercent = Mathf.Clamp(initialVisiblePercent, minimumVisiblePercent, maximumVisiblePercent);
        }

        void EnsureOpaqueMapVisuals() {
            Color backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
            RenderTexture renderTexture = EnsureWorldMapRenderTexture();

            if (mapManager != null) {
                EnsurePanelBackdrop(mapManager.transform, backgroundColor);
                mapManager.mapOpacity = 1f;
                mapManager.mapColor = Color.white;
                mapManager.haveBorder = false;
                mapManager.clearFlags = MapClearFlags.SolidColor;
                mapManager.cameraBGColor = backgroundColor;
                mapManager.haveBackgroundImage = false;
                mapManager.haveZoomButtons = false;
                mapManager.haveExitButton = false;
                mapManager.disableMinimap = true;
            }

            if (mapCamera != null) {
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = backgroundColor;
                if (renderTexture != null) mapCamera.targetTexture = renderTexture;
            }

            Transform mapRoot = mapManager != null ? mapManager.transform : null;
            Transform mask = mapRoot != null ? mapRoot.Find("Map Mask") : null;
            if (mask == null) return;

            ApplyViewportLayout(mask.GetComponent<RectTransform>());
            ApplyCustomFrameLayout();

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
                display.material = null;
                display.raycastTarget = false;
                if (renderTexture != null) display.texture = renderTexture;
                Stretch(display.rectTransform);
                display.transform.SetSiblingIndex(1);
            }

            Transform zoomButtons = mapRoot.Find("Map Zoom Buttons");
            if (zoomButtons != null) zoomButtons.gameObject.SetActive(false);

            Transform exitButton = mapRoot.Find("Map Exit Button");
            if (exitButton != null) exitButton.gameObject.SetActive(false);

            StyleAndArrangeOverlay();
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

            if (texture != null && !texture.IsCreated()) texture.Create();
            if (mapManager != null) mapManager.renderTexture = texture;
            if (mapCamera != null) {
                mapCamera.targetTexture = texture;
                mapCamera.aspect = targetAspect;
            }
            return texture;
        }

        static void EnsurePanelBackdrop(Transform mapRoot, Color color) {
            if (mapRoot == null) return;

            Image backdrop = mapRoot.GetComponent<Image>();
            if (backdrop == null) backdrop = mapRoot.gameObject.AddComponent<Image>();
            backdrop.color = new Color(color.r, color.g, color.b, 0.96f);
            backdrop.raycastTarget = true;

            CanvasGroup group = mapRoot.GetComponent<CanvasGroup>();
            if (group == null) return;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        void ApplyViewportLayout(RectTransform rect) {
            if (!enforceRuntimeViewport || rect == null) return;

            // Full-screen map surface. Zoom limits still control how much of the world is visible.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        void SetMinimapVisible(bool visible) {
            // Minimap is intentionally removed from the current HUD. Keep AA objects disabled
            // so the setup tool or AA defaults cannot revive it on the second Play press.
            if (minimapRoot == null) {
                MinimapManager minimap = FindFirst<MinimapManager>();
                if (minimap != null) minimapRoot = minimap.gameObject;
            }

            if (minimapRoot != null) minimapRoot.SetActive(false);

            MinimapManager manager = FindFirst<MinimapManager>();
            if (manager != null) manager.gameObject.SetActive(false);

            GameObject cameraObject = GameObject.Find("Minimap Camera");
            if (cameraObject != null) cameraObject.SetActive(false);
        }

        void RestoreMinimapVisible() {
            SetMinimapVisible(false);
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

        void StyleAndArrangeOverlay() {
            StyleButton(closeButton, new Color(0.02f, 0.02f, 0.018f, 0.92f), Color.black);
            StyleButton(zoomInButton, new Color(0.02f, 0.02f, 0.018f, 0.92f), Color.black);
            StyleButton(zoomOutButton, new Color(0.02f, 0.02f, 0.018f, 0.92f), Color.black);
            StyleButton(centerOnPlayerButton, new Color(0.02f, 0.02f, 0.018f, 0.92f), Color.black);
            StyleButton(clearWaypointButton, new Color(0.86f, 0.78f, 0.58f, 0.94f), Color.black);

            PositionButton(closeButton, new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(42f, 42f), new Vector2(1f, 1f));
            PositionButton(zoomInButton, new Vector2(1f, 1f), new Vector2(-150f, -42f), new Vector2(38f, 38f), new Vector2(1f, 1f));
            PositionButton(zoomOutButton, new Vector2(1f, 1f), new Vector2(-106f, -42f), new Vector2(38f, 38f), new Vector2(1f, 1f));
            PositionButton(centerOnPlayerButton, new Vector2(1f, 1f), new Vector2(-62f, -42f), new Vector2(38f, 38f), new Vector2(1f, 1f));
            PositionButton(clearWaypointButton, new Vector2(1f, 0f), new Vector2(-36f, 34f), new Vector2(150f, 36f), new Vector2(1f, 0f));

            RectTransform zoomRoot = zoomInButton != null ? zoomInButton.transform.parent as RectTransform : null;
            if (zoomRoot != null) {
                PositionRect(zoomRoot, new Vector2(1f, 1f), new Vector2(-46f, -34f), new Vector2(158f, 46f), new Vector2(1f, 1f));
                Image image = zoomRoot.GetComponent<Image>();
                if (image != null) image.color = new Color(0.02f, 0.10f, 0.09f, 0.45f);
            }

            if (regionNameText != null) {
                regionNameText.color = Color.black;
                regionNameText.fontStyle = FontStyle.Bold;
                RectTransform region = regionNameText.transform.parent as RectTransform;
                if (region != null) PositionRect(region, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(360f, 34f), new Vector2(0.5f, 1f));
                Image regionBg = region != null ? region.GetComponent<Image>() : null;
                if (regionBg != null) regionBg.color = new Color(0.90f, 0.82f, 0.62f, 0.58f);
            }

            RectTransform filterBar = filterBindings.Count > 0 && filterBindings[0].toggle != null
                ? filterBindings[0].toggle.transform.parent as RectTransform
                : null;
            if (filterBar != null) {
                PositionRect(filterBar, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(760f, 36f), new Vector2(0.5f, 0f));
                Image barImage = filterBar.GetComponent<Image>();
                if (barImage != null) barImage.color = new Color(0.82f, 0.74f, 0.55f, 0.62f);
            }

            for (int i = 0; i < filterBindings.Count; i++) {
                Toggle toggle = filterBindings[i].toggle;
                if (toggle == null) continue;

                RectTransform rect = toggle.transform as RectTransform;
                if (rect != null) {
                    rect.anchorMin = new Vector2(0f, 0.5f);
                    rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.sizeDelta = new Vector2(84f, 28f);
                    rect.anchoredPosition = new Vector2(12f + i * 92f, 0f);
                }

                Image bg = toggle.GetComponent<Image>();
                if (bg != null) bg.color = toggle.isOn
                    ? new Color(0.17f, 0.34f, 0.19f, 0.92f)
                    : new Color(0.08f, 0.13f, 0.11f, 0.68f);

                Text label = toggle.GetComponentInChildren<Text>(true);
                if (label != null) label.color = Color.black;
            }
        }

        static void StyleButton(Button button, Color background, Color textColor) {
            if (button == null) return;

            Image image = button.GetComponent<Image>();
            if (image != null) image.color = background;

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) {
                text.color = textColor;
                text.fontStyle = FontStyle.Bold;
            }
        }

        static void PositionButton(Button button, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Vector2 pivot) {
            if (button == null) return;
            PositionRect(button.transform as RectTransform, anchor, anchoredPosition, size, pivot);
        }

        static void PositionRect(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Vector2 pivot) {
            if (rect == null) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
        void WireButtons() {
            if (buttonsWired) return;
            buttonsWired = true;

            if (closeButton != null) closeButton.onClick.AddListener(CloseMap);
            if (zoomInButton != null) zoomInButton.onClick.AddListener(() => Zoom(1f));
            if (zoomOutButton != null) zoomOutButton.onClick.AddListener(() => Zoom(-1f));
            if (centerOnPlayerButton != null) centerOnPlayerButton.onClick.AddListener(CenterOnPlayer);
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

        void SetCustomFrameVisible(bool visible) {
            if (customFrame != null) customFrame.gameObject.SetActive(false);
        }
        void BringWorldMapToFront() {
            if (mapManager != null) mapManager.transform.SetAsLastSibling();
            if (overlayRoot != null) overlayRoot.SetAsLastSibling();
            if (customFrame != null) customFrame.SetAsLastSibling();
        }

        void SetHudRootsVisibleForMap(bool mapVisible) {
            if (!hideGameplayHudWhileOpen) return;

            if (mapVisible) HideGameplayHudRoots();
            else RestoreHiddenHudRoots();
        }

        void HideGameplayHudRoots() {
            ResolveDefaultHudRoots();

            for (int i = 0; i < hudRootsToHide.Count; i++) {
                GameObject root = hudRootsToHide[i];
                if (root == null || IsMapOwnedRoot(root) || IsHiddenHudRoot(root)) continue;

                hiddenHudRoots.Add(new HudRootState(root, root.activeSelf));
                root.SetActive(false);
            }
        }

        void RestoreHiddenHudRoots() {
            for (int i = 0; i < hiddenHudRoots.Count; i++) {
                HudRootState state = hiddenHudRoots[i];
                if (state.root != null) state.root.SetActive(state.wasActive);
            }

            hiddenHudRoots.Clear();
        }

        void ResolveDefaultHudRoots() {
            AddHudRoot(GameObject.Find("GameplayHUDRoot"));
            AddHudRoot(GameObject.Find("CompassHUDRoot"));
            AddHudRoot(GameObject.Find("QuestTrackerHUD"));
        }

        void AddHudRoot(GameObject candidate) {
            if (candidate == null || hudRootsToHide.Contains(candidate)) return;
            hudRootsToHide.Add(candidate);
        }

        bool IsHiddenHudRoot(GameObject candidate) {
            for (int i = 0; i < hiddenHudRoots.Count; i++) {
                if (hiddenHudRoots[i].root == candidate) return true;
            }

            return false;
        }

        bool IsMapOwnedRoot(GameObject candidate) {
            if (candidate == null) return true;
            Transform candidateTransform = candidate.transform;

            if (mapManager != null && (candidate == mapManager.gameObject || candidateTransform.IsChildOf(mapManager.transform))) return true;
            if (overlayRoot != null && (candidate == overlayRoot.gameObject || candidateTransform.IsChildOf(overlayRoot))) return true;
            if (customFrame != null && (candidate == customFrame.gameObject || candidateTransform.IsChildOf(customFrame))) return true;

            return false;
        }

        void ApplyCustomFrameLayout() {
            if (customFrame != null) customFrame.gameObject.SetActive(false);
        }
        void SetWorldMapRootVisible(bool visible) {
            if (mapManager == null) return;

            if (!mapManager.gameObject.activeSelf) mapManager.gameObject.SetActive(true);

            CanvasGroup group = mapManager.GetComponent<CanvasGroup>();
            if (group == null) group = mapManager.gameObject.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        void EnableMapSafely() {
            if (mapManager == null) return;

            if (!mapManager.gameObject.activeSelf) mapManager.gameObject.SetActive(true);
            mapManager.disableMinimap = true;
            mapManager.haveBorder = false;
            mapManager.haveZoomButtons = false;
            mapManager.haveExitButton = false;
            mapManager.displayDirections = false;
            mapManager.displayGrid = false;
            EnsureWorldMapRenderTexture();
            mapManager.mapEnabled = true;
            SetMapChildActive("Map Mask", true);
            SetMapChildActive("Map Border", false);
            SetMapChildActive("Map Directions", false);
            SetMapChildActive("Map Zoom Buttons", false);
            SetMapChildActive("Map Exit Button", false);
            if (mapCamera != null) mapCamera.gameObject.SetActive(true);
        }

        void DisableMapSafely() {
            if (mapManager == null) return;

            if (!mapManager.gameObject.activeSelf) mapManager.gameObject.SetActive(true);
            mapManager.disableMinimap = true;
            mapManager.haveBorder = false;
            mapManager.haveZoomButtons = false;
            mapManager.haveExitButton = false;
            mapManager.displayDirections = false;
            mapManager.displayGrid = false;
            mapManager.mapEnabled = false;
            SetMapChildActive("Map Mask", false);
            SetMapChildActive("Map Border", false);
            SetMapChildActive("Map Directions", false);
            SetMapChildActive("Map Zoom Buttons", false);
            SetMapChildActive("Map Exit Button", false);
            if (mapCamera != null) mapCamera.gameObject.SetActive(false);
        }

        void SetMapChildActive(string childName, bool active) {
            if (mapManager == null || string.IsNullOrWhiteSpace(childName)) return;
            Transform child = mapManager.transform.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
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
            GetEffectiveWorldBounds(out float minWorldX, out float maxWorldX, out float minWorldZ, out float maxWorldZ);

            float minX = minWorldX + halfWidth;
            float maxX = maxWorldX - halfWidth;
            float minZ = minWorldZ + halfHeight;
            float maxZ = maxWorldZ - halfHeight;

            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (minWorldX + maxWorldX) * 0.5f;
            position.z = minZ <= maxZ ? Mathf.Clamp(position.z, minZ, maxZ) : (minWorldZ + maxWorldZ) * 0.5f;
            return position;
        }

        Vector3 ClampWorldPosition(Vector3 position) {
            if (!clampToWorldBounds) return position;

            GetEffectiveWorldBounds(out float minWorldX, out float maxWorldX, out float minWorldZ, out float maxWorldZ);
            position.x = Mathf.Clamp(position.x, minWorldX, maxWorldX);
            position.z = Mathf.Clamp(position.z, minWorldZ, maxWorldZ);
            return position;
        }

        void GetEffectiveWorldBounds(out float minX, out float maxX, out float minZ, out float maxZ) {
            float halfWorldX = Mathf.Max(0.5f, worldSize.x * 0.5f);
            float halfWorldZ = Mathf.Max(0.5f, worldSize.y * 0.5f);
            float leftInset = worldSize.x * Mathf.Clamp01(worldBoundsInsetPercent.x);
            float topInset = worldSize.y * Mathf.Clamp01(worldBoundsInsetPercent.y);
            float rightInset = worldSize.x * Mathf.Clamp01(worldBoundsInsetPercent.z);
            float bottomInset = worldSize.y * Mathf.Clamp01(worldBoundsInsetPercent.w);

            minX = worldCenter.x - halfWorldX + leftInset;
            maxX = worldCenter.x + halfWorldX - rightInset;
            minZ = worldCenter.y - halfWorldZ + bottomInset;
            maxZ = worldCenter.y + halfWorldZ - topInset;

            if (minX > maxX) {
                float center = (minX + maxX) * 0.5f;
                minX = center;
                maxX = center;
            }

            if (minZ > maxZ) {
                float center = (minZ + maxZ) * 0.5f;
                minZ = center;
                maxZ = center;
            }
        }

        void OnValidate() {
            NormalizeVisiblePercents();
            viewportSize = new Vector2(Mathf.Max(320f, viewportSize.x), Mathf.Max(240f, viewportSize.y));
            worldSize = new Vector2(Mathf.Max(1f, worldSize.x), Mathf.Max(1f, worldSize.y));
            frameOverscan = Mathf.Max(0f, frameOverscan);
            worldBoundsInsetPercent.x = Mathf.Clamp01(worldBoundsInsetPercent.x);
            worldBoundsInsetPercent.y = Mathf.Clamp01(worldBoundsInsetPercent.y);
            worldBoundsInsetPercent.z = Mathf.Clamp01(worldBoundsInsetPercent.z);
            worldBoundsInsetPercent.w = Mathf.Clamp01(worldBoundsInsetPercent.w);
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



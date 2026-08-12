using System.Collections.Generic;
using System.IO;
using AAMAP;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Capstone.Game.MapSystem.Editor {
    public static class MapSetupEditor {
        const string GeneratedFolder = "Assets/Game/MapSystem/Generated";
        const string SpriteFolder = "Assets/Game/MapSystem/Sprites";
        const string MapMinimapSpriteFolder = "Assets/Game/MapSystem/Sprites/MapMinimap";
        const string AaPackageMarker = "AA Map and Minimap System";

        enum GeneratedIconShape {
            Circle,
            Triangle,
            Diamond,
            Square,
            Ring,
            Star
        }

        [MenuItem("Tools/ToolCuaThang/Game Map/Setup Complete Map System")]
        public static void SetupCompleteMapSystem() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogWarning("Map setup: stop Play Mode before running setup. Changes made while playing are temporary and will be lost.");
                return;
            }

            EnsureFolders();
            EnsureMapMinimapSpriteImports();

            Transform player = FindSelectedOrScenePlayer();
            if (player == null) Debug.LogWarning("Map setup: cannot find Player. Select Player or add BasicPlayerMovement/tag Player, then run setup again.");

            Bounds mapBounds = EstimateMapBounds(player);
            Vector2 mapCenter = new Vector2(mapBounds.center.x, mapBounds.center.z);
            float mapSide = Mathf.Max(60f, mapBounds.size.x, mapBounds.size.z);
            Vector2 mapSize = new Vector2(mapSide, mapSide);
            const float worldMapAspect = 16f / 9f;
            const float initialVisiblePercent = 0.12f;
            const float minimumVisiblePercent = 0.05f;
            const float maximumVisiblePercent = 0.30f;
            float worldMapInitialSize = CalculatePercentOrthographicSize(mapSize, worldMapAspect, initialVisiblePercent);
            float worldMapCloseSize = CalculatePercentOrthographicSize(mapSize, worldMapAspect, minimumVisiblePercent);
            float worldMapFarSize = CalculatePercentOrthographicSize(mapSize, worldMapAspect, maximumVisiblePercent);

            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            GameObject minimapObject = EnsureAAMinimap(canvas.transform);
            GameObject worldMapObject = EnsureAAWorldMap(canvas.transform);
            GameObject minimapCameraObject = EnsureScenePrefabOrCamera("Minimap Camera", "Minimap Camera");
            GameObject mapCameraObject = EnsureScenePrefabOrCamera("Map Camera", "Map Camera");
            GameObject mapIconPrefab = FindPrefab("Map Icon");

            RenderTexture minimapTexture = EnsureRenderTexture(GeneratedFolder + "/Minimap_RT.renderTexture", 512, 512, "Minimap_RT");
            RenderTexture worldMapTexture = EnsureRenderTexture(GeneratedFolder + "/WorldMap_RT.renderTexture", 2048, 1152, "WorldMap_RT");

            MinimapManager minimapManager = minimapObject.GetComponent<MinimapManager>();
            MapManager mapManager = worldMapObject.GetComponent<MapManager>();
            Camera minimapCamera = minimapCameraObject.GetComponent<Camera>();
            Camera mapCamera = mapCameraObject.GetComponent<Camera>();

            ConfigureCanvas(canvas);
            ConfigureMinimap(minimapObject, minimapManager, minimapCameraObject, minimapTexture, player);
            ConfigureWorldMap(worldMapObject, mapManager, mapCameraObject, worldMapTexture, minimapObject, mapCenter, worldMapInitialSize, worldMapCloseSize, worldMapFarSize);
            ConfigureCamera(minimapCamera, player != null ? player.position : Vector3.zero, 60f, 28f, minimapTexture);
            ConfigureCamera(mapCamera, player != null ? player.position : new Vector3(mapCenter.x, 0f, mapCenter.y), 180f, worldMapInitialSize, worldMapTexture);
            DisableSceneMinimap(minimapObject, minimapCameraObject);

            RectTransform mapRect = FindChildComponent<RectTransform>(worldMapObject.transform, "Map Mask");
            if (mapRect == null) mapRect = worldMapObject.GetComponent<RectTransform>();
            RectTransform overlay = EnsureWorldMapOverlay(
                worldMapObject.transform,
                out Text regionLabel,
                out Button closeButton,
                out Button zoomInButton,
                out Button zoomOutButton,
                out Button centerOnPlayerButton,
                out Button clearWaypoint,
                out List<WorldMapController.MapFilterBinding> filters);

            GameObject root = EnsureRootObject("MapSystem");
            EnsureIconContainers(root.transform);

            AAMapRuntimeBinder binder = GetOrAddComponent<AAMapRuntimeBinder>(root);
            MinimapController minimapController = GetOrAddComponent<MinimapController>(root);
            WorldMapController worldMapController = GetOrAddComponent<WorldMapController>(root);
            MapMarkerManager markerManager = GetOrAddComponent<MapMarkerManager>(root);
            MapIconRegistry iconRegistry = GetOrAddComponent<MapIconRegistry>(root);
            MapInputController inputController = GetOrAddComponent<MapInputController>(root);
            MapSystemController mapSystem = GetOrAddComponent<MapSystemController>(root);
            MapSetupValidator validator = GetOrAddComponent<MapSetupValidator>(root);
            QuestMapMarkerBridge questBridge = GetOrAddComponent<QuestMapMarkerBridge>(root);
            QuestPanelMapConnector questConnector = GetOrAddComponent<QuestPanelMapConnector>(root);
            LocalPlayerControlLock controlLock = FindFirst<LocalPlayerControlLock>();
            Texture questIcon = FindCustomIconTexture("Thang_Icon_Quest") ?? FindAAMapIconTexture("Map Icon 6") ?? EnsureGeneratedIcon("MapIcon_Quest", GeneratedIconShape.Star);

            SetObjectReference(binder, "minimapManager", minimapManager);
            SetObjectReference(binder, "mapManager", mapManager);
            SetObjectReference(binder, "minimapCamera", minimapCameraObject);
            SetObjectReference(binder, "mapCamera", mapCameraObject);
            SetObjectReference(binder, "playerTarget", player);
            SetBool(binder, "minimapDisabled", true);

            SetObjectReference(minimapController, "minimapManager", minimapManager);
            SetObjectReference(minimapController, "minimapCamera", minimapCamera);
            SetObjectReference(minimapController, "target", player);
            SetBool(minimapController, "clampToWorldBounds", true);
            SetVector2(minimapController, "worldCenter", mapCenter);
            SetVector2(minimapController, "worldSize", mapSize);
            minimapController.enabled = false;
            EditorUtility.SetDirty(minimapController);

            SetObjectReference(worldMapController, "mapManager", mapManager);
            SetObjectReference(worldMapController, "mapCamera", mapCamera);
            SetObjectReference(worldMapController, "minimapRoot", minimapObject);
            SetObjectReference(worldMapController, "mapInteractionRect", mapRect);
            SetObjectReference(worldMapController, "overlayRoot", overlay);
            SetObjectReference(worldMapController, "regionNameText", regionLabel);
            SetObjectReference(worldMapController, "closeButton", closeButton);
            SetObjectReference(worldMapController, "zoomInButton", zoomInButton);
            SetObjectReference(worldMapController, "zoomOutButton", zoomOutButton);
            SetObjectReference(worldMapController, "centerOnPlayerButton", centerOnPlayerButton);
            SetObjectReference(worldMapController, "clearWaypointButton", clearWaypoint);
            SetObjectReference(worldMapController, "iconRegistry", iconRegistry);
            SetObjectReference(worldMapController, "playerTarget", player);
            SetObjectReference(worldMapController, "customFrame", FindChildComponent<RectTransform>(overlay, "Thang World Map Frame"));
            SetBool(worldMapController, "clampToWorldBounds", true);
            SetVector2(worldMapController, "worldCenter", mapCenter);
            SetVector2(worldMapController, "worldSize", mapSize);
            SetVector4(worldMapController, "worldBoundsInsetPercent", new Vector4(0.07f, 0f, 0.03f, 0.16f));
            SetBool(worldMapController, "useMapSetBounds", true);
            SetFloat(worldMapController, "minimumVisiblePercent", minimumVisiblePercent);
            SetFloat(worldMapController, "initialVisiblePercent", initialVisiblePercent);
            SetFloat(worldMapController, "maximumVisiblePercent", maximumVisiblePercent);
            SetFilterBindings(worldMapController, filters);

            SetObjectReference(markerManager, "mapBinder", binder);
            SetObjectReference(markerManager, "mapIconPrefab", mapIconPrefab);
            AssignDefaultMarkerIcons(markerManager);
            ConfigureMarkerPresentation(markerManager);
            SetObjectReference(iconRegistry, "markerManager", markerManager);
            SetObjectReference(inputController, "mapSystem", mapSystem);
            SetObjectReference(inputController, "mapBinder", binder);
            SetObjectReference(inputController, "worldMap", worldMapController);
            SetObjectReference(inputController, "mapManager", mapManager);
            SetObjectReference(inputController, "controlLock", controlLock);

            SetObjectReference(mapSystem, "aaBinder", binder);
            SetObjectReference(mapSystem, "minimap", minimapController);
            SetObjectReference(mapSystem, "worldMap", worldMapController);
            SetObjectReference(mapSystem, "markerManager", markerManager);
            SetObjectReference(mapSystem, "iconRegistry", iconRegistry);
            SetObjectReference(mapSystem, "input", inputController);
            SetObjectReference(mapSystem, "controlLock", controlLock);
            SetObjectReference(mapSystem, "playerTarget", player);
            SetBool(mapSystem, "disableMinimap", true);
            SetObjectReference(questBridge, "trackedQuestIcon", questIcon);
            SetObjectReference(questConnector, "mapSystem", mapSystem);

            binder.SetReferences(minimapManager, mapManager, minimapCameraObject, mapCameraObject, player);
            minimapController.SetReferences(minimapManager, minimapCamera, player);
            minimapController.SetWorldBounds(mapCenter, mapSize);
            worldMapController.SetReferences(mapManager, mapCamera, mapRect, overlay, player);
            worldMapController.SetWorldBounds(mapCenter, mapSize);
            inputController.SetReferences(mapSystem, worldMapController, mapManager, controlLock);
            iconRegistry.SetMarkerManager(markerManager);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(minimapObject);
            EditorUtility.SetDirty(worldMapObject);
            AssetDatabase.SaveAssets();

            bool ok = validator.ValidateSetup(true);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log(ok
                ? "Map setup finished and scene saved. Press M in Play Mode to test World Map."
                : "Map setup finished with warnings and scene saved. Use Tools > ToolCuaThang > Game Map > Validate Setup.");
        }

        [MenuItem("Tools/ToolCuaThang/Game Map/Validate Setup")]
        public static void ValidateSetup() {
            MapSetupValidator validator = FindFirst<MapSetupValidator>();
            if (validator == null) {
                Debug.LogWarning("Map setup: missing MapSetupValidator. Run Tools > ToolCuaThang > Game Map > Setup Complete Map System first.");
                return;
            }
            validator.ValidateSetup(true);
        }

        static void EnsureFolders() {
            EnsureFolder("Assets", "Game");
            EnsureFolder("Assets/Game", "MapSystem");
            EnsureFolder("Assets/Game/MapSystem", "Editor");
            EnsureFolder("Assets/Game/MapSystem", "Generated");
            EnsureFolder("Assets/Game/MapSystem", "Sprites");
            EnsureFolder("Assets/Game/MapSystem/Sprites", "MapMinimap");
        }

        static Canvas EnsureCanvas() {
            Canvas canvas = FindFirst<Canvas>();
            if (canvas != null) return canvas;

            GameObject obj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(obj, "Create map canvas");
            return obj.GetComponent<Canvas>();
        }

        static void ConfigureCanvas(Canvas canvas) {
            Undo.RecordObject(canvas, "Configure map canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            Undo.RecordObject(scaler, "Configure canvas scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
        }

        static void EnsureEventSystem() {
            EventSystem eventSystem = FindFirst<EventSystem>();
            if (eventSystem == null) {
                GameObject obj = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(obj, "Create EventSystem");
                eventSystem = obj.GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null) Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null) Undo.AddComponent<StandaloneInputModule>(eventSystem.gameObject);
#endif
        }

        static GameObject EnsureAAMinimap(Transform canvas) {
            MinimapManager existing = FindFirst<MinimapManager>();
            GameObject obj = existing != null ? existing.gameObject : InstantiatePrefab("Minimap", canvas);
            if (obj == null) obj = CreateFallbackUIObject("MinimapPanel", canvas, typeof(MinimapManager));
            obj.name = "MinimapPanel";
            obj.transform.SetParent(canvas, false);
            return obj;
        }

        static GameObject EnsureAAWorldMap(Transform canvas) {
            MapManager existing = FindFirst<MapManager>();
            GameObject obj = existing != null ? existing.gameObject : InstantiatePrefab("Map", canvas);
            if (obj == null) obj = CreateFallbackUIObject("WorldMapPanel", canvas, typeof(MapManager));
            obj.name = "WorldMapPanel";
            obj.transform.SetParent(canvas, false);
            return obj;
        }

        static void ConfigureMinimap(GameObject obj, MinimapManager manager, GameObject cameraObject, RenderTexture texture, Transform player) {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null) {
                Undo.RecordObject(rect, "Configure minimap rect");
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(28f, -28f);
                rect.sizeDelta = new Vector2(236f, 236f);
                rect.localScale = Vector3.one;
            }

            if (manager != null) {
                Undo.RecordObject(manager, "Configure minimap manager");
                manager.SetCamera(cameraObject);
                if (player != null) manager.SetTargetObject(player.gameObject);
                manager.rotateWithTarget = false;
                manager.clearFlags = MinimapClearFlags.SolidColor;
                manager.backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
                manager.minimapOpacity = 1f;
                manager.minimapColor = Color.white;
                manager.haveBorder = false;
                manager.haveZoomButtons = false;
                manager.displayDirections = false;
                manager.displayGrid = false;
                manager.minimapHeight = 60f;
                manager.minimapRange = 28f;
                manager.renderTexture = texture;
                Sprite circle = FindSprite("Minimap Shape Circle");
                if (circle != null) manager.minimapShape = circle;
                Sprite border = FindCustomSprite("Thang_Minimap_Frame") ?? FindSprite("Minimap Circle Border 6") ?? FindSprite("Minimap Circle Border 1");
                if (border != null) manager.borderSprite = border;
            }

            RectTransform mask = EnsureMaskChild(obj.transform, "Minimap Mask", manager != null ? manager.minimapShape : null);
            ConfigureMinimapMask(mask);
            RectTransform backgroundFiller = EnsureOpaqueImageChild(mask, "Minimap Background Filler", new Color(0.015f, 0.035f, 0.035f, 1f));
            backgroundFiller.transform.SetAsFirstSibling();
            RectTransform grid = EnsureOpaqueImageChild(mask, "Minimap Grid", Color.clear);
            grid.gameObject.SetActive(false);
            RawImage display = FindChildComponent<RawImage>(mask, "Minimap Display") ?? EnsureRawImageChild(mask, "Minimap Display");
            if (display != null) {
                Undo.RecordObject(display, "Configure minimap display");
                display.texture = texture;
                display.color = Color.white;
                Stretch(display.GetComponent<RectTransform>());
                display.transform.SetSiblingIndex(1);
            }

            Sprite customFrame = FindCustomSprite("Thang_Minimap_Frame");
            if (customFrame != null) {
                RectTransform frame = EnsureImageChild(obj.transform, "Thang Minimap Frame", customFrame, Color.white, Image.Type.Simple, true);
                ConfigureMinimapFrame(frame);
                frame.SetAsLastSibling();
            }

            SetTransparentImage(obj.GetComponent<Image>());
            SetChildActive(obj.transform, "Minimap Border", false);
            SetChildActive(obj.transform, "Minimap Directions", false);
            SetChildActive(obj.transform, "Minimap Zoom Buttons", false);
        }

        static void DisableSceneMinimap(GameObject minimapObject, GameObject minimapCameraObject) {
            if (minimapObject != null) {
                Undo.RecordObject(minimapObject, "Disable minimap panel");
                minimapObject.SetActive(false);
            }

            if (minimapCameraObject != null) {
                Undo.RecordObject(minimapCameraObject, "Disable minimap camera");
                minimapCameraObject.SetActive(false);
            }
        }
        static void ConfigureWorldMap(GameObject obj, MapManager manager, GameObject cameraObject, RenderTexture texture, GameObject minimapObject, Vector2 mapCenter, float initialSize, float closeSize, float farSize) {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null) {
                Undo.RecordObject(rect, "Configure world map rect");
                Stretch(rect);
            }

            Image backdrop = EnsureImage(obj, new Color(0.015f, 0.03f, 0.03f, 0.96f));
            backdrop.raycastTarget = true;

            if (manager != null) {
                Undo.RecordObject(manager, "Configure world map manager");
                obj.SetActive(true);
                manager.mapEnabled = false;
                manager.enablingShortcut = KeyCode.None;
                manager.disablingShortcut = KeyCode.None;
                manager.SetMapCamera(cameraObject);
                manager.cameraPosition = new Vector3(mapCenter.x, 180f, mapCenter.y);
                manager.cameraOrthographicSize = initialSize;
                manager.minimumRange = Mathf.Min(closeSize, farSize);
                manager.maximumRange = Mathf.Max(closeSize, farSize);
                manager.haveBorder = false;
                manager.haveZoomButtons = false;
                manager.haveExitButton = false;
                manager.displayDirections = false;
                manager.displayGrid = false;
                manager.clearFlags = MapClearFlags.SolidColor;
                manager.cameraBGColor = new Color(0.02f, 0.04f, 0.05f, 1f);
                manager.haveBackgroundImage = false;
                manager.disableMinimap = true;
                manager.minimapGameObject = null;
                manager.minimapManager = null;
                manager.renderTexture = texture;
                Sprite shape = FindSprite("Map Shape Rounded Rectangle") ?? FindSprite("Map Shape Sharp Rectangle") ?? FindSprite("Map Shape Square");
                if (shape != null) manager.mapShape = shape;
                manager.mapColor = Color.white;
                manager.mapOpacity = 1f;
            }

            RectTransform mask = EnsureMaskChild(obj.transform, "Map Mask", manager != null ? manager.mapShape : null);
            ConfigureWorldMapViewport(mask);
            RectTransform backgroundFiller = EnsureOpaqueImageChild(mask, "Map Background Filler", new Color(0.015f, 0.035f, 0.035f, 1f));
            backgroundFiller.transform.SetAsFirstSibling();
            RectTransform background = EnsureOpaqueImageChild(mask, "Map Background", Color.clear);
            background.gameObject.SetActive(false);
            RectTransform grid = EnsureOpaqueImageChild(mask, "Map Grid", Color.clear);
            grid.gameObject.SetActive(false);
            RawImage display = FindChildComponent<RawImage>(mask, "Map Display") ?? EnsureRawImageChild(mask, "Map Display");
            if (display != null) {
                Undo.RecordObject(display, "Configure world map display");
                display.texture = texture;
                display.color = Color.white;
                display.material = null;
                Stretch(display.GetComponent<RectTransform>());
                display.transform.SetSiblingIndex(1);
            }

            SetChildActive(obj.transform, "Map Mask", true);
            SetChildActive(obj.transform, "Map Border", false);
            SetChildActive(obj.transform, "Map Directions", false);
            SetChildActive(obj.transform, "Map Zoom Buttons", false);
            SetChildActive(obj.transform, "Map Exit Button", false);
            SetChildActive(obj.transform, "Thang World Map Frame", false);
            obj.transform.SetAsLastSibling();
        }

        static void ConfigureCamera(Camera camera, Vector3 focus, float height, float size, RenderTexture texture) {
            if (camera == null) return;
            Undo.RecordObject(camera, "Configure map camera");
            Undo.RecordObject(camera.transform, "Configure map camera transform");
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(1000f, height + 500f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.04f, 0.05f, 1f);
            camera.targetTexture = texture;
            if (texture != null && texture.height > 0) camera.aspect = (float)texture.width / texture.height;
            camera.transform.position = new Vector3(focus.x, height, focus.z);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        static void ApplyMapCameraLayerMask(Camera camera) {
            if (camera == null) return;

            string[] excludedLayers = { "Water", "Suimono_Water", "Suimono_Depth", "Suimono_Screen", "TransparentFX", "SmallVFX", "MedVFX", "LargeVFX" };
            int mask = camera.cullingMask;
            for (int i = 0; i < excludedLayers.Length; i++) {
                int layer = LayerMask.NameToLayer(excludedLayers[i]);
                if (layer >= 0) mask &= ~(1 << layer);
            }

            camera.cullingMask = mask;
        }
        static RectTransform EnsureWorldMapOverlay(
            Transform worldMap,
            out Text regionLabel,
            out Button closeButton,
            out Button zoomInButton,
            out Button zoomOutButton,
            out Button centerOnPlayerButton,
            out Button clearWaypointButton,
            out List<WorldMapController.MapFilterBinding> filters) {
            filters = new List<WorldMapController.MapFilterBinding>();
            RectTransform overlay = EnsureUIChild(worldMap, "World Map Overlay");
            Stretch(overlay);
            overlay.gameObject.SetActive(false);

            Sprite customFrame = FindCustomSprite("Thang_WorldMap_Frame");
            if (customFrame != null) {
                RectTransform frame = EnsureImageChild(overlay, "Thang World Map Frame", customFrame, Color.white, Image.Type.Sliced, false);
                ConfigureWorldMapFrame(frame);
                frame.SetAsLastSibling();
            }

            RectTransform region = EnsureUIChild(overlay, "Region Name");
            region.anchorMin = new Vector2(0.5f, 1f);
            region.anchorMax = new Vector2(0.5f, 1f);
            region.pivot = new Vector2(0.5f, 1f);
            region.anchoredPosition = new Vector2(0f, -32f);
            region.sizeDelta = new Vector2(420f, 44f);
            Image regionBg = EnsureImage(region.gameObject, new Color(0.05f, 0.14f, 0.13f, 0.7f));
            regionBg.raycastTarget = false;
            regionLabel = EnsureText(region, "Region Text", "Region", 24, TextAnchor.MiddleCenter, Color.black);

            RectTransform close = EnsureUIChild(overlay, "Close Button");
            close.anchorMin = new Vector2(1f, 1f);
            close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-34f, -30f);
            close.sizeDelta = new Vector2(48f, 48f);
            closeButton = EnsureSpriteButton(close, "X", FindCustomSprite("Thang_Button_Close") ?? FindSprite("Map Exit Button 1"), Color.white);

            RectTransform zoomControls = EnsureUIChild(overlay, "Zoom Controls");
            zoomControls.anchorMin = new Vector2(1f, 1f);
            zoomControls.anchorMax = new Vector2(1f, 1f);
            zoomControls.pivot = new Vector2(1f, 1f);
            zoomControls.anchoredPosition = new Vector2(-92f, -108f);
            zoomControls.sizeDelta = new Vector2(170f, 56f);
            EnsureImage(zoomControls.gameObject, new Color(0.04f, 0.10f, 0.09f, 0.74f));

            RectTransform zoomIn = EnsureUIChild(zoomControls, "Zoom In Button");
            zoomIn.anchorMin = new Vector2(0f, 0.5f);
            zoomIn.anchorMax = new Vector2(0f, 0.5f);
            zoomIn.pivot = new Vector2(0f, 0.5f);
            zoomIn.anchoredPosition = new Vector2(10f, 0f);
            zoomIn.sizeDelta = new Vector2(42f, 42f);
            zoomInButton = EnsureSpriteButton(zoomIn, "+", FindCustomSprite("Thang_Button_ZoomIn") ?? FindSprite("Zoom In Icon 1"), Color.white);

            RectTransform zoomOut = EnsureUIChild(zoomControls, "Zoom Out Button");
            zoomOut.anchorMin = new Vector2(0f, 0.5f);
            zoomOut.anchorMax = new Vector2(0f, 0.5f);
            zoomOut.pivot = new Vector2(0f, 0.5f);
            zoomOut.anchoredPosition = new Vector2(62f, 0f);
            zoomOut.sizeDelta = new Vector2(42f, 42f);
            zoomOutButton = EnsureSpriteButton(zoomOut, "-", FindCustomSprite("Thang_Button_ZoomOut") ?? FindSprite("Zoom Out Icon 1"), Color.white);

            RectTransform center = EnsureUIChild(zoomControls, "Center On Player Button");
            center.anchorMin = new Vector2(0f, 0.5f);
            center.anchorMax = new Vector2(0f, 0.5f);
            center.pivot = new Vector2(0f, 0.5f);
            center.anchoredPosition = new Vector2(114f, 0f);
            center.sizeDelta = new Vector2(42f, 42f);
            centerOnPlayerButton = EnsureSpriteButton(center, "P", FindCustomSprite("Thang_Button_Center") ?? FindSprite("Map Icon 1"), Color.white);

            RectTransform filterBar = EnsureUIChild(overlay, "Filter Bar");
            filterBar.anchorMin = new Vector2(0.5f, 0f);
            filterBar.anchorMax = new Vector2(0.5f, 0f);
            filterBar.pivot = new Vector2(0.5f, 0f);
            filterBar.anchoredPosition = new Vector2(0f, 34f);
            filterBar.sizeDelta = new Vector2(980f, 46f);
            EnsureImage(filterBar.gameObject, new Color(0.04f, 0.10f, 0.09f, 0.72f));

            MapMarkerType[] types = { MapMarkerType.NPC, MapMarkerType.Pet, MapMarkerType.Enemy, MapMarkerType.Boss, MapMarkerType.QuestTarget, MapMarkerType.Shop, MapMarkerType.FastTravel, MapMarkerType.CoopPlayer };
            string[] labels = { "NPC", "Pet", "Enemy", "Boss", "Quest", "Shop", "Travel", "Co-op" };
            for (int i = 0; i < types.Length; i++) {
                Toggle toggle = EnsureFilterToggle(filterBar, labels[i], i);
                filters.Add(new WorldMapController.MapFilterBinding { toggle = toggle, markerType = types[i] });
            }

            RectTransform clear = EnsureUIChild(overlay, "Clear Waypoint Button");
            clear.anchorMin = new Vector2(1f, 0f);
            clear.anchorMax = new Vector2(1f, 0f);
            clear.pivot = new Vector2(1f, 0f);
            clear.anchoredPosition = new Vector2(-36f, 34f);
            clear.sizeDelta = new Vector2(170f, 42f);
            clearWaypointButton = EnsureButton(clear, "Clear Waypoint", new Color(0.86f, 0.78f, 0.58f, 0.94f));
            return overlay;
        }

        static Toggle EnsureFilterToggle(RectTransform parent, string label, int index) {
            RectTransform root = EnsureUIChild(parent, "Filter " + label);
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(104f, 34f);
            root.anchoredPosition = new Vector2(14f + index * 116f, 0f);

            Image bg = EnsureImage(root.gameObject, new Color(0.19f, 0.31f, 0.20f, 0.95f));
            Toggle toggle = root.GetComponent<Toggle>();
            if (toggle == null) toggle = Undo.AddComponent<Toggle>(root.gameObject);
            toggle.isOn = true;
            toggle.targetGraphic = bg;

            RectTransform check = EnsureUIChild(root, "Checkmark");
            check.anchorMin = new Vector2(0f, 0.5f);
            check.anchorMax = new Vector2(0f, 0.5f);
            check.pivot = new Vector2(0f, 0.5f);
            check.anchoredPosition = new Vector2(8f, 0f);
            check.sizeDelta = new Vector2(16f, 16f);
            toggle.graphic = EnsureImage(check.gameObject, new Color(0.87f, 0.72f, 0.38f, 1f));

            Text text = EnsureText(root, "Label", label, 15, TextAnchor.MiddleLeft, Color.black);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.offsetMin = new Vector2(30f, 0f);
            textRect.offsetMax = new Vector2(-6f, 0f);
            return toggle;
        }

        static void EnsureIconContainers(Transform root) {
            Transform parent = EnsurePlainChild(root, "Map Icon Containers");
            string[] names = { "Player Icons", "NPC Icons", "Pet Icons", "Enemy Icons", "Boss Icons", "Quest Icons", "Shop Icons", "Fast Travel Icons", "Item Icons", "Co-op Player Icons" };
            for (int i = 0; i < names.Length; i++) EnsurePlainChild(parent, names[i]);
        }

        static GameObject EnsureScenePrefabOrCamera(string prefabName, string objectName) {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null) return existing;

            GameObject result = InstantiatePrefab(prefabName, null);
            if (result == null) {
                result = new GameObject(objectName, typeof(Camera));
                Undo.RegisterCreatedObjectUndo(result, "Create " + objectName);
            }
            result.name = objectName;
            if (result.GetComponent<Camera>() == null) Undo.AddComponent<Camera>(result);
            return result;
        }

        static GameObject EnsureRootObject(string name) {
            GameObject root = GameObject.Find(name);
            if (root != null) return root;
            root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            return root;
        }

        static GameObject InstantiatePrefab(string prefabName, Transform parent) {
            GameObject prefab = FindPrefab(prefabName);
            if (prefab == null) {
                Debug.LogWarning("Map setup: missing AA prefab " + prefabName + ".");
                return null;
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance != null) Undo.RegisterCreatedObjectUndo(instance, "Create " + prefabName);
            return instance;
        }

        static GameObject FindPrefab(string prefabName) {
            string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.Contains(AaPackageMarker)) continue;
                if (Path.GetFileNameWithoutExtension(path) != prefabName) continue;
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        static Sprite FindSprite(string spriteName) {
            string[] guids = AssetDatabase.FindAssets(spriteName + " t:Sprite");
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.Contains(AaPackageMarker)) continue;
                if (Path.GetFileNameWithoutExtension(path) != spriteName) continue;
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        static RenderTexture EnsureRenderTexture(string assetPath, int width, int height, string name) {
            RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
            if (texture != null) {
                if (texture.width != width || texture.height != height) {
                    texture.Release();
                    texture.width = width;
                    texture.height = height;
                    EditorUtility.SetDirty(texture);
                }
                return texture;
            }

            texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) {
                name = name,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            AssetDatabase.CreateAsset(texture, assetPath);
            return texture;
        }

        static T GetOrAddComponent<T>(GameObject source) where T : Component {
            T component = source.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(source);
        }

        static RectTransform EnsureUIChild(Transform parent, string name) {
            Transform existing = parent.Find(name);
            GameObject child = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null) {
                Undo.RegisterCreatedObjectUndo(child, "Create " + name);
                child.transform.SetParent(parent, false);
            }
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect == null) rect = Undo.AddComponent<RectTransform>(child);
            return rect;
        }

        static Transform EnsurePlainChild(Transform parent, string name) {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create " + name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        static GameObject CreateFallbackUIObject(string name, Transform parent, params System.Type[] components) {
            List<System.Type> all = new List<System.Type> { typeof(RectTransform) };
            all.AddRange(components);
            GameObject obj = new GameObject(name, all.ToArray());
            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            obj.transform.SetParent(parent, false);
            return obj;
        }

        static RawImage EnsureRawImageChild(Transform parent, string name) {
            RectTransform rect = EnsureUIChild(parent, name);
            RawImage rawImage = rect.GetComponent<RawImage>();
            if (rawImage == null) rawImage = Undo.AddComponent<RawImage>(rect.gameObject);
            return rawImage;
        }

        static RectTransform EnsureMaskChild(Transform parent, string name, Sprite shape) {
            RectTransform rect = EnsureUIChild(parent, name);
            Stretch(rect);

            Image image = EnsureImage(rect.gameObject, Color.white);
            image.raycastTarget = false;
            if (shape != null) image.sprite = shape;

            Mask mask = rect.GetComponent<Mask>();
            if (mask == null) mask = Undo.AddComponent<Mask>(rect.gameObject);
            mask.showMaskGraphic = false;
            rect.gameObject.SetActive(true);
            return rect;
        }

        static Image EnsureImage(GameObject source, Color color) {
            Image image = source.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(source);
            image.color = color;
            return image;
        }

        static void SetTransparentImage(Image image) {
            if (image == null) return;
            Undo.RecordObject(image, "Make map image transparent");
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        static RectTransform EnsureOpaqueImageChild(Transform parent, string name, Color color) {
            RectTransform rect = EnsureUIChild(parent, name);
            Image image = EnsureImage(rect.gameObject, color);
            image.raycastTarget = false;
            Stretch(rect);
            return rect;
        }

        static RectTransform EnsureImageChild(Transform parent, string name, Sprite sprite, Color color, Image.Type imageType, bool preserveAspect) {
            RectTransform rect = EnsureUIChild(parent, name);
            Image image = EnsureImage(rect.gameObject, color);
            image.raycastTarget = false;
            image.sprite = sprite;
            image.type = imageType;
            image.preserveAspect = preserveAspect;
            rect.gameObject.SetActive(sprite != null);
            return rect;
        }

        static void ConfigureWorldMapFrame(RectTransform rect) {
            if (rect == null) return;

            Undo.RecordObject(rect, "Configure world map custom frame");
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1408f, 848f);
            rect.localScale = Vector3.one;
        }

        static void ConfigureMinimapMask(RectTransform rect) {
            if (rect == null) return;

            Undo.RecordObject(rect, "Configure minimap mask");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(20f, 20f);
            rect.offsetMax = new Vector2(-20f, -20f);
            rect.localScale = Vector3.one;
        }

        static void ConfigureMinimapFrame(RectTransform rect) {
            if (rect == null) return;

            Undo.RecordObject(rect, "Configure minimap custom frame");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-30f, -30f);
            rect.offsetMax = new Vector2(30f, 30f);
            rect.localScale = Vector3.one;
        }

        static Text EnsureText(RectTransform parent, string name, string value, int size, TextAnchor anchor, Color color) {
            RectTransform textRect = EnsureUIChild(parent, name);
            Text text = textRect.GetComponent<Text>();
            if (text == null) text = Undo.AddComponent<Text>(textRect.gameObject);
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            Stretch(textRect);
            return text;
        }

        static Button EnsureButton(RectTransform rect, string label, Color color) {
            Image image = EnsureImage(rect.gameObject, color);
            Button button = rect.GetComponent<Button>();
            if (button == null) button = Undo.AddComponent<Button>(rect.gameObject);
            button.targetGraphic = image;
            EnsureText(rect, "Text", label, 18, TextAnchor.MiddleCenter, Color.black);
            return button;
        }

        static Button EnsureSpriteButton(RectTransform rect, string fallbackLabel, Sprite sprite, Color color) {
            Image image = EnsureImage(rect.gameObject, color);
            image.raycastTarget = true;
            image.sprite = sprite;
            image.preserveAspect = sprite != null;

            Button button = rect.GetComponent<Button>();
            if (button == null) button = Undo.AddComponent<Button>(rect.gameObject);
            button.targetGraphic = image;

            Transform text = rect.Find("Text");
            if (sprite != null) {
                if (text != null) text.gameObject.SetActive(false);
            } else {
                Text label = EnsureText(rect, "Text", fallbackLabel, 22, TextAnchor.MiddleCenter, new Color(0.04f, 0.04f, 0.04f, 1f));
                label.gameObject.SetActive(true);
            }

            return button;
        }

        static void ConfigureWorldMapViewport(RectTransform rect) {
            if (rect == null) return;

            Undo.RecordObject(rect, "Configure world map viewport");
            Stretch(rect);
        }

        static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static void SetChildActive(Transform parent, string childName, bool active) {
            Transform child = parent.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        static T FindChildComponent<T>(Transform root, string childName) where T : Component {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
                if (child.name == childName) return child.GetComponent<T>();
            }
            return null;
        }

        static Sprite FindCustomSprite(string spriteName) {
            string path = MapMinimapSpriteFolder + "/" + spriteName + ".png";
            if (!AssetExists(path)) return null;
            EnsureSpriteImportSettings(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture FindCustomIconTexture(string spriteName) {
            Sprite sprite = FindCustomSprite(spriteName);
            return sprite != null ? sprite.texture : null;
        }

        static void EnsureMapMinimapSpriteImports() {
            if (!AssetDatabase.IsValidFolder(MapMinimapSpriteFolder)) return;

            AssetDatabase.Refresh();
            string[] guids = AssetDatabase.FindAssets("Thang_ t:Texture2D", new[] { MapMinimapSpriteFolder });
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EnsureSpriteImportSettings(path);
            }
        }

        static void EnsureSpriteImportSettings(string path) {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            string name = Path.GetFileNameWithoutExtension(path);
            Vector4 border = GetCustomSpriteBorder(name);
            bool changed = false;
            changed |= SetImporterValue(importer.textureType != TextureImporterType.Sprite, () => importer.textureType = TextureImporterType.Sprite);
            changed |= SetImporterValue(importer.spriteImportMode != SpriteImportMode.Single, () => importer.spriteImportMode = SpriteImportMode.Single);
            changed |= SetImporterValue(!importer.alphaIsTransparency, () => importer.alphaIsTransparency = true);
            changed |= SetImporterValue(importer.mipmapEnabled, () => importer.mipmapEnabled = false);
            changed |= SetImporterValue(importer.wrapMode != TextureWrapMode.Clamp, () => importer.wrapMode = TextureWrapMode.Clamp);
            changed |= SetImporterValue(importer.filterMode != FilterMode.Bilinear, () => importer.filterMode = FilterMode.Bilinear);
            changed |= SetImporterValue(importer.textureCompression != TextureImporterCompression.Uncompressed, () => importer.textureCompression = TextureImporterCompression.Uncompressed);
            changed |= SetImporterValue(importer.spritePixelsPerUnit != 100f, () => importer.spritePixelsPerUnit = 100f);
            changed |= SetImporterValue(importer.spriteBorder != border, () => importer.spriteBorder = border);

            if (changed) importer.SaveAndReimport();
        }

        static Vector4 GetCustomSpriteBorder(string name) {
            if (name == "Thang_WorldMap_Frame") return new Vector4(144f, 144f, 144f, 144f);
            if (name == "Thang_Minimap_Frame") return new Vector4(0f, 0f, 0f, 0f);
            if (name.StartsWith("Thang_Button_")) return new Vector4(30f, 30f, 30f, 30f);
            return Vector4.zero;
        }

        static bool SetImporterValue(bool condition, System.Action setter) {
            if (!condition) return false;
            setter();
            return true;
        }

        static bool AssetExists(string projectRelativePath) {
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, projectRelativePath);
            return File.Exists(absolute);
        }

        static Transform FindSelectedOrScenePlayer() {
            if (Selection.activeGameObject != null && LooksLikePlayer(Selection.activeGameObject)) return Selection.activeGameObject.transform;
            return AAMapRuntimeBinder.FindPlayerTarget();
        }

        static bool LooksLikePlayer(GameObject obj) {
            if (obj == null) return false;
            if (obj.CompareTag("Player")) return true;
            foreach (MonoBehaviour behaviour in obj.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour != null && behaviour.GetType().Name == "BasicPlayerMovement") return true;
            }
            return false;
        }

        static Bounds EstimateMapBounds(Transform player) {
            if (TryGetNamedObjectBounds("MapSet", out Bounds mapSetBounds)) {
                float mapSetExpand = Mathf.Max(1f, Mathf.Max(mapSetBounds.size.x, mapSetBounds.size.z) * 0.01f);
                mapSetBounds.Expand(new Vector3(mapSetExpand, 0f, mapSetExpand));
                return mapSetBounds;
            }

            if (TryGetLikelyMapContentBounds(out Bounds likelyMapBounds)) {
                float likelyExpand = Mathf.Max(1f, Mathf.Max(likelyMapBounds.size.x, likelyMapBounds.size.z) * 0.01f);
                likelyMapBounds.Expand(new Vector3(likelyExpand, 0f, likelyExpand));
                return likelyMapBounds;
            }

            bool hasBounds = false;
            Bounds bounds = new Bounds(player != null ? player.position : Vector3.zero, new Vector3(120f, 1f, 120f));

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < terrains.Length; i++) {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                Vector3 size = terrain.terrainData.size;
                Bounds terrainBounds = new Bounds(terrain.transform.position + size * 0.5f, size);
                if (!hasBounds) {
                    bounds = terrainBounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(terrainBounds);
                }
            }

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (!IsMapBoundsCandidate(renderer, player)) continue;

                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds) bounds = new Bounds(player != null ? player.position : Vector3.zero, new Vector3(120f, 1f, 120f));
            float expand = Mathf.Max(10f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.05f);
            bounds.Expand(new Vector3(expand, 0f, expand));
            return bounds;
        }

        static bool TryGetNamedObjectBounds(string objectName, out Bounds bounds) {
            bounds = default;
            if (string.IsNullOrWhiteSpace(objectName)) return false;

            Transform root = null;
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++) {
                if (transforms[i] != null && transforms[i].name == objectName) {
                    root = transforms[i];
                    break;
                }
            }

            if (root == null) return false;

            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.GetComponentInParent<Canvas>() != null) continue;

                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        static bool TryGetLikelyMapContentBounds(out Bounds bounds) {
            bounds = default;
            Transform best = null;
            float bestArea = 0f;

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Transform candidate = GetMapContentRoot(renderer.transform);
                if (candidate == null) continue;

                Bounds rendererBounds = renderer.bounds;
                float area = Mathf.Max(0f, rendererBounds.size.x) * Mathf.Max(0f, rendererBounds.size.z);
                if (area <= bestArea) continue;

                bestArea = area;
                best = candidate;
            }

            return best != null && TryGetTransformBounds(best, out bounds);
        }

        static bool TryGetTransformBounds(Transform root, out Bounds bounds) {
            bounds = default;
            if (root == null) return false;

            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.GetComponentInParent<Canvas>() != null) continue;

                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
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

        static float CalculateFitOrthographicSize(Bounds bounds, float aspect, float padding) {
            float halfHeight = Mathf.Max(1f, bounds.size.z * 0.5f);
            float halfWidth = Mathf.Max(1f, bounds.size.x * 0.5f / Mathf.Max(0.01f, aspect));
            return Mathf.Max(halfHeight, halfWidth) * Mathf.Max(1f, padding);
        }

        static float CalculatePercentOrthographicSize(Vector2 boundsSize, float aspect, float visiblePercent) {
            float referenceLength = Mathf.Max(1f, Mathf.Min(boundsSize.x, boundsSize.y));
            float maxVisibleLongSide = referenceLength * Mathf.Clamp(visiblePercent, 0.01f, 1f);
            float safeAspect = Mathf.Max(0.01f, aspect);
            return safeAspect >= 1f ? maxVisibleLongSide / (2f * safeAspect) : maxVisibleLongSide * 0.5f;
        }

        static bool IsMapBoundsCandidate(Renderer renderer, Transform player) {
            if (renderer == null) return false;

            GameObject obj = renderer.gameObject;
            if (obj.GetComponentInParent<Canvas>() != null) return false;
            if (obj.GetComponentInParent<Camera>() != null) return false;
            if (obj.GetComponentInParent<MapMarker>() != null) return false;
            if (player != null && renderer.transform.IsChildOf(player)) return false;

            string name = obj.name;
            if (name.Contains("Map Icon") || name.Contains("Minimap") || name.Contains("WorldMap") || name.Contains("MapSystem")) return false;
            return renderer.bounds.size.x > 0.01f && renderer.bounds.size.z > 0.01f;
        }

        static void AssignDefaultMarkerIcons(MapMarkerManager markerManager) {
            if (markerManager == null) return;

            SetObjectReference(markerManager, "playerIcon", FindCustomIconTexture("Thang_Icon_Player") ?? FindAAMapIconTexture("Map Icon 1") ?? EnsureGeneratedIcon("MapIcon_Player", GeneratedIconShape.Triangle));
            SetObjectReference(markerManager, "petIcon", FindCustomIconTexture("Thang_Icon_Pet") ?? FindAAMapIconTexture("Map Icon 2") ?? EnsureGeneratedIcon("MapIcon_Pet", GeneratedIconShape.Circle));
            SetObjectReference(markerManager, "enemyIcon", FindCustomIconTexture("Thang_Icon_Enemy") ?? FindAAMapIconTexture("Map Icon 3") ?? EnsureGeneratedIcon("MapIcon_Enemy", GeneratedIconShape.Diamond));
            SetObjectReference(markerManager, "bossIcon", FindCustomIconTexture("Thang_Icon_Boss") ?? FindAAMapIconTexture("Map Icon 4") ?? EnsureGeneratedIcon("MapIcon_Boss", GeneratedIconShape.Ring));
            SetObjectReference(markerManager, "npcIcon", FindCustomIconTexture("Thang_Icon_NPC") ?? FindAAMapIconTexture("Map Icon 5") ?? EnsureGeneratedIcon("MapIcon_NPC", GeneratedIconShape.Square));
            SetObjectReference(markerManager, "questIcon", FindCustomIconTexture("Thang_Icon_Quest") ?? FindAAMapIconTexture("Map Icon 6") ?? EnsureGeneratedIcon("MapIcon_Quest", GeneratedIconShape.Star));
            SetObjectReference(markerManager, "itemIcon", FindCustomIconTexture("Thang_Icon_Item") ?? FindAAMapIconTexture("Map Icon 7") ?? EnsureGeneratedIcon("MapIcon_Item", GeneratedIconShape.Circle));
            SetObjectReference(markerManager, "shopIcon", FindCustomIconTexture("Thang_Icon_Shop") ?? FindAAMapIconTexture("Asset_Icon_1") ?? EnsureGeneratedIcon("MapIcon_Shop", GeneratedIconShape.Square));
            SetObjectReference(markerManager, "fastTravelIcon", FindCustomIconTexture("Thang_Icon_FastTravel") ?? FindAAMapIconTexture("Asset_Icon_2") ?? EnsureGeneratedIcon("MapIcon_FastTravel", GeneratedIconShape.Ring));
            SetObjectReference(markerManager, "coopPlayerIcon", FindCustomIconTexture("Thang_Icon_Coop") ?? FindAAMapIconTexture("Map Icon 1") ?? EnsureGeneratedIcon("MapIcon_CoopPlayer", GeneratedIconShape.Triangle));
        }

        static void ConfigureMarkerPresentation(MapMarkerManager markerManager) {
            if (markerManager == null) return;

            SetBool(markerManager, "useMarkerTextureOverrides", false);
            SetVector3(markerManager, "defaultIconScale", new Vector3(0.28f, 1f, 0.28f));
            SetVector3(markerManager, "playerIconScale", new Vector3(0.32f, 1f, 0.32f));
            SetVector3(markerManager, "bossIconScale", new Vector3(0.42f, 1f, 0.42f));
        }

        static Texture FindAAMapIconTexture(string spriteName) {
            Sprite sprite = FindSprite(spriteName);
            return sprite != null ? sprite.texture : null;
        }

        static Texture2D EnsureGeneratedIcon(string assetName, GeneratedIconShape shape) {
            string assetPath = GeneratedFolder + "/" + assetName + ".asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null) return existing;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = assetName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    bool fill = IsInsideGeneratedIconShape(x, y, size, shape);
                    pixels[y * size + x] = fill ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, assetPath);
            return texture;
        }

        static bool IsInsideGeneratedIconShape(int x, int y, int size, GeneratedIconShape shape) {
            float nx = ((x + 0.5f) / size) * 2f - 1f;
            float ny = ((y + 0.5f) / size) * 2f - 1f;
            float radius = Mathf.Sqrt(nx * nx + ny * ny);

            switch (shape) {
                case GeneratedIconShape.Triangle:
                    return ny > -0.75f && ny < 0.88f && Mathf.Abs(nx) < (ny + 0.9f) * 0.5f;
                case GeneratedIconShape.Diamond:
                    return Mathf.Abs(nx) + Mathf.Abs(ny) <= 0.95f;
                case GeneratedIconShape.Square:
                    return Mathf.Abs(nx) <= 0.68f && Mathf.Abs(ny) <= 0.68f;
                case GeneratedIconShape.Ring:
                    return radius <= 0.82f && radius >= 0.42f;
                case GeneratedIconShape.Star:
                    return Mathf.Abs(nx) + Mathf.Abs(ny) <= 0.78f || (radius <= 0.72f && (Mathf.Abs(nx) <= 0.18f || Mathf.Abs(ny) <= 0.18f));
                default:
                    return radius <= 0.78f;
            }
        }

        static void EnsureFolder(string parent, string child) {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        static T FindFirst<T>() where T : Object {
            T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }

        static void SetObjectReference(Object target, string propertyName, Object value) {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetObjectReferenceIfEmpty(Object target, string propertyName, Object value) {
            if (target == null || value == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string propertyName, bool value) {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string propertyName, float value) {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector2(Object target, string propertyName, Vector2 value) {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector3(Object target, string propertyName, Vector3 value) {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector4(Object target, string propertyName, Vector4 value) {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.vector4Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFilterBindings(WorldMapController controller, List<WorldMapController.MapFilterBinding> filters) {
            if (controller == null || filters == null) return;
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty property = serialized.FindProperty("filterBindings");
            if (property == null) return;
            property.arraySize = filters.Count;
            for (int i = 0; i < filters.Count; i++) {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("toggle").objectReferenceValue = filters[i].toggle;
                item.FindPropertyRelative("markerType").enumValueIndex = (int)filters[i].markerType;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}



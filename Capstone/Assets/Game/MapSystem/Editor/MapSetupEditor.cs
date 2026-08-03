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

            Transform player = FindSelectedOrScenePlayer();
            if (player == null) Debug.LogWarning("Map setup: cannot find Player. Select Player or add BasicPlayerMovement/tag Player, then run setup again.");

            Bounds mapBounds = EstimateMapBounds(player);
            Vector2 mapCenter = new Vector2(mapBounds.center.x, mapBounds.center.z);
            float mapSide = Mathf.Max(60f, mapBounds.size.x, mapBounds.size.z);
            Vector2 mapSize = new Vector2(mapSide, mapSide);
            const float worldMapAspect = 16f / 9f;
            float worldMapDefaultSize = CalculateFitOrthographicSize(mapBounds, worldMapAspect, 1.02f);
            float worldMapMaxSize = Mathf.Max(worldMapDefaultSize * 1.25f, worldMapDefaultSize + 1f);

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
            ConfigureWorldMap(worldMapObject, mapManager, mapCameraObject, worldMapTexture, minimapObject, mapCenter, worldMapDefaultSize, worldMapMaxSize);
            ConfigureCamera(minimapCamera, player != null ? player.position : Vector3.zero, 60f, 28f, minimapTexture);
            ConfigureCamera(mapCamera, new Vector3(mapCenter.x, 0f, mapCenter.y), 180f, worldMapDefaultSize, worldMapTexture);

            RectTransform mapRect = FindChildComponent<RectTransform>(worldMapObject.transform, "Map Mask");
            if (mapRect == null) mapRect = worldMapObject.GetComponent<RectTransform>();
            RectTransform overlay = EnsureWorldMapOverlay(worldMapObject.transform, out Text regionLabel, out Button clearWaypoint, out List<WorldMapController.MapFilterBinding> filters);
            Button closeButton = FindChildComponent<Button>(worldMapObject.transform, "Map Exit Button");
            Button zoomInButton = FindChildComponent<Button>(worldMapObject.transform, "Zoom In Button");
            Button zoomOutButton = FindChildComponent<Button>(worldMapObject.transform, "Zoom Out Button");

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
            Texture2D questIcon = EnsureGeneratedIcon("MapIcon_Quest", GeneratedIconShape.Star);

            SetObjectReference(binder, "minimapManager", minimapManager);
            SetObjectReference(binder, "mapManager", mapManager);
            SetObjectReference(binder, "minimapCamera", minimapCameraObject);
            SetObjectReference(binder, "mapCamera", mapCameraObject);
            SetObjectReference(binder, "playerTarget", player);

            SetObjectReference(minimapController, "minimapManager", minimapManager);
            SetObjectReference(minimapController, "minimapCamera", minimapCamera);
            SetObjectReference(minimapController, "target", player);
            SetBool(minimapController, "clampToWorldBounds", true);
            SetVector2(minimapController, "worldCenter", mapCenter);
            SetVector2(minimapController, "worldSize", mapSize);

            SetObjectReference(worldMapController, "mapManager", mapManager);
            SetObjectReference(worldMapController, "mapCamera", mapCamera);
            SetObjectReference(worldMapController, "minimapRoot", minimapObject);
            SetObjectReference(worldMapController, "mapInteractionRect", mapRect);
            SetObjectReference(worldMapController, "overlayRoot", overlay);
            SetObjectReference(worldMapController, "regionNameText", regionLabel);
            SetObjectReference(worldMapController, "closeButton", closeButton);
            SetObjectReference(worldMapController, "zoomInButton", zoomInButton);
            SetObjectReference(worldMapController, "zoomOutButton", zoomOutButton);
            SetObjectReference(worldMapController, "clearWaypointButton", clearWaypoint);
            SetObjectReference(worldMapController, "iconRegistry", iconRegistry);
            SetObjectReference(worldMapController, "playerTarget", player);
            SetBool(worldMapController, "clampToWorldBounds", true);
            SetVector2(worldMapController, "worldCenter", mapCenter);
            SetVector2(worldMapController, "worldSize", mapSize);
            SetFloat(worldMapController, "defaultOrthographicSize", worldMapDefaultSize);
            SetFloat(worldMapController, "maxOrthographicSize", worldMapMaxSize);
            SetFilterBindings(worldMapController, filters);

            SetObjectReference(markerManager, "mapBinder", binder);
            SetObjectReference(markerManager, "mapIconPrefab", mapIconPrefab);
            AssignDefaultMarkerIcons(markerManager);
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
            SetObjectReferenceIfEmpty(questBridge, "trackedQuestIcon", questIcon);
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
            Debug.Log(ok ? "Map setup finished. Press M in Play Mode to test World Map." : "Map setup finished with warnings. Use Tools > ToolCuaThang > Game Map > Validate Setup.");

            if (EditorUtility.DisplayDialog("Map setup complete", "Minimap/World Map setup finished. Save the open scene now?", "Save Scene", "Not Now")) {
                EditorSceneManager.SaveOpenScenes();
            }
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
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-28f, -28f);
                rect.sizeDelta = new Vector2(230f, 230f);
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
                manager.haveBorder = true;
                manager.haveZoomButtons = true;
                manager.minimapHeight = 60f;
                manager.minimapRange = 28f;
                manager.renderTexture = texture;
                Sprite circle = FindSprite("Minimap Shape Circle");
                if (circle != null) manager.minimapShape = circle;
                Sprite border = FindSprite("Minimap Circle Border 6") ?? FindSprite("Minimap Circle Border 1");
                if (border != null) manager.borderSprite = border;
            }

            RectTransform mask = EnsureMaskChild(obj.transform, "Minimap Mask", manager != null ? manager.minimapShape : null);
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
        }

        static void ConfigureWorldMap(GameObject obj, MapManager manager, GameObject cameraObject, RenderTexture texture, GameObject minimapObject, Vector2 mapCenter, float defaultSize, float maxSize) {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null) {
                Undo.RecordObject(rect, "Configure world map rect");
                Stretch(rect);
            }

            if (manager != null) {
                Undo.RecordObject(manager, "Configure world map manager");
                manager.mapEnabled = false;
                manager.enablingShortcut = KeyCode.None;
                manager.disablingShortcut = KeyCode.None;
                manager.SetMapCamera(cameraObject);
                manager.cameraPosition = new Vector3(mapCenter.x, 180f, mapCenter.y);
                manager.cameraOrthographicSize = defaultSize;
                manager.minimumRange = Mathf.Min(70f, defaultSize);
                manager.maximumRange = Mathf.Max(maxSize, defaultSize);
                manager.haveZoomButtons = false;
                manager.haveExitButton = false;
                manager.clearFlags = MapClearFlags.SolidColor;
                manager.cameraBGColor = new Color(0.02f, 0.04f, 0.05f, 1f);
                manager.haveBackgroundImage = false;
                manager.disableMinimap = false;
                manager.minimapGameObject = minimapObject;
                manager.minimapManager = minimapObject != null ? minimapObject.GetComponent<MinimapManager>() : null;
                manager.renderTexture = texture;
                Sprite shape = FindSprite("Map Shape Square") ?? FindSprite("Map Shape Rounded Square");
                if (shape != null) manager.mapShape = shape;
                manager.mapColor = Color.white;
                manager.mapOpacity = 1f;
            }

            RectTransform mask = EnsureMaskChild(obj.transform, "Map Mask", manager != null ? manager.mapShape : null);
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
                Stretch(display.GetComponent<RectTransform>());
                display.transform.SetSiblingIndex(1);
            }

            SetChildActive(obj.transform, "Map Mask", true);
            SetChildActive(obj.transform, "Map Border", true);
            SetChildActive(obj.transform, "Map Directions", false);
            SetChildActive(obj.transform, "Map Zoom Buttons", false);
            SetChildActive(obj.transform, "Map Exit Button", false);
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

        static RectTransform EnsureWorldMapOverlay(Transform worldMap, out Text regionLabel, out Button clearWaypointButton, out List<WorldMapController.MapFilterBinding> filters) {
            filters = new List<WorldMapController.MapFilterBinding>();
            RectTransform overlay = EnsureUIChild(worldMap, "World Map Overlay");
            Stretch(overlay);
            overlay.gameObject.SetActive(false);

            RectTransform region = EnsureUIChild(overlay, "Region Name");
            region.anchorMin = new Vector2(0.5f, 1f);
            region.anchorMax = new Vector2(0.5f, 1f);
            region.pivot = new Vector2(0.5f, 1f);
            region.anchoredPosition = new Vector2(0f, -32f);
            region.sizeDelta = new Vector2(420f, 44f);
            Image regionBg = EnsureImage(region.gameObject, new Color(0.05f, 0.14f, 0.13f, 0.7f));
            regionBg.raycastTarget = false;
            regionLabel = EnsureText(region, "Region Text", "Region", 24, TextAnchor.MiddleCenter, new Color(0.92f, 0.86f, 0.68f, 1f));

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
            clearWaypointButton = EnsureButton(clear, "Clear Waypoint", new Color(0.42f, 0.24f, 0.18f, 0.9f));
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

            Text text = EnsureText(root, "Label", label, 15, TextAnchor.MiddleLeft, new Color(0.92f, 0.86f, 0.68f, 1f));
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

            Image image = EnsureImage(rect.gameObject, Color.clear);
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

        static RectTransform EnsureOpaqueImageChild(Transform parent, string name, Color color) {
            RectTransform rect = EnsureUIChild(parent, name);
            Image image = EnsureImage(rect.gameObject, color);
            image.raycastTarget = false;
            Stretch(rect);
            return rect;
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
            EnsureText(rect, "Text", label, 18, TextAnchor.MiddleCenter, new Color(0.95f, 0.89f, 0.72f, 1f));
            return button;
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

            SetObjectReferenceIfEmpty(markerManager, "playerIcon", EnsureGeneratedIcon("MapIcon_Player", GeneratedIconShape.Triangle));
            SetObjectReferenceIfEmpty(markerManager, "petIcon", EnsureGeneratedIcon("MapIcon_Pet", GeneratedIconShape.Circle));
            SetObjectReferenceIfEmpty(markerManager, "enemyIcon", EnsureGeneratedIcon("MapIcon_Enemy", GeneratedIconShape.Diamond));
            SetObjectReferenceIfEmpty(markerManager, "bossIcon", EnsureGeneratedIcon("MapIcon_Boss", GeneratedIconShape.Ring));
            SetObjectReferenceIfEmpty(markerManager, "npcIcon", EnsureGeneratedIcon("MapIcon_NPC", GeneratedIconShape.Square));
            SetObjectReferenceIfEmpty(markerManager, "questIcon", EnsureGeneratedIcon("MapIcon_Quest", GeneratedIconShape.Star));
            SetObjectReferenceIfEmpty(markerManager, "itemIcon", EnsureGeneratedIcon("MapIcon_Item", GeneratedIconShape.Circle));
            SetObjectReferenceIfEmpty(markerManager, "shopIcon", EnsureGeneratedIcon("MapIcon_Shop", GeneratedIconShape.Square));
            SetObjectReferenceIfEmpty(markerManager, "fastTravelIcon", EnsureGeneratedIcon("MapIcon_FastTravel", GeneratedIconShape.Ring));
            SetObjectReferenceIfEmpty(markerManager, "coopPlayerIcon", EnsureGeneratedIcon("MapIcon_CoopPlayer", GeneratedIconShape.Triangle));
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

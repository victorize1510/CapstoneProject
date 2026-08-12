using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace Capstone.Game.UISystem.Editor {
    public static class GameMenuSetupEditor {
        const string MenuPath = "Game Tools/GameToolThang/HUD/Create Main Menu";
        const string MenuObjectName = "GameMenu";
        const string CanvasObjectName = "GameplayHUDCanvas";

        [MenuItem(MenuPath)]
        public static void CreateMainMenu() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogWarning("Main Menu setup: stop Play Mode before running setup. Scene edits made while playing are temporary.");
                return;
            }

            Undo.SetCurrentGroupName("Create Main Menu");
            int undoGroup = Undo.GetCurrentGroup();

            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            GameObject menuObject = EnsureMenuObject();
            GameMenuController menu = GetOrAdd<GameMenuController>(menuObject);
            LocalPlayerControlLock controlLock = FindFirst<LocalPlayerControlLock>() ?? GetOrAdd<LocalPlayerControlLock>(menuObject);
            MonsterInventoryController inventory = FindFirst<MonsterInventoryController>();
            InventoryInputController inventoryInput = FindFirst<InventoryInputController>();
            MapInputController mapInput = FindFirst<MapInputController>();

            AssignObject(menu, "targetCanvas", canvas);
            AssignObject(menu, "inventory", inventory);
            AssignObject(menu, "inventoryInput", inventoryInput);
            AssignObject(menu, "mapInput", mapInput);
            AssignObject(menu, "controlLock", controlLock);
            AssignBool(menu, "buildOnAwake", true);
            AssignBool(menu, "closeOnStart", true);
            AssignBool(menu, "disableStandaloneUiHotkeys", true);

            ConfigureInventoryInput(inventoryInput);
            ConfigureMapInput(mapInput);

            menu.RebuildMenu();

            EditorUtility.SetDirty(menuObject);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(menuObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = menuObject;

            Debug.Log("Main Menu setup: created/updated GameMenu. TAB opens the main menu, I opens Inventory, M opens Map, Escape closes the active UI.");
        }

        static GameObject EnsureMenuObject() {
            GameObject existing = FindSceneObject(MenuObjectName);
            if (existing != null) {
                Undo.RecordObject(existing, "Update GameMenu");
                return existing;
            }

            GameObject created = new GameObject(MenuObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create GameMenu");
            return created;
        }

        static Canvas EnsureCanvas() {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas existing in canvases) {
                if (existing.renderMode == RenderMode.ScreenSpaceOverlay) {
                    ConfigureCanvas(existing);
                    return existing;
                }
            }

            GameObject obj = new GameObject(CanvasObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(obj, "Create Main Menu Canvas");
            Canvas canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvas(canvas);
            return canvas;
        }

        static void ConfigureCanvas(Canvas canvas) {
            Undo.RecordObject(canvas, "Configure Main Menu Canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            Undo.RecordObject(scaler, "Configure Main Menu Canvas Scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
        }

        static void EnsureEventSystem() {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
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

        static void ConfigureInventoryInput(InventoryInputController input) {
            if (input == null) {
                Debug.LogWarning("Main Menu setup: InventoryInputController was not found. The Inventory button will be disabled until InventoryUI exists.");
                return;
            }

            AssignBool(input, "enableOpenCloseHotkeys", false);
            AssignBool(input, "allowSecondaryToggleKey", false);
        }

        static void ConfigureMapInput(MapInputController input) {
            if (input == null) {
                Debug.LogWarning("Main Menu setup: MapInputController was not found. The Map button will be disabled until MapSystem exists.");
                return;
            }

            AssignBool(input, "enableOpenCloseHotkeys", false);
        }

        static GameObject FindSceneObject(string objectName) {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject obj in objects) {
                if (obj != null && obj.name == objectName) return obj;
            }

            return null;
        }

        static T FindFirst<T>() where T : Object {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return objects.Length > 0 ? objects[0] : null;
        }

        static T GetOrAdd<T>(GameObject obj) where T : Component {
            T component = obj.GetComponent<T>();
            if (component != null) {
                Undo.RecordObject(component, "Update " + typeof(T).Name);
                return component;
            }

            return Undo.AddComponent<T>(obj);
        }

        static void AssignObject(Object target, string propertyName, Object value) {
            AssignProperty(target, propertyName, property => property.objectReferenceValue = value);
        }

        static void AssignBool(Object target, string propertyName, bool value) {
            AssignProperty(target, propertyName, property => property.boolValue = value);
        }

        static void AssignProperty(Object target, string propertyName, System.Action<SerializedProperty> setter) {
            if (target == null || setter == null) return;

            Undo.RecordObject(target, "Assign " + propertyName);
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) {
                Debug.LogWarning($"Main Menu setup: serialized field '{propertyName}' was not found on {target.GetType().Name}.");
                return;
            }

            setter(property);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}

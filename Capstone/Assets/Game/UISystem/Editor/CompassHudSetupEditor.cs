using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace Capstone.Game.UISystem.Editor {
    public static class CompassHudSetupEditor {
        const string MenuPath = "Game Tools/GameToolThang/HUD/Create Compass HUD";
        const string ObjectName = "CompassHUD";

        [MenuItem(MenuPath)]
        public static void CreateCompassHud() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogWarning("Compass HUD setup: stop Play Mode before running setup. Scene edits made while playing are temporary.");
                return;
            }

            Undo.SetCurrentGroupName("Create Compass HUD");
            int group = Undo.GetCurrentGroup();

            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            GameObject hudObject = EnsureHudObject();
            CompassHudController compass = GetOrAdd<CompassHudController>(hudObject);

            SerializedObject serialized = new SerializedObject(compass);
            SetObject(serialized, "targetCanvas", canvas);
            SetObject(serialized, "targetCamera", Camera.main);
            SetObject(serialized, "viewer", FindPlayer());
            SetBool(serialized, "buildOnAwake", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            compass.RebuildCompass();

            EditorUtility.SetDirty(hudObject);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(hudObject.scene);
            Undo.CollapseUndoOperations(group);

            Debug.Log("Compass HUD setup: created/updated CompassHUD. It reads Camera.main and MapMarker data at runtime.");
        }

        static GameObject EnsureHudObject() {
            GameObject existing = GameObject.Find(ObjectName);
            if (existing != null) {
                Undo.RecordObject(existing, "Update CompassHUD");
                return existing;
            }

            GameObject created = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create CompassHUD");
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

            GameObject obj = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(obj, "Create Compass HUD Canvas");
            Canvas canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvas(canvas);
            return canvas;
        }

        static void ConfigureCanvas(Canvas canvas) {
            Undo.RecordObject(canvas, "Configure Compass HUD Canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            Undo.RecordObject(scaler, "Configure Compass HUD Canvas Scaler");
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

        static Transform FindPlayer() {
            try {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) return tagged.transform;
            } catch (UnityException) {
            }

            GameObject named = GameObject.Find("Player");
            return named != null ? named.transform : null;
        }

        static T GetOrAdd<T>(GameObject obj) where T : Component {
            T component = obj.GetComponent<T>();
            if (component != null) return component;
            return Undo.AddComponent<T>(obj);
        }

        static void SetObject(SerializedObject serialized, string propertyName, Object value) {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        static void SetBool(SerializedObject serialized, string propertyName, bool value) {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }
    }
}

using Capstone.Game.QuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace Capstone.Game.HudSystem.Editor {
    public static class GameplayHudSetupEditor {
        const string MenuPath = "Game Tools/GameToolThang/HUD/Create Gameplay HUD";
        const string HudObjectName = "GameplayHUD";
        const string LegacyQuestTrackerObjectName = "QuestTrackerHUD";

        [MenuItem(MenuPath)]
        public static void CreateGameplayHud() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogWarning("Gameplay HUD setup: stop Play Mode before running setup. Scene edits made while playing are temporary.");
                return;
            }

            Undo.SetCurrentGroupName("Create Gameplay HUD");
            int group = Undo.GetCurrentGroup();

            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            GameObject hudObject = EnsureHudObject();
            GameplayHudController controller = GetOrAdd<GameplayHudController>(hudObject);
            PetCommandHudProvider provider = GetOrAdd<PetCommandHudProvider>(hudObject);

            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "targetCanvas", canvas);
            SetObject(serialized, "minimapPanel", FindMinimapPanel());
            SetObject(serialized, "petHudProvider", provider);
            SetObject(serialized, "questManager", Object.FindFirstObjectByType<QuestManager>());
            SetObject(serialized, "localPlayer", FindPlayer());
            SetBool(serialized, "buildOnAwake", true);
            SetBool(serialized, "autoFindReferences", true);
            SetBool(serialized, "positionExistingMinimap", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            controller.RebuildHud();
            DisableLegacyQuestTrackerHud();

            EditorUtility.SetDirty(hudObject);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(hudObject.scene);
            Undo.CollapseUndoOperations(group);

            Debug.Log("Gameplay HUD setup: created/updated GameplayHUD. Press Play to test minimap, quest tracker, pet status, pet slots and Z/X/C/V skill bar.");
        }

        static GameObject EnsureHudObject() {
            GameObject existing = GameObject.Find(HudObjectName);
            if (existing != null) {
                Undo.RecordObject(existing, "Update GameplayHUD");
                return existing;
            }

            GameObject created = new GameObject(HudObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create GameplayHUD");
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
            Undo.RegisterCreatedObjectUndo(obj, "Create Gameplay HUD Canvas");
            Canvas canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvas(canvas);
            return canvas;
        }

        static void ConfigureCanvas(Canvas canvas) {
            Undo.RecordObject(canvas, "Configure Gameplay HUD Canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            Undo.RecordObject(scaler, "Configure Gameplay HUD Canvas Scaler");
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

        static RectTransform FindMinimapPanel() {
            GameObject minimap = GameObject.Find("MinimapPanel");
            return minimap != null ? minimap.GetComponent<RectTransform>() : null;
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

        static void DisableLegacyQuestTrackerHud() {
            GameObject legacy = GameObject.Find(LegacyQuestTrackerObjectName);
            if (legacy == null) return;

            Undo.RecordObject(legacy, "Disable old QuestTrackerHUD");
            legacy.SetActive(false);
            EditorUtility.SetDirty(legacy);
            Debug.Log("Gameplay HUD setup: disabled old QuestTrackerHUD to avoid duplicate quest tracker UI.");
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

using Capstone.Game.QuestSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Capstone.Game.QuestSystem.Editor {
    public static class QuestTrackerHudSetupTool {
        const string MenuPath = "Game Tools/Quest/Create Quest Tracker HUD";
        const string HudObjectName = "QuestTrackerHUD";
        const string UiFolder = "Assets/Game/QuestSystem/UI";
        const string HudUxmlPath = UiFolder + "/QuestTrackerHud.uxml";
        const string PanelSettingsPath = UiFolder + "/QuestTrackerHudPanelSettings.asset";

        [MenuItem(MenuPath)]
        public static void CreateQuestTrackerHud() {
            Undo.SetCurrentGroupName("Create Quest Tracker HUD");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject hudObject = FindOrCreateHudObject();
            UIDocument document = GetOrAddComponent<UIDocument>(hudObject);
            QuestTrackerHudController controller = GetOrAddComponent<QuestTrackerHudController>(hudObject);

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            if (uxml == null) {
                Debug.LogError("QuestTrackerHudSetupTool: Khong tim thay QuestTrackerHud.uxml.");
            } else {
                Undo.RecordObject(document, "Assign quest tracker UXML");
                document.visualTreeAsset = uxml;
                EditorUtility.SetDirty(document);
            }

            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null) panelSettings = CreatePanelSettings();
            if (panelSettings != null) {
                Undo.RecordObject(document, "Assign quest tracker panel settings");
                document.panelSettings = panelSettings;
                EditorUtility.SetDirty(document);
            }

            AssignSerializedReference(controller, "document", document);

            QuestManager questManager = Object.FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include);
            if (questManager != null) {
                AssignSerializedReference(controller, "questManager", questManager);
            } else {
                Debug.LogWarning("QuestTrackerHudSetupTool: Chua tim thay QuestManager trong scene. HUD se tu tim khi Play, hoac ban keo reference thu cong.");
            }

            Transform localPlayer = FindPlayerTransform();
            if (localPlayer != null) {
                AssignSerializedReference(controller, "localPlayer", localPlayer);
            } else {
                Debug.LogWarning("QuestTrackerHudSetupTool: Chua tim thay Player. Distance se an cho den khi co local player/reference.");
            }

            Selection.activeGameObject = hudObject;
            MarkSceneDirty(hudObject);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("QuestTrackerHudSetupTool: Da tao/cap nhat QuestTrackerHUD.");
        }

        static GameObject FindOrCreateHudObject() {
            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject sceneObject in sceneObjects) {
                if (sceneObject != null && sceneObject.name == HudObjectName) {
                    Undo.RecordObject(sceneObject, "Update QuestTrackerHUD");
                    return sceneObject;
                }
            }

            var created = new GameObject(HudObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create QuestTrackerHUD");
            return created;
        }

        static T GetOrAddComponent<T>(GameObject target) where T : Component {
            T component = target.GetComponent<T>();
            if (component != null) {
                Undo.RecordObject(component, "Update " + typeof(T).Name);
                return component;
            }

            return Undo.AddComponent<T>(target);
        }

        static PanelSettings CreatePanelSettings() {
            EnsureFolder(UiFolder);

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            ConfigurePanelSettings(panelSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return panelSettings;
        }

        static void ConfigurePanelSettings(PanelSettings panelSettings) {
            if (panelSettings == null) return;

            var serializedPanel = new SerializedObject(panelSettings);
            TrySetEnum(serializedPanel, "m_ScaleMode", 1);
            TrySetVector2(serializedPanel, "m_ReferenceResolution", new Vector2(1920f, 1080f));
            TrySetEnum(serializedPanel, "m_ScreenMatchMode", 0);
            TrySetFloat(serializedPanel, "m_Match", 0.5f);
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panelSettings);
        }

        static void AssignSerializedReference(Object target, string propertyName, Object value) {
            if (target == null || value == null) return;

            Undo.RecordObject(target, "Assign " + propertyName);
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null) {
                Debug.LogWarning($"QuestTrackerHudSetupTool: Khong tim thay field '{propertyName}' tren {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        static Transform FindPlayerTransform() {
            try {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null) return taggedPlayer.transform;
            } catch (UnityException) {
            }

            GameObject namedPlayer = GameObject.Find("Player");
            return namedPlayer != null ? namedPlayer.transform : null;
        }

        static void EnsureFolder(string unityFolderPath) {
            string[] parts = unityFolderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") return;

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++) {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        static void MarkSceneDirty(GameObject sceneObject) {
            if (sceneObject == null) return;

            Scene scene = sceneObject.scene;
            if (scene.IsValid()) {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        static void TrySetEnum(SerializedObject serializedObject, string propertyName, int enumValueIndex) {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.enumValueIndex = enumValueIndex;
        }

        static void TrySetFloat(SerializedObject serializedObject, string propertyName, float value) {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
        }

        static void TrySetVector2(SerializedObject serializedObject, string propertyName, Vector2 value) {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.vector2Value = value;
        }
    }
}

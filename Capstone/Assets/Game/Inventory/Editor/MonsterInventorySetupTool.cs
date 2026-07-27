using Capstone.Game.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Editor {
    public static class MonsterInventorySetupTool {
        const string MenuPath = "Game Tools/Inventory/Create Monster Inventory UI";
        const string InventoryObjectName = "InventoryUI";
        const string CreatedUiFolder = "Assets/Game/Inventory/UI";
        const string CreatedPanelSettingsPath = CreatedUiFolder + "/MonsterInventoryPanelSettings.asset";

        static readonly string[] MonsterInventoryUxmlPaths = {
            "Assets/Game/Inventory/UI/MonsterInventory.uxml",
            "Assets/ThangTest/Inventory/UI/MonsterInventory.uxml"
        };

        static readonly string[] MonsterItemRowUxmlPaths = {
            "Assets/Game/Inventory/UI/MonsterItemRow.uxml",
            "Assets/ThangTest/Inventory/UI/MonsterItemRow.uxml"
        };

        static readonly string[] PanelSettingsPaths = {
            "Assets/Game/Inventory/UI/MonsterInventoryPanelSettings.asset",
            "Assets/ThangTest/Inventory/UI/MonsterInventoryPanelSettings.asset"
        };

        [MenuItem(MenuPath)]
        public static void CreateMonsterInventoryUi() {
            GameObject inventoryObject = FindOrCreateInventoryObject();
            Undo.SetCurrentGroupName("Create Monster Inventory UI");
            int undoGroup = Undo.GetCurrentGroup();

            UIDocument document = GetOrAddComponent<UIDocument>(inventoryObject);
            MonsterInventoryController controller = GetOrAddComponent<MonsterInventoryController>(inventoryObject);
            InventoryInputController inputController = GetOrAddComponent<InventoryInputController>(inventoryObject);

            VisualTreeAsset inventoryUxml = FindAssetByPaths<VisualTreeAsset>(MonsterInventoryUxmlPaths);
            if (inventoryUxml == null) {
                Debug.LogError("MonsterInventorySetupTool: Khong tim thay MonsterInventory.uxml. Hay kiem tra Assets/Game/Inventory/UI hoac Assets/ThangTest/Inventory/UI.");
            } else {
                Undo.RecordObject(document, "Assign inventory UXML");
                document.visualTreeAsset = inventoryUxml;
                EditorUtility.SetDirty(document);
            }

            PanelSettings panelSettings = FindAssetByPaths<PanelSettings>(PanelSettingsPaths);
            if (panelSettings == null) {
                panelSettings = CreatePanelSettings();
            }

            if (panelSettings != null) {
                Undo.RecordObject(document, "Assign inventory panel settings");
                document.panelSettings = panelSettings;
                EditorUtility.SetDirty(document);
            }

            VisualTreeAsset rowTemplate = FindAssetByPaths<VisualTreeAsset>(MonsterItemRowUxmlPaths);
            AssignSerializedReference(controller, "document", document);
            AssignSerializedReference(controller, "rowTemplate", rowTemplate);

            MonsterInventoryAdapter adapter = Object.FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);
            if (adapter != null) {
                AssignSerializedReference(controller, "adapter", adapter);
            } else {
                Debug.LogWarning("MonsterInventorySetupTool: Chua tim thay MonsterInventoryAdapter trong scene. Tool khong gan adapter gia; hay tao/gan MonsterInventoryAdapter roi chay lai tool hoac keo reference thu cong.");
            }

            AssignSerializedReference(inputController, "inventory", controller);
            AssignSerializedReference(inputController, "document", document);

            Selection.activeGameObject = inventoryObject;
            MarkSceneDirty(inventoryObject);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("MonsterInventorySetupTool: Da tao/cap nhat InventoryUI. Neu Console co canh bao adapter, hay them MonsterInventoryAdapter vao scene roi chay lai tool.");
        }

        static GameObject FindOrCreateInventoryObject() {
            GameObject existing = FindInventoryObject();
            if (existing != null) {
                Undo.RecordObject(existing, "Update InventoryUI");
                return existing;
            }

            var created = new GameObject(InventoryObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create InventoryUI");
            return created;
        }

        static GameObject FindInventoryObject() {
            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject sceneObject in sceneObjects) {
                if (sceneObject != null && sceneObject.name == InventoryObjectName) {
                    return sceneObject;
                }
            }

            return null;
        }

        static T GetOrAddComponent<T>(GameObject target) where T : Component {
            T component = target.GetComponent<T>();
            if (component != null) {
                Undo.RecordObject(component, "Update " + typeof(T).Name);
                return component;
            }

            return Undo.AddComponent<T>(target);
        }

        static T FindAssetByPaths<T>(string[] paths) where T : Object {
            foreach (string path in paths) {
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) {
                    return asset;
                }
            }

            return null;
        }

        static PanelSettings CreatePanelSettings() {
            EnsureFolder(CreatedUiFolder);

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panelSettings, CreatedPanelSettingsPath);
            ConfigurePanelSettings(panelSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return panelSettings;
        }

        static void ConfigurePanelSettings(PanelSettings panelSettings) {
            if (panelSettings == null) {
                return;
            }

            var serializedPanel = new SerializedObject(panelSettings);
            TrySetEnum(serializedPanel, "m_ScaleMode", 1);
            TrySetVector2(serializedPanel, "m_ReferenceResolution", new Vector2(1920f, 1080f));
            TrySetEnum(serializedPanel, "m_ScreenMatchMode", 0);
            TrySetFloat(serializedPanel, "m_Match", 0.5f);
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panelSettings);
        }

        static void AssignSerializedReference(Object target, string propertyName, Object value) {
            if (target == null || value == null) {
                return;
            }

            Undo.RecordObject(target, "Assign " + propertyName);
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null) {
                Debug.LogWarning($"MonsterInventorySetupTool: Khong tim thay field serialized '{propertyName}' tren {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        static void EnsureFolder(string unityFolderPath) {
            string[] parts = unityFolderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") {
                return;
            }

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
            if (sceneObject == null) {
                return;
            }

            Scene scene = sceneObject.scene;
            if (scene.IsValid()) {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        static void TrySetEnum(SerializedObject serializedObject, string propertyName, int enumValueIndex) {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) {
                property.enumValueIndex = enumValueIndex;
            }
        }

        static void TrySetFloat(SerializedObject serializedObject, string propertyName, float value) {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) {
                property.floatValue = value;
            }
        }

        static void TrySetVector2(SerializedObject serializedObject, string propertyName, Vector2 value) {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) {
                property.vector2Value = value;
            }
        }
    }
}

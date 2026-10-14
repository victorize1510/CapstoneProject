using Capstone.Game.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Editor {
    public static class InventoryUiVisibilityTool {
        const string TogglePath = "Tools/ToolCuaThang/UI/Toggle Inventory UI In Edit Scene";
        const string HidePath = "Tools/ToolCuaThang/UI/Hide Inventory UI In Edit Scene";
        const string ShowPath = "Tools/ToolCuaThang/UI/Show Inventory UI In Edit Scene";

        [MenuItem(TogglePath)]
        public static void ToggleInventoryUi() {
            InventoryEditSceneVisibility visibility = EnsureVisibilityComponent();
            if (visibility == null) return;
            SetHidden(visibility, !visibility.HideInEditMode);
        }

        [MenuItem(HidePath)]
        public static void HideInventoryUi() {
            InventoryEditSceneVisibility visibility = EnsureVisibilityComponent();
            if (visibility == null) return;
            SetHidden(visibility, true);
        }

        [MenuItem(ShowPath)]
        public static void ShowInventoryUi() {
            InventoryEditSceneVisibility visibility = EnsureVisibilityComponent();
            if (visibility == null) return;
            SetHidden(visibility, false);
        }

        static void SetHidden(InventoryEditSceneVisibility visibility, bool hidden) {
            Undo.RecordObject(visibility, hidden ? "Hide Inventory UI in edit scene" : "Show Inventory UI in edit scene");
            visibility.SetHideInEditMode(hidden);
            EditorUtility.SetDirty(visibility);
            EditorSceneManager.MarkSceneDirty(visibility.gameObject.scene);
            Debug.Log(hidden ? "Inventory UI hidden in Edit Scene." : "Inventory UI visible in Edit Scene.");
        }

        static InventoryEditSceneVisibility EnsureVisibilityComponent() {
            GameObject inventory = FindInventoryUiObject();
            if (inventory == null) {
                Debug.LogWarning("Inventory UI visibility: cannot find InventoryUI or MonsterInventoryController in this scene.");
                return null;
            }

            InventoryEditSceneVisibility visibility = inventory.GetComponent<InventoryEditSceneVisibility>();
            if (visibility == null) visibility = Undo.AddComponent<InventoryEditSceneVisibility>(inventory);
            return visibility;
        }

        static GameObject FindInventoryUiObject() {
            MonsterInventoryController controller = Object.FindFirstObjectByType<MonsterInventoryController>(FindObjectsInactive.Include);
            if (controller != null) return controller.gameObject;

            UIDocument[] documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < documents.Length; i++) {
                UIDocument document = documents[i];
                if (document == null) continue;
                if (document.gameObject.name == "InventoryUI") return document.gameObject;
            }

            return GameObject.Find("InventoryUI");
        }
    }
}

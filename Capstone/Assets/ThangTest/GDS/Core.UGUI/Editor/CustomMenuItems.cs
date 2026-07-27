using UnityEditor;
using UnityEngine;

namespace GDS.Core.UGUI {

    public class CustomMenuItems {

        // Note: these need to be const and not static to be accessible in the attribute
        const string menuPath = "GameObject/GDS";
        const string coreUguiPath = "Assets/GDS/Core.UGUI";
        const string viewsPath = coreUguiPath + "/Views";
        const string behaviorsPath = coreUguiPath + "/Systems";
        const string commonPrefabPath = "Assets/GDS/Common/Prefabs";

        public static void CreatePrefab(MenuCommand command, string path, string name) => CreatePrefab((GameObject)command.context, path, name);
        public static void CreatePrefab(GameObject parent, string path, string name) {
            var fullPath = $"{path}/{name}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (prefab == null) { Debug.LogError($"<color=red>{name} not found</color> at the specified path ({fullPath})"); return; }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create Prefab: " + name);
            GameObjectUtility.SetParentAndAlign(instance, parent);
            Selection.activeGameObject = instance;
        }

        [MenuItem(menuPath + "/ListBag", true)]
        [MenuItem(menuPath + "/SetBag", true)]
        [MenuItem(menuPath + "/Slot", true)]
        public static bool InsideCanvas() => Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<Canvas>() == true;

        [MenuItem(menuPath + "/ListBag", false, -1000)]
        public static void CreateListBag(MenuCommand command) => CreatePrefab(command, viewsPath, "ListBag");

        [MenuItem(menuPath + "/SetBag", false, -1000)]
        public static void CreateSetBag(MenuCommand command) => CreatePrefab(command, viewsPath, "SetBag");

        [MenuItem(menuPath + "/Slot", false, -1000)]
        public static void CreateSlot(MenuCommand command) => CreatePrefab(command, viewsPath, "Slot");

        [MenuItem(menuPath + "/DragAndDropSystem", false, 100)]
        public static void DragAndDropSystem(MenuCommand command) => CreatePrefab(command, behaviorsPath, "DragAndDropSystem");

        [MenuItem(menuPath + "/TooltipSystem", false, 100)]
        public static void TooltipSystem(MenuCommand command) => CreatePrefab(command, behaviorsPath, "TooltipSystem");

        [MenuItem(menuPath + "/DefaultStore", false, 200)]
        public static void DefaultStore(MenuCommand command) => CreatePrefab(command, commonPrefabPath, "DefaultStore");

        [MenuItem(menuPath + "/DefaultSfx", false, 200)]
        public static void DefaultSfx(MenuCommand command) => CreatePrefab(command, commonPrefabPath, "DefaultSfx");

    }
}
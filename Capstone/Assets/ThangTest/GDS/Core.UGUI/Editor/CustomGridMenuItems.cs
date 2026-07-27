using UnityEngine;
using UnityEditor;

namespace GDS.Core.UGUI {

    public static class CustomGridMenuItems {
        const string menuPath = "GameObject/GDS";
        const string assetPath = "Assets/GDS/Core.UGUI/Views/Grid";
        const string gridBagMenuPath = menuPath + "/GridBag";

        [MenuItem(gridBagMenuPath, true)]
        public static bool InsideCanvas() => CustomMenuItems.InsideCanvas();

        [MenuItem(gridBagMenuPath, false, -1000)]
        public static void CreateListBag(MenuCommand menuCommand) {
            var parent = (GameObject)menuCommand.context;
            CustomMenuItems.CreatePrefab(parent, assetPath, "GridBag");
        }
    }

}
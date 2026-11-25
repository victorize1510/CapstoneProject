using UnityEngine;

namespace GDS.Core {

    public static class GameObjectExt {

        public static void Clear(this Transform target) {
            var count = target.childCount;
#if UNITY_EDITOR
            int group = UnityEditor.Undo.GetCurrentGroup();
            for (int i = count - 1; i >= 0; i--) {
                if (UnityEditor.EditorApplication.isPlaying) GameObject.DestroyImmediate(target.GetChild(i).gameObject);
                else UnityEditor.Undo.DestroyObjectImmediate(target.GetChild(i).gameObject);
            }
            UnityEditor.Undo.SetCurrentGroupName("Clear container");
            UnityEditor.Undo.CollapseUndoOperations(group);
#else
        for (int i = count - 1; i >= 0; i--) {
            GameObject.DestroyImmediate(target.GetChild(i).gameObject);            
        }
#endif

        }
    }
}
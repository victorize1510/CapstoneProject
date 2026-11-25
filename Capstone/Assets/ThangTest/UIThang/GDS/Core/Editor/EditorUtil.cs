using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {

    public static class EditorUtil {

        public static IEnumerable<SerializedProperty> IterateChildren(SerializedProperty parent) {
            var iterator = parent.Copy();
            var end = parent.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end)) {
                enterChildren = false; // Only enter once: on the first iteration
                yield return iterator.Copy();
            }
        }
        public static void TryAddStylesheet(VisualElement el, string path) {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (styleSheet == null) Debug.LogAssertion($"Failed to load stylesheet from path: <color=yellow>{path}</color> ");
            else el.styleSheets.Add(styleSheet);
        }

        private static StyleSheet editorStylesheet;
        public const string DefaultSylesheetPath = "Assets/GDS/Core/Editor/EditorStyles.uss";
        public static void AddDefaultEditorStylesheet(VisualElement root) {
            editorStylesheet ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(DefaultSylesheetPath);
            if (editorStylesheet == null) Debug.LogAssertion($"Failed to load stylesheet from path: <color=yellow>{DefaultSylesheetPath}</color> ");
            root.styleSheets.Add(editorStylesheet);
        }

        public static int GetPropertyIndex(SerializedProperty property) {
            var matches = System.Text.RegularExpressions.Regex.Matches(property.propertyPath, @"\[(\d+)\]");
            if (matches.Count == 0) return -1;
            return int.Parse(matches[matches.Count - 1].Groups[1].Value);
        }

        public static string CreateTagsLabel(SerializedProperty tagsProp) {
            if (!tagsProp.isArray) return "Tags";
            if (tagsProp.arraySize == 0) return "Tags <color=#888><i><empty></i></color>";
            var str = "";
            var count = tagsProp.arraySize;
            for (var i = 0; i < count; i++) {
                var tag = tagsProp.GetArrayElementAtIndex(i).objectReferenceValue as Tag;
                if (tag == null) continue;
                str += tag.name + (i == count - 1 ? "" : ", ");
            }
            return $"Tags: <i><u><size=10> {str} </size></u></i>";
        }


    }
}
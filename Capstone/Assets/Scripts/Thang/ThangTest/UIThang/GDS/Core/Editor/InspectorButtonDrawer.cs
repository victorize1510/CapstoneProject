using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace GDS.Core {

    [CustomPropertyDrawer(typeof(InspectorButtonAttribute))]
    public class InspectorButtonDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var attr = (InspectorButtonAttribute)attribute;
            var button = new Button(() => Invoke(property, attr.MethodName)) {
                text = ObjectNames.NicifyVariableName(attr.MethodName)
            };
            return button;
        }

        private void Invoke(SerializedProperty property, string methodName) {
            var target = property.serializedObject.targetObject;
            var type = target.GetType();
            var method = type.GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            if (method == null) {
                Debug.LogError($"Method '{methodName}' not found on {type.Name}");
                return;
            }

            Undo.RecordObject(target, $"Invoke {methodName}");
            method.Invoke(target, null);
            EditorUtility.SetDirty(target);
        }
    }
}
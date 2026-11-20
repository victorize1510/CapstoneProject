using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.Elements.Editor
{
    [CustomPropertyDrawer(typeof(SupportedElementAttribute))]
    public sealed class SupportedElementDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var values = new List<int>();
            var names = new List<string>();
            if (((SupportedElementAttribute)attribute).AllowUnknown)
            {
                values.Add(0);
                names.Add("Unknown (khong co)");
            }

            foreach (var entry in ElementCatalog.Entries)
            {
                values.Add((int)entry.Element);
                names.Add(entry.Element.ToString());
            }

            int selected = values.IndexOf(property.intValue);
            if (selected < 0)
            {
                selected = values.Count;
                values.Add(property.intValue);
                names.Add("Chua phan loai - raw " + property.intValue);
            }

            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(position, label.text, selected, names.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = values[next];
            }

            EditorGUI.EndProperty();
        }
    }
}

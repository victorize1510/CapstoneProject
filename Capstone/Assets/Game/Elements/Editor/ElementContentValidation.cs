using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.Elements.Editor
{
    [CustomEditor(typeof(ElementMatchupTable))]
    public sealed class ElementMatchupTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var table = (ElementMatchupTable)target;
            foreach (string error in table.Lookup.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }

    public static class ElementContentValidation
    {
        [MenuItem("Assets/Capstone/Kiem tra he noi dung da chon", false, 2100)]
        public static void ValidateSelected()
        {
            var issues = new List<string>();
            int count = 0;
            foreach (var selected in Selection.objects)
            {
                if (selected is PetSkillDefinition skill)
                {
                    count++;
                    if (!ElementCatalog.IsSupported(skill.element)) issues.Add(skill.Id + ": he skill chua phan loai.");
                }
                else if (selected is GameObject root)
                {
                    foreach (var actor in root.GetComponentsInChildren<PetCollectionMetadata>(true))
                    {
                        count++;
                        if (!ElementCatalog.IsValidCreatureTypes(actor.Element, actor.SecondaryElement)) issues.Add(actor.DefinitionId + "/" + actor.name + ": he chinh/phu khong hop le cho noi dung moi.");
                    }
                }
            }
            if (issues.Count > 0) Debug.LogError(string.Join("\n", issues) + "\nGiu raw legacy; can tac gia phan loai, khong tu map.");
            else Debug.Log("Kiem tra he: " + count + " muc hop le. Khong thay doi asset hoac save.");
        }
    }
}

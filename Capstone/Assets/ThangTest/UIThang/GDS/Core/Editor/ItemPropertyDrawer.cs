using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(Item))]
    public class ItemPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            if (property.propertyType is not SerializedPropertyType.ManagedReference) {
                Debug.LogAssertion("Item should be a serialized reference");
                return new PropertyField(property);
            }
            var root = new VisualElement();
            EditorUtil.AddDefaultEditorStylesheet(root);

            var baseField = new ObjectField("<empty slot>") { objectType = typeof(ItemBase) };
            baseField.AddToClassList(ObjectField.alignedFieldUssClassName);
            baseField.RegisterValueChangedCallback(e => onBaseChange(e, property, root));

            if (property.managedReferenceValue == null) {
                root.Add(baseField);
                return root;
            }

            if (property.managedReferenceValue is not Item item) {
                root.Add(new Label($"Wrong managed reference type: {GetType()}"));
                return root;
            }

            Render(property, root, item);
            root.style.flexDirection = FlexDirection.Row;
            root.style.minHeight = 60;
            return root;
        }

        void onBaseChange(ChangeEvent<Object> e, SerializedProperty property, VisualElement root) {
            if (e.newValue is not ItemBase b) return;
            var item = b.CreateItem();
            property.managedReferenceValue = item;
            property.serializedObject.ApplyModifiedProperties();
            Render(property, root, item);
        }

        void Render(SerializedProperty property, VisualElement root, Item item) {
            var baseField = new ObjectField("Base") { objectType = typeof(ItemBase), value = item.Base };
            baseField.AddToClassList(ObjectField.alignedFieldUssClassName);
            baseField.RegisterValueChangedCallback(e => onBaseChange(e, property, root));
            var idField = new PropertyField(property.FindPropertyRelative(nameof(Item.Id)));
            var nameField = new PropertyField(property.FindPropertyRelative(nameof(Item.Name)));
            var stackSizeField = new PropertyField(property.FindPropertyRelative(nameof(Item.StackSize)));
            var fieldsContainer = new VisualElement();
            fieldsContainer.style.flexGrow = 1;


            fieldsContainer.Add(baseField);
            if (EditorApplication.isPlaying) fieldsContainer.Add(idField);
            fieldsContainer.Add(nameField);
            if (item.Stackable) fieldsContainer.Add(stackSizeField);

            int iconSize = 64;
            baseField.style.marginRight = iconSize;
            idField.style.marginRight = iconSize;
            nameField.style.marginRight = iconSize;
            if (!EditorApplication.isPlaying) stackSizeField.style.marginRight = iconSize;


            var fields = EditorUtil.IterateChildren(property);
            var marginIndex = 2;
            if (!EditorApplication.isPlaying) marginIndex++;
            if (!item.Stackable) marginIndex++;

            for (var i = 0; i < fields.Count(); i++) {
                var field = fields.ElementAt(i);
                if (skipFields.Contains(field.name)) continue;
                var propField = new PropertyField(field);
                fieldsContainer.Add(propField);
                if (i <= marginIndex) propField.style.marginRight = 64;
            }

            root.Clear();
            root.Add(fieldsContainer);
            root.Add(IconPreview(item.Base));
            root.Add(ClearButton(property));
        }

        HashSet<string> skipFields = new(4) { nameof(Item.Base), nameof(Item.Id), nameof(Item.Name), nameof(Item.StackSize) };

        VisualElement IconPreview(ItemBase itemBase) {
            VisualElement el = itemBase.Icon == null
                ? new Label("No Icon")
                : new Image() { sprite = itemBase.Icon };
            el.AddToClassList("icon-preview");
            return el;
        }

        VisualElement ClearButton(SerializedProperty property) {
            var el = new Button(() => {
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            }) { text = "x" };

            return el.WithClass("btn-delete-item");
        }

    }
}
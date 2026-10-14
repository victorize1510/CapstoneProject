using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(SetBag), true)]
    [CustomPropertyDrawer(typeof(ListBag), true)]
    public class ListBagPropertyDrawer : PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            ItemCatalogSO catalog = null;

            // The main purpose of this drawer is to add import catalog thing
            var slotsProp = property.FindPropertyRelative("slots");
            var root = new VisualElement();
            var propField = new PropertyField(property);
            propField.SetEnabled(!EditorApplication.isPlaying);
            var objectField = new ObjectField() { objectType = typeof(ItemCatalogSO) };
            var importButton = new Button(OnImportClick) { text = "Import" }.Enabled(false);
            var clearButton = new Button(OnClearClick) { text = "Clear" };
            objectField.RegisterValueChangedCallback((e) => {
                catalog = e.newValue as ItemCatalogSO;
                importButton.SetEnabled(e.newValue != null);
            });
            var actionGroup = Dom.Div(objectField, importButton, clearButton);
            actionGroup.pickingMode = PickingMode.Ignore;
            actionGroup.style.flexDirection = FlexDirection.Row;
            actionGroup.style.position = Position.Absolute;
            actionGroup.style.overflow = Overflow.Hidden;
            actionGroup.style.justifyContent = Justify.FlexEnd;
            actionGroup.style.top = 0;
            // actionGroup.style.bottom = 0;
            actionGroup.style.left = 60;
            actionGroup.style.right = 0;
            objectField.style.flexShrink = 1;



            root.Add(propField, actionGroup);
            root.SetEnabled(!EditorApplication.isPlaying);


            void OnImportClick() {
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Import from catalog");
                Undo.RecordObject(property.serializedObject.targetObject, "Import from catalog");
                var count = slotsProp.arraySize;
                var imported = 0;
                for (var i = 0; i < count; i++) {
                    var element = slotsProp.GetArrayElementAtIndex(i);
                    if (element.propertyType is not SerializedPropertyType.ManagedReference) { Debug.LogWarning($"list slot element {i} is not a managed reference"); continue; }
                    if (element.managedReferenceValue is not Slot slot) continue;
                    slot.Item = catalog.Items.ElementAtOrDefault(i)?.Clone();
                    if (slot.Full()) imported++;
                }
                EditorUtility.SetDirty(property.serializedObject.targetObject);
                Undo.CollapseUndoOperations(group);
                Debug.Log($"Imported {imported} items from {catalog.name}");
            }

            void OnClearClick() {
                var group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Clear");
                Undo.RecordObject(property.serializedObject.targetObject, "Clear");
                var count = slotsProp.arraySize;
                for (var i = 0; i < count; i++) {
                    var element = slotsProp.GetArrayElementAtIndex(i);
                    if (element.propertyType is not SerializedPropertyType.ManagedReference) { Debug.LogWarning($"list slot element {i} is not a managed reference"); continue; }
                    if (element.managedReferenceValue is not Slot slot) continue;
                    slot.Clear();
                }
                EditorUtility.SetDirty(property.serializedObject.targetObject);
                Undo.CollapseUndoOperations(group);
                Debug.Log($"Cleared {count} slots!");
            }



            return root;

        }
    }
}
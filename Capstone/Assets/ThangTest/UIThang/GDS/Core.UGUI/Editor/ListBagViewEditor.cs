using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GDS.Core.UGUI {
    [CustomEditor(typeof(ListBagView), true)]
    public class ListBagViewEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            ListBagView bag = target as ListBagView;
            var root = new VisualElement();
            EditorUtil.AddDefaultEditorStylesheet(root);

            PropertyField prop(string name) => new PropertyField(serializedObject.FindProperty(name));

            var config = new Foldout() { text = "Config", viewDataKey = "ListBagViewConfig" };
            var data = prop("data");
            var showSlotDataToggle = prop("showLocalData");
            config.Add(
                prop("container"),
                prop("slotPrefab"),
                showSlotDataToggle
            );

            showSlotDataToggle.RegisterValueChangeCallback(e => {
                data.SetVisible(serializedObject.FindProperty("showLocalData").boolValue);
            });

            var buttonBar = Dom.Div("row",
                Dom.Button("flex-1", "Generate slot views", bag.GenerateSlots),
                Dom.Button("flex-1", "Clear container", bag.ClearContainerEditor),
                Dom.Button("flex-1", "Resize to fit", bag.ResizeEditor)
            );
            var box = Dom.Div("odd-row p-8", buttonBar);
            var actions = new Foldout() { text = "Container actions", viewDataKey = "ListBagViewContainerActions" };
            actions.Add(box);
            var debug = new Foldout() { text = "Debug", viewDataKey = "ListBagViewDebug" };
            debug.Add(prop("debugText"));

            root.Add(config, data, actions, debug);
            return root;
        }

    }

}
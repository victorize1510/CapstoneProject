using UnityEditor;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(HelpBoxAttribute))]
    public class HelpBoxDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var attr = (HelpBoxAttribute)attribute;
            var helpBox = new HelpBox(attr.Message, attr.MessageType);
            return helpBox;
        }
    }
}
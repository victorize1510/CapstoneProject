using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core;
using GDS.Core.UITK;

namespace GDS.Examples.UITK {

    public class RightClick_UiController : MonoBehaviour {
        [SerializeField, Required] RightClick_Store store;
        [SerializeField, Required] UIDocument document;

        [SerializeField, Space(12)] ListBag bagLeft = new() { Size = 20 };
        [SerializeField, Space(12)] ListBag bagRight = new() { Size = 20 };

        void OnEnable() {
            store.Left = bagLeft;
            store.Right = bagRight;

            var root = document.rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.AddManipulator(new RightClickManipulator(store));

            var listBagViewLeft = root.Q<ListBagView>("Left");
            listBagViewLeft.Init(bagLeft);

            var listBagViewRight = root.Q<ListBagView>("Right");
            listBagViewRight.Init(bagRight);
        }

    }
}
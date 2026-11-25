using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core;
using GDS.Core.UITK;

namespace GDS.Examples.UITK {

    public class MoveItems_UiController : MonoBehaviour {
        // Store can also be passed as serialized a reference
        [SerializeField, Required] MoveItems_Store store;
        [SerializeField, Required] UIDocument document;
        [SerializeField, Space(10)] ListBag bagLeft = new() { Size = 10 };
        [SerializeField, Space(10)] ListBag bagRight = new() { Size = 10 };

        void Start() {
            // The store requires references to the two bags
            store.Init(bagRight, bagLeft);

            var root = document.rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagViewLeft = root.Q<ListBagView>("Left");
            listBagViewLeft.Init(bagLeft);

            var listBagViewRight = root.Q<ListBagView>("Right");
            listBagViewRight.Init(bagRight);
        }

    }

}
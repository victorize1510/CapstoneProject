using GDS.Core;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples.UITK {

    [RequireComponent(typeof(UIDocument))]
    public class BagInABag_Controller : MonoBehaviour {

        public ListBag bag = new() { Size = 4, Name = "ContainerBag" };

        void Start() {
            var store = StoreLocator.Get();
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.Q<ContainerBagView>().Init(bag);
        }
    }

}
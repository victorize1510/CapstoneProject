using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core;
using GDS.Core.UITK;

namespace GDS.Examples.UITK {

    public class SplitItemStack_UiController : MonoBehaviour {

        [SerializeField, Required] Store store;
        [SerializeField, Required] UIDocument document;
        [SerializeField, Space(10)] ListBag listBag = new() { Size = 20 };
        [SerializeField, Space(10)] SetBag setBag = new() { Size = 5 };

        void Start() {
            var root = document.rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            root.Q<ListBagView>().Init(listBag);
            root.Q<SetBagView>().Init(setBag);
        }
    }

}
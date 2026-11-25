using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core;
using GDS.Core.UITK;

namespace GDS.Examples.UITK {

    public class Minimal_UiController : MonoBehaviour {
        [SerializeField, Required] UIDocument document;
        [SerializeField] ListBag listBag = new() { Size = 20 };

        void Start() {
            var store = StoreLocator.Get();
            var root = document.rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.AddManipulator(new TooltipManipulator());

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);
        }
    }

}
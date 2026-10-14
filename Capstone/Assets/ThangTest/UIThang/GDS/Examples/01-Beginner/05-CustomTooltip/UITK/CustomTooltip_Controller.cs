using GDS.Core;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class CustomTooltip_Controller : MonoBehaviour {

        [SerializeField, Required] VisualTreeAsset TooltipViewAsset;
        [SerializeField, Space(12)] ListBag listBag = new() { Size = 20 };

        void Start() {
            var store = StoreLocator.Get();
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            // TooltipManipulator accepts a custom TooltipView parameter
            root.AddManipulator(new TooltipManipulator(new CustomTooltipView(TooltipViewAsset)));

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);
        }

    }

}
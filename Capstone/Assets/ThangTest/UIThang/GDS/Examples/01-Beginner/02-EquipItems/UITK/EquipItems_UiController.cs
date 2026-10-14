using GDS.Core;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples.UITK {

    public class EquipItems_UiController : MonoBehaviour {
        [SerializeField, Required] UIDocument document;
        [SerializeField] SetBag Equipment = new() { Size = 5 };
        [SerializeField] ListBag Inventory = new() { Size = 20 };

        void Start() {
            var store = StoreLocator.Get();
            var root = document.rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.AddManipulator(new HighlightSlotManipulator(store));

            var equipmentView = root.Q<SetBagView>();
            equipmentView.Init(Equipment);

            var inventoryView = root.Q<ListBagView>();
            inventoryView.Init(Inventory);
        }
    }
}

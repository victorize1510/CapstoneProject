using GDS.Core;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples.UITK {

    public class CraftingBench_Controller : MonoBehaviour {
        [SerializeField] CraftingBench Bench = new();
        [SerializeField] ListBag Inventory = new();
        MoveItems_Store store;

        void Start() {
            store = StoreLocator.Get<MoveItems_Store>();
            store.Init(Inventory, Bench);

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.AddManipulator(new HighlightSlotManipulator(store));

            var craftingBenchView = root.Q<CraftingBenchView>("CraftingBenchView");
            craftingBenchView.Init(Bench);

            var inventoryView = root.Q<ListBagView>("InventoryView");
            inventoryView.Init(Inventory);
        }

    }

}
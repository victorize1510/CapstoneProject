
using GDS.Core;
using GDS.Core.Events;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Demos.Basic.UITK {

    public class Basic_UiController_UITK : MonoBehaviour {
        [SerializeField, Required] Basic_Store Store;
        [SerializeField, Required] UIDocument document;

        private void Start() {
            var root = document.rootVisualElement;
            root.AddManipulator(new DragDropManipulator(Store));
            root.AddManipulator(new HighlightSlotManipulator(Store));
            root.AddManipulator(new TooltipManipulator(new BasicTooltipView()));

            var left = root.Q<VisualElement>("Left");
            var right = root.Q<VisualElement>("Right");
            var backdrop = root.Q<VisualElement>("Backdrop");

            right.Add(new InventoryWindow(Store.PlayerInventory, Store));

            root.Observe(Store.UiOpen, value => root.SetVisible(value));
            root.Observe(Store.SideWindow, value => {
                left.Clear();
                if (value == null) return;
                left.Add(CreateSideWindowView(value, Store));
            });

            backdrop.RegisterCallback<PointerUpEvent>(_ => {
                if (Store.Ghost.Value == null) return;
                Store.Bus.Publish(new DropGhostItem());
            });
        }

        //  Creates a side window 
        VisualElement CreateSideWindowView(object handle, Basic_Store store) => handle switch {
            CharacterSheet b => new CharacterSheetWindow(b, store),
            Chest b => new ChestWindow(b, store),
            Stash b => new StashWindow(b, store),
            Shop b => new ShopWindow(b, store),
            CraftingBench b => new CraftingBenchWindow(b, store),
            _ => Dom.Div()
        };

    }

}
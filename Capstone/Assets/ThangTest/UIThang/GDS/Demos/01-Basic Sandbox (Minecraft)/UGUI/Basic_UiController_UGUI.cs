using GDS.Core;
using UnityEngine;

namespace GDS.Demos.Basic.UGUI {

    public class Basic_UiController_UGUI : MonoBehaviour {
        [SerializeField, Required] Basic_Store store;
        [SerializeField] InventoryWindow inventoryWindow;
        [SerializeField] ChestWindow chestWindow;
        [SerializeField] StashWindow stashWindow;
        [SerializeField] ShopWindow shopWindow;
        [SerializeField] CraftingBenchWindow craftingBenchWindow;
        [SerializeField] CharacterSheetWindow characterSheetWindow;

        GameObject lastWindow;

        private void Start() {
            inventoryWindow.Init(store.PlayerInventory);
        }

        void OnEnable() {
            store.UiOpen.OnChange += OnUiOpenChange;
            store.SideWindow.OnChange += OnSideWindowChange;
        }

        void OnDisable() {
            store.UiOpen.OnChange -= OnUiOpenChange;
            store.SideWindow.OnChange -= OnSideWindowChange;
        }

        void OnUiOpenChange(bool value) {
            inventoryWindow.gameObject.SetActive(value);
        }

        void OnSideWindowChange(object handle) {
            if (lastWindow != null) {
                lastWindow.SetActive(false);
                lastWindow = null;
            }

            var newWindow = GetWindow(handle);
            if (newWindow == null) return;
            newWindow.SetActive(true);
            lastWindow = newWindow;
        }

        GameObject GetWindow(object handle) => handle switch {
            _ when handle is CharacterSheet c => characterSheetWindow.Init(c).gameObject,
            _ when handle is CraftingBench c => craftingBenchWindow.Init(c).gameObject,
            _ when handle is Shop c => shopWindow.Init(c).gameObject,
            _ when handle is Chest c => chestWindow.Init(c).gameObject,
            _ when handle is Stash c => stashWindow.Init(c).gameObject,
            _ => null
        };

    }

}
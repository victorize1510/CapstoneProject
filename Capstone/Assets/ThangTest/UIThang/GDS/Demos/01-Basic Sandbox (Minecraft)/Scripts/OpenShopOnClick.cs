using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Demos.Basic {

    public class OpenShopOnClick : MonoBehaviour, IPointerClickHandler {

        // [Required]
        Basic_Store Store;

        [Space(12)]
        public Shop Bag = new();

        void Awake() {
            Store = StoreLocator.Get<Basic_Store>();
            Bag.Init(Store.PlayerInventory.Inventory, Store.PlayerInventory.PlayerGold);
        }

        public void OnPointerClick(PointerEventData eventData) {
            Store.Bus.Publish(new OpenWindow(Bag));
        }
    }

}
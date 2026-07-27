using UnityEngine;
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Examples {
    public class RightClick_Store : Store {

        public ListBag Left { get; set; }
        public ListBag Right { get; set; }

        void OnEnable() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceGhostItem>(OnPlaceItem);
            Bus.On<CustomRightClickEvent>(OnRightClick);
            Bus.On<CustomDoubleClickEvent>(OnDoubleClick);
        }

        void OnDisable() {
            Bus.Off<PickItem>(OnPickItem);
            Bus.Off<PlaceGhostItem>(OnPlaceItem);
            Bus.Off<CustomRightClickEvent>(OnRightClick);
            Bus.Off<CustomDoubleClickEvent>(OnDoubleClick);
        }

        void OnPickItem(PickItem e) {
            Result result = e.Bag.Remove(e.Item);
            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

        void OnPlaceItem(PlaceGhostItem e) {
            Result result = e.Bag.AddAt(e.Slot, Ghost.Item);
            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

        void OnRightClick(CustomRightClickEvent e) {
            Debug.Log(e);
            ListBag targetBag = e.Bag == Left ? Right : Left;
            BagExt.MoveItem(e.Bag, e.Item, targetBag);
        }

        void OnDoubleClick(CustomDoubleClickEvent e) {
            Debug.Log(e);
            ListBag targetBag = e.Bag == Left ? Right : Left;
            BagExt.MoveItem(e.Bag, e.Item, targetBag);
        }

    }
}
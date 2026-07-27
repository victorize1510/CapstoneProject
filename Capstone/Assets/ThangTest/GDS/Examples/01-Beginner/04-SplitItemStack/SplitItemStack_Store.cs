using UnityEngine;
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Examples {

    public class SplitItemStack_Store : Store {

        [SerializeField] EventModifiers splitStackKey = EventModifiers.Shift;
        bool ShouldSplitStack(EventModifiers mods) => mods.HasFlag(splitStackKey);
        public bool ShouldTransfer(EventModifiers mods, IItemContext target) => mods.HasFlag(splitStackKey) && ItemExt.CanStackOn(Ghost.Item, target.Slot.Item);

        void Awake() { StoreLocator.Register(this); }

        void OnEnable() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceGhostItem>(OnPlaceItem);
        }

        void OnDisable() {
            Bus.Off<PickItem>(OnPickItem);
            Bus.Off<PlaceGhostItem>(OnPlaceItem);
        }

        void OnPickItem(PickItem e) {
            var mods = InputUtil.GetModifiers();
            Result result = ShouldSplitStack(mods)
                ? e.Bag.SplitHalf(e.Item)
                : e.Bag.Remove(e.Item);

            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

        void OnPlaceItem(PlaceGhostItem e) {
            var mods = InputUtil.GetModifiers();
            Result result = ShouldTransfer(mods, e.Context)
                ? e.Bag.TransferOne(Ghost.Item, e.Slot, e.Slot.Item)
                : e.Bag.AddAt(e.Slot, Ghost.Item);

            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

    }

}

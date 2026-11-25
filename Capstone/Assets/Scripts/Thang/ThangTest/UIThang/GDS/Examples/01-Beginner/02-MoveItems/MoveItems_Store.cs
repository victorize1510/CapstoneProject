using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Examples {

    public class MoveItems_Store : Store {

        public EventModifiers MoveModifier = EventModifiers.Control;

        Bag Main, Secondary;

        bool ShouldMove(EventModifiers mods) => mods.HasFlag(MoveModifier);
        Bag GetOtherBag(Bag bag) => bag == Main ? Secondary : Main;

        public void Init(Bag main, Bag secondary) {
            Main = main;
            Secondary = secondary;
        }

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
            Result result;
            if (ShouldMove(InputUtil.GetModifiers())) {
                result = e.Bag.MoveItem(e.Item, GetOtherBag(e.Bag));
            } else {
                result = e.Bag.Remove(e.Item);
                UpdateGhost(result, e.Context);
            }
            Bus.Publish(result);
        }

        void OnPlaceItem(PlaceGhostItem e) {
            Result result = e.Bag.AddAt(e.Slot, Ghost.Item);
            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

    }

}
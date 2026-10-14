using GDS.Core;
using GDS.Core.Events;

namespace GDS.Common {

    // Default store handles the basic pick and place item behavior
    public class Default_Store : Store {

        // Register in the store locator on awake to allow referencing this without direct inspector references
        // The superclass has a lower execution order, making it safe to reference inside Awake
        void Awake() { StoreLocator.Register(this); }

        // Register event handlers for pick and place
        // The events are published by the DragAndDrop System
        void OnEnable() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceGhostItem>(OnPlaceItem);
        }

        void OnDisable() {
            Bus.Off<PickItem>(OnPickItem);
            Bus.Off<PlaceGhostItem>(OnPlaceItem);
        }

        // On pick, remove the item from the item from the bag, update the ghost and publish the resulting event
        void OnPickItem(PickItem e) {
            Result result = e.Context.Bag.Remove(e.Context.Item);
            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

        // On place, add the ghost item to the bag, potentially swapping with the one in the target slot, then publish the resulting event
        void OnPlaceItem(PlaceGhostItem e) {
            Result result = e.Context.Bag.AddAt(e.Context.Slot, Ghost.Item);
            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }
    }

}
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Examples {

    public class SwapItems_Store : Store {

        void Awake() { StoreLocator.Register(this); }

        void OnEnable() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceGhostItem>(OnPlaceItem);
            Bus.On<DropGhostItem>(OnCancelDrag);
        }

        void OnDisable() {
            Bus.Off<PickItem>(OnPickItem);
            Bus.Off<PlaceGhostItem>(OnPlaceItem);
        }

        void OnPickItem(PickItem e) {
            Ghost.SetValue(e.Context);
            Bus.Publish(new PickItemSuccess(e.Item));
        }

        void OnPlaceItem(PlaceGhostItem e) {
            Result result = BagExt.SwapItems(Ghost, e.Context);
            Ghost.Reset();
            Bus.Publish(result);
        }

        void OnCancelDrag(DropGhostItem _) {
            Ghost.Reset();
        }
    }

}
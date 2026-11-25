using GDS.Core;
using GDS.Core.Events;
using GDS.Core.UITK;

namespace GDS.Demos.Basic.UITK {

    public class ShopWindow : WindowView {
        public ShopWindow(Shop bag, Store store) {
            Init(bag.Name, () => store.Bus.Publish(new CloseWindow(bag)));
            Container.Add(
                "align-items-start",
                new ListBagView().Init(bag, 5, $"{bag.Name} (Uses Player Gold)"),
                Dom.Button("Refresh", () => bag.Refresh())
            );
        }
    }

}
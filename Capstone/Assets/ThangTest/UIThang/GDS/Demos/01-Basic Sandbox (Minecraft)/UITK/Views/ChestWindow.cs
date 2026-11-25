using System.Linq;
using GDS.Core;
using GDS.Core.Events;
using GDS.Core.UITK;
using UnityEngine.UIElements;

namespace GDS.Demos.Basic.UITK {

    public class ChestWindow : WindowView {

        public ChestWindow(Chest bag, Store store) {



            Init(bag.Name, () => store.Bus.Publish(new CloseWindow(bag)));

            var CollectAll = Dom.Button("Collect All", () => store.Bus.Publish(new CollectAll(bag)));
            Container.Add(
                "align-items-start",
                new ListBagView().Init(bag, 5, $"{bag.Name} (Remove only)"),
                CollectAll
            );

            void OnCollectionChanged() => CollectAll.SetEnabled(bag.Items.Count() != 0);
            bag.OnCollectionChanged += OnCollectionChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => bag.OnCollectionChanged -= OnCollectionChanged);

            OnCollectionChanged();
        }

    }

}
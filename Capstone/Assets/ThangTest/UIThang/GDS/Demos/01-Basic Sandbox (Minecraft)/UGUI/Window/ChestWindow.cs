using UnityEngine;
using UnityEngine.UI;
using GDS.Core.Events;
using GDS.Core.UGUI;
using GDS.Core;

namespace GDS.Demos.Basic.UGUI {

    public class ChestWindow : MonoBehaviour {
        [SerializeField] WindowView windowView;
        [SerializeField] ListBagView listBagView;
        [SerializeField] Button CollectAllButton;

        Chest bag;
        EventBus bus;

        public ChestWindow Init(Chest bag) {
            this.bag = bag;
            listBagView.Init(bag, true);
            windowView.Init(bag);
            return this;
        }
        void Awake() { bus = StoreLocator.Get().Bus; }
        void OnEnable() { CollectAllButton.onClick.AddListener(OnCollectAllClick); }
        void OnDisable() { CollectAllButton.onClick.RemoveListener(OnCollectAllClick); }
        void OnCollectAllClick() { bus.Publish(new CollectAll(bag)); }
    }

}
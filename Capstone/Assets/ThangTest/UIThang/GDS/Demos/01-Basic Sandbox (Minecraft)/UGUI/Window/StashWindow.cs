using UnityEngine;
using GDS.Core;
using GDS.Core.Events;
using GDS.Core.UGUI;

namespace GDS.Demos.Basic.UGUI {

    public class StashWindow : MonoBehaviour {
        [SerializeField] WindowView windowView;
        [SerializeField] TabsView tabsView;


        Stash bag;
        EventBus bus;

        public StashWindow Init(Stash bag) {
            this.bag = bag;
            tabsView.Init(bag);
            windowView.Init(bag);
            return this;
        }
        void Awake() { bus = StoreLocator.Get().Bus; }
    }

}
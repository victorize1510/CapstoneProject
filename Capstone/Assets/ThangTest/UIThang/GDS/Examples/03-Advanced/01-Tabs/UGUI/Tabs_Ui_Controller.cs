using GDS.Core.UGUI;
using UnityEngine;

namespace GDS.Examples.UGUi {
    public class Tabs_Ui_Controller : MonoBehaviour {
        [SerializeField] MoveItems_Store store;
        [SerializeField] ListBagView MainView;
        [SerializeField] TabsView SecondaryView;

        void Awake() {
            store.Init(MainView.Bag, SecondaryView.Collection);
        }
    }
}

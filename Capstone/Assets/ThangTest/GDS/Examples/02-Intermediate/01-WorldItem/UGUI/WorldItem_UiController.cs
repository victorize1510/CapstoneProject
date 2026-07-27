using UnityEngine;
using GDS.Core;
using GDS.Core.UGUI;

namespace GDS.Examples {

    public class WorldItem_UiController_UGUI : MonoBehaviour {

        [SerializeField, Required] WorldItem_Store store;
        [SerializeField, Required] ListBagView listBagView;

        void Start() {
            listBagView.Init(store.Bag);
        }

    }

}
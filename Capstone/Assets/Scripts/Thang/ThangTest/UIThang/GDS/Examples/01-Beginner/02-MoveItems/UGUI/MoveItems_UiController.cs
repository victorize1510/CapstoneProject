using UnityEngine;
using GDS.Core;
using GDS.Core.UGUI;

namespace GDS.Examples.UGUI {

    public class MoveItems_UiController : MonoBehaviour {
        [SerializeField, Required] MoveItems_Store store;
        [SerializeField, Required] ListBagView bagViewLeft;
        [SerializeField, Required] ListBagView bagViewRight;

        void Start() {
            // The store requires references to the two bags
            store.Init(bagViewLeft.Bag, bagViewRight.Bag);
        }

    }

}
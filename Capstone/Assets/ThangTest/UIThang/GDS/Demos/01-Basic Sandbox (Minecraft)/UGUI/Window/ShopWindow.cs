using UnityEngine;
using UnityEngine.UI;
using GDS.Core.UGUI;

namespace GDS.Demos.Basic.UGUI {

    public class ShopWindow : MonoBehaviour {
        [SerializeField] WindowView windowView;
        [SerializeField] ListBagView listBagView;
        [SerializeField] Button refreshButton;

        public ShopWindow Init(Shop bag) {
            listBagView.Init(bag, true);
            windowView.Init(bag);
            refreshButton.onClick.AddListener(bag.Refresh);
            return this;
        }
    }
}
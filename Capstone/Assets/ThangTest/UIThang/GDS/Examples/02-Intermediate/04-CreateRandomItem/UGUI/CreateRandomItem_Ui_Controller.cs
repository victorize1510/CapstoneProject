using GDS.Core;
using GDS.Core.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace GDS.Examples.UGUI {

    public class CreateRandomItem_Ui_Controller : MonoBehaviour {
        [SerializeField] ListBagView listBagView;
        [SerializeField] ItemBaseCatalogSO catalog;
        [SerializeField] Button createItemButton;

        void Awake() {
            var store = StoreLocator.Get();
            createItemButton.onClick.AddListener(() => {
                var result = listBagView.Bag.Add(catalog.CreateRandomItem());
                store.Bus.Publish(result);
            });
        }
    }

}
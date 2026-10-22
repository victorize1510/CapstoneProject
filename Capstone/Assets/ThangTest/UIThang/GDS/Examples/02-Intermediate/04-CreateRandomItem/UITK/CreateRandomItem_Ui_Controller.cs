using GDS.Core;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples.UITK {

    [RequireComponent(typeof(UIDocument))]
    public class CreateRandomItem_Ui_Controller : MonoBehaviour {
        [SerializeField] ListBag listBag = new() { Size = 20 };
        [SerializeField] ItemBaseCatalogSO catalog;

        void Start() {
            var store = StoreLocator.Get();
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);

            var createItemButton = root.Q<Button>("CreateItem");
            createItemButton.RegisterCallback<ClickEvent>(_ => {
                var result = listBag.Add(catalog.CreateRandomItem());
                store.Bus.Publish(result);
            });

            var backdrop = root.Q<VisualElement>("Backdrop");
            backdrop.RegisterCallback<PointerUpEvent>(_ => store.Ghost.SetValue(null));
        }
    }

}
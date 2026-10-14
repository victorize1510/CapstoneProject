using GDS.Core;
using GDS.Core.Events;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples.UITK {
    [RequireComponent(typeof(UIDocument))]
    public class AddSounds_Controller : MonoBehaviour {
        [SerializeField, Required] Store store;
        [SerializeField, Space(10)] ListBag listBag = new() { Size = 20 };

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);

            var backdrop = root.Q<VisualElement>("Backdrop");
            backdrop.RegisterCallback<PointerUpEvent>(_ => store.Bus.Publish(Result.Fail));
        }
    }

}
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using GDS.Core;
using GDS.Core.UITK;
using GDS.Core.Events;

namespace GDS.Examples.UITK {

    public class WorldItem_UiController : MonoBehaviour {

        [SerializeField, Required] WorldItem_Store store;
        [SerializeField, Required] UIDocument document;
        // [SerializeField] LayerMask mask;

        [SerializeField] bool isGameUiOpen = true;

        void Start() {
            document.rootVisualElement.SetVisible(isGameUiOpen);

            var root = document.rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagView = root.Q<ListBagView>().Init(store.Bag);
            var backdrop = root.Q<VisualElement>("Backdrop");
            backdrop.RegisterCallback<PointerUpEvent>(_ => {
                if (store.Ghost.Value == null) return;
                store.Bus.Publish(new DropGhostItem());
            });
        }

        void Update() {
            if (Keyboard.current.tabKey.wasPressedThisFrame) {
                isGameUiOpen = !isGameUiOpen;
                document.rootVisualElement.SetVisible(isGameUiOpen);
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame) {
                isGameUiOpen = false;
                document.rootVisualElement.SetVisible(isGameUiOpen);
            }

            // if (Mouse.current.leftButton.wasPressedThisFrame) {
            //     var ray = Camera.main.ScreenPointToRay(Mouse.current.position.value);
            //     Physics.Raycast(ray, out var hitInfo, 100, mask);
            //     Debug.Log(hitInfo.point);
            //     store.Bus.Publish(new DropWorldItem(store.Ghost.Value, hitInfo.point));
            // }
        }

    }

}
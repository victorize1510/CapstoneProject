using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UI;

namespace GDS.Core.UGUI {

    public class WindowView : MonoBehaviour {
        [SerializeField] Button CloseButton;

        object handle;
        EventBus bus;

        public virtual WindowView Init(object handle) { this.handle = handle; return this; }
        void Awake() { bus = StoreLocator.Get().Bus; }
        void OnEnable() { CloseButton.onClick.AddListener(OnClose); }
        void OnDisable() { CloseButton.onClick.RemoveListener(OnClose); }
        void OnClose() {
            if (handle == null) { Debug.LogWarning($"Window has no handle! ({this})"); }
            bus.Publish(new CloseWindow(handle));
            gameObject.SetActive(false);
        }
    }
}

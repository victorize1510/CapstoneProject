using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory {
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    public sealed class InventoryEditSceneVisibility : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] bool hideInEditMode = true;
        [SerializeField] bool restoreVisibleWhenDisabled = true;

        public bool HideInEditMode => hideInEditMode;

        void OnEnable() {
            ResolveDocument();
            ApplyNow();
        }

        void OnDisable() {
            if (!Application.isPlaying && restoreVisibleWhenDisabled) SetDisplay(DisplayStyle.Flex);
        }

        void OnValidate() {
            ResolveDocument();
            ApplyNow();
        }

        void Update() {
            if (!Application.isPlaying) ApplyNow();
        }

        public void SetHideInEditMode(bool value) {
            hideInEditMode = value;
            ApplyNow();
        }

        public void ApplyNow() {
            if (Application.isPlaying) return;
            ResolveDocument();
            SetDisplay(hideInEditMode ? DisplayStyle.None : DisplayStyle.Flex);
        }

        void ResolveDocument() {
            if (document == null) document = GetComponent<UIDocument>();
        }

        void SetDisplay(DisplayStyle display) {
            if (document == null || document.rootVisualElement == null) return;
            document.rootVisualElement.style.display = display;
        }
    }
}

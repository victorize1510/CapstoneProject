using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GDS.Core.UGUI {

    public class HighlightSlotBehavior : MonoBehaviour {
        readonly WaitForSecondsRealtime HoverDelay = new(0.05f);
        readonly List<RaycastResult> results = new();
        readonly PointerEventData eventData = new(EventSystem.current);
        IStore store;
        SlotView lastSlotView;

        void Awake() { store = StoreLocator.Get(); }
        void OnEnable() { StartCoroutine(Tick()); }
        void OnDisable() { StopCoroutine(Tick()); }

        private IEnumerator Tick() {
            while (true) {
                OnTick();
                yield return HoverDelay;
            }
        }

        void OnTick() {
            if (store.Ghost.Empty) { Hide(); return; }

            eventData.position = Mouse.current.position.ReadValue();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var hit in results) {
                if (hit.gameObject.TryGetComponent<SlotView>(out var slotView) && slotView != null) {
                    Show(slotView);
                    return;
                }
            }

            Hide();
        }

        void Show(SlotView slotView) {
            if (slotView == lastSlotView) return;
            Hide();
            if (!slotView.Ready) return;
            var valid = slotView.Bag.Accepts(store.Ghost.Item) && slotView.Slot.Accepts(store.Ghost.Item);
            slotView.ShowOverlay(valid);
            lastSlotView = slotView;
        }

        void Hide() {
            if (lastSlotView == null) return;
            lastSlotView.HideOverlay();
            lastSlotView = null;
        }
    }

}
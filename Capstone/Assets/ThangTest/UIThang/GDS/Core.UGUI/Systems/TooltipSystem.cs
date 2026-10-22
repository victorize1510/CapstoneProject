using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GDS.Core.UGUI {
    public class TooltipSystem : MonoBehaviour {

        [SerializeField, Required] BaseTooltipView TooltipPrefab;
        [SerializeField] TextMeshProUGUI debugText;
        [SerializeField, Range(0, 2)] float raycastTickDelta = 0.05f;

        WaitForSecondsRealtime HoverDelay;
        List<RaycastResult> results = new();
        PointerEventData eventData = new(EventSystem.current);

        BaseTooltipView TooltipView;

        void Awake() { TooltipView = TooltipPrefab.gameObject.scene.IsValid() ? TooltipPrefab : Instantiate(TooltipPrefab, transform); }
        void OnEnable() { StartCoroutine(Tick()); }
        void OnDisable() { StopCoroutine(Tick()); }

        private IEnumerator Tick() {
            HoverDelay ??= new(raycastTickDelta);
            while (true) {
                OnTick();
                yield return HoverDelay;
            }
        }

        void OnTick() {
            eventData.position = Mouse.current.position.ReadValue();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var hit in results) {
                // Debug.Log(hit);
                if (hit.gameObject.TryGetComponent<IItemContext>(out var context) && context.Item != null) {
                    TooltipView.gameObject.SetActive(true);
                    TooltipView.Render(context);
                    TooltipView.Position((RectTransform)transform, context.WorldBound);
                    return;
                }
            }

            TooltipView.gameObject.SetActive(false);
        }
    }
}
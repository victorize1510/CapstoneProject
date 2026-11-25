using System.Collections;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.ProfileSystem {
    public sealed class ProfileCameraTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler {
        public string Text;
        GameObject tip; Coroutine delay;
        void Show() { Hide(); delay = StartCoroutine(Wait()); }
        IEnumerator Wait() {
            yield return new WaitForSecondsRealtime(.35f);
            if (tip == null) {
                tip = new GameObject("AvatarCameraTooltip", typeof(RectTransform), typeof(ProfileR2Graphic)); tip.transform.SetParent(transform, false);
                var rect = (RectTransform)tip.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 12); rect.sizeDelta = new Vector2(240, 48);
                var face = tip.GetComponent<ProfileR2Graphic>(); face.Shape = "parallel"; face.color = GreenAngularGeometry.Ink; face.raycastTarget = false;
                var label = new GameObject("Caption", typeof(RectTransform), typeof(UnityEngine.UI.Text)); label.transform.SetParent(tip.transform, false);
                var lr = (RectTransform)label.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(12, 4); lr.offsetMax = new Vector2(-12, -4);
                var text = label.GetComponent<UnityEngine.UI.Text>(); text.font = MenuTypography.Font; text.fontSize = 22; text.color = GreenAngularGeometry.Light; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.text = Text;
            }
            tip.SetActive(true); delay = null;
        }
        void Hide() { if (delay != null) StopCoroutine(delay); delay = null; if (tip != null) tip.SetActive(false); }
        public void OnPointerEnter(PointerEventData e) => Show(); public void OnPointerExit(PointerEventData e) => Hide();
        public void OnSelect(BaseEventData e) => Show(); public void OnDeselect(BaseEventData e) => Hide();
        void OnDisable() => Hide();
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GDS.Core.UGUI {

    public class SlotView : MonoBehaviour, IItemContext, IPointerEnterHandler, IPointerExitHandler {

        [SerializeField] protected ItemView ItemView;
        [SerializeField] protected Image Background;
        [SerializeField] protected Image Border;
        [SerializeField] protected Image Overlay;
        [SerializeField] protected GameObject Warning;
        [SerializeField] protected TextMeshProUGUI NameText;

        [Header("Colors")]
        [SerializeField] protected Color ValidColor = new(.5f, 1f, .5f, .1f);
        [SerializeField] protected Color InvalidColor = new(1f, .5f, .5f, .1f);

        [NonSerialized] protected Bag bag;
        [NonSerialized] protected Slot slot;

        public Bag Bag => bag;
        public Slot Slot => slot;
        public Item Item => slot?.Item;
        public Rect WorldBound => GetWorldBound();
        public Image Icon => ItemView.Icon;
        public bool Ready => bag != null && slot != null;

        protected RectTransform rectTransform => transform as RectTransform;

        void Awake() { UpdateWarning(); }

        public virtual void Init(Bag bag, Slot slot) {
            this.bag = bag;
            this.slot = slot;
            Render();
            UpdateWarning();
        }

        void OnDisable() {
            if (Border != null) Border.gameObject.SetActive(false);
            if (Overlay != null) Overlay.gameObject.SetActive(false);
        }

        public virtual void OnPointerEnter(PointerEventData eventData) {
            if (Border == null) return;
            Border.gameObject.SetActive(true);
        }

        public virtual void OnPointerExit(PointerEventData eventData) {
            if (Border == null) return;
            Border.gameObject.SetActive(false);
        }

        public virtual void Render() {
            if (slot == null) return;
            ItemView.Item = slot.Item;
            ItemView.gameObject.SetActive(slot.Full());
        }

        public virtual void SetName(string name) {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                UnityEditor.Undo.RecordObject(gameObject, "Rename slots");
                UnityEditor.Undo.RecordObject(NameText, "Rename slots");
            }
#endif            
            this.name = name;
            if (NameText == null) return;
            NameText.text = name;

        }

        public virtual void ShowOverlay(bool valid) {
            if (Overlay == null) return;
            Overlay.color = valid ? ValidColor : InvalidColor;
            Overlay.gameObject.SetActive(true);
        }

        public virtual void HideOverlay() {
            if (Overlay == null) return;
            Overlay.gameObject.SetActive(false);
        }

        public virtual void IsGhost(bool isGhost) {
            var c = Icon.color;
            c.a = isGhost ? 0.25f : 1;
            Icon.color = c;
            Icon.transform.localScale = isGhost ? new Vector2(0.5f, 0.5f) : Vector2.one;
        }

        void UpdateWarning() {
            if (Warning == null) return;
            Warning.SetActive(slot == null);
        }

        Rect GetWorldBound() {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);
            return new(screenPoint, rectTransform.rect.size);
        }

    }

}
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.HudSystem {
    // A small, text-only prefab using the HUD's existing uGUI typography and geometry.
    public sealed class EvolutionHudPromptView : MonoBehaviour, ICancelHandler {
        const float Width = 408f;
        const float Height = 148f;
        const float Padding = 14f;
        const float ButtonGap = 10f;
        const float PopupScale = 2.236068f; // Five times the previous area.
        Text message, reason;
        public Button Confirm { get; private set; }
        public Button Later { get; private set; }
        public event Action Confirmed, Deferred;

        void Awake() => Build();
        public void Build() {
            if (message != null) return;
            var rect = (RectTransform)transform;
            var center = new Vector2(.5f, .5f);
            V.Place(rect, center, center, Vector2.zero, new Vector2(Width, Height));
            rect.localScale = Vector3.one * PopupScale;
            var surface = V.Frame(transform, "Surface", HudFrameGraphic.Shape.Chamfered,
                new Color(.10f, .14f, .21f, .72f), new Color(.70f, .66f, .56f, .80f), 1.2f, 10f);
            V.Stretch(surface.rectTransform);
            surface.raycastTarget = true;
            float contentWidth = Width - 2 * Padding;
            var title = V.Text(surface.transform, "Title", "Tiến hóa", 22, TextAnchor.MiddleCenter);
            V.Top(title.rectTransform, Padding, 8, contentWidth, 26);
            title.fontStyle = FontStyle.Bold; title.color = Color.white;
            message = V.Text(surface.transform, "Message", "", 18, TextAnchor.MiddleCenter);
            message.color = Color.white;
            V.Top(message.rectTransform, Padding, 36, contentWidth, 40);
            FitText(message, 16, 18);
            reason = V.Text(surface.transform, "Reason", "", 15, TextAnchor.MiddleCenter);
            reason.color = new Color(.98f, .91f, .75f, 1f);
            V.Top(reason.rectTransform, Padding, 76, contentWidth, 28);
            FitText(reason, 13, 15);
            float buttonWidth = (contentWidth - ButtonGap) * .5f;
            Confirm = MakeButton(surface.transform, "Confirm", "Tiến hóa", Padding, buttonWidth, true);
            Later = MakeButton(surface.transform, "Later", "Để sau", Padding + buttonWidth + ButtonGap, buttonWidth, false);
            Confirm.onClick.AddListener(() => Confirmed?.Invoke());
            Later.onClick.AddListener(() => Deferred?.Invoke());
            foreach (var button in new[] { Confirm, Later }) {
                var trigger = button.gameObject.AddComponent<EventTrigger>();
                var cancel = new EventTrigger.Entry { eventID = EventTriggerType.Cancel };
                cancel.callback.AddListener(data => { Deferred?.Invoke(); data.Use(); });
                trigger.triggers.Add(cancel);
            }
            Confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = Later, selectOnLeft = Later };
            Later.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = Confirm, selectOnLeft = Confirm };
        }
        static void FitText(Text label, int min, int max) {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = min;
            label.resizeTextMaxSize = max;
        }
        static Button MakeButton(Transform parent, string name, string text, float x, float width, bool primary) {
            var button = V.Button(parent, name, HudFrameGraphic.Shape.Chamfered, 8);
            var frame = (HudFrameGraphic)button.targetGraphic;
            frame.color = primary ? new Color(.28f, .33f, .43f, .78f) : new Color(.14f, .18f, .25f, .58f);
            frame.SetBorder(primary ? new Color(.81f, .73f, .54f, .88f) : V.Line, 1.2f);
            var colors = button.colors;
            colors.highlightedColor = new Color(.89f, .96f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            V.Bottom((RectTransform)button.transform, x, 10, width, 32);
            var label = V.Text(button.transform, "Label", text, 17, TextAnchor.MiddleCenter);
            label.color = Color.white;
            label.fontStyle = FontStyle.Bold;
            V.Stretch(label.rectTransform, 3);
            return button;
        }
        public void Display(string petName, bool canConfirm, string disabledReason) {
            Build();
            message.text = canConfirm ? petName + " đã đủ điều kiện tiến hóa." : "Tiến hóa " + petName;
            reason.text = canConfirm ? "Y: Tiến hóa  ·  N: Để sau" : disabledReason;
            Confirm.interactable = canConfirm;
        }
        public void OnCancel(BaseEventData eventData) { Deferred?.Invoke(); eventData.Use(); }
    }
}

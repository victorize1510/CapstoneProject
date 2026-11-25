using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>Native selectable states update content without an Update loop.</summary>
    public sealed class GuiKitButton : Button {
        [SerializeField] GuiButtonKind kind;
        [SerializeField] Text label;
        [SerializeField] Image icon;
        [SerializeField] Image focus;
        bool selected;
        Color? foreground;
        public void Configure(GuiButtonKind buttonKind, Text text, Image symbol, Image focusRing, Color? contentColor = null) {
            foreground = contentColor;
            kind = buttonKind; label = text; icon = symbol; focus = focusRing;
            var state = colors;
            state.normalColor = Color.white;
            state.highlightedColor = kind == GuiButtonKind.Primary ? new Color(1, 1, .93f) : GuiKitPalette.Hover;
            state.pressedColor = kind == GuiButtonKind.Primary ? new Color(.83f, .9f, .83f) : GuiKitPalette.Pressed;
            state.selectedColor = Color.white;
            state.disabledColor = GuiKitPalette.Disabled;
            state.fadeDuration = .1f;
            colors = state;
            RefreshContent();
        }
        protected override void DoStateTransition(SelectionState state, bool instant) {
            base.DoStateTransition(state, instant);
            RefreshContent();
        }
        public override void OnPointerDown(PointerEventData eventData){InteractionFeedback.KeyboardNavigation=false;base.OnPointerDown(eventData);}
        public override void OnMove(AxisEventData eventData){InteractionFeedback.KeyboardNavigation=true;base.OnMove(eventData);}
        public override void OnSelect(BaseEventData eventData) {
            selected = true; base.OnSelect(eventData); RefreshContent();
        }
        public override void OnDeselect(BaseEventData eventData) {
            selected = false; base.OnDeselect(eventData); RefreshContent();
        }
        protected override void OnDisable() {
            selected = false; base.OnDisable(); RefreshContent();
        }
        void RefreshContent() {
            bool enabled = IsInteractable() && isActiveAndEnabled;
            var color = !enabled ? GuiKitPalette.Muted : foreground ?? (kind == GuiButtonKind.Primary ? Color.white : kind == GuiButtonKind.Danger ? GuiKitPalette.Danger : GuiKitPalette.Text);
            if (label != null) label.color = color;
            if (icon != null) icon.color = color;
            if (focus != null) focus.enabled = selected && enabled;
        }
    }
}

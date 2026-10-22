using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    public static class PanelFocusUtility {
        sealed class ReturnFocus {
            public VisualElement Target;
            public EventCallback<NavigationMoveEvent> TabHandler;
            public int Revision;
            public readonly List<VisualElement> Focusable = new List<VisualElement>();
        }
        static readonly ConditionalWeakTable<VisualElement, ReturnFocus> targets = new ConditionalWeakTable<VisualElement, ReturnFocus>();
        public static bool TryCloseNativeDropdown(VisualElement documentRoot) {
            var popup = documentRoot?.panel?.visualTree.Q(className: "unity-base-dropdown__container-outer");
            var content = popup?.Q<ScrollView>()?.contentContainer;
            if (content == null) return false;
            // Use the supported navigation event so Unity performs its own
            // cleanup and restores focus to the dropdown field.
            using (var cancel = NavigationCancelEvent.GetPooled()) { cancel.target = content; content.SendEvent(cancel); }
            return true;
        }
        public static void Open(VisualElement overlay) {
            if (overlay == null || overlay.panel == null) return;
            var state = targets.GetValue(overlay, _ => new ReturnFocus());
            var current = overlay.focusController?.focusedElement as VisualElement;
            if (current != null && !overlay.Contains(current)) state.Target = current;
            if (state.TabHandler == null) {
                state.TabHandler = evt => {
                    // A modal owns navigation while focused. Let its handler run
                    // instead of cycling through controls behind the modal.
                    for (var modalAncestor = evt.target as VisualElement; modalAncestor != null && modalAncestor != overlay; modalAncestor = modalAncestor.parent)
                        if (targets.TryGetValue(modalAncestor, out var nested) && nested.TabHandler != null) return;
                    bool reverse = evt.direction == NavigationMoveEvent.Direction.Previous;
                    if (!reverse && evt.direction != NavigationMoveEvent.Direction.Next) return;
                    state.Focusable.Clear();
                    overlay.Query<VisualElement>().ForEach(element => {
                        if (!element.focusable || element.tabIndex < 0 || !IsVisible(element)) return;
                        // TextField delegates focus to its text input. Treat that subtree
                        // as one stop instead of repeatedly focusing its parent and child.
                        for (var ancestor = element.parent; ancestor != null && ancestor != overlay; ancestor = ancestor.parent)
                            if (ancestor.delegatesFocus && state.Focusable.Contains(ancestor)) return;
                        state.Focusable.Add(element);
                    });
                    if (state.Focusable.Count == 0) return;
                    var focused = overlay.focusController?.focusedElement as VisualElement;
                    int index = state.Focusable.IndexOf(focused);
                    if (index < 0) index = state.Focusable.FindIndex(element => element.Contains(focused));
                    int next = index < 0 ? (reverse ? state.Focusable.Count - 1 : 0)
                        : (index + (reverse ? -1 : 1) + state.Focusable.Count) % state.Focusable.Count;
                    state.Focusable[next].Focus();
                    evt.StopImmediatePropagation(); overlay.focusController?.IgnoreEvent(evt);
                };
                overlay.RegisterCallback(state.TabHandler, TrickleDown.TrickleDown);
            }
            int revision = ++state.Revision;
            overlay.schedule.Execute(() => {
                if (revision != state.Revision || !IsVisible(overlay)) return;
                var input = overlay.Q<TextField>();
                if (input != null && IsVisible(input)) input.Focus();
                else overlay.Query<Button>().ForEach(button => {
                    if (revision == state.Revision && IsVisible(button)) { button.Focus(); revision = -1; }
                });
            });
        }
        public static void Close(VisualElement overlay) {
            if (overlay == null || !targets.TryGetValue(overlay, out var state)) return;
            ++state.Revision;
            if (state.TabHandler != null) overlay.UnregisterCallback(state.TabHandler, TrickleDown.TrickleDown);
            state.TabHandler = null; state.Focusable.Clear();
            var target = state.Target; state.Target = null;
            if (IsVisible(target)) target.Focus();
        }
        static bool IsVisible(VisualElement element) {
            if (element == null || element.panel == null || !element.enabledInHierarchy) return false;
            for (var current = element; current != null; current = current.parent)
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }
    }
}

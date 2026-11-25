using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    public static class MenuKeyboardNavigation {
        static readonly List<Selectable> candidates = new List<Selectable>();
        public static void FocusFirst(Transform root) {
            Collect(root);
            if (candidates.Count > 0) EventSystem.current.SetSelectedGameObject(candidates[0].gameObject);
        }
        public static void Advance(Transform root, bool reverse) {
            Collect(root);
            if (candidates.Count == 0) return;
            int current = -1;
            var selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < candidates.Count; i++) if (candidates[i].gameObject == selected) { current = i; break; }
            int next = current < 0 ? (reverse ? candidates.Count - 1 : 0) : (current + (reverse ? -1 : 1) + candidates.Count) % candidates.Count;
            EventSystem.current.SetSelectedGameObject(candidates[next].gameObject);
        }
        static void Collect(Transform root) {
            candidates.Clear();
            if (root == null || EventSystem.current == null) return;
            root.GetComponentsInChildren(false, candidates);
            for (int i = candidates.Count - 1; i >= 0; i--)
                if (!candidates[i].IsActive() || !candidates[i].IsInteractable() || candidates[i].navigation.mode == Navigation.Mode.None) candidates.RemoveAt(i);
        }
        public static void Restore(GameObject target) {
            if (target != null && target.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(target);
        }
    }
}

using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.QuestSystem.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem {
    /// <summary>Temporary visual suppression. Never disables gameplay components or changes HUD preferences.</summary>
    public sealed class TrainerMenuPresentation {
        readonly List<(CanvasGroup group, float alpha, bool interactable, bool raycasts)> groups = new List<(CanvasGroup,float,bool,bool)>();
        readonly List<(Renderer renderer, bool wasOff)> labels = new List<(Renderer,bool)>();
        readonly List<(VisualElement root, StyleEnum<Visibility> visibility)> trackers = new List<(VisualElement,StyleEnum<Visibility>)>();
        bool active;
        public bool IsSuppressed => active;
        public void SetMenuVisible(bool visible) {
            if (active == visible) return;
            active = visible;
            if (!visible) { Restore(); return; }
            foreach (var hud in Object.FindObjectsByType<GameplayHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Hide(hud.PresentationRoot);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.renderMode == RenderMode.WorldSpace) Hide(canvas.transform);
            foreach (var label in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None)) HideLabel(label.GetComponent<Renderer>());
            foreach (var label in Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None)) HideLabel(label.GetComponent<Renderer>());
            foreach (var tracker in Object.FindObjectsByType<QuestTrackerHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                var doc = tracker.GetComponent<UIDocument>();
                var root = doc != null ? doc.rootVisualElement : null;
                if (root == null) continue;
                trackers.Add((root, root.style.visibility)); root.style.visibility = Visibility.Hidden;
            }
            // The save service still owns its timer and preference. Suppress only its
            // gameplay indicator while these menus own the screen.
            foreach(var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
                var indicator=doc.rootVisualElement?.Q("game-save-indicator");if(indicator==null)continue;
                trackers.Add((indicator,indicator.style.visibility));indicator.style.visibility=Visibility.Hidden;
            }
        }
        void Hide(Transform root) {
            if (root == null) return;
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) { group = root.gameObject.AddComponent<CanvasGroup>(); group.hideFlags = HideFlags.DontSave; }
            if (groups.Exists(x => x.group == group)) return;
            groups.Add((group, group.alpha, group.interactable, group.blocksRaycasts));
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
        }
        void HideLabel(Renderer renderer) {
            if (renderer == null || labels.Exists(x => x.renderer == renderer)) return;
            labels.Add((renderer, renderer.forceRenderingOff)); renderer.forceRenderingOff = true;
        }
        void Restore() {
            foreach (var state in groups) if (state.group != null) {
                state.group.alpha = state.alpha; state.group.interactable = state.interactable; state.group.blocksRaycasts = state.raycasts;
            }
            foreach (var state in labels) if (state.renderer != null) state.renderer.forceRenderingOff = state.wasOff;
            foreach (var state in trackers) state.root.style.visibility = state.visibility;
            groups.Clear(); labels.Clear(); trackers.Clear();
        }
    }
}

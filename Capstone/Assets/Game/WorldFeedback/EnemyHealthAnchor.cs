using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.WorldFeedback {
    /// <summary>Optional authored anchor; defaults to the cached visual's top, excluding particles/trails.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthAnchor : MonoBehaviour {
        public Transform head;
        public float topPadding = .18f;
        internal Renderer[] Visuals;
        internal Animator Bound;
        CreatureAnimationDriver driver;
        void Awake() { driver = GetComponent<CreatureAnimationDriver>(); }
        public Vector3 Position {
            get {
                if (head != null) return head.position + Vector3.up * topPadding;
                var next = driver != null ? driver.BoundAnimator : Bound;
                if (Visuals == null || next != Bound) {
                    Bound = next;
                    var all = next != null ? next.GetComponentsInChildren<Renderer>() : GetComponentsInChildren<Renderer>();
                    var body = new System.Collections.Generic.List<Renderer>(all.Length);
                    foreach (var candidate in all) if (candidate.GetComponentInParent<AAMAP.MapIcon>(true) == null) body.Add(candidate);
                    Visuals = body.ToArray();
                }
                bool found = false; Bounds bounds = default;
                foreach (var renderer in Visuals) {
                    if (renderer == null || !renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
                }
                return found ? new Vector3(bounds.center.x, bounds.max.y + topPadding, bounds.center.z)
                    : transform.position + Vector3.up * 1.5f;
            }
        }
    }
}

using UnityEngine;

namespace Capstone.Game.WorldFeedback {
    /// <summary>Optional scenery classification; physics and actor opacity remain independent.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraOccluder : MonoBehaviour {
        public Renderer[] renderers;
        public enum Kind { Auto, Wall, Column, Tree, Foliage, Glass, Ignore }
        public Kind kind;
        [Tooltip("Legacy actor classification. Actors are excluded from scenery cutaway.")]
        public bool actor;
        internal Renderer[] Resolve() {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            return renderers;
        }
    }
}

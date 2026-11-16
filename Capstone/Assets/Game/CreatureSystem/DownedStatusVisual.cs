using Capstone.Game.Skills;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Presentation only. The enemy owns the capture deadline and all gameplay state.</summary>
    [DisallowMultipleComponent]
    public sealed class DownedStatusVisual : MonoBehaviour {
        public GameObject effectPrefab;
        DummyEnemy enemy;
        GameObject effect;
        void Awake() { enemy = GetComponent<DummyEnemy>(); }
        void Update() {
            bool show = enemy != null && enemy.IsDowned && effectPrefab != null;
            if (!show) { Clear(); return; }
            if (effect != null) return;
            var collider = GetComponent<Collider>();
            float height = collider != null ? Mathf.Max(.6f, collider.bounds.max.y - transform.position.y) : 1.4f;
            effect = CombatEffectPool.Rent(effectPrefab, transform.position + Vector3.up * (height + .18f), Quaternion.identity, transform);
            if (effect != null) effect.transform.localScale = Vector3.one * Mathf.Clamp(height * .35f, .35f, 1.2f);
        }
        void Clear() { if (effect != null) CombatEffectPool.Return(effect); effect = null; }
        void OnDisable() { Clear(); }
    }
}

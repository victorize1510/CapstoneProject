using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Target-owned wounds survive source recall. No save state, coroutines or per-frame searches.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatBleed : MonoBehaviour {
        sealed class Wound {
            public GameObject source;
            public string sourceId, skill;
            public int cast, remaining;
            public float raw, next;
        }
        readonly List<Wound> wounds = new List<Wound>(4);
        DummyEnemy target;
        CreatureCombatant role;
        int sceneHandle;
        public int ActiveWounds => wounds.Count;
        public int TicksApplied { get; private set; }
        internal void ClearWounds() { wounds.Clear(); }
        void Awake() { target = GetComponent<DummyEnemy>(); role = GetComponent<CreatureCombatant>(); sceneHandle = gameObject.scene.handle; }
        void OnEnable() { SceneManager.activeSceneChanged += SceneChanged; }
        void OnDisable() { SceneManager.activeSceneChanged -= SceneChanged; wounds.Clear(); }
        void SceneChanged(Scene previous, Scene next) { wounds.Clear(); }
        public static void Apply(DummyEnemy victim, CombatHit hit, float rawPerTick) {
            if (victim == null || !victim.IsAlive || rawPerTick <= 0 || float.IsInfinity(rawPerTick) || float.IsNaN(rawPerTick)) return;
            var effect = victim.GetComponent<CombatBleed>();
            if (effect == null) effect = victim.gameObject.AddComponent<CombatBleed>();
            effect.Refresh(hit, rawPerTick);
        }
        void Refresh(CombatHit hit, float raw) {
            var metadata = hit.Source != null ? hit.Source.GetComponent<HudSystem.PetCollectionMetadata>() : null;
            string key = metadata != null ? metadata.PetId : hit.Source != null ? hit.Source.GetInstanceID().ToString() : "unknown";
            Wound wound = null;
            foreach (var existing in wounds) if (existing.sourceId == key) { wound = existing; break; }
            if (wound == null) {
                if (wounds.Count == 8) wounds.RemoveAt(0);
                wound = new Wound(); wounds.Add(wound);
            }
            wound.source = hit.Source; wound.sourceId = key; wound.skill = hit.SkillId;
            wound.cast = hit.CastId; wound.raw = raw; wound.remaining = 3; wound.next = Time.time + 1;
        }
        void Update() {
            if (target == null || !target.IsAlive || gameObject.scene.handle != sceneHandle || (role != null && !role.IsEnemy)) { wounds.Clear(); return; }
            for (int i = wounds.Count - 1; i >= 0; i--) {
                var wound = wounds[i];
                if (Time.time < wound.next) continue;
                // Missed/locked ticks are consumed, never replayed as a damage burst.
                int elapsed = Mathf.Min(wound.remaining, 1 + Mathf.FloorToInt(Time.time - wound.next));
                wound.next += elapsed; wound.remaining -= elapsed;
                for (int due = 0; due < elapsed && !target.CaptureLocked && target.IsAlive; due++) {
                    var hit = new CombatHit(wound.source, wound.skill, wound.cast, wound.raw,
                        CreatureDamageKind.Physical, false, true, target.TargetPosition);
                    if (target.ApplyCombatHit(hit).HpLost > 0) TicksApplied++;
                }
                if (i >= wounds.Count || !ReferenceEquals(wounds[i], wound)) break; // Death/capture may clear synchronously inside ApplyCombatHit.
                if (wound.remaining == 0 || !target.IsAlive) wounds.RemoveAt(i);
                if (!target.IsAlive) { wounds.Clear(); break; }
            }
        }
    }
}

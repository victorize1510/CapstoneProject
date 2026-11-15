using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    public enum CreatureDamageKind { Physical, Magical }
    /// <summary>Combat wiring; never changes the eight-stat progression formula or save format.</summary>
    public static class CreatureCombatRules {
        public static float Mitigate(float damage, int defense) => float.IsNaN(damage) || float.IsInfinity(damage) ? 0 : Mathf.Max(0, damage) * 100f / (100f + Mathf.Max(0, defense));
        public static float MovementScale(int speed) => Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1, speed) / 50f), .75f, 1.35f);
        public static float AttackIntervalScale(int speed) => Mathf.Clamp(1f / MovementScale(speed), .8f, 1.25f);
        public static float Offense(PetStatSnapshot stats, CreatureDamageKind kind, float power, float bonus, float criticalRoll) {
            float value = Mathf.Max(0, (kind == CreatureDamageKind.Magical ? stats.MagicAttack : stats.Attack) * power + bonus);
            return criticalRoll < stats.CriticalRate / 100f ? value * (1f + stats.CriticalDamage / 100f) : value;
        }
    }
    [DisallowMultipleComponent, RequireComponent(typeof(PetHudRuntimeStats), typeof(PetCollectionMetadata))]
    public sealed class CreatureCombatant : MonoBehaviour {
        PetHudRuntimeStats stats;
        PetCollectionMetadata metadata;
        PetController pet;
        DummyEnemy enemy;
        Collider body;
        PetStatSnapshot cachedStats;
        float refreshStatsAt;
        Animator collisionVisual;
        float offenseBuffUntil, offenseBuffMultiplier = 1;
        GameObject offenseBuffVisual;
        public float OffenseMultiplier => Time.time < offenseBuffUntil ? offenseBuffMultiplier : 1;
        public float OffenseBuffRemaining => Mathf.Max(0, offenseBuffUntil - Time.time);
        void Awake() { stats = GetComponent<PetHudRuntimeStats>(); metadata = GetComponent<PetCollectionMetadata>(); pet = GetComponent<PetController>(); enemy = GetComponent<DummyEnemy>(); body = GetComponent<Collider>(); pet?.BindCombatant(this); BindVisual(stats.VisualAnimator); }
        void OnEnable() { if (metadata != null) metadata.Changed += Invalidate; if (stats != null) stats.PersistentDataChanged += Invalidate; Invalidate(); }
        void OnDisable() { if (metadata != null) metadata.Changed -= Invalidate; if (stats != null) stats.PersistentDataChanged -= Invalidate; ClearOffenseBuff(); }
        void Update() {
            if (offenseBuffUntil > 0 && (Time.time >= offenseBuffUntil || !IsAlive || (pet != null && !pet.CanReceiveCommands))) ClearOffenseBuff();
        }
        public void ApplyOffenseBuff(float percent, float duration, GameObject visual) {
            if (!IsAlive || float.IsNaN(percent) || float.IsNaN(duration) || float.IsInfinity(percent) || float.IsInfinity(duration)) return;
            ClearOffenseBuff(); // Recast refreshes one modifier, not multiplicative stacking.
            offenseBuffMultiplier = 1 + Mathf.Clamp(percent, 0, 100) / 100;
            offenseBuffUntil = Time.time + Mathf.Clamp(duration, .1f, 60);
            if (visual != null) {
                offenseBuffVisual = Skills.CombatEffectPool.Rent(visual, Center, transform.rotation, transform);
                offenseBuffVisual.transform.localPosition = transform.InverseTransformPoint(Center);
                offenseBuffVisual.transform.localScale = Vector3.one * Mathf.Clamp(body != null ? body.bounds.extents.x * 1.6f : 1, .75f, 2);
            }
        }
        public void ClearOffenseBuff() {
            offenseBuffUntil = 0; offenseBuffMultiplier = 1;
            if (offenseBuffVisual != null) { Skills.CombatEffectPool.Return(offenseBuffVisual); offenseBuffVisual = null; }
        }
        void Invalidate() { refreshStatsAt = -1; }
        // Fit only when the active model changes, never against an animated pose every frame.
        // The same path runs for a newly loaded form and an in-place evolution.
        internal void BindVisual(Animator next) {
            if (next == null || collisionVisual == next) return;
            if (collisionVisual != null) ClearOffenseBuff();
            collisionVisual = next;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) return; // Preserve legacy box/character-controller contracts.
            bool found = false; Bounds bounds = default;
            foreach (var renderer in next.GetComponentsInChildren<Renderer>(true)) {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!found || bounds.size.sqrMagnitude < .001f) return;
            Vector3 size = transform.InverseTransformVector(bounds.size);
            capsule.radius = Mathf.Clamp(Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.z)) * .23f, .2f, .65f);
            capsule.height = Mathf.Max(capsule.radius * 2, Mathf.Abs(size.y));
            Vector3 center = transform.InverseTransformPoint(bounds.center);
            center.y = Mathf.Max(center.y, capsule.height * .5f + .04f);
            capsule.center = center; body = capsule;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) { agent.radius = capsule.radius; agent.height = capsule.height; }
        }
        PetStatSnapshot Snapshot { get { if (Time.time >= refreshStatsAt) { cachedStats = metadata.CurrentStats; refreshStatsAt = Time.time + .5f; } return cachedStats; } }
        public bool IsEnemy => enemy != null && enemy.enabled && (pet == null || pet.IsWildEncounter);
        public bool IsAlive => isActiveAndEnabled && stats != null && (IsEnemy ? enemy.IsAlive : stats.Health > 0);
        public bool CanBeAttacked => IsAlive && (IsEnemy ? !enemy.CaptureLocked : pet != null && pet.CanReceiveCommands);
        public Vector3 Center => body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up * .5f;
        public float MovementScale => CreatureCombatRules.MovementScale(Snapshot.Speed);
        public float AttackIntervalScale => CreatureCombatRules.AttackIntervalScale(Snapshot.Speed);
        public float Offense(CreatureDamageKind kind, float power = 1, float bonus = 0) => RollOffense(kind, power, bonus).RawDamage;
        public CombatOffenseRoll RollOffense(CreatureDamageKind kind, float power = 1, float bonus = 0) {
            var value = Snapshot; float roll = Random.value;
            return new CombatOffenseRoll(CreatureCombatRules.Offense(value, kind, power * OffenseMultiplier, bonus, roll), roll < value.CriticalRate / 100f);
        }
        public float NonCriticalOffense(CreatureDamageKind kind, float power) => CreatureCombatRules.Offense(Snapshot, kind, power * OffenseMultiplier, 0, 1);
        public float Mitigate(float damage, CreatureDamageKind kind) { var value = Snapshot; return CreatureCombatRules.Mitigate(damage, kind == CreatureDamageKind.Magical ? value.MagicDefense : value.Defense); }
        public bool ReceiveDamage(float rawDamage, CreatureDamageKind kind, GameObject source) {
            return ReceiveHit(new CombatHit(source, "basic", 0, rawDamage, kind, false, false, Center));
        }
        public bool ReceiveHit(CombatHit hit) {
            if (!CanBeAttacked || hit.RawDamage <= 0 || float.IsNaN(hit.RawDamage) || float.IsInfinity(hit.RawDamage)) return false;
            if (IsEnemy) return enemy.ApplyCombatHit(hit).HpLost > 0;
            float before = stats.Health;
            stats.SetStatus(Mathf.Max(0, stats.Health - Mitigate(hit.RawDamage, hit.Kind)), stats.MaxHealth, stats.Energy, stats.MaxEnergy);
            CombatDamageEvents.Publish(new CombatDamageResult(hit, gameObject, before - stats.Health, stats.Health <= 0));
            if (stats.Health <= 0) pet.HandleCombatDefeat();
            return true;
        }
        internal void SyncEnemyHealth(float current, float maximum) {
            if (stats != null) stats.SetStatus(current, maximum, stats.Energy, stats.MaxEnergy);
        }
    }
}

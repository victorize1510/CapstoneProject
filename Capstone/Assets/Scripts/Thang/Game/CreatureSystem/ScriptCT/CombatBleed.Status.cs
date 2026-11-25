using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    public readonly struct CombatStatusInfo {
        public readonly CombatAilment Kind;
        public readonly float Remaining, Value;
        public readonly int Count;
        public CombatStatusInfo(CombatAilment kind, float remaining, float value, int count) {
            Kind = kind; Remaining = remaining; Value = value; Count = count;
        }
    }

    public sealed partial class CombatBleed {
        sealed class Effect {
            public CombatAilment Kind;
            public int Source;
            public string Skill;
            public float Until, Value, NextTick;
        }

        readonly List<Effect> effects = new(12);
        float stunImmuneUntil;
        float fireContactUntil, nextFireContactCheck;
        Dictionary<int, float> fireContactCooldown;
        public event Action Changed;
        public event Action<CombatAilment> Applied;
        public CombatAilment ActiveAilments {
            get {
                var result = CombatAilment.None;
                foreach (var effect in effects) if (effect.Until > Time.time) result |= effect.Kind;
                foreach (var wound in wounds) result |= wound.kind;
                if (role != null && role.OffenseBuffRemaining > 0) result |= CombatAilment.PowerUp;
                return result;
            }
        }

        internal static CombatBleed For(GameObject actor) =>
            actor.GetComponent<CombatBleed>() ?? actor.AddComponent<CombatBleed>();

        internal void NotifyApplied(CombatAilment kind) {
            Changed?.Invoke();
            Applied?.Invoke(kind);
            Skills.CombatStatusVisuals.PlayStatus(this, target, kind);
        }

        internal void NotifyChanged() => Changed?.Invoke();

        public float Magnitude(CombatAilment kind) {
            float value = 0;
            foreach (var effect in effects)
                if (effect.Kind == kind && effect.Until > Time.time) value = Mathf.Max(value, effect.Value);
            return value;
        }

        public bool Has(CombatAilment kind) => (ActiveAilments & kind) != 0;

        internal void ApplyTimed(CombatAilment kind, float value, float seconds, GameObject source, string skill) {
            if (!target.Alive || !float.IsFinite(value) || !float.IsFinite(seconds) || seconds <= 0) return;
            if (kind == CombatAilment.Stun) {
                if (Time.time < stunImmuneUntil) return;
                if (target.Enemy != null && target.Enemy.IsBoss) seconds = Mathf.Min(.35f, seconds);
                stunImmuneUntil = Time.time + seconds + 3f;
                target.Object.GetComponent<Skills.PetCombatActionDriver>()?.Cancel();
            }
            int sourceId = source != null ? source.GetInstanceID() : 0;
            var effect = effects.Find(entry => entry.Kind == kind &&
                (kind == CombatAilment.Shield || kind == CombatAilment.Regeneration
                    || (entry.Source == sourceId && entry.Skill == skill)));
            if (effect == null) {
                if (effects.Count >= 32) return;
                effect = new Effect { Kind = kind, Source = sourceId, Skill = skill };
                effects.Add(effect);
            }
            effect.Value = value;
            effect.Until = Time.time + seconds;
            effect.NextTick = Time.time + 1f;
            NotifyApplied(kind);
        }

        internal void RemoveSourceEffect(CombatAilment kind, GameObject source) {
            if (source == null) return;
            int id = source.GetInstanceID();
            if (effects.RemoveAll(effect => effect.Kind == kind && effect.Source == id) > 0)
                Changed?.Invoke();
        }

        public bool TryRead(CombatAilment kind, out CombatStatusInfo info) {
            float remaining = 0, value = 0;
            int count = 0;
            foreach (var effect in effects) {
                if (effect.Kind != kind || effect.Until <= Time.time) continue;
                remaining = Mathf.Max(remaining, effect.Until - Time.time);
                value = Mathf.Max(value, effect.Value);
                count++;
            }
            foreach (var wound in wounds) {
                if (wound.kind != kind) continue;
                remaining = Mathf.Max(remaining, Mathf.Max(0, wound.next + wound.remaining - 1 - Time.time));
                value = Mathf.Max(value, wound.raw);
                count++;
            }
            if (kind == CombatAilment.PowerUp && role != null && role.OffenseBuffRemaining > 0) {
                remaining = Mathf.Max(remaining, role.OffenseBuffRemaining);
                value = Mathf.Max(value, role.OffenseMultiplier - 1f);
                count++;
            }
            info = new CombatStatusInfo(kind, remaining, value, kind == CombatAilment.Bleed ? count : Mathf.Min(count, 1));
            return count > 0;
        }

        internal float Absorb(float damage) {
            if (damage <= 0 || Has(CombatAilment.Invulnerable)) return 0;
            var shield = effects.Find(entry => entry.Kind == CombatAilment.Shield && entry.Until > Time.time);
            if (shield == null) return damage;
            float used = Mathf.Min(shield.Value, damage);
            shield.Value -= used;
            if (shield.Value <= .001f) effects.Remove(shield);
            Changed?.Invoke();
            return damage - used;
        }

        void TickEffects() {
            bool changed = false;
            for (int i = effects.Count - 1; i >= 0; i--) {
                var effect = effects[i];
                if (effect.Kind == CombatAilment.Regeneration && Time.time >= effect.NextTick && effect.NextTick <= effect.Until + .001f) {
                    int ticks = 1 + Mathf.FloorToInt(Mathf.Min(Time.time, effect.Until + .001f) - effect.NextTick);
                    effect.NextTick += ticks;
                    role?.HealFromStatus(effect.Value * ticks);
                }
                if (effect.Until > Time.time) continue;
                effects.RemoveAt(i);
                changed = true;
            }
            if (changed) Changed?.Invoke();
            TickFireContact();
        }

        internal void EnableFireContact() {
            fireContactUntil = Time.time + 10f;
            fireContactCooldown ??= new Dictionary<int, float>();
        }

        void TickFireContact() {
            if (fireContactUntil <= Time.time || Time.time < nextFireContactCheck || role == null) return;
            nextFireContactCheck = Time.time + .2f;
            foreach (var actor in CreatureCombatant.Active)
                if (actor != null) BurnOnFireContact(new CreatureCombatTarget(actor), actor.MaximumHealth);
            if (!role.IsEnemy)
                foreach (var enemy in DummyEnemy.ActiveEnemies)
                    if (enemy != null && !enemy.TryGetComponent<CreatureCombatant>(out _))
                        BurnOnFireContact(new CreatureCombatTarget(enemy), enemy.maxHealth);
        }

        void BurnOnFireContact(CreatureCombatTarget other, float maxHealth) {
            if (!other.Alive || other.IsEnemy == role.IsEnemy) return;

            Vector3 nearestOnOwner = role.ClosestPoint(other.Center);
            Vector3 nearestOnTarget = other.ClosestPoint(nearestOnOwner);
            if ((nearestOnTarget - nearestOnOwner).sqrMagnitude > 1.3f * 1.3f) return;

            int id = other.Object.GetInstanceID();
            if (fireContactCooldown.TryGetValue(id, out float next) && next > Time.time) return;
            if (fireContactCooldown.Count > 64) fireContactCooldown.Clear();
            fireContactCooldown[id] = Time.time + 1f;

            var otherStatus = For(other.Object);
            if (otherStatus.Has(CombatAilment.Burn)) return;
            float raw = Mathf.Min(role.MagicAttack * .08f, maxHealth * .008f);
            if (raw <= 0f) return;
            var hit = new CombatHit(gameObject, "shield.fire", 0, raw, CreatureDamageKind.Magical, false, true, other.Center);
            otherStatus.Refresh(hit, raw, CombatAilment.Burn, 10, true);
        }
    }
}

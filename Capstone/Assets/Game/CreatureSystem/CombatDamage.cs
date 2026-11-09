using System;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    public readonly struct CombatOffenseRoll {
        public readonly float RawDamage;
        public readonly bool Critical;
        public CombatOffenseRoll(float rawDamage, bool critical) { RawDamage = rawDamage; Critical = critical; }
    }

    /// <summary>One already-rolled attack. Presentation must never roll or calculate damage.</summary>
    public readonly struct CombatHit {
        public readonly GameObject Source;
        public readonly string SkillId;
        public readonly int CastId;
        public readonly float RawDamage;
        public readonly CreatureDamageKind Kind;
        public readonly bool Critical, DamageOverTime;
        public readonly Vector3 Point;
        public CombatHit(GameObject source, string skillId, int castId, float rawDamage,
            CreatureDamageKind kind, bool critical, bool damageOverTime, Vector3 point) {
            Source = source; SkillId = skillId; CastId = castId; RawDamage = rawDamage;
            Kind = kind; Critical = critical; DamageOverTime = damageOverTime; Point = point;
        }
    }

    public readonly struct CombatDamageResult {
        public readonly CombatHit Hit;
        public readonly GameObject Target;
        public readonly float HpLost;
        public readonly bool Killed;
        public CombatDamageResult(CombatHit hit, GameObject target, float hpLost, bool killed) {
            Hit = hit; Target = target; HpLost = hpLost; Killed = killed;
        }
    }

    public static class CombatDamageEvents {
        public static event Action<CombatDamageResult> Applied;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Applied = null; }
        internal static void Publish(CombatDamageResult result) {
            if (!(result.HpLost > 0) || float.IsInfinity(result.HpLost) || float.IsNaN(result.HpLost)) return;
            HudSystem.FloatingCombatText.ShowDamage(result);
            CombatFeedback.Show(result);
            Applied?.Invoke(result);
        }
    }
}

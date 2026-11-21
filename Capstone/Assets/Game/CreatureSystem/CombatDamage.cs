using System;
using Capstone.Game.Elements;
using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    public readonly struct CombatOffenseRoll
    {
        public readonly float RawDamage;
        public readonly bool Critical;
        public CombatOffenseRoll(float rawDamage, bool critical)
        {
            RawDamage = rawDamage;
            Critical = critical;
        }
    }

    /// <summary>Don da roll; presentation chi doc HP mat that, khong tinh lai damage.</summary>
    public readonly struct CombatHit
    {
        public readonly GameObject Source;
        public readonly string SkillId;
        public readonly int CastId;
        public readonly int EmissionIndex;
        public readonly int EmissionCount;
        public readonly float RawDamage;
        public readonly CreatureDamageKind Kind;
        public readonly bool Critical, DamageOverTime;
        public readonly Vector3 Point;
        public readonly CombatElementSnapshot Elements;
        public CombatHit(GameObject source, string skillId, int castId, float rawDamage, CreatureDamageKind kind, bool critical, bool damageOverTime, Vector3 point) : this(source, skillId, castId, rawDamage, kind, critical, damageOverTime, point, 0)
        {
        }

        public CombatHit(GameObject source, string skillId, int castId, float rawDamage, CreatureDamageKind kind, bool critical, bool damageOverTime, Vector3 point, int emissionIndex) : this(source, skillId, castId, rawDamage, kind, critical, damageOverTime, point, emissionIndex, default)
        {
        }

        public CombatHit(GameObject source, string skillId, int castId, float rawDamage, CreatureDamageKind kind, bool critical, bool damageOverTime, Vector3 point, int emissionIndex, CombatElementSnapshot elements)
            : this(source, skillId, castId, rawDamage, kind, critical, damageOverTime, point, emissionIndex, elements, 1)
        {
        }

        public CombatHit(GameObject source, string skillId, int castId, float rawDamage, CreatureDamageKind kind, bool critical, bool damageOverTime, Vector3 point, int emissionIndex, CombatElementSnapshot elements, int emissionCount)
        {
            Source = source;
            SkillId = skillId;
            CastId = castId;
            RawDamage = rawDamage;
            Kind = kind;
            Critical = critical;
            DamageOverTime = damageOverTime;
            Point = point;
            EmissionIndex = emissionIndex;
            EmissionCount = emissionCount;
            Elements = elements;
        }
    }

    public readonly struct CombatDamageResult
    {
        public readonly CombatHit Hit;
        public readonly GameObject Target;
        public readonly float HpLost;
        public readonly bool Killed;
        public readonly float Effectiveness;
        public CombatDamageResult(CombatHit hit, GameObject target, float hpLost, bool killed) : this(hit, target, hpLost, killed, 1)
        {
        }

        public CombatDamageResult(CombatHit hit, GameObject target, float hpLost, bool killed, float effectiveness)
        {
            Hit = hit;
            Target = target;
            HpLost = hpLost;
            Killed = killed;
            Effectiveness = effectiveness;
        }
    }

    public static class CombatDamageEvents
    {
        public static event Action<CombatDamageResult> Applied;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Applied = null;
        }

        internal static void Publish(CombatDamageResult result)
        {
            if (!(result.HpLost > 0) || float.IsInfinity(result.HpLost) || float.IsNaN(result.HpLost))
            {
                return;
            }

            PveCombatState.Record(result);
            HudSystem.FloatingCombatText.ShowDamage(result);
            if (result.Target != null) WorldFeedback.EnemyHealthBars.RevealHit(result.Target.GetComponent<DummyEnemy>());
            CombatFeedback.Show(result);
            Applied?.Invoke(result);
        }
    }
}

using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    /// <summary>Toan combat thuan; giu cong thuc tam chi so va dinh dang save.</summary>
    public static class CreatureCombatRules
    {
        public static float Mitigate(float damage, int defense) => float.IsNaN(damage) || float.IsInfinity(damage) ? 0 : Mathf.Max(0, damage) * 100f / (100f + Mathf.Max(0, defense));
        public static float MovementScale(int speed) => Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1, speed) / 50f), .75f, 1.35f);
        public static float AttackIntervalScale(int speed) => Mathf.Clamp(1f / MovementScale(speed), .8f, 1.25f);
        public static float Offense(PetStatSnapshot stats, CreatureDamageKind kind, float power, float bonus, float criticalRoll)
        {
            float value = Mathf.Max(0, (kind == CreatureDamageKind.Magical ? stats.MagicAttack : stats.Attack) * power + bonus);
            return criticalRoll < stats.CriticalRate / 100f ? value * (1f + stats.CriticalDamage / 100f) : value;
        }
    }
}

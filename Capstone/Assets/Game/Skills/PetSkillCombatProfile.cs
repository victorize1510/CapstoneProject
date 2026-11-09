using Capstone.Game.CreatureSystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    // Append values: existing assets serialize these enum indices.
    public enum PetSkillDelivery { Melee, Projectile, Targeted, SelfBuff }
    [CreateAssetMenu(menuName = "Capstone/Pet Skills/Combat", fileName = "Combat_")]
    public sealed class PetSkillCombatProfile : ScriptableObject {
        public CreatureDamageKind damageKind;
        public PetSkillDelivery delivery;
        [Min(.01f)] public float power = 1;
        [Min(.1f)] public float range = 3;
        [Min(.1f)] public float projectileSpeed = 9;
        [Range(.01f, 1)] public float projectileRadius = .18f;
        [Range(.1f, 10)] public float lifetime = 3;
        public GameObject projectileVisual;
        public string releaseSocket = "Mouth";
        [Header("Targeting: lock, nearest visible enemy, then forward")]
        public bool autoAcquireTarget = true;
        [Range(5, 89)] public float meleeHalfAngle = 55;
        [Min(.01f)] public float visualScale = .35f;
        [Header("Optional self buff (runtime only; refreshes, never stacks)")]
        [Range(0, 100)] public float offenseBuffPercent = 20;
        [Min(.1f)] public float buffDuration = 6;
        public GameObject buffVisual;
        [Header("Optional wound: three one-second physical ticks; refresh, no stacking per source")]
        [Range(0, 1)] public float bleedPower;
        [Header("Fast retreat before ranged casts (0 = no retreat)")]
        [Min(0)] public float retreatDistance;
        [Min(.1f)] public float retreatSpeed = 12;
        [Min(0)] public float preferredDistance = 5;
    }
}

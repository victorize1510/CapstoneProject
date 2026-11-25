using Capstone.Game.CreatureSystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    // Chi them gia tri cuoi enum; asset cu da serialize cac chi so nay.
    public enum PetSkillDelivery { Melee, Projectile, Targeted, SelfBuff }
    public enum PetSkillPattern { Default, Area, Cone, Beam, TravelingArea, ImpactArea }
    public enum PetSkillStatusValue { Fraction, DamageFraction, MagicAttackFraction, PoisonCap, MaxHealthFraction }
    [System.Serializable]
    public struct PetSkillStatusCue {
        public CombatAilment kind;
        [Range(0, 1)] public float chance;
        [Min(.1f)] public float duration;
        public float magnitude;
        public PetSkillStatusValue valueMode;
        public bool self;
    }
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
        [Min(0)] public float travelVisualWarmupSeconds;
        [Range(0, 2)] public float travelVisualTailSeconds;
        public string releaseSocket = "Mouth";
        [Tooltip("Cho phép chiêu cập nhật hướng hoặc điểm đánh theo mục tiêu đến lúc phát.")]
        public bool trackTargetUntilRelease;
        [Tooltip("Place the impact effect on the ground below the target.")]
        public bool impactAtTargetFeet;
        public bool SpawnsAtTarget => pattern == PetSkillPattern.Area ||
            (delivery == PetSkillDelivery.Targeted && pattern == PetSkillPattern.Default);
        public bool GroundsImpact => SpawnsAtTarget || impactAtTargetFeet;
        public float ObstacleRadius => authoredStatusRules && statusOnly ? Mathf.Min(projectileRadius, .18f) : projectileRadius;
        internal bool TracksUntilRelease => trackTargetUntilRelease || SpawnsAtTarget;
        internal bool RequiresTarget => delivery == PetSkillDelivery.Melee || SpawnsAtTarget;
        public float CastRange => delivery == PetSkillDelivery.Projectile || delivery == PetSkillDelivery.Targeted
            ? range + (authoredStatusRules ? 4f : 1f) : range;
        public float TravelRange => CastRange + 2f;
        [Min(.1f)] public float targetedImpactRadius = 1f;
        [Header("Ngam: khoa, dich gan nhin thay, roi huong dang quay")]
        public bool autoAcquireTarget = true;
        [Tooltip("Cho phép phóng theo hướng mục tiêu khi mục tiêu đang chọn ở ngoài tầm hoặc sau vật cản.")]
        public bool allowFreeCastWhenTargetUnavailable;
        [Range(5, 89)] public float meleeHalfAngle = 55;
        [Min(.01f)] public float visualScale = .35f;
        [Header("Buff ban than: chi runtime, lam moi thoi gian, khong cong don")]
        [Range(0, 100)] public float offenseBuffPercent = 20;
        [Min(.1f)] public float buffDuration = 6;
        public GameObject buffVisual;
        [Header("Chay mau: ba nhip moi giay, lam moi, khong cong don cung nguon")]
        [Range(0, 1)] public float bleedPower;
        [Header("Lui nhanh truoc don tam xa (0 = khong lui)")]
        [Min(0)] public float retreatDistance;
        [Min(.1f)] public float retreatSpeed = 12;
        [Min(0)] public float preferredDistance = 5;
        [Header("Ngan sach damage cho mot lan cast")]
        [Tooltip("Loat gio 2/3/4/5 phat theo trong so 10/40/40/10%; power la TONG cua ca loat.")]
        public bool weightedWindVolley;
        [Range(.01f,1)] public float burstEndNormalized = .78f;

        [Header("Combat mechanics for the 100 MP skill set")]
        public bool authoredStatusRules, statusOnly, interruptsCast, fireContact;
        public AudioClip authoredReleaseSound;
        public PetSkillPattern pattern;
        public int minimumHits = 1, maximumHits = 1;
        public float effectSeconds, splashFraction, displacement, interruptChance;
        public PetSkillStatusCue[] statusCues = System.Array.Empty<PetSkillStatusCue>();
    }
}

using Capstone.Game.CreatureSystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    // Chi them gia tri cuoi enum; asset cu da serialize cac chi so nay.
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
        [Header("Ngam: khoa, dich gan nhin thay, roi huong dang quay")]
        public bool autoAcquireTarget = true;
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
    }
}

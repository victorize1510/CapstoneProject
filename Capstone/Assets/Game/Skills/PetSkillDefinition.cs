using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>Shared authoring data. Progress and cooldown belong to PetHudRuntimeStats, never this asset.</summary>
    [CreateAssetMenu(menuName = "Capstone/Pet Skills/Skill", fileName = "Skill_")]
    public sealed class PetSkillDefinition : ScriptableObject {
        [Tooltip("Persistent key. Do not rename an ID already used by saves; keep old keys in Legacy IDs.")]
        public string skillId;
        public string[] legacyIds = System.Array.Empty<string>();
        public string displayName;
        [TextArea(2, 5)] public string description;
        public PetElement element;
        public Sprite icon;
        [Min(0)] public float cooldownSeconds;
        [Range(0, 100)] public float criticalRateBonus;
        [Range(0, 100)] public float criticalDamageBonus;
        [Header("Default animation (species can override)")]
        public string[] animatorStates = System.Array.Empty<string>();
        [Min(0.05f)] public float animationDuration = 0.85f;
        [Min(0)] public float animationFade = 0.08f;
        [Min(0)] public float windupSeconds;
        [Min(0)] public float recoverySeconds;
        public PetSkillPresentationProfile presentation;

        public string Id => (skillId ?? string.Empty).Trim();
        public SkillHudData CreateHudData(int requiredLevel = 1, bool unlocked = true) => new SkillHudData {
            skillId = Id, legacySkillIds = legacyIds, displayName = displayName, description = description,
            element = element, icon = icon, cooldownSeconds = Mathf.Max(0, cooldownSeconds),
            criticalRateBonus = criticalRateBonus, criticalDamageBonus = criticalDamageBonus,
            animatorStates = animatorStates, animationDuration = animationDuration, animationFade = animationFade,
            windupSeconds = windupSeconds, recoverySeconds = recoverySeconds,
            requiredPetLevel = Mathf.Max(1, requiredLevel), skillLevel = 1, unlocked = unlocked, usable = true
        };
    }
}

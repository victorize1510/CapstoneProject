using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.Skills
{
    /// <summary>Du lieu authoring chung; tien trinh va cooldown thuoc runtime, khong luu tren asset nay.</summary>
    [CreateAssetMenu(menuName = "Capstone/Pet Skills/Skill", fileName = "Skill_")]
    public sealed class PetSkillDefinition : ScriptableObject
    {
        [Tooltip("Khoa on dinh. Khong doi ID da dung trong save; giu khoa cu trong Legacy IDs.")]
        public string skillId;
        public string[] legacyIds = System.Array.Empty<string>();
        public string displayName;
        [TextArea(2, 5)]
        public string description;
        [Capstone.Game.Elements.SupportedElement]
        public PetElement element;
        public Sprite icon;
        [Min(0)]
        public float cooldownSeconds;
        [Min(0), Tooltip("Mana dung khi tung chieu. Chi co combat va gia tri 0 dung mac dinh 10.")]
        public float manaCost;
        [Range(0, 100)]
        public float criticalRateBonus;
        [Range(0, 100)]
        public float criticalDamageBonus;
        [Header("Animation mac dinh - loai co the override")]
        public string[] animatorStates = System.Array.Empty<string>();
        [Min(0.05f)]
        public float animationDuration = 0.85f;
        [Min(0)]
        public float animationFade = 0.08f;
        [Min(0)]
        public float windupSeconds;
        [Min(0)]
        public float recoverySeconds;
        public PetSkillPresentationProfile presentation;
        [Header("Thuc thi combat dung chung giua cac loai")]
        public PetSkillCombatProfile combat;
        [Header("Mo ta UI tuy chon - khong sinh co che combat")]
        public SkillUiMetadata uiMetadata = new SkillUiMetadata();
        public float ManaCost => manaCost > 0f ? manaCost : combat != null ? 10f : 0f;
        public string Id => (skillId ?? string.Empty).Trim();
        public SkillHudData CreateHudData(int requiredLevel = 1, bool unlocked = true) => new SkillHudData{skillId = Id, legacySkillIds = legacyIds, displayName = displayName, description = description, element = Capstone.Game.Elements.SkillElementAssignments.Resolve(Id, element), icon = icon, cooldownSeconds = Mathf.Max(0, cooldownSeconds), manaCost = ManaCost, combatProfile = combat, uiMetadata = uiMetadata, criticalRateBonus = criticalRateBonus, criticalDamageBonus = criticalDamageBonus, animatorStates = animatorStates, animationDuration = animationDuration, animationFade = animationFade, windupSeconds = windupSeconds, recoverySeconds = recoverySeconds, requiredPetLevel = Mathf.Max(1, requiredLevel), unlocked = unlocked, usable = true};
    }
}

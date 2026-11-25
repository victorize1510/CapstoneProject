using System;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    public enum PetEvolutionTrigger {
        Level,
        Item,
        LevelOrItem
    }

    [Serializable]
    public sealed class PetEvolutionRule {
        [Header("Giai doan")]
        [SerializeField] string evolutionId = string.Empty;
        [SerializeField, Min(0)] int sourceStage;
        [SerializeField] string targetFormId = string.Empty;

        [Header("Dang dich")]
        [SerializeField] string targetDisplayName = string.Empty;
        [SerializeField] string targetSpecies = string.Empty;
        [SerializeField] Sprite targetIcon = null;
        [SerializeField, Capstone.Game.Elements.SupportedElement(true)] PetElement targetElement = PetElement.Unknown;
        [SerializeField, Capstone.Game.Elements.SupportedElement(true)] PetElement targetSecondaryElement = PetElement.Unknown;
        [Tooltip("Cho phep xoa he phu cua form legacy khi rule khong khai bao he phu.")]
        [SerializeField] bool replaceSecondaryElement;
        [SerializeField] PetRarity targetRarity = PetRarity.Unknown;
        [Tooltip("Prefab chi chua model. Gan PetEvolutionVisualController truoc khi dung.")]
        [SerializeField] GameObject targetVisualPrefab = null;

        [Header("Dieu kien")]
        [SerializeField] PetEvolutionTrigger trigger = PetEvolutionTrigger.Level;
        [SerializeField, Min(1)] int requiredLevel = 1;
        [SerializeField] string requiredItemId = string.Empty;
        [SerializeField, Min(1)] int requiredItemQuantity = 1;

        [Header("Tang chi so mot lan")]
        [SerializeField, Min(1f)] float maxHealthMultiplier = 1.15f;
        [SerializeField, Min(0)] int attackBonus;
        [SerializeField, Min(0)] int defenseBonus;
        [SerializeField, Min(0)] int speedBonus;
        [SerializeField, Min(0)] int magicAttackBonus;
        [SerializeField, Min(0)] int magicDefenseBonus;

        public string EvolutionId => string.IsNullOrWhiteSpace(evolutionId)
            ? $"stage-{SourceStage}-to-{TargetStage}-{TargetFormId}"
            : evolutionId.Trim();
        public int SourceStage => Mathf.Max(0, sourceStage);
        public int TargetStage => SourceStage + 1;
        public string TargetFormId => string.IsNullOrWhiteSpace(targetFormId)
            ? $"form-{TargetStage}"
            : targetFormId.Trim();
        public string TargetDisplayName => Capstone.Game.CreatureSystem.CreatureDisplayNames.LocalizeSystemName(targetDisplayName?.Trim());
        public string TargetSpecies => targetSpecies?.Trim() ?? string.Empty;
        public Sprite TargetIcon => targetIcon;
        public PetElement TargetElement => targetElement;
        public PetElement TargetSecondaryElement => targetSecondaryElement;
        public bool ReplaceSecondaryElement => replaceSecondaryElement;
        public PetRarity TargetRarity => targetRarity;
        public GameObject TargetVisualPrefab => targetVisualPrefab;
        public PetEvolutionTrigger Trigger => trigger;
        public int RequiredLevel => Mathf.Max(1, requiredLevel);
        public string RequiredItemId => requiredItemId?.Trim() ?? string.Empty;
        public int RequiredItemQuantity => Mathf.Max(1, requiredItemQuantity);
        public float MaxHealthMultiplier => Mathf.Max(1f, maxHealthMultiplier);
        public int AttackBonus => Mathf.Max(0, attackBonus);
        public int DefenseBonus => Mathf.Max(0, defenseBonus);
        public int SpeedBonus => Mathf.Max(0, speedBonus);
        public int MagicAttackBonus => Mathf.Max(0, magicAttackBonus);
        public int MagicDefenseBonus => Mathf.Max(0, magicDefenseBonus);

        public static PetEvolutionRule CreateLegacy(
            string targetName,
            Sprite targetSprite,
            int levelRequirement,
            string itemRequirement) {
            var rule = new PetEvolutionRule {
                evolutionId = "legacy-stage-0",
                sourceStage = 0,
                targetFormId = "legacy-form-1",
                targetDisplayName = targetName ?? string.Empty,
                targetSpecies = targetName ?? string.Empty,
                targetIcon = targetSprite,
                requiredLevel = Mathf.Max(1, levelRequirement),
                trigger = string.IsNullOrWhiteSpace(itemRequirement)
                    ? PetEvolutionTrigger.Level
                    : PetEvolutionTrigger.Item,
                requiredItemId = itemRequirement ?? string.Empty,
                requiredItemQuantity = 1
            };
            return rule;
        }
    }
}

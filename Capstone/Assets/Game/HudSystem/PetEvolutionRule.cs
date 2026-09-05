using System;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    public enum PetEvolutionTrigger {
        Level,
        Item
    }

    [Serializable]
    public sealed class PetEvolutionRule {
        [Header("Stage")]
        [SerializeField] string evolutionId = string.Empty;
        [SerializeField, Min(0)] int sourceStage;
        [SerializeField] string targetFormId = string.Empty;

        [Header("Target Form")]
        [SerializeField] string targetDisplayName = string.Empty;
        [SerializeField] string targetSpecies = string.Empty;
        [SerializeField] Sprite targetIcon = null;
        [SerializeField] PetElement targetElement = PetElement.Unknown;
        [SerializeField] PetRarity targetRarity = PetRarity.Unknown;
        [Tooltip("Optional model-only prefab. Assign a PetEvolutionVisualController before using this field.")]
        [SerializeField] GameObject targetVisualPrefab = null;

        [Header("Requirement")]
        [SerializeField] PetEvolutionTrigger trigger = PetEvolutionTrigger.Level;
        [SerializeField, Min(1)] int requiredLevel = 1;
        [SerializeField] string requiredItemId = string.Empty;
        [SerializeField, Min(1)] int requiredItemQuantity = 1;

        [Header("One-time Stat Growth")]
        [SerializeField, Min(1f)] float maxHealthMultiplier = 1.15f;
        [SerializeField, Min(0)] int attackBonus;
        [SerializeField, Min(0)] int defenseBonus;
        [SerializeField, Min(0)] int speedBonus;

        public string EvolutionId => string.IsNullOrWhiteSpace(evolutionId)
            ? $"stage-{SourceStage}-to-{TargetStage}-{TargetFormId}"
            : evolutionId.Trim();
        public int SourceStage => Mathf.Max(0, sourceStage);
        public int TargetStage => SourceStage + 1;
        public string TargetFormId => string.IsNullOrWhiteSpace(targetFormId)
            ? $"form-{TargetStage}"
            : targetFormId.Trim();
        public string TargetDisplayName => targetDisplayName?.Trim() ?? string.Empty;
        public string TargetSpecies => targetSpecies?.Trim() ?? string.Empty;
        public Sprite TargetIcon => targetIcon;
        public PetElement TargetElement => targetElement;
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

using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    [Serializable]
    public sealed class CreatureFormDefinition {
        public string formId;
        public string displayName;
        [Tooltip("Existing authored prefab. May contain gameplay scripts: this is NOT a model-only spawn contract.")]
        public GameObject sourcePrefab;
        public Sprite avatar;
        [Tooltip("Optional large 2D artwork, independent of the slot avatar. Supplied by the artist.")]
        public Sprite heroPortrait;
        public PetStatProfile statProfile;
        public PetSpeciesSkillSet skillSet;
        [Header("Verified runtime assets (optional until this form is integrated)")]
        public GameObject visualPrefab;
        public GameObject petPrefab;
        public GameObject wildPrefab;
        public GameObject enemyPrefab;
        public CreatureAnimationProfile animationProfile;
    }

    /// <summary>Shared authoring data only. Never stores an individual's health, genetics, cooldown or progress.</summary>
    [CreateAssetMenu(menuName = "Capstone/Creatures/Family", fileName = "Family_")]
    public sealed class CreatureFamilyDefinition : ScriptableObject {
        [Tooltip("Stable lookup key. Existing save IDs must not be renamed.")]
        public string definitionId;
        public string displayName;
        public string[] legacyDefinitionIds = Array.Empty<string>();
        [Tooltip("Review flag only, never enables or modifies gameplay evolution rules.")]
        public bool evolutionRulesReviewed;
        public List<CreatureFormDefinition> forms = new List<CreatureFormDefinition>();
    }
}

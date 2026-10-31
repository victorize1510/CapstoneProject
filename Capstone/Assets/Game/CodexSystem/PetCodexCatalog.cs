using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.CodexSystem {
    [Serializable]
    public sealed class PetCodexCatalogEntry {
        [Tooltip("Codex identity only; does not replace gameplay DefinitionId or FormId.")]
        public string id;
        [Min(1)] public int number = 1;
        public GameObject sourcePrefab;
        [Tooltip("Optional large illustration, distinct from the pet's slot avatar.")]
        public Sprite portrait;
        public string region, height, weight, habitat, trait, favoriteFood;
        public PetCollectionMetadata Metadata => sourcePrefab != null ? sourcePrefab.GetComponentInChildren<PetCollectionMetadata>(true) : null;
        public Sprite Icon => sourcePrefab != null ? sourcePrefab.GetComponentInChildren<PetHudRuntimeStats>(true)?.Icon : null;
        public bool Matches(PetCollectionMetadata pet) {
            var source = Metadata;
            if (pet == null || source == null) return false;
            return string.Equals(source.DefinitionId, pet.DefinitionId, StringComparison.OrdinalIgnoreCase)
                && source.EvolutionStage == pet.EvolutionStage;
        }
    }

    [CreateAssetMenu(menuName = "Capstone/Pet Codex/Catalog", fileName = "PetCodexCatalog")]
    public sealed class PetCodexCatalog : ScriptableObject {
        public const string ResourcePath = "PetCodex/PetCodexCatalog";
        public List<PetCodexCatalogEntry> species = new List<PetCodexCatalogEntry>();
        public List<MonsterItemDefinition> evolutionItems = new List<MonsterItemDefinition>();
        static PetCodexCatalog cachedDefault;
        public static PetCodexCatalog Default => cachedDefault != null ? cachedDefault : cachedDefault = Resources.Load<PetCodexCatalog>(ResourcePath);
        public PetCodexCatalogEntry Find(PetCollectionMetadata metadata) {
            if (species != null) foreach (var entry in species) if (entry != null && entry.Matches(metadata)) return entry;
            return null;
        }
        public PetCodexCatalogEntry FindForm(string definitionId, string formId) {
            if (species != null) foreach (var entry in species) {
                var source = entry?.Metadata;
                if (source != null && string.Equals(source.DefinitionId, definitionId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(source.CurrentFormId, formId, StringComparison.OrdinalIgnoreCase)) return entry;
            }
            return null;
        }
        public static string NumberFor(PetCollectionMetadata metadata, string fallback) {
            var entry = Default?.Find(metadata);
            return entry != null ? entry.number.ToString("D2") : fallback ?? string.Empty;
        }
    }
}

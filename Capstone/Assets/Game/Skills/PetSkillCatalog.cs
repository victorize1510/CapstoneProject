using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.Skills {
    [CreateAssetMenu(menuName = "Capstone/Pet Skills/Catalog", fileName = "PetSkillCatalog")]
    public sealed class PetSkillCatalog : ScriptableObject {
        public List<PetSkillDefinition> skills = new List<PetSkillDefinition>();
        [Tooltip("Optional auto-binding for scene pets, matched against PetCollectionMetadata.Species.")]
        public List<PetSpeciesSkillSet> speciesSets = new List<PetSpeciesSkillSet>();
        public PetSpeciesSkillSet FindSpecies(string speciesId) {
            if (string.IsNullOrWhiteSpace(speciesId) || speciesSets == null) return null;
            foreach (var set in speciesSets) if (set != null && string.Equals(set.speciesId?.Trim(), speciesId.Trim(), StringComparison.OrdinalIgnoreCase)) return set;
            return null;
        }
        readonly Dictionary<string, PetSkillDefinition> byId = new Dictionary<string, PetSkillDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> errors = new List<string>();
        bool cached;
        void OnEnable() => Invalidate();
        void OnValidate() => Invalidate();
        public void Invalidate() { cached = false; byId.Clear(); errors.Clear(); }
        public bool TryGet(string id, out PetSkillDefinition skill) {
            EnsureCache(); skill = null;
            return !string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id.Trim(), out skill) && skill != null;
        }
        public IReadOnlyList<string> ValidateCatalog() { EnsureCache(); return errors; }
        void EnsureCache() {
            if (cached) return;
            cached = true; byId.Clear(); errors.Clear();
            if (skills == null) { errors.Add("Catalog list is null."); return; }
            foreach (var skill in skills) {
                if (skill == null) { errors.Add("Missing skill reference."); continue; }
                if (string.IsNullOrWhiteSpace(skill.Id)) { errors.Add(skill.name + ": missing ID."); continue; }
                AddKey(skill.Id, skill);
                if (skill.legacyIds != null) foreach (string alias in skill.legacyIds) AddKey(alias, skill);
            }
            var speciesIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (speciesSets != null) foreach (var set in speciesSets) {
                if (set == null) { errors.Add("Missing species set reference."); continue; }
                if (!speciesIds.Add((set.speciesId ?? string.Empty).Trim())) errors.Add("Ambiguous auto-binding species: " + set.speciesId);
                if (set.catalog != this) errors.Add(set.name + ": species points to a different catalog.");
            }
        }
        void AddKey(string key, PetSkillDefinition skill) {
            if (string.IsNullOrWhiteSpace(key)) return;
            key = key.Trim();
            if (byId.TryGetValue(key, out var existing)) {
                if (existing != skill) errors.Add("Ambiguous skill ID/alias: " + key);
                return;
            }
            byId.Add(key, skill);
        }
    }
}

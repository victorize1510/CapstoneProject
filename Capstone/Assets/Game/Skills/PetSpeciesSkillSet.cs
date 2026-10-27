using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    [Serializable]
    public sealed class PetSpeciesSkillEntry {
        public PetSkillDefinition skill;
        [Min(1)] public int learnAtLevel = 1;
        [Tooltip("Use this species' animation names/timing rather than the common skill animation.")]
        public bool overrideAnimation;
        public string[] animatorStates = Array.Empty<string>();
        [Min(0.05f)] public float animationDuration = 0.85f;
        [Min(0)] public float animationFade = 0.08f;
        [Min(0)] public float windupSeconds;
        [Min(0)] public float recoverySeconds;
        [Tooltip("Optional species-specific VFX/audio variant.")]
        public PetSkillPresentationProfile presentationOverride;

        public SkillHudData CreateHudData(int petLevel) {
            if (skill == null) return default;
            var data = skill.CreateHudData(learnAtLevel, petLevel >= learnAtLevel);
            if (overrideAnimation) {
                data.animatorStates = animatorStates; data.animationDuration = animationDuration;
                data.animationFade = animationFade; data.windupSeconds = windupSeconds; data.recoverySeconds = recoverySeconds;
            }
            return data;
        }
        public PetSkillPresentationProfile Presentation => presentationOverride != null ? presentationOverride : skill != null ? skill.presentation : null;
    }

    [CreateAssetMenu(menuName = "Capstone/Pet Skills/Species Skill Set", fileName = "Species_")]
    public sealed class PetSpeciesSkillSet : ScriptableObject {
        public string speciesId;
        public PetSkillCatalog catalog;
        public List<PetSpeciesSkillEntry> learnset = new List<PetSpeciesSkillEntry>();
        [Tooltip("Initial equipped skills for a NEW pet only. Loading a save never refills empty slots.")]
        public List<PetSkillDefinition> defaultLoadout = new List<PetSkillDefinition>();
        readonly Dictionary<string, PetSpeciesSkillEntry> byId = new Dictionary<string, PetSpeciesSkillEntry>(StringComparer.OrdinalIgnoreCase);
        bool cached;
        void OnEnable() => Invalidate();
        void OnValidate() => Invalidate();
        public void Invalidate() { cached = false; byId.Clear(); }
        public bool TryGet(string id, out PetSpeciesSkillEntry entry) {
            entry = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (!cached) {
                cached = true; byId.Clear();
                if (learnset != null) foreach (var item in learnset) {
                    if (item?.skill == null || string.IsNullOrWhiteSpace(item.skill.Id)) continue;
                    byId.TryAdd(item.skill.Id, item);
                    if (item.skill.legacyIds != null) foreach (var alias in item.skill.legacyIds)
                        if (!string.IsNullOrWhiteSpace(alias)) byId.TryAdd(alias.Trim(), item);
                }
            }
            return byId.TryGetValue(id.Trim(), out entry);
        }
        public List<string> ValidateSet() {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(speciesId)) errors.Add("Missing species ID.");
            if (catalog == null) errors.Add("Missing catalog.");
            else foreach (var error in catalog.ValidateCatalog()) errors.Add(error);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (learnset != null) foreach (var entry in learnset) {
                if (entry?.skill == null) { errors.Add("Missing learnset skill."); continue; }
                if (!seen.Add(entry.skill.Id)) errors.Add("Duplicate learnset skill: " + entry.skill.Id);
                if (catalog != null && (!catalog.TryGet(entry.skill.Id, out var defined) || defined != entry.skill)) errors.Add("Skill is not in catalog: " + entry.skill.Id);
                if (entry.learnAtLevel < 1) errors.Add("Invalid learn level: " + entry.skill.Id);
            }
            seen.Clear();
            if (defaultLoadout != null) foreach (var skill in defaultLoadout) {
                if (skill == null || !TryGet(skill.Id, out _)) errors.Add("Default skill is not in learnset.");
                else if (!seen.Add(skill.Id)) errors.Add("Duplicate default skill: " + skill.Id);
            }
            return errors;
        }
    }
}

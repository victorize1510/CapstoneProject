using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.Skills
{
    [Serializable]
    public sealed class PetSpeciesSkillEntry
    {
        public PetSkillDefinition skill;
        [Min(1)]
        public int learnAtLevel = 1;
        [Tooltip("Dung ten va timing animation rieng cua loai thay cho animation chung.")]
        public bool overrideAnimation;
        public string[] animatorStates = Array.Empty<string>();
        [Min(0.05f)]
        public float animationDuration = 0.85f;
        [Min(0)]
        public float animationFade = 0.08f;
        [Min(0)]
        public float windupSeconds;
        [Min(0)]
        public float recoverySeconds;
        [Tooltip("Bien the VFX/am thanh rieng cua loai neu co.")]
        public PetSkillPresentationProfile presentationOverride;
        public SkillHudData CreateHudData(int petLevel)
        {
            if (skill == null)
            {
                return default;
            }

            var data = skill.CreateHudData(learnAtLevel, petLevel >= learnAtLevel);
            if (overrideAnimation)
            {
                data.animatorStates = animatorStates;
                data.animationDuration = animationDuration;
                data.animationFade = animationFade;
                data.windupSeconds = windupSeconds;
                data.recoverySeconds = recoverySeconds;
            }

            return data;
        }

        public PetSkillPresentationProfile Presentation => presentationOverride != null ? presentationOverride : skill != null ? skill.presentation : null;
    }

    [CreateAssetMenu(menuName = "Capstone/Pet Skills/Species Skill Set", fileName = "Species_")]
    public sealed class PetSpeciesSkillSet : ScriptableObject
    {
        public string speciesId;
        [Min(0), Tooltip("Du lieu MP cu de tuong thich. Runtime da retire MP; khong cap nang luong tu truong nay.")]
        public float startingMana = 100;
        public PetSkillCatalog catalog;
        [Tooltip("Chi dung cho ca the Wild MOI trong EncounterLevel. Mac dinh tat de giu hanh vi cac bo cu; khong ap khi capture/load/recall.")]
        public bool initializeFreshWildLoadout;
        public List<PetSpeciesSkillEntry> learnset = new List<PetSpeciesSkillEntry>();
        [Tooltip("Loadout ban dau chi cho pet moi. Load save khong tu lap day o trong.")]
        public List<PetSkillDefinition> defaultLoadout = new List<PetSkillDefinition>();

        // Tra ve du lieu moi, khong sua set chung hay loadout dang luu cua ca the.
        public List<SkillHudData> CreateFreshDefaultLoadout(int level, int maximumSlots = 4)
        {
            var result = new List<SkillHudData>();
            if (level < 1 || maximumSlots < 1 || defaultLoadout == null) return result;
            int limit = Math.Min(4, maximumSlots);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in defaultLoadout)
            {
                if (result.Count >= limit) break;
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id)
                    || !TryGet(definition.Id, out var entry) || entry.learnAtLevel > level
                    || !seen.Add(definition.Id)) continue;
                result.Add(entry.CreateHudData(level));
            }
            return result;
        }
        readonly Dictionary<string, PetSpeciesSkillEntry> byId = new Dictionary<string, PetSpeciesSkillEntry>(StringComparer.OrdinalIgnoreCase);
        bool cached;
        void OnEnable() => Invalidate();
        void OnValidate() => Invalidate();
        public void Invalidate()
        {
            cached = false;
            byId.Clear();
        }

        public bool TryGet(string id, out PetSpeciesSkillEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (!cached)
            {
                cached = true;
                byId.Clear();
                if (learnset != null)
                {
                    foreach (var item in learnset)
                    {
                        if (item?.skill == null || string.IsNullOrWhiteSpace(item.skill.Id))
                        {
                            continue;
                        }

                        byId.TryAdd(item.skill.Id, item);
                        if (item.skill.legacyIds != null)
                        {
                            foreach (var alias in item.skill.legacyIds)
                            {
                                if (!string.IsNullOrWhiteSpace(alias))
                                {
                                    byId.TryAdd(alias.Trim(), item);
                                }
                            }
                        }
                    }
                }
            }

            return byId.TryGetValue(id.Trim(), out entry);
        }

        public List<string> ValidateSet()
        {
            // Learnset va alias co the doi ma khong goi OnValidate tren set nay.
            Invalidate();
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                errors.Add("Missing species ID.");
            }

            if (catalog == null)
            {
                errors.Add("Missing catalog.");
            }
            else
            {
                foreach (var error in catalog.ValidateCatalog())
                {
                    errors.Add(error);
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (learnset != null)
            {
                foreach (var entry in learnset)
                {
                    if (entry?.skill == null)
                    {
                        errors.Add("Missing learnset skill.");
                        continue;
                    }

                    if (!seen.Add(entry.skill.Id))
                    {
                        errors.Add("Duplicate learnset skill: " + entry.skill.Id);
                    }

                    if (catalog != null && (!catalog.TryGet(entry.skill.Id, out var defined) || defined != entry.skill))
                    {
                        errors.Add("Skill is not in catalog: " + entry.skill.Id);
                    }

                    if (entry.learnAtLevel < 1)
                    {
                        errors.Add("Invalid learn level: " + entry.skill.Id);
                    }
                }
            }

            seen.Clear();
            if (defaultLoadout != null)
            {
                foreach (var skill in defaultLoadout)
                {
                    if (skill == null || !TryGet(skill.Id, out _))
                    {
                        errors.Add("Default skill is not in learnset.");
                    }
                    else if (!seen.Add(skill.Id))
                    {
                        errors.Add("Duplicate default skill: " + skill.Id);
                    }
                }
            }

            return errors;
        }
    }
}

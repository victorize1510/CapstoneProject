using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    public readonly struct PetStatSnapshot {
        public readonly int Hp, Attack, Defense, MagicAttack, MagicDefense, Speed, MaxHealth;
        public readonly float CriticalRate, CriticalDamage;

        public PetStatSnapshot(int hp, int attack, int defense, int magicAttack, int magicDefense,
            int speed, int maxHealth, float criticalRate, float criticalDamage) {
            Hp = hp; Attack = attack; Defense = defense; MagicAttack = magicAttack;
            MagicDefense = magicDefense; Speed = speed; MaxHealth = maxHealth;
            CriticalRate = criticalRate; CriticalDamage = criticalDamage;
        }
    }

    public static class PetStatCalculator {
        public const int MaxLevel = 100;
        public const int PetHealthMultiplier = 4;
        public const int MaximumIndividualValue = 31;
        public const int MaximumEffortPerStat = 252;
        public const int MaximumEffortTotal = 510;

        static readonly PetStatValues Neutral = new PetStatValues(70, 70, 70, 70, 70, 70);
        static readonly HashSet<string> MissingProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static PetStatProfile[] profiles;

        public static int CalculateCoreStat(int baseStat, int iv, int ev, int level,
            PetCoreStat stat, string nature) {
            int clampedLevel = Mathf.Clamp(level, 1, MaxLevel);
            int scaled = ((2 * Mathf.Clamp(baseStat, 1, 255) + Mathf.Clamp(iv, 0, MaximumIndividualValue)
                + Mathf.Clamp(ev, 0, MaximumEffortPerStat) / 4) * clampedLevel) / 100;
            if (stat == PetCoreStat.Hp) return scaled + clampedLevel + 10;
            int result = scaled + 5;
            float multiplier = PetNature.Multiplier(nature, stat);
            return multiplier > 1f ? result * 11 / 10 : multiplier < 1f ? result * 9 / 10 : result;
        }

        public static int CalculateMaxHealth(int baseHp, int iv, int ev, int level) =>
            CalculateCoreStat(baseHp, iv, ev, level, PetCoreStat.Hp, "Serious") * PetHealthMultiplier;

        public static float DisplayRatio(int value, int level, PetCoreStat stat) {
            PetStatValues reference = GetDisplayBaseMaximum();
            int maximum = CalculateCoreStat(reference.Get(stat), MaximumIndividualValue,
                MaximumEffortPerStat, level, stat, "Serious");
            maximum = stat == PetCoreStat.Hp ? maximum * PetHealthMultiplier : maximum * 11 / 10;
            return maximum > 0 ? Mathf.Clamp01(value / (float)maximum) : 0f;
        }

        public static PetStatValues GetBaseStats(PetCollectionMetadata metadata) {
            PetStatProfile profile = ResolveProfile(metadata, out _);
            return profile != null ? profile.BaseStats : Neutral;
        }

        public static int GetBaseStatTotal(PetCollectionMetadata metadata) => GetBaseStats(metadata).Total;

        public static PetStatSnapshot Calculate(PetCollectionMetadata metadata, int level) {
            PetStatProfile profile = ResolveProfile(metadata, out string missingKey);
            PetStatValues baseStats = profile != null ? profile.BaseStats : Neutral;
            if (profile == null && MissingProfiles.Add(missingKey))
                Debug.LogWarning($"Pet stat profile missing for '{missingKey}'; using neutral 420 BST.");

            PetStatValues iv = metadata != null ? metadata.IndividualValues : default;
            PetStatValues ev = metadata != null ? metadata.EffortValues : default;
            string nature = metadata != null ? metadata.Nature : "Serious";
            int hp = CalculateMaxHealth(baseStats.hp, iv.hp, ev.hp, level);
            float rate = profile != null ? profile.CriticalRate : 6.25f;
            float damage = profile != null ? profile.CriticalDamage : 50f;
            PetHudRuntimeStats runtime = metadata != null ? metadata.GetComponentInChildren<PetHudRuntimeStats>(true) : null;
            if (runtime != null) {
                IReadOnlyList<SkillHudData> equipped = runtime.GetSkills();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < equipped.Count; i++) {
                    SkillHudData skill = equipped[i];
                    if (!skill.unlocked) continue;
                    string key = string.IsNullOrWhiteSpace(skill.skillId) ? skill.displayName : skill.skillId;
                    if (!string.IsNullOrWhiteSpace(key) && !seen.Add(key.Trim())) continue;
                    rate += skill.criticalRateBonus;
                    damage += skill.criticalDamageBonus;
                }
            }

            return new PetStatSnapshot(hp,
                CalculateCoreStat(baseStats.attack, iv.attack, ev.attack, level, PetCoreStat.Attack, nature),
                CalculateCoreStat(baseStats.defense, iv.defense, ev.defense, level, PetCoreStat.Defense, nature),
                CalculateCoreStat(baseStats.magicAttack, iv.magicAttack, ev.magicAttack, level, PetCoreStat.MagicAttack, nature),
                CalculateCoreStat(baseStats.magicDefense, iv.magicDefense, ev.magicDefense, level, PetCoreStat.MagicDefense, nature),
                CalculateCoreStat(baseStats.speed, iv.speed, ev.speed, level, PetCoreStat.Speed, nature),
                hp, Mathf.Clamp(rate, 0f, 100f), Mathf.Clamp(damage, 0f, 100f));
        }

        static PetStatValues GetDisplayBaseMaximum() {
            EnsureProfiles();
            PetStatValues maximum = Neutral;
            for (int i = 0; i < profiles.Length; i++) {
                if (profiles[i] == null) continue;
                PetStatValues value = profiles[i].BaseStats;
                for (int axis = 0; axis < 6; axis++) {
                    var stat = (PetCoreStat)axis;
                    maximum.Set(stat, Mathf.Max(maximum.Get(stat), value.Get(stat)));
                }
            }
            return maximum;
        }

        static PetStatProfile ResolveProfile(PetCollectionMetadata metadata, out string missingKey) {
            string id = CanonicalProfileId(metadata != null ? metadata.DefinitionId : string.Empty);
            string species = CanonicalProfileId(metadata != null ? metadata.Species : string.Empty);
            string form = CanonicalProfileFormId(metadata != null ? metadata.CurrentFormId : "base");
            if (string.Equals(form, "base", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(id, "Dragon Dusk", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(species, "Dragon Dusk", StringComparison.OrdinalIgnoreCase)))
                form = "dragon-dusk-base";
            missingKey = $"{id}/{form}";
            EnsureProfiles();
            for (int i = 0; i < profiles.Length; i++) {
                PetStatProfile profile = profiles[i];
                if (profile == null || !string.Equals(profile.FormId, form, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(profile.DefinitionId, id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(profile.DefinitionId, species, StringComparison.OrdinalIgnoreCase)) return profile;
            }
            return null;
        }

        // Legacy form names remain valid prefab/save IDs; normalize only for stat lookup.
        static string CanonicalProfileId(string id) {
            if (string.Equals(id, "Dragon Dusk Winged", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "Dragon Dusk Elder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "Dragon Nightfall", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "Dragon Darkness", StringComparison.OrdinalIgnoreCase))
                return "Dragon Dusk";
            return id;
        }

        static string CanonicalProfileFormId(string form) {
            if (string.Equals(form, "01", StringComparison.OrdinalIgnoreCase)) return "dragon-dusk-base";
            if (string.Equals(form, "02", StringComparison.OrdinalIgnoreCase)) return "dragon-dusk-winged";
            if (string.Equals(form, "03", StringComparison.OrdinalIgnoreCase)) return "dragon-dusk-elder";
            return form;
        }

        static void EnsureProfiles() => profiles ??= Resources.LoadAll<PetStatProfile>("PetStats");
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Capstone.Game.SaveSystem;
using GDS.Core;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    public enum PetElement {
        Unknown,
        Nature,
        Fire,
        Water,
        Wind,
        Earth,
        Electric,
        Ice,
        Light,
        Dark
    }

    public enum PetRarity {
        Unknown,
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    [DisallowMultipleComponent]
    public sealed class PetCollectionMetadata : MonoBehaviour {
        const int MaxNicknameLength = 16;

        [Header("Identity")]
        [SerializeField] string petId = string.Empty;
        [SerializeField] string definitionId = string.Empty;
        [SerializeField] string species = string.Empty;
        [SerializeField] string gender = string.Empty;
        [SerializeField] PetElement element = PetElement.Unknown;
        [SerializeField] PetRarity rarity = PetRarity.Unknown;

        [Header("Player Customization")]
        [SerializeField] string nickname = string.Empty;
        [SerializeField] bool favorite;
        [SerializeField, HideInInspector] bool released;

        [Header("Progress")]
        [SerializeField, Min(1)] int maxLevel = 60;
        [SerializeField, Min(0)] int experience;
        [SerializeField, Min(0)] int experienceToNextLevel;
        [SerializeField, Min(0)] int attack;
        [SerializeField, Min(0)] int defense;
        [SerializeField, Min(0)] int speed;
        [SerializeField, Range(0f, 100f)] float criticalRate;
        [SerializeField, Min(0f)] float criticalDamagePercent;
        [SerializeField] long obtainedOrder;

        [Header("Pet Details")]
        [SerializeField] string captureDate = string.Empty;
        [SerializeField] string captureLocation = string.Empty;
        [SerializeField] string trainerName = string.Empty;
        [SerializeField] string personality = string.Empty;

        [Header("Codex")]
        [SerializeField] bool codexRegistered;
        [SerializeField] string codexNumber = string.Empty;
        [SerializeField, TextArea(2, 4)] string codexDescription = string.Empty;

        [Header("Evolution State")]
        [SerializeField] string currentFormId = "base";
        [SerializeField, Min(0)] int evolutionStage;
        [SerializeField] List<string> promptedEvolutionIds = new List<string>();
        [SerializeField] List<PetEvolutionRule> evolutionRules = new List<PetEvolutionRule>();

        [Header("Investment History")]
        [SerializeField] List<PetResourceInvestment> resourceInvestments = new List<PetResourceInvestment>();

        [Header("Legacy Evolution (fallback)")]
        [SerializeField] bool hasNextEvolution;
        [SerializeField] string nextEvolutionName = string.Empty;
        [SerializeField] Sprite nextEvolutionIcon = null;
        [SerializeField] bool nextEvolutionDiscovered;
        [SerializeField, Min(0)] int evolutionLevelRequirement;
        [SerializeField, Min(0)] int evolutionFriendshipRequirement;
        [SerializeField] string extraEvolutionRequirement = string.Empty;

        [NonSerialized] PetEvolutionRule legacyEvolutionRule;

        public string PetId => EnsurePersistentId();
        public string PersistentId => EnsurePersistentId();
        public string DefinitionId => string.IsNullOrWhiteSpace(definitionId)
            ? (species ?? string.Empty).Trim()
            : definitionId.Trim();
        public string Species => species;
        public string Gender => gender;
        public PetElement Element => element;
        public PetRarity Rarity => rarity;
        public string Nickname => nickname ?? string.Empty;
        public bool IsFavorite => favorite;
        public bool IsReleased => released;
        public int MaxLevel => Mathf.Max(1, maxLevel);
        public int Experience => Mathf.Max(0, experience);
        public int ExperienceToNextLevel => Mathf.Max(0, experienceToNextLevel);
        public int Attack => Mathf.Max(0, attack);
        public int Defense => Mathf.Max(0, defense);
        public int Speed => Mathf.Max(0, speed);
        public float CriticalRate => Mathf.Clamp(criticalRate, 0f, 100f);
        public float CriticalDamagePercent => Mathf.Max(0f, criticalDamagePercent);
        public long ObtainedOrder => obtainedOrder;
        public string CaptureDate => captureDate ?? string.Empty;
        public string CaptureLocation => captureLocation ?? string.Empty;
        public string TrainerName => trainerName ?? string.Empty;
        public string Personality => personality ?? string.Empty;
        public bool CodexRegistered => codexRegistered;
        public string CodexNumber => codexNumber ?? string.Empty;
        public string CodexDescription => codexDescription ?? string.Empty;
        public string CurrentFormId => string.IsNullOrWhiteSpace(currentFormId) ? "base" : currentFormId.Trim();
        public int EvolutionStage => Mathf.Max(0, evolutionStage);
        public IReadOnlyList<PetEvolutionRule> EvolutionRules => evolutionRules;
        public IReadOnlyList<PetResourceInvestment> ResourceInvestments => resourceInvestments;
        public bool HasNextEvolution => TryGetNextEvolutionRule(out _);
        public string NextEvolutionName => TryGetNextEvolutionRule(out PetEvolutionRule rule)
            ? rule.TargetDisplayName
            : string.Empty;
        public Sprite NextEvolutionIcon => TryGetNextEvolutionRule(out PetEvolutionRule rule)
            ? rule.TargetIcon
            : null;
        public bool NextEvolutionDiscovered => nextEvolutionDiscovered;
        public int EvolutionLevelRequirement => TryGetNextEvolutionRule(out PetEvolutionRule rule)
            && rule.Trigger == PetEvolutionTrigger.Level
                ? rule.RequiredLevel
                : Mathf.Max(0, evolutionLevelRequirement);
        public int EvolutionFriendshipRequirement => Mathf.Max(0, evolutionFriendshipRequirement);
        public string ExtraEvolutionRequirement => extraEvolutionRequirement ?? string.Empty;

        public event Action Changed;

        void Awake() {
            EnsurePersistentId();
        }

        public void AssignPersistentId(string value) {
            if (string.IsNullOrWhiteSpace(value)) return;
            petId = value.Trim();
        }

        public void AssignDefinitionId(string value) {
            definitionId = value?.Trim() ?? string.Empty;
        }

        public string ResolveDisplayName(string fallback) {
            return string.IsNullOrWhiteSpace(nickname) ? fallback : nickname.Trim();
        }

        public bool TrySetNickname(string value, out string error) {
            string next = value?.Trim() ?? string.Empty;
            if (next.Length == 0) {
                error = "Tên không được để trống.";
                return false;
            }
            if (next.Length > MaxNicknameLength) {
                error = $"Tên phải từ 1 đến {MaxNicknameLength} ký tự.";
                return false;
            }

            nickname = next;
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool ToggleFavorite() {
            favorite = !favorite;
            Changed?.Invoke();
            return favorite;
        }

        public void SetReleased(bool value) {
            if (released == value) return;
            released = value;
            Changed?.Invoke();
        }

        public void RecordResourceInvestment(ItemBase itemBase, string itemId, string displayName, int quantity, bool refundable = true) {
            if (quantity <= 0) return;

            string resolvedId = !string.IsNullOrWhiteSpace(itemId)
                ? itemId.Trim()
                : itemBase != null
                    ? itemBase.name
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedId)) return;

            resourceInvestments ??= new List<PetResourceInvestment>();
            for (int i = 0; i < resourceInvestments.Count; i++) {
                PetResourceInvestment investment = resourceInvestments[i];
                if (investment == null || !investment.Matches(resolvedId)) continue;

                investment.Add(itemBase, displayName, quantity, refundable);
                Changed?.Invoke();
                return;
            }

            resourceInvestments.Add(new PetResourceInvestment(
                itemBase,
                resolvedId,
                displayName,
                quantity,
                refundable));
            Changed?.Invoke();
        }

        public void SetProgress(int currentExperience, int requiredExperience) {
            experience = Mathf.Max(0, currentExperience);
            experienceToNextLevel = Mathf.Max(0, requiredExperience);
            Changed?.Invoke();
        }

        public void SetStats(int nextAttack, int nextDefense, int nextSpeed) {
            attack = Mathf.Max(0, nextAttack);
            defense = Mathf.Max(0, nextDefense);
            speed = Mathf.Max(0, nextSpeed);
            Changed?.Invoke();
        }

        public bool TryGetNextEvolutionRule(out PetEvolutionRule rule) {
            if (evolutionRules != null) {
                for (int i = 0; i < evolutionRules.Count; i++) {
                    PetEvolutionRule candidate = evolutionRules[i];
                    if (candidate != null && candidate.SourceStage == EvolutionStage) {
                        rule = candidate;
                        return true;
                    }
                }
            }

            if (hasNextEvolution && EvolutionStage == 0) {
                legacyEvolutionRule ??= PetEvolutionRule.CreateLegacy(
                    nextEvolutionName,
                    nextEvolutionIcon,
                    evolutionLevelRequirement,
                    string.Empty);
                rule = legacyEvolutionRule;
                return true;
            }

            rule = null;
            return false;
        }

        public bool CanApplyEvolution(PetEvolutionRule rule) {
            if (rule == null || rule.SourceStage != EvolutionStage) return false;
            return TryGetNextEvolutionRule(out PetEvolutionRule current)
                && string.Equals(current.EvolutionId, rule.EvolutionId, StringComparison.OrdinalIgnoreCase);
        }

        public bool ApplyEvolution(PetEvolutionRule rule) {
            if (!CanApplyEvolution(rule)) return false;

            evolutionStage = rule.TargetStage;
            currentFormId = rule.TargetFormId;
            if (!string.IsNullOrWhiteSpace(rule.TargetSpecies)) species = rule.TargetSpecies;
            if (rule.TargetElement != PetElement.Unknown) element = rule.TargetElement;
            if (rule.TargetRarity != PetRarity.Unknown) rarity = rule.TargetRarity;
            nextEvolutionDiscovered = false;
            Changed?.Invoke();
            return true;
        }

        public bool WasEvolutionPrompted(string evolutionId) {
            if (string.IsNullOrWhiteSpace(evolutionId) || promptedEvolutionIds == null) return false;
            for (int i = 0; i < promptedEvolutionIds.Count; i++) {
                if (string.Equals(promptedEvolutionIds[i], evolutionId, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public void MarkEvolutionPrompted(string evolutionId) {
            if (string.IsNullOrWhiteSpace(evolutionId) || WasEvolutionPrompted(evolutionId)) return;
            promptedEvolutionIds ??= new List<string>();
            promptedEvolutionIds.Add(evolutionId.Trim());
            Changed?.Invoke();
        }

        public void RestoreEvolutionPresentation(PetHudRuntimeStats runtimeStats) {
            PetEvolutionRule applied = FindAppliedEvolutionRule();
            if (applied == null) return;

            if (!string.IsNullOrWhiteSpace(applied.TargetSpecies)) species = applied.TargetSpecies;
            if (applied.TargetElement != PetElement.Unknown) element = applied.TargetElement;
            if (applied.TargetRarity != PetRarity.Unknown) rarity = applied.TargetRarity;
            if (runtimeStats != null) {
                string baseName = string.IsNullOrWhiteSpace(applied.TargetDisplayName)
                    ? runtimeStats.DisplayName
                    : applied.TargetDisplayName;
                runtimeStats.SetIdentity(baseName, runtimeStats.Level, applied.TargetIcon ?? runtimeStats.Icon);
            }

            PetController pet = GetComponentInParent<PetController>();
            PetEvolutionVisualController visualController = pet != null
                ? pet.GetComponentInChildren<PetEvolutionVisualController>(true)
                : null;
            if (visualController != null && applied.TargetVisualPrefab != null) {
                visualController.TryApply(applied.TargetVisualPrefab, out _);
            }
        }

        public PetCustomizationSaveData CreateSaveData() {
            return new PetCustomizationSaveData {
                nickname = Nickname,
                favorite = IsFavorite,
                experience = Experience,
                experienceToNextLevel = ExperienceToNextLevel,
                attack = Attack,
                defense = Defense,
                speed = Speed,
                criticalRate = CriticalRate,
                criticalDamagePercent = CriticalDamagePercent,
                obtainedOrder = ObtainedOrder,
                captureDate = CaptureDate,
                captureLocation = CaptureLocation,
                trainerName = TrainerName,
                personality = Personality,
                codexRegistered = CodexRegistered,
                codexNumber = CodexNumber,
                codexDescription = CodexDescription,
                nextEvolutionDiscovered = NextEvolutionDiscovered,
                currentFormId = CurrentFormId,
                evolutionStage = EvolutionStage,
                resourceInvestments = CreateInvestmentSaveData(),
                promptedEvolutionIds = promptedEvolutionIds != null
                    ? new List<string>(promptedEvolutionIds)
                    : new List<string>()
            };
        }

        public void RestoreFromSaveData(PetCustomizationSaveData saveData) {
            if (saveData == null) return;

            nickname = NormalizeStoredNickname(saveData.nickname);
            favorite = saveData.favorite;
            released = false;
            experience = Mathf.Max(0, saveData.experience);
            experienceToNextLevel = Mathf.Max(0, saveData.experienceToNextLevel);
            attack = Mathf.Max(0, saveData.attack);
            defense = Mathf.Max(0, saveData.defense);
            speed = Mathf.Max(0, saveData.speed);
            criticalRate = Mathf.Clamp(saveData.criticalRate, 0f, 100f);
            criticalDamagePercent = Mathf.Max(0f, saveData.criticalDamagePercent);
            obtainedOrder = saveData.obtainedOrder;
            captureDate = saveData.captureDate ?? string.Empty;
            captureLocation = saveData.captureLocation ?? string.Empty;
            trainerName = saveData.trainerName ?? string.Empty;
            personality = saveData.personality ?? string.Empty;
            codexRegistered = saveData.codexRegistered;
            codexNumber = saveData.codexNumber ?? string.Empty;
            codexDescription = saveData.codexDescription ?? string.Empty;
            nextEvolutionDiscovered = saveData.nextEvolutionDiscovered;
            currentFormId = string.IsNullOrWhiteSpace(saveData.currentFormId) ? "base" : saveData.currentFormId.Trim();
            evolutionStage = Mathf.Max(0, saveData.evolutionStage);
            resourceInvestments = RestoreInvestmentSaveData(saveData.resourceInvestments);
            promptedEvolutionIds = saveData.promptedEvolutionIds != null
                ? new List<string>(saveData.promptedEvolutionIds)
                : new List<string>();

            Changed?.Invoke();
        }

        void OnValidate() {
            nickname = NormalizeStoredNickname(nickname);
            maxLevel = Mathf.Max(1, maxLevel);
            experience = Mathf.Max(0, experience);
            experienceToNextLevel = Mathf.Max(0, experienceToNextLevel);
            attack = Mathf.Max(0, attack);
            defense = Mathf.Max(0, defense);
            speed = Mathf.Max(0, speed);
            criticalRate = Mathf.Clamp(criticalRate, 0f, 100f);
            criticalDamagePercent = Mathf.Max(0f, criticalDamagePercent);
            evolutionLevelRequirement = Mathf.Max(0, evolutionLevelRequirement);
            evolutionFriendshipRequirement = Mathf.Max(0, evolutionFriendshipRequirement);
            evolutionStage = Mathf.Max(0, evolutionStage);
            promptedEvolutionIds ??= new List<string>();
            evolutionRules ??= new List<PetEvolutionRule>();
            resourceInvestments ??= new List<PetResourceInvestment>();
        }

        List<PetResourceInvestmentSaveData> CreateInvestmentSaveData() {
            var result = new List<PetResourceInvestmentSaveData>();
            if (resourceInvestments == null) return result;

            for (int i = 0; i < resourceInvestments.Count; i++) {
                PetResourceInvestment investment = resourceInvestments[i];
                if (investment == null || investment.Quantity <= 0 || string.IsNullOrWhiteSpace(investment.ItemId)) continue;
                result.Add(new PetResourceInvestmentSaveData {
                    itemId = investment.ItemId,
                    displayName = investment.DisplayName,
                    quantity = investment.Quantity,
                    refundable = investment.Refundable
                });
            }

            return result;
        }

        static List<PetResourceInvestment> RestoreInvestmentSaveData(
            IReadOnlyList<PetResourceInvestmentSaveData> saveData) {
            var result = new List<PetResourceInvestment>();
            if (saveData == null) return result;

            for (int i = 0; i < saveData.Count; i++) {
                PetResourceInvestmentSaveData investment = saveData[i];
                if (investment == null || investment.quantity <= 0 || string.IsNullOrWhiteSpace(investment.itemId)) continue;
                result.Add(new PetResourceInvestment(
                    null,
                    investment.itemId,
                    investment.displayName,
                    investment.quantity,
                    investment.refundable));
            }

            return result;
        }

        PetEvolutionRule FindAppliedEvolutionRule() {
            if (EvolutionStage <= 0) return null;
            if (evolutionRules != null) {
                for (int i = 0; i < evolutionRules.Count; i++) {
                    PetEvolutionRule candidate = evolutionRules[i];
                    if (candidate == null || candidate.TargetStage != EvolutionStage) continue;
                    if (string.IsNullOrWhiteSpace(currentFormId)
                        || string.Equals(candidate.TargetFormId, currentFormId, StringComparison.OrdinalIgnoreCase)) {
                        return candidate;
                    }
                }
            }

            if (EvolutionStage == 1 && hasNextEvolution) {
                legacyEvolutionRule ??= PetEvolutionRule.CreateLegacy(
                    nextEvolutionName,
                    nextEvolutionIcon,
                    evolutionLevelRequirement,
                    string.Empty);
                return legacyEvolutionRule;
            }

            return null;
        }

        static string NormalizeStoredNickname(string value) {
            string normalized = value?.Trim() ?? string.Empty;
            return normalized.Length <= MaxNicknameLength
                ? normalized
                : normalized.Substring(0, MaxNicknameLength).Trim();
        }

        string EnsurePersistentId() {
            if (!string.IsNullOrWhiteSpace(petId)) {
                petId = petId.Trim();
                return petId;
            }

            petId = "auto-" + Encode(BuildFallbackIdentity());
            return petId;
        }

        static string Encode(string value) {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return encoded;
        }

        string BuildFallbackIdentity() {
            if (!string.IsNullOrWhiteSpace(species) && obtainedOrder > 0) {
                return species.Trim() + ":" + obtainedOrder;
            }

            Transform root = transform.root;
            string scenePath = root.gameObject.scene.path;
            return scenePath + ":" + root.name.Replace("(Clone)", string.Empty).Trim() + ":" + root.GetSiblingIndex();
        }
    }
}

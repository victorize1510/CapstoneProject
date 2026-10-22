using System;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetLevelUpService : MonoBehaviour {
        public const int DefaultBaseExperienceRequirement = 100;
        public const int DefaultExperienceRequirementPerLevel = 50;
        public const int DefaultQuadraticExperienceRequirement = 10;

        [Header("Temporary Level Item")]
        [SerializeField] string levelUpItemId = "Leaf Berry";
        [SerializeField] string levelUpItemDisplayName = "Leaf Berry";
        [SerializeField, Min(1)] int experiencePerItem = 50;

        [Header("Level Curve")]
        [SerializeField, Min(1)] int baseExperienceRequirement = DefaultBaseExperienceRequirement;
        [SerializeField, Min(0)] int experienceRequirementPerLevel = DefaultExperienceRequirementPerLevel;
        [SerializeField, Min(0)] int quadraticExperienceRequirement = DefaultQuadraticExperienceRequirement;

        MonsterInventoryAdapter inventory;
        PlayerSaveController saveController;

        public event Action<PetController> PetLeveledUp;
        public static event Action<PetController> AnyPetLeveledUp;

        public void Bind(MonsterInventoryAdapter targetInventory) {
            inventory = targetInventory != null ? targetInventory : inventory;
        }

        public PetLevelUpPreview CreatePreview(PetController pet) {
            var preview = new PetLevelUpPreview { Pet = pet };
            if (pet == null) {
                preview.DisabledReason = "Chưa chọn pet để tăng cấp.";
                return preview;
            }

            preview.RuntimeStats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            preview.Metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (preview.Metadata == null) preview.Metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();
            if (preview.RuntimeStats == null) {
                preview.DisabledReason = "Pet chưa có PetHudRuntimeStats.";
                return preview;
            }

            ResolveInventory();
            ResolveSaveController();
            int currentLevel = Mathf.Max(1, preview.RuntimeStats.Level);
            int maxLevel = preview.Metadata.MaxLevel;
            int currentExperience = preview.Metadata.Experience;
            int requiredExperience = CalculateExperienceRequirement(currentLevel);
            int experienceNeeded = Mathf.Max(0, requiredExperience - currentExperience);
            int itemRequirement = Mathf.Max(1, Mathf.CeilToInt(experienceNeeded / (float)Mathf.Max(1, experiencePerItem)));

            preview.IsValid = true;
            preview.IsMaxLevel = currentLevel >= maxLevel;
            preview.CurrentLevel = currentLevel;
            preview.NextLevel = preview.IsMaxLevel ? currentLevel : currentLevel + 1;
            preview.MaxLevel = maxLevel;
            preview.CurrentExperience = currentExperience;
            preview.RequiredExperience = requiredExperience;
            preview.ExperienceNeeded = experienceNeeded;
            preview.ItemName = string.IsNullOrWhiteSpace(levelUpItemDisplayName) ? levelUpItemId : levelUpItemDisplayName;
            preview.RequiredItemQuantity = itemRequirement;
            preview.Item = FindLevelUpItem();
            preview.OwnedItemQuantity = preview.Item != null ? preview.Item.Quantity : 0;
            preview.ItemIcon = preview.Item?.Icon;

            PetStatSnapshot currentScores = preview.Metadata.GetStatsAtLevel(currentLevel);
            PetStatSnapshot nextScores = preview.Metadata.GetStatsAtLevel(preview.NextLevel);
            preview.CurrentHealth = currentScores.Hp;
            preview.NextHealth = nextScores.Hp;
            preview.CurrentAttack = currentScores.Attack;
            preview.NextAttack = nextScores.Attack;
            preview.CurrentDefense = currentScores.Defense;
            preview.NextDefense = nextScores.Defense;
            preview.CurrentSpeed = currentScores.Speed;
            preview.NextSpeed = nextScores.Speed;
            preview.CurrentMagicAttack = currentScores.MagicAttack;
            preview.NextMagicAttack = nextScores.MagicAttack;
            preview.CurrentMagicDefense = currentScores.MagicDefense;
            preview.NextMagicDefense = nextScores.MagicDefense;
            preview.HasNewSkill = !preview.IsMaxLevel
                && preview.RuntimeStats.TryGetSkillUnlockingAtLevel(preview.NextLevel, out preview.NewSkill);

            if (preview.IsMaxLevel) {
                preview.DisabledReason = "Pet đã đạt cấp tối đa.";
            }
            else if (preview.Item == null) {
                preview.DisabledReason = $"Chưa có {preview.ItemName} trong Bag.";
            }
            else if (preview.OwnedItemQuantity < preview.RequiredItemQuantity) {
                preview.DisabledReason = $"Cần thêm {preview.RequiredItemQuantity - preview.OwnedItemQuantity} {preview.ItemName}.";
            }
            else if (saveController == null) {
                preview.DisabledReason = "Không tìm thấy PlayerSaveController để lưu tăng cấp.";
            }

            preview.CanLevelUp = string.IsNullOrEmpty(preview.DisabledReason);
            return preview;
        }

        public bool TryLevelUp(PetController pet, out string message) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryBegin(this, "level-up", out var operation)) { message = string.Empty; return false; }
            using (operation) return TryLevelUpCore(pet, out message);
        }

        bool TryLevelUpCore(PetController pet, out string message) {
            PetLevelUpPreview preview = CreatePreview(pet);
            if (!preview.CanLevelUp) {
                message = preview.DisabledReason;
                return false;
            }

            PetCustomizationSaveData metadataSnapshot = preview.Metadata.CreateSaveData();
            PetRuntimeStatsSaveData statsSnapshot = preview.RuntimeStats.CreateSaveData();

            Result removeResult = inventory.RemoveItem(preview.Item.ItemBase, preview.RequiredItemQuantity);
            if (removeResult is Fail) {
                message = $"Không thể dùng {preview.ItemName}. Hãy kiểm tra lại Bag.";
                return false;
            }

            preview.Metadata.RecordResourceInvestment(
                preview.Item.ItemBase,
                preview.Item.ItemId,
                preview.Item.Name,
                preview.RequiredItemQuantity);

            int grantedExperience = preview.RequiredItemQuantity * Mathf.Max(1, experiencePerItem);
            int remainingExperience = Mathf.Max(0,
                preview.CurrentExperience + grantedExperience - preview.RequiredExperience);
            int nextRequirement = preview.NextLevel >= preview.MaxLevel
                ? 0
                : CalculateExperienceRequirement(preview.NextLevel);

            PetHudRuntimeStats stats = preview.RuntimeStats;
            float oldMaxHealth = stats.MaxHealth;
            float newMaxHealth = preview.Metadata.GetStatsAtLevel(preview.NextLevel).MaxHealth;
            float newHealth = stats.Health <= 0f ? 0f : oldMaxHealth > 0f
                ? Mathf.Clamp(stats.Health + newMaxHealth - oldMaxHealth, 0f, newMaxHealth)
                : newMaxHealth;

            preview.Metadata.SetProgress(remainingExperience, nextRequirement);
            stats.SetIdentity(stats.DisplayName, preview.NextLevel, stats.Icon);
            stats.SetStatus(newHealth, newMaxHealth, stats.Energy, stats.MaxEnergy);
            stats.UnlockSkillsUpToLevel(preview.NextLevel, out _);

            if (saveController == null || !saveController.SaveNow()) {
                preview.Metadata.RestoreFromSaveData(metadataSnapshot);
                stats.RestoreFromSaveData(statsSnapshot);
                inventory.AddItem(preview.Item.ItemBase, preview.RequiredItemQuantity);
                message = "Không thể lưu lần tăng cấp. Mọi thay đổi đã được hoàn tác.";
                return false;
            }

            NotifyPetLeveledUp(pet);
            message = $"{preview.Metadata.ResolveDisplayName(stats.DisplayName)} đã đạt Lv. {preview.NextLevel}.";
            return true;
        }

        public bool GrantBattleExperience(
            PetController pet,
            int experienceAmount,
            out PetExperienceGainResult result) {
            result = new PetExperienceGainResult {
                Pet = pet,
                ExperienceGranted = Mathf.Max(0, experienceAmount)
            };
            if (pet == null || experienceAmount <= 0) return false;

            PetHudRuntimeStats stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            if (stats == null) return false;

            PetCollectionMetadata metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (metadata == null) metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();

            int previousLevel = Mathf.Max(1, stats.Level);
            int currentLevel = previousLevel;
            int maxLevel = metadata.MaxLevel;
            result.PreviousLevel = previousLevel;
            if (currentLevel >= maxLevel) {
                result.CurrentLevel = currentLevel;
                result.CurrentExperience = metadata.Experience;
                result.ExperienceToNextLevel = 0;
                return false;
            }

            long accumulatedExperience = (long)metadata.Experience + experienceAmount;
            int levelsGained = 0;
            while (currentLevel < maxLevel) {
                int requiredExperience = CalculateExperienceRequirement(currentLevel);
                if (accumulatedExperience < requiredExperience) break;

                accumulatedExperience -= requiredExperience;
                currentLevel++;
                levelsGained++;
            }

            if (currentLevel >= maxLevel) accumulatedExperience = 0;
            int currentExperience = (int)Math.Min(int.MaxValue, accumulatedExperience);
            int nextRequirement = currentLevel >= maxLevel
                ? 0
                : CalculateExperienceRequirement(currentLevel);

            metadata.SetProgress(currentExperience, nextRequirement);
            if (levelsGained > 0) {
                ApplyLevelGrowth(stats, metadata, currentLevel);
                NotifyPetLeveledUp(pet);
            }

            ResolveSaveController();
            saveController?.RequestSave();
            FindFirstObjectByType<PetCommandHudProvider>()?.NotifyHudDataChanged();

            result.CurrentLevel = currentLevel;
            result.LevelsGained = levelsGained;
            result.CurrentExperience = currentExperience;
            result.ExperienceToNextLevel = nextRequirement;
            return true;
        }

        public int CalculateExperienceRequirement(int level) {
            long offset = Mathf.Max(0, level - 1);
            long requirement = baseExperienceRequirement
                + offset * experienceRequirementPerLevel
                + offset * offset * quadraticExperienceRequirement;
            return (int)Math.Min(int.MaxValue, Math.Max(1L, requirement));
        }

        public static int CalculateDefaultExperienceRequirement(int level) {
            long offset = Mathf.Max(0, level - 1);
            long requirement = DefaultBaseExperienceRequirement
                + offset * DefaultExperienceRequirementPerLevel
                + offset * offset * DefaultQuadraticExperienceRequirement;
            return (int)Math.Min(int.MaxValue, Math.Max(1L, requirement));
        }

        void NotifyPetLeveledUp(PetController pet) {
            PetLeveledUp?.Invoke(pet);
            AnyPetLeveledUp?.Invoke(pet);
        }

        public void NotifyDebugLevelChange(PetController pet) => NotifyPetLeveledUp(pet);

        void ApplyLevelGrowth(
            PetHudRuntimeStats stats,
            PetCollectionMetadata metadata,
            int newLevel) {
            float oldMaxHealth = stats.MaxHealth;
            float newMaxHealth = metadata.GetStatsAtLevel(newLevel).MaxHealth;
            float newHealth = stats.Health <= 0f ? 0f : oldMaxHealth > 0f
                ? Mathf.Clamp(stats.Health + newMaxHealth - oldMaxHealth, 0f, newMaxHealth)
                : newMaxHealth;

            stats.SetIdentity(stats.DisplayName, newLevel, stats.Icon);
            stats.SetStatus(newHealth, newMaxHealth, stats.Energy, stats.MaxEnergy);
            stats.UnlockSkillsUpToLevel(newLevel, out _);
        }

        void ResolveInventory() {
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
        }

        void ResolveSaveController() {
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>();
        }

        InventoryItemSnapshot FindLevelUpItem() {
            if (inventory == null) return null;
            var items = inventory.GetItems();
            for (int i = 0; i < items.Count; i++) {
                InventoryItemSnapshot item = items[i];
                if (item == null) continue;
                if (string.Equals(item.ItemId, levelUpItemId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Name, levelUpItemId, StringComparison.OrdinalIgnoreCase)) {
                    return item;
                }
            }

            return null;
        }

        void OnValidate() {
            experiencePerItem = Mathf.Max(1, experiencePerItem);
            baseExperienceRequirement = Mathf.Max(1, baseExperienceRequirement);
            experienceRequirementPerLevel = Mathf.Max(0, experienceRequirementPerLevel);
            quadraticExperienceRequirement = Mathf.Max(0, quadraticExperienceRequirement);
        }
    }

    public sealed class PetExperienceGainResult {
        public PetController Pet;
        public int ExperienceGranted;
        public int PreviousLevel;
        public int CurrentLevel;
        public int LevelsGained;
        public int CurrentExperience;
        public int ExperienceToNextLevel;
    }

    public sealed class PetLevelUpPreview {
        public PetController Pet;
        public PetHudRuntimeStats RuntimeStats;
        public PetCollectionMetadata Metadata;
        public InventoryItemSnapshot Item;
        public Sprite ItemIcon;
        public bool IsValid;
        public bool IsMaxLevel;
        public bool CanLevelUp;
        public string DisabledReason = string.Empty;
        public string ItemName = string.Empty;
        public int CurrentLevel;
        public int NextLevel;
        public int MaxLevel;
        public int CurrentExperience;
        public int RequiredExperience;
        public int ExperienceNeeded;
        public int OwnedItemQuantity;
        public int RequiredItemQuantity;
        public int CurrentHealth;
        public int NextHealth;
        public int CurrentAttack;
        public int NextAttack;
        public int CurrentDefense;
        public int NextDefense;
        public int CurrentSpeed;
        public int NextSpeed;
        public int CurrentMagicAttack;
        public int NextMagicAttack;
        public int CurrentMagicDefense;
        public int NextMagicDefense;
        public bool HasNewSkill;
        public SkillHudData NewSkill;
    }
}

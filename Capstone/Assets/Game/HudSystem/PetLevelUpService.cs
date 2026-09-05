using System;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetLevelUpService : MonoBehaviour {
        [Header("Temporary Level Item")]
        [SerializeField] string levelUpItemId = "Leaf Berry";
        [SerializeField] string levelUpItemDisplayName = "Leaf Berry";
        [SerializeField, Min(1)] int experiencePerItem = 250;

        [Header("Level Curve")]
        [SerializeField, Min(1)] int baseExperienceRequirement = 1000;
        [SerializeField, Min(0)] int experienceRequirementPerLevel = 200;

        [Header("Stat Growth Per Level")]
        [SerializeField, Min(0)] int healthGrowth = 25;
        [SerializeField, Min(0)] int attackGrowth = 5;
        [SerializeField, Min(0)] int defenseGrowth = 4;
        [SerializeField, Min(0)] int speedGrowth = 3;

        MonsterInventoryAdapter inventory;
        PlayerSaveController saveController;

        public event Action<PetController> PetLeveledUp;

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
            int requiredExperience = preview.Metadata.ExperienceToNextLevel > 0
                ? preview.Metadata.ExperienceToNextLevel
                : CalculateExperienceRequirement(currentLevel);
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

            preview.CurrentHealth = Mathf.RoundToInt(preview.RuntimeStats.MaxHealth);
            preview.NextHealth = preview.CurrentHealth + healthGrowth;
            preview.CurrentAttack = preview.Metadata.Attack;
            preview.NextAttack = preview.CurrentAttack + attackGrowth;
            preview.CurrentDefense = preview.Metadata.Defense;
            preview.NextDefense = preview.CurrentDefense + defenseGrowth;
            preview.CurrentSpeed = preview.Metadata.Speed;
            preview.NextSpeed = preview.CurrentSpeed + speedGrowth;
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
            float newMaxHealth = oldMaxHealth > 0f
                ? oldMaxHealth + healthGrowth
                : Mathf.Max(1, healthGrowth);
            float newHealth = oldMaxHealth > 0f
                ? Mathf.Min(newMaxHealth, stats.Health + healthGrowth)
                : newMaxHealth;

            preview.Metadata.SetProgress(remainingExperience, nextRequirement);
            preview.Metadata.SetStats(preview.NextAttack, preview.NextDefense, preview.NextSpeed);
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

            PetLeveledUp?.Invoke(pet);
            message = $"{preview.Metadata.ResolveDisplayName(stats.DisplayName)} đã đạt Lv. {preview.NextLevel}.";
            return true;
        }

        int CalculateExperienceRequirement(int level) {
            return Mathf.Max(1, baseExperienceRequirement
                + Mathf.Max(0, level - 1) * experienceRequirementPerLevel);
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
            healthGrowth = Mathf.Max(0, healthGrowth);
            attackGrowth = Mathf.Max(0, attackGrowth);
            defenseGrowth = Mathf.Max(0, defenseGrowth);
            speedGrowth = Mathf.Max(0, speedGrowth);
        }
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
        public bool HasNewSkill;
        public SkillHudData NewSkill;
    }
}

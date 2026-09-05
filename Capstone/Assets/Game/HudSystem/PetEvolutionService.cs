using System;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetEvolutionService : MonoBehaviour {
        MonsterInventoryAdapter inventory;
        PlayerSaveController saveController;

        public event Action<PetController> PetEvolved;

        public void Bind(MonsterInventoryAdapter targetInventory) {
            inventory = targetInventory != null ? targetInventory : inventory;
        }

        public PetEvolutionPreview CreatePreview(PetController pet) {
            var preview = new PetEvolutionPreview { Pet = pet };
            if (pet == null) {
                preview.DisabledReason = "Chưa chọn pet để tiến hóa.";
                return preview;
            }

            preview.RuntimeStats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            preview.Metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (preview.Metadata == null) preview.Metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();
            if (preview.RuntimeStats == null) {
                preview.DisabledReason = "Pet chưa có PetHudRuntimeStats.";
                return preview;
            }

            preview.IsValid = true;
            preview.CurrentLevel = Mathf.Max(1, preview.RuntimeStats.Level);
            preview.CurrentName = preview.Metadata.ResolveDisplayName(preview.RuntimeStats.DisplayName);
            preview.CurrentIcon = preview.RuntimeStats.Icon;
            if (!preview.Metadata.TryGetNextEvolutionRule(out PetEvolutionRule rule)) {
                preview.DisabledReason = "Pet đã đạt dạng tiến hóa cuối.";
                return preview;
            }

            ResolveInventory();
            ResolveSaveController();
            preview.Rule = rule;
            preview.IsConfigured = true;
            preview.TargetName = string.IsNullOrWhiteSpace(rule.TargetDisplayName)
                ? rule.TargetFormId
                : rule.TargetDisplayName;
            preview.TargetIcon = rule.TargetIcon;
            preview.RequiredLevel = rule.RequiredLevel;
            preview.RequiredItemId = rule.RequiredItemId;
            preview.RequiredItemQuantity = rule.RequiredItemQuantity;

            if (rule.Trigger == PetEvolutionTrigger.Level) {
                preview.RequirementMet = preview.CurrentLevel >= rule.RequiredLevel;
                if (!preview.RequirementMet) {
                    preview.DisabledReason = $"Cần đạt Lv. {rule.RequiredLevel}. Hiện tại Lv. {preview.CurrentLevel}.";
                }
            }
            else {
                preview.Item = FindItem(rule.RequiredItemId);
                preview.OwnedItemQuantity = preview.Item?.Quantity ?? 0;
                preview.RequirementMet = !string.IsNullOrWhiteSpace(rule.RequiredItemId)
                    && preview.Item != null
                    && preview.OwnedItemQuantity >= rule.RequiredItemQuantity;
                if (string.IsNullOrWhiteSpace(rule.RequiredItemId)) {
                    preview.DisabledReason = "Rule tiến hóa chưa khai báo Item ID.";
                }
                else if (preview.Item == null) {
                    preview.DisabledReason = $"Chưa có {rule.RequiredItemId} trong Bag.";
                }
                else if (!preview.RequirementMet) {
                    preview.DisabledReason = $"Cần thêm {rule.RequiredItemQuantity - preview.OwnedItemQuantity} {rule.RequiredItemId}.";
                }
            }

            if (preview.RequirementMet && saveController == null) {
                preview.DisabledReason = "Không tìm thấy PlayerSaveController để lưu tiến hóa.";
            }

            preview.CanEvolve = preview.RequirementMet && saveController != null;
            preview.ShouldAutoPrompt = rule.Trigger == PetEvolutionTrigger.Level
                && preview.CanEvolve
                && !preview.Metadata.WasEvolutionPrompted(rule.EvolutionId);
            return preview;
        }

        public bool TryEvolve(PetController pet, out string message) {
            PetEvolutionPreview preview = CreatePreview(pet);
            if (!preview.CanEvolve || preview.Rule == null) {
                message = preview.DisabledReason;
                return false;
            }

            if (!preview.Metadata.CanApplyEvolution(preview.Rule)) {
                message = "Dạng tiến hóa không còn khớp với trạng thái hiện tại.";
                return false;
            }

            PetCustomizationSaveData metadataSnapshot = preview.Metadata.CreateSaveData();
            PetRuntimeStatsSaveData statsSnapshot = preview.RuntimeStats.CreateSaveData();

            bool consumedEvolutionItem = preview.Rule.Trigger == PetEvolutionTrigger.Item;
            if (consumedEvolutionItem) {
                Result removeResult = inventory.RemoveItem(preview.Item.ItemBase, preview.Rule.RequiredItemQuantity);
                if (removeResult is Fail) {
                    message = $"Không thể dùng {preview.Rule.RequiredItemId}. Hãy kiểm tra lại Bag.";
                    return false;
                }
            }

            PetHudRuntimeStats stats = preview.RuntimeStats;
            float healthRatio = stats.MaxHealth > 0f ? Mathf.Clamp01(stats.Health / stats.MaxHealth) : 1f;
            float newMaxHealth = Mathf.Max(1f, stats.MaxHealth * preview.Rule.MaxHealthMultiplier);
            float newHealth = newMaxHealth * healthRatio;

            if (!preview.Metadata.ApplyEvolution(preview.Rule)) {
                if (consumedEvolutionItem) {
                    inventory.AddItem(preview.Item.ItemBase, preview.Rule.RequiredItemQuantity);
                }
                message = "Không thể áp dụng dạng tiến hóa đã chọn.";
                return false;
            }

            preview.Metadata.SetStats(
                preview.Metadata.Attack + preview.Rule.AttackBonus,
                preview.Metadata.Defense + preview.Rule.DefenseBonus,
                preview.Metadata.Speed + preview.Rule.SpeedBonus);

            if (consumedEvolutionItem) {
                preview.Metadata.RecordResourceInvestment(
                    preview.Item.ItemBase,
                    preview.Item.ItemId,
                    preview.Item.Name,
                    preview.Rule.RequiredItemQuantity);
            }

            string nextBaseName = string.IsNullOrWhiteSpace(preview.Rule.TargetDisplayName)
                ? stats.DisplayName
                : preview.Rule.TargetDisplayName;
            stats.SetIdentity(nextBaseName, stats.Level, preview.Rule.TargetIcon ?? stats.Icon);
            stats.SetStatus(newHealth, newMaxHealth, stats.Energy, stats.MaxEnergy);

            if (saveController == null || !saveController.SaveNow()) {
                preview.Metadata.RestoreFromSaveData(metadataSnapshot);
                stats.RestoreFromSaveData(statsSnapshot);
                if (consumedEvolutionItem) {
                    inventory.AddItem(preview.Item.ItemBase, preview.Rule.RequiredItemQuantity);
                }
                message = "Không thể lưu lần tiến hóa. Mọi thay đổi đã được hoàn tác.";
                return false;
            }

            PetEvolutionVisualController visualController = pet.GetComponentInChildren<PetEvolutionVisualController>(true);
            if (preview.Rule.TargetVisualPrefab != null && visualController != null
                && !visualController.TryApply(preview.Rule.TargetVisualPrefab, out string visualError)) {
                Debug.LogWarning(visualError, pet);
            }

            PetEvolved?.Invoke(pet);
            message = $"{preview.CurrentName} đã tiến hóa thành {preview.TargetName}.";
            return true;
        }

        public void MarkAutoPromptShown(PetEvolutionPreview preview) {
            if (preview?.Metadata == null || preview.Rule == null) return;
            preview.Metadata.MarkEvolutionPrompted(preview.Rule.EvolutionId);
            ResolveSaveController();
            saveController?.RequestSave();
        }

        void ResolveInventory() {
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
        }

        void ResolveSaveController() {
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>();
        }

        InventoryItemSnapshot FindItem(string itemId) {
            if (inventory == null || string.IsNullOrWhiteSpace(itemId)) return null;
            var items = inventory.GetItems();
            for (int i = 0; i < items.Count; i++) {
                InventoryItemSnapshot item = items[i];
                if (item == null) continue;
                if (string.Equals(item.ItemId, itemId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Name, itemId, StringComparison.OrdinalIgnoreCase)) {
                    return item;
                }
            }

            return null;
        }
    }

    public sealed class PetEvolutionPreview {
        public PetController Pet;
        public PetHudRuntimeStats RuntimeStats;
        public PetCollectionMetadata Metadata;
        public PetEvolutionRule Rule;
        public InventoryItemSnapshot Item;
        public bool IsValid;
        public bool IsConfigured;
        public bool RequirementMet;
        public bool CanEvolve;
        public bool ShouldAutoPrompt;
        public string DisabledReason = string.Empty;
        public string CurrentName = string.Empty;
        public string TargetName = string.Empty;
        public Sprite CurrentIcon;
        public Sprite TargetIcon;
        public int CurrentLevel;
        public int RequiredLevel;
        public string RequiredItemId = string.Empty;
        public int RequiredItemQuantity;
        public int OwnedItemQuantity;
    }
}

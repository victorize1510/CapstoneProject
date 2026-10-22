using System;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    public enum PetEvolutionMethod { Level, Item }

    [DisallowMultipleComponent]
    public sealed class PetEvolutionService : MonoBehaviour {
        MonsterInventoryAdapter inventory;
        PlayerSaveController saveController;

        public event Action<PetController> PetEvolved;

        public void Bind(MonsterInventoryAdapter targetInventory) {
            inventory = targetInventory != null ? targetInventory : inventory;
        }

        public PetEvolutionPreview CreatePreview(PetController pet, PetEvolutionMethod method = PetEvolutionMethod.Level,
            string requestedItemId = null) {
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

            bool levelAllowed = rule.Trigger != PetEvolutionTrigger.Item;
            bool itemAllowed = rule.Trigger != PetEvolutionTrigger.Level;
            if (method == PetEvolutionMethod.Item && itemAllowed) {
                preview.Item = FindItem(rule.RequiredItemId);
                preview.OwnedItemQuantity = preview.Item?.Quantity ?? 0;
            }
            preview.UsesItem = method == PetEvolutionMethod.Item;
            if (method == PetEvolutionMethod.Level) {
                preview.RequirementMet = levelAllowed && preview.CurrentLevel >= rule.RequiredLevel;
                if (!levelAllowed) preview.DisabledReason = "Dạng này chỉ tiến hóa bằng vật phẩm trong Bag.";
                else if (!preview.RequirementMet) preview.DisabledReason = $"Cần đạt Lv. {rule.RequiredLevel}.";
            } else if (!itemAllowed || string.IsNullOrWhiteSpace(rule.RequiredItemId)
                || !string.Equals(requestedItemId, rule.RequiredItemId, StringComparison.OrdinalIgnoreCase)) {
                preview.DisabledReason = "Vật phẩm không phù hợp với dạng tiến hóa này.";
            } else {
                preview.RequirementMet = preview.Item != null
                    && preview.OwnedItemQuantity >= rule.RequiredItemQuantity;
                if (preview.Item == null) preview.DisabledReason = $"Chưa có {rule.RequiredItemId} trong Bag.";
                else if (!preview.RequirementMet)
                    preview.DisabledReason = $"Cần thêm {rule.RequiredItemQuantity - preview.OwnedItemQuantity} {rule.RequiredItemId}.";
            }

            Capstone.Game.PetSummon.PetSummonDirector director =
                FindFirstObjectByType<Capstone.Game.PetSummon.PetSummonDirector>();
            if (director != null && director.IsSequenceRunning) {
                preview.DisabledReason = "Hãy đợi thao tác gọi/thu pet hoàn tất.";
                preview.RequirementMet = false;
            }
            if (pet.IsSummonPresentationPending) {
                preview.DisabledReason = "Hãy đợi pet xuất hiện hoàn tất.";
                preview.RequirementMet = false;
            }

            if (preview.RequirementMet && saveController == null) {
                preview.DisabledReason = "Không tìm thấy PlayerSaveController để lưu tiến hóa.";
            }

            preview.CanEvolve = preview.RequirementMet && saveController != null;
            preview.ShouldAutoPrompt = method == PetEvolutionMethod.Level && preview.CanEvolve
                && !preview.Metadata.WasEvolutionPrompted(rule.EvolutionId);
            return preview;
        }

        public bool TryEvolve(PetController pet, out string message,
            PetEvolutionMethod method = PetEvolutionMethod.Level, string requestedItemId = null) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryBegin(this, "evolve", out var operation)) { message = string.Empty; return false; }
            using (operation) return TryEvolveCore(pet, out message, method, requestedItemId);
        }

        bool TryEvolveCore(PetController pet, out string message,
            PetEvolutionMethod method = PetEvolutionMethod.Level, string requestedItemId = null) {
            PetEvolutionPreview preview = CreatePreview(pet, method, requestedItemId);
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

            bool consumedEvolutionItem = preview.UsesItem;

            PetHudRuntimeStats stats = preview.RuntimeStats;
            float healthRatio = stats.MaxHealth > 0f ? Mathf.Clamp01(stats.Health / stats.MaxHealth) : 1f;
            if (!preview.Metadata.ApplyEvolution(preview.Rule)) {
                message = "Không thể áp dụng dạng tiến hóa đã chọn.";
                return false;
            }

            float newMaxHealth = preview.Metadata.GetStatsAtLevel(stats.Level).MaxHealth;
            float newHealth = newMaxHealth * healthRatio;

            string nextBaseName = string.IsNullOrWhiteSpace(preview.Rule.TargetDisplayName)
                ? stats.DisplayName
                : preview.Rule.TargetDisplayName;
            stats.SetIdentity(nextBaseName, stats.Level, preview.Rule.TargetIcon ?? stats.Icon);
            stats.SetStatus(newHealth, newMaxHealth, stats.Energy, stats.MaxEnergy);

            PetEvolutionVisualController visualController = pet.GetComponentInChildren<PetEvolutionVisualController>(true);
            string visualError = string.Empty;
            if (preview.Rule.TargetVisualPrefab == null || visualController == null
                || !visualController.TryApply(preview.Rule.TargetVisualPrefab, out visualError)) {
                preview.Metadata.RestoreFromSaveData(metadataSnapshot);
                stats.RestoreFromSaveData(statsSnapshot);
                message = preview.Rule.TargetVisualPrefab == null
                    ? "Chưa gán model cho dạng tiến hóa."
                    : visualController == null ? "Pet thiếu PetEvolutionVisualController." : visualError;
                return false;
            }

            if (consumedEvolutionItem) {
                Result removeResult = inventory.RemoveItem(preview.Item.ItemBase, preview.Rule.RequiredItemQuantity);
                if (removeResult is Fail) {
                    preview.Metadata.RestoreFromSaveData(metadataSnapshot);
                    stats.RestoreFromSaveData(statsSnapshot);
                    visualController.RestoreForm(metadataSnapshot.evolutionStage);
                    message = $"Không thể dùng {preview.Rule.RequiredItemId}. Hãy kiểm tra lại Bag.";
                    return false;
                }
                preview.Metadata.RecordResourceInvestment(preview.Item.ItemBase, preview.Item.ItemId,
                    preview.Item.Name, preview.Rule.RequiredItemQuantity);
            }

            if (saveController == null || !saveController.SaveNow()) {
                preview.Metadata.RestoreFromSaveData(metadataSnapshot);
                stats.RestoreFromSaveData(statsSnapshot);
                visualController.RestoreForm(metadataSnapshot.evolutionStage);
                if (consumedEvolutionItem) {
                    inventory.AddItem(preview.Item.ItemBase, preview.Rule.RequiredItemQuantity);
                }
                message = "Không thể lưu lần tiến hóa. Mọi thay đổi đã được hoàn tác.";
                return false;
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
        public bool UsesItem;
    }
}

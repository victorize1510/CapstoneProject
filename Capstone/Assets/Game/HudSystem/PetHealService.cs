using System;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetHealService : MonoBehaviour {
        [Header("Healing Item")]
        [SerializeField] string preferredHealingItemId = "Potion";
        [SerializeField, Min(1)] int requiredQuantity = 1;
        [SerializeField, Min(1)] int fallbackHealAmount = 400;

        MonsterInventoryAdapter inventory;
        PlayerSaveController saveController;

        public event Action<PetController> PetHealed;

        public void Bind(MonsterInventoryAdapter targetInventory) {
            inventory = targetInventory != null ? targetInventory : inventory;
        }

        public PetHealPreview CreatePreview(PetController pet) => CreatePreview(pet, null);

        public PetHealPreview CreatePreview(PetController pet, ItemBase exactItem) {
            var preview = new PetHealPreview { Pet = pet };
            if (pet == null) {
                preview.DisabledReason = "Chưa chọn thú để hồi phục.";
                return preview;
            }

            preview.RuntimeStats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            preview.Metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (preview.RuntimeStats == null) {
                preview.DisabledReason = "Thú chưa có dữ liệu HP.";
                return preview;
            }

            ResolveInventory();
            ResolveSaveController();
            preview.Item = exactItem == null ? FindHealingItem() : FindExactHealingItem(exactItem);
            preview.ItemName = preview.Item != null ? preview.Item.Name : preferredHealingItemId;
            preview.ItemIcon = preview.Item?.Icon;
            preview.OwnedQuantity = preview.Item != null ? preview.Item.Quantity : 0;
            preview.RequiredQuantity = Mathf.Max(1, requiredQuantity);
            preview.HealAmount = preview.Item != null ? preview.Item.HealAmount : Mathf.Max(1, fallbackHealAmount);
            preview.EnergyRestoreAmount = preview.Item?.EnergyRestoreAmount ?? 0;

            preview.CurrentHealth = preview.RuntimeStats.Health;
            preview.MaxHealth = preview.RuntimeStats.MaxHealth;
            preview.CurrentEnergy = preview.RuntimeStats.Energy;
            preview.MaxEnergy = preview.RuntimeStats.MaxEnergy;
            preview.ResultEnergy = Mathf.Min(preview.MaxEnergy, preview.CurrentEnergy + preview.EnergyRestoreAmount);
            preview.ActualEnergyRestore = Mathf.Max(0f,preview.ResultEnergy - preview.CurrentEnergy);
            preview.IsValid = preview.RestoresEnergy ? preview.MaxEnergy > 0f : preview.MaxHealth > 0f;
            preview.IsFullHealth = preview.IsValid
                && preview.CurrentHealth >= preview.MaxHealth - 0.01f;
            preview.ResultHealth = preview.IsValid
                ? Mathf.Min(preview.MaxHealth, preview.CurrentHealth + preview.HealAmount)
                : 0f;
            preview.ActualHealAmount = Mathf.Max(0f, preview.ResultHealth - preview.CurrentHealth);

            if (!preview.IsValid) {
                preview.DisabledReason = preview.RestoresEnergy ? "Thú này chưa sử dụng MP." : "Thú chưa có Max HP hợp lệ.";
            }
            else if (preview.IsFullResource) {
                preview.DisabledReason = preview.ResourceName + " ĐÃ ĐẦY";
            }
            else if (preview.Item == null) {
                preview.DisabledReason = $"Không có {preview.ItemName} trong Túi đồ.";
            }
            else if (preview.OwnedQuantity < preview.RequiredQuantity) {
                preview.DisabledReason = $"Không đủ {preview.ItemName}.";
            }
            else if (saveController == null) {
                preview.DisabledReason = "Không tìm thấy PlayerSaveController để lưu hồi phục.";
            }

            preview.CanHeal = string.IsNullOrEmpty(preview.DisabledReason);
            return preview;
        }

        public bool TryHeal(PetController pet, out string message) => TryHeal(pet, null, out message);

        public bool TryHeal(PetController pet, ItemBase exactItem, out string message) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryBegin(this, "heal", out var operation)) { message = string.Empty; return false; }
            using (operation) return TryHealCore(pet, exactItem, out message);
        }

        bool TryHealCore(PetController pet, ItemBase exactItem, out string message) {
            PetHealPreview preview = CreatePreview(pet, exactItem);
            if (!preview.CanHeal) {
                message = preview.DisabledReason;
                return false;
            }

            PetRuntimeStatsSaveData statsSnapshot = preview.RuntimeStats.CreateSaveData();

            Result removeResult = inventory.RemoveItem(preview.Item.ItemBase, preview.RequiredQuantity);
            if (removeResult is Fail) {
                message = $"Không thể dùng {preview.ItemName}. Hãy kiểm tra lại Túi đồ.";
                return false;
            }

            PetHudRuntimeStats stats = preview.RuntimeStats;
            stats.SetStatus(preview.ResultHealth, stats.MaxHealth, preview.ResultEnergy, stats.MaxEnergy);

            if (saveController == null || !saveController.SaveNow()) {
                stats.RestoreFromSaveData(statsSnapshot);
                inventory.AddItem(preview.Item.ItemBase, preview.RequiredQuantity);
                message = "Không thể lưu lần hồi phục. Mọi thay đổi đã được hoàn tác.";
                return false;
            }

            PetHealed?.Invoke(pet);
            string baseName = stats.DisplayName;
            string displayName = preview.Metadata != null
                ? preview.Metadata.ResolveDisplayName(baseName)
                : baseName;
            message = $"{displayName} đã hồi {Mathf.RoundToInt(preview.ActualRestoreAmount):N0} {preview.ResourceName}.";
            return true;
        }

        void ResolveInventory() {
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
        }

        void ResolveSaveController() {
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>();
        }

        InventoryItemSnapshot FindExactHealingItem(ItemBase itemBase) {
            if (inventory == null) return null;
            foreach (InventoryItemSnapshot item in inventory.GetItems()) {
                if (item.ItemBase == itemBase && item.IsRecoveryItem) return item;
            }
            return null;
        }

        InventoryItemSnapshot FindHealingItem() {
            if (inventory == null) return null;

            var items = inventory.GetItems();
            for (int i = 0; i < items.Count; i++) {
                InventoryItemSnapshot item = items[i];
                if (item == null || !item.IsRecoveryItem || item.HealAmount <= 0) continue;
                if (string.Equals(item.ItemId, preferredHealingItemId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Name, preferredHealingItemId, StringComparison.OrdinalIgnoreCase)) {
                    return item;
                }
            }

            for (int i = 0; i < items.Count; i++) {
                InventoryItemSnapshot item = items[i];
                if (item != null && item.IsRecoveryItem && item.HealAmount > 0) {
                    return item;
                }
            }

            return null;
        }

        void OnValidate() {
            requiredQuantity = Mathf.Max(1, requiredQuantity);
            fallbackHealAmount = Mathf.Max(1, fallbackHealAmount);
        }
    }

    public sealed class PetHealPreview {
        public PetController Pet;
        public PetHudRuntimeStats RuntimeStats;
        public PetCollectionMetadata Metadata;
        public InventoryItemSnapshot Item;
        public Sprite ItemIcon;
        public bool IsValid;
        public bool IsFullHealth;
        public bool CanHeal;
        public string DisabledReason = string.Empty;
        public string ItemName = string.Empty;
        public float CurrentHealth;
        public float MaxHealth;
        public float ResultHealth;
        public float ActualHealAmount;
        public float CurrentEnergy, MaxEnergy, ResultEnergy, ActualEnergyRestore;
        public int EnergyRestoreAmount;
        public bool RestoresEnergy => EnergyRestoreAmount > 0;
        public string ResourceName => RestoresEnergy ? "MP" : "HP";
        public float CurrentValue => RestoresEnergy ? CurrentEnergy : CurrentHealth;
        public float MaximumValue => RestoresEnergy ? MaxEnergy : MaxHealth;
        public float ResultValue => RestoresEnergy ? ResultEnergy : ResultHealth;
        public float ActualRestoreAmount => RestoresEnergy ? ActualEnergyRestore : ActualHealAmount;
        public int RestoreAmount => RestoresEnergy ? EnergyRestoreAmount : HealAmount;
        public bool IsFullResource => RestoresEnergy ? MaxEnergy > 0 && CurrentEnergy >= MaxEnergy - .01f : IsFullHealth;
        public int HealAmount;
        public int OwnedQuantity;
        public int RequiredQuantity;
    }
}

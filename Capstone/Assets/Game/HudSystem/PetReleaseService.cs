using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetReleaseService : MonoBehaviour {
        [Header("References")]
        [SerializeField] MonsterInventoryAdapter inventory = null;
        [SerializeField] PetBoxRuntimeProvider roster = null;
        [SerializeField] PlayerCurrencyWallet currencyWallet = null;
        [SerializeField] PlayerSaveController saveController = null;

        [Header("Refund")]
        [SerializeField, Range(0f, 1f)] float releaseRefundRate = 0.30f;

        [Header("Gold Value")]
        [SerializeField, Min(0)] int baseGold = 300;
        [SerializeField, Min(0)] int levelGoldBonus = 20;
        [SerializeField, Min(0f)] float commonMultiplier = 1f;
        [SerializeField, Min(0f)] float uncommonMultiplier = 1.2f;
        [SerializeField, Min(0f)] float rareMultiplier = 1.5f;
        [SerializeField, Min(0f)] float epicMultiplier = 2f;
        [SerializeField, Min(0f)] float legendaryMultiplier = 3f;
        [SerializeField, Min(0f)] float stageOneMultiplier = 1f;
        [SerializeField, Min(0f)] float stageTwoMultiplier = 1.35f;
        [SerializeField, Min(0f)] float stageThreeMultiplier = 1.7f;

        [Header("Protection")]
        [SerializeField] List<string> protectedSpecies = new List<string>();
        [SerializeField, Min(1)] int dailyReleaseLimit = 100;

        public float ReleaseRefundRate => Mathf.Clamp01(releaseRefundRate);
        public int DailyReleaseLimit => Mathf.Max(1, dailyReleaseLimit);

        public event Action<PetReleasePreview> PetReleased;

        public void Bind(MonsterInventoryAdapter targetInventory) {
            inventory = targetInventory != null ? targetInventory : inventory;
            ResolveReferences();
        }

        public PetReleasePreview CreatePreview(PetController pet) {
            ResolveReferences();
            var preview = new PetReleasePreview { Pet = pet };
            if (pet == null) {
                preview.DisabledReason = "Chưa chọn pet để thả.";
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
            preview.DisplayName = preview.Metadata.ResolveDisplayName(preview.RuntimeStats.DisplayName);
            preview.Species = preview.Metadata.Species;
            preview.Element = preview.Metadata.Element;
            preview.Rarity = preview.Metadata.Rarity;
            preview.Level = Mathf.Max(0, preview.RuntimeStats.Level);
            preview.EvolutionStage = preview.Metadata.EvolutionStage + 1;
            preview.Icon = preview.RuntimeStats.Icon;
            preview.IsFavorite = preview.Metadata.IsFavorite;
            preview.GoldValue = CalculateGoldValue(preview.Rarity, preview.Level, preview.EvolutionStage);
            preview.ReleasedToday = roster != null ? roster.ReleasedToday : 0;
            preview.DailyReleaseLimit = DailyReleaseLimit;

            BuildRefunds(preview);

            if (preview.Metadata.IsReleased) preview.DisabledReason = "Pet này đã được thả.";
            else if (preview.IsFavorite) preview.DisabledReason = "Bỏ Yêu thích trước khi thả pet.";
            else if (IsProtectedSpecies(preview.Species)) preview.DisabledReason = "Pet này được bảo vệ và không thể thả.";
            else if (roster == null || !roster.Contains(pet)) preview.DisabledReason = "Pet không còn nằm trong Party hoặc Box.";
            else if (roster.OwnedCount <= 1) preview.DisabledReason = "Không thể thả pet cuối cùng của bạn.";
            else if (roster.ReleasedToday >= DailyReleaseLimit) preview.DisabledReason = $"Đã đạt giới hạn thả {DailyReleaseLimit} pet hôm nay.";
            else if (currencyWallet == null) preview.DisabledReason = "Không tìm thấy ví Gold của người chơi.";
            else if (saveController == null) preview.DisabledReason = "Không tìm thấy PlayerSaveController để lưu thao tác.";
            else if (preview.HasUnresolvedRefund) preview.DisabledReason = "Thiếu Item Definition để hoàn lại tài nguyên đã đầu tư.";

            preview.CanRelease = string.IsNullOrWhiteSpace(preview.DisabledReason);
            return preview;
        }

        public bool TryRelease(PetController pet, out string message) {
            PetReleasePreview preview = CreatePreview(pet);
            if (!preview.CanRelease) {
                message = preview.DisabledReason;
                return false;
            }

            var addedRefunds = new List<PetReleaseRefundPreview>();
            for (int i = 0; i < preview.Refunds.Count; i++) {
                PetReleaseRefundPreview refund = preview.Refunds[i];
                Result addResult = inventory.AddItem(refund.ItemBase, refund.RefundQuantity);
                if (addResult is Fail) {
                    RollbackRefunds(addedRefunds);
                    message = "Bag không đủ chỗ để nhận toàn bộ tài nguyên hoàn lại.";
                    return false;
                }
                addedRefunds.Add(refund);
            }

            currencyWallet.AddGold(preview.GoldValue);
            if (!roster.TryDetachPetForRelease(pet, out PetRosterReleaseToken rosterToken, out string rosterError)) {
                currencyWallet.TrySpendGold(preview.GoldValue);
                RollbackRefunds(addedRefunds);
                message = rosterError;
                return false;
            }

            if (!roster.TryConsumeDailyRelease(DailyReleaseLimit, out string dailyLimitError)) {
                roster.RestoreDetachedPet(rosterToken, out _);
                currencyWallet.TrySpendGold(preview.GoldValue);
                RollbackRefunds(addedRefunds);
                message = dailyLimitError;
                return false;
            }

            preview.Metadata.SetReleased(true);
            if (!saveController.SaveNow()) {
                preview.Metadata.SetReleased(false);
                roster.UndoDailyRelease();
                roster.RestoreDetachedPet(rosterToken, out _);
                currencyWallet.TrySpendGold(preview.GoldValue);
                RollbackRefunds(addedRefunds);
                message = "Không thể lưu game nên pet chưa bị thả.";
                return false;
            }

            pet.gameObject.SetActive(false);
            PetReleased?.Invoke(preview);
            message = $"Đã thả {preview.DisplayName}. Nhận {preview.GoldValue:N0} Gold.";
            return true;
        }

        void BuildRefunds(PetReleasePreview preview) {
            IReadOnlyList<PetResourceInvestment> investments = preview.Metadata.ResourceInvestments;
            if (investments == null) return;

            for (int i = 0; i < investments.Count; i++) {
                PetResourceInvestment investment = investments[i];
                if (investment == null || !investment.Refundable || investment.Quantity <= 0) continue;

                int refundQuantity = Mathf.FloorToInt(investment.Quantity * ReleaseRefundRate);
                if (refundQuantity <= 0) continue;

                ItemBase itemBase = investment.ItemBase != null
                    ? investment.ItemBase
                    : inventory?.FindItemBase(investment.ItemId);
                if (itemBase == null) preview.HasUnresolvedRefund = true;

                preview.Refunds.Add(new PetReleaseRefundPreview {
                    ItemBase = itemBase,
                    ItemId = investment.ItemId,
                    DisplayName = string.IsNullOrWhiteSpace(investment.DisplayName)
                        ? investment.ItemId
                        : investment.DisplayName,
                    Icon = itemBase != null ? itemBase.Icon : null,
                    InvestedQuantity = investment.Quantity,
                    RefundQuantity = refundQuantity
                });
            }
        }

        int CalculateGoldValue(PetRarity rarity, int level, int evolutionStage) {
            float rarityMultiplier = GetRarityMultiplier(rarity);
            float evolutionMultiplier = evolutionStage <= 1
                ? stageOneMultiplier
                : evolutionStage == 2
                    ? stageTwoMultiplier
                    : stageThreeMultiplier;
            float value = Mathf.Max(0, baseGold) * Mathf.Max(0f, rarityMultiplier) * Mathf.Max(0f, evolutionMultiplier)
                + Mathf.Max(0, level) * Mathf.Max(0, levelGoldBonus);
            return Mathf.Max(0, Mathf.FloorToInt(value));
        }

        float GetRarityMultiplier(PetRarity rarity) {
            switch (rarity) {
                case PetRarity.Uncommon: return uncommonMultiplier;
                case PetRarity.Rare: return rareMultiplier;
                case PetRarity.Epic: return epicMultiplier;
                case PetRarity.Legendary: return legendaryMultiplier;
                default: return commonMultiplier;
            }
        }

        bool IsProtectedSpecies(string species) {
            if (string.IsNullOrWhiteSpace(species) || protectedSpecies == null) return false;
            for (int i = 0; i < protectedSpecies.Count; i++) {
                if (string.Equals(protectedSpecies[i]?.Trim(), species.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        void RollbackRefunds(IReadOnlyList<PetReleaseRefundPreview> refunds) {
            if (inventory == null || refunds == null) return;
            for (int i = refunds.Count - 1; i >= 0; i--) {
                PetReleaseRefundPreview refund = refunds[i];
                if (refund?.ItemBase != null && refund.RefundQuantity > 0) {
                    inventory.RemoveItem(refund.ItemBase, refund.RefundQuantity);
                }
            }
        }

        void ResolveReferences() {
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);
            if (roster == null) roster = GetComponent<PetBoxRuntimeProvider>();
            if (roster == null) roster = GetComponentInParent<PetBoxRuntimeProvider>();
            if (roster == null) roster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
            if (currencyWallet == null) currencyWallet = FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
            if (currencyWallet == null && Application.isPlaying) currencyWallet = gameObject.AddComponent<PlayerCurrencyWallet>();
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
        }

        void OnValidate() {
            releaseRefundRate = Mathf.Clamp01(releaseRefundRate);
            baseGold = Mathf.Max(0, baseGold);
            levelGoldBonus = Mathf.Max(0, levelGoldBonus);
            commonMultiplier = Mathf.Max(0f, commonMultiplier);
            uncommonMultiplier = Mathf.Max(0f, uncommonMultiplier);
            rareMultiplier = Mathf.Max(0f, rareMultiplier);
            epicMultiplier = Mathf.Max(0f, epicMultiplier);
            legendaryMultiplier = Mathf.Max(0f, legendaryMultiplier);
            stageOneMultiplier = Mathf.Max(0f, stageOneMultiplier);
            stageTwoMultiplier = Mathf.Max(0f, stageTwoMultiplier);
            stageThreeMultiplier = Mathf.Max(0f, stageThreeMultiplier);
            dailyReleaseLimit = Mathf.Max(1, dailyReleaseLimit);
            protectedSpecies ??= new List<string>();
        }
    }

    public sealed class PetReleasePreview {
        public PetController Pet;
        public PetHudRuntimeStats RuntimeStats;
        public PetCollectionMetadata Metadata;
        public readonly List<PetReleaseRefundPreview> Refunds = new List<PetReleaseRefundPreview>();
        public bool IsValid;
        public bool CanRelease;
        public bool IsFavorite;
        public bool HasUnresolvedRefund;
        public string DisabledReason = string.Empty;
        public string DisplayName = string.Empty;
        public string Species = string.Empty;
        public PetElement Element;
        public PetRarity Rarity;
        public Sprite Icon;
        public int Level;
        public int EvolutionStage;
        public int GoldValue;
        public int ReleasedToday;
        public int DailyReleaseLimit;
    }

    public sealed class PetReleaseRefundPreview {
        public ItemBase ItemBase;
        public string ItemId = string.Empty;
        public string DisplayName = string.Empty;
        public Sprite Icon;
        public int InvestedQuantity;
        public int RefundQuantity;
    }
}

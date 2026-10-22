using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.SaveSystem;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class QuestRewardService : MonoBehaviour {
        readonly HashSet<string> processingQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [SerializeField] QuestManager questManager;
        [SerializeField] MonsterInventoryAdapter inventory;
        [SerializeField] PlayerCurrencyWallet currencyWallet;
        [SerializeField] PlayerProfileRuntimeProvider profileProvider;
        [SerializeField] PlayerSaveController saveController;

        bool subscribed;

        public string LastFailedQuestId { get; private set; } = string.Empty;
        public string LastFailureMessage { get; private set; } = string.Empty;
        public event Action<string, string> RewardGrantFailed;
        public event Action<string> RewardsGranted;

        public void Bind(
            QuestManager manager,
            MonsterInventoryAdapter inventoryAdapter,
            PlayerCurrencyWallet wallet,
            PlayerProfileRuntimeProvider profile,
            PlayerSaveController saves) {
            Unsubscribe();
            questManager = manager;
            inventory = inventoryAdapter;
            currencyWallet = wallet;
            profileProvider = profile;
            saveController = saves;
            Subscribe();
        }

        void OnEnable() {
            ResolveReferences();
            Subscribe();
        }

        void Start() {
            ResolveReferences();
            Subscribe();
            RetryPendingRewards();
        }

        void OnDisable() {
            Unsubscribe();
        }

        void Subscribe() {
            if (subscribed || questManager == null) return;
            questManager.QuestRewardsReady += HandleRewardsReady;
            if (saveController != null) saveController.LoadCompleted += HandleLoadCompleted;
            subscribed = true;
        }

        void Unsubscribe() {
            if (!subscribed) return;
            if (questManager != null) questManager.QuestRewardsReady -= HandleRewardsReady;
            if (saveController != null) saveController.LoadCompleted -= HandleLoadCompleted;
            subscribed = false;
        }

        void HandleLoadCompleted(PlayerSaveData _) {
            RetryPendingRewards();
        }

        public void RetryPendingRewards() {
            if (questManager == null) return;
            foreach (QuestRuntimeState state in new List<QuestRuntimeState>(questManager.GetAllQuests())) {
                if (state == null || state.Status != QuestStatus.Completed || state.RewardsClaimed) continue;
                HandleRewardsReady(state, state.Definition != null ? state.Definition.Rewards : Array.Empty<QuestRewardDefinition>());
            }
        }

        void HandleRewardsReady(QuestRuntimeState state, IReadOnlyList<QuestRewardDefinition> rewards) {
            if (state == null || state.Status != QuestStatus.Completed || state.RewardsClaimed) return;
            if (string.IsNullOrWhiteSpace(state.QuestId) || !processingQuestIds.Add(state.QuestId)) return;

            try {
                if (!TryGrantTransaction(state, rewards, out string error)) {
                    LastFailedQuestId = state.QuestId;
                    LastFailureMessage = error;
                    RewardGrantFailed?.Invoke(state.QuestId, error);
                    Debug.LogWarning($"Could not grant rewards for quest '{state.QuestId}': {error}", this);
                }
                else {
                    if (string.Equals(LastFailedQuestId, state.QuestId, StringComparison.OrdinalIgnoreCase)) {
                        LastFailedQuestId = string.Empty;
                        LastFailureMessage = string.Empty;
                    }
                    RewardsGranted?.Invoke(state.QuestId);
                }
            } finally {
                processingQuestIds.Remove(state.QuestId);
            }
        }

        bool TryGrantTransaction(
            QuestRuntimeState state,
            IReadOnlyList<QuestRewardDefinition> rewards,
            out string error) {
            error = string.Empty;
            if (saveController == null) {
                error = "Không thể nhận thưởng vì hệ thống lưu chưa sẵn sàng.";
                return false;
            }
            if (!ValidateRewards(rewards, out error)) return false;

            int grantedGold = 0;
            var grantedItems = new List<GrantedItem>();
            PlayerProfileSaveData profileSnapshot = profileProvider != null ? profileProvider.CreateSaveData() : null;

            try {
                if (rewards != null) {
                    foreach (QuestRewardDefinition reward in rewards) {
                        if (reward == null) continue;
                        switch (reward.RewardType) {
                            case QuestRewardType.Gold:
                            case QuestRewardType.Currency:
                                currencyWallet.AddGold(reward.Amount);
                                grantedGold += reward.Amount;
                                break;

                            case QuestRewardType.Item:
                                ItemBase itemBase = ResolveItem(reward);
                                Result addResult = inventory.AddItem(itemBase, reward.Amount);
                                if (addResult is not Success) {
                                    error = $"Túi đồ không còn chỗ cho {reward.DisplayName}. Hãy dọn túi rồi mở lại Quest để thử nhận thưởng.";
                                    Rollback(grantedGold, grantedItems, profileSnapshot, state);
                                    return false;
                                }
                                grantedItems.Add(new GrantedItem(itemBase, reward.Amount));
                                break;

                            case QuestRewardType.Experience:
                                profileProvider.AddExperience(reward.Amount);
                                break;

                        }
                    }
                }

                if (!questManager.SetRewardsClaimed(state, true)) {
                    error = "Không thể cập nhật trạng thái đã nhận thưởng của nhiệm vụ.";
                    Rollback(grantedGold, grantedItems, profileSnapshot, state);
                    return false;
                }

                if (!saveController.SaveNow()) {
                    error = "Không thể lưu giao dịch nhận thưởng. Phần thưởng đã được hoàn tác.";
                    Rollback(grantedGold, grantedItems, profileSnapshot, state);
                    return false;
                }

                return true;
            } catch (Exception exception) {
                error = exception.Message;
                Rollback(grantedGold, grantedItems, profileSnapshot, state);
                return false;
            }
        }

        bool ValidateRewards(IReadOnlyList<QuestRewardDefinition> rewards, out string error) {
            error = string.Empty;
            if (rewards == null) return true;

            foreach (QuestRewardDefinition reward in rewards) {
                if (reward == null) continue;
                switch (reward.RewardType) {
                    case QuestRewardType.Gold:
                        if (currencyWallet != null) continue;
                        error = "Ví Gold chưa sẵn sàng.";
                        return false;

                    case QuestRewardType.Currency:
                        if (currencyWallet != null && IsGoldCurrency(reward.CurrencyId)) continue;
                        error = $"Loại tiền '{reward.CurrencyId}' chưa được kết nối.";
                        return false;

                    case QuestRewardType.Item:
                        if (inventory != null && ResolveItem(reward) != null) continue;
                        error = $"Item thưởng '{reward.TargetId}' chưa có trong danh mục Inventory.";
                        return false;

                    case QuestRewardType.Experience:
                        if (profileProvider != null) continue;
                        error = "Hồ sơ người chơi chưa sẵn sàng để nhận EXP.";
                        return false;

                    case QuestRewardType.Unlock:
                        error = $"Phần thưởng mở khóa '{reward.TargetId}' chưa có xử lý gameplay.";
                        return false;

                    default:
                        error = $"Loại phần thưởng '{reward.RewardType}' chưa có xử lý gameplay.";
                        return false;
                }
            }

            return true;
        }

        void Rollback(
            int grantedGold,
            IReadOnlyList<GrantedItem> grantedItems,
            PlayerProfileSaveData profileSnapshot,
            QuestRuntimeState state) {
            for (int i = grantedItems.Count - 1; i >= 0; i--) {
                GrantedItem granted = grantedItems[i];
                inventory?.RemoveItem(granted.ItemBase, granted.Quantity);
            }

            if (grantedGold > 0) currencyWallet?.TrySpendGold(grantedGold);
            if (profileSnapshot != null) profileProvider?.RestoreFromSaveData(profileSnapshot);
            questManager?.SetRewardsClaimed(state, false);
        }

        ItemBase ResolveItem(QuestRewardDefinition reward) {
            if (inventory == null || reward == null) return null;
            string itemId = !string.IsNullOrWhiteSpace(reward.TargetId) ? reward.TargetId : reward.RewardId;
            return inventory.FindItemBase(itemId);
        }

        static bool IsGoldCurrency(string currencyId) {
            return string.IsNullOrWhiteSpace(currencyId)
                || string.Equals(currencyId.Trim(), "gold", StringComparison.OrdinalIgnoreCase);
        }

        void ResolveReferences() {
            if (questManager == null) questManager = FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include);
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);
            if (currencyWallet == null) currencyWallet = FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
            if (profileProvider == null) profileProvider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
        }

        readonly struct GrantedItem {
            public GrantedItem(ItemBase itemBase, int quantity) {
                ItemBase = itemBase;
                Quantity = quantity;
            }

            public ItemBase ItemBase { get; }
            public int Quantity { get; }
        }
    }
}

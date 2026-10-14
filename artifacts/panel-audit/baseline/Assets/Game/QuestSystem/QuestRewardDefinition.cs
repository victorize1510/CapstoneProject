using System;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestRewardType {
        Experience = 0,
        Gold = 1,
        Currency = 2,
        Item = 3,
        Creature = 4,
        Monster = Creature,
        Unlock = 5,
        Reputation = 6,
        Custom = 100
    }

    [Serializable]
    public sealed class QuestRewardDefinition {
        [SerializeField] string rewardId = string.Empty;
        [SerializeField] string displayName = string.Empty;
        [SerializeField] QuestRewardType rewardType = QuestRewardType.Custom;
        [SerializeField] int amount = 1;
        [SerializeField] string targetId = string.Empty;
        [SerializeField] string currencyId = string.Empty;
        [SerializeField] string customKey = string.Empty;
        [SerializeField] Sprite icon = null;

        public string RewardId => rewardId;
        public string DisplayName => displayName;
        public QuestRewardType RewardType => rewardType;
        public int Amount => Mathf.Max(1, amount);
        public string TargetId => targetId;
        public string CurrencyId => currencyId;
        public string CustomKey => customKey;
        public Sprite Icon => icon;
    }
}

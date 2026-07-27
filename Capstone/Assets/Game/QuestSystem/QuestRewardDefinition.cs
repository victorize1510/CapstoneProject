using System;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestRewardType {
        Experience,
        Currency,
        Item,
        Monster,
        Unlock,
        Custom
    }

    [Serializable]
    public sealed class QuestRewardDefinition {
        [SerializeField] string rewardId = string.Empty;
        [SerializeField] string displayName = string.Empty;
        [SerializeField] QuestRewardType rewardType = QuestRewardType.Custom;
        [SerializeField] int amount = 1;
        [SerializeField] Sprite icon = null;

        public string RewardId => rewardId;
        public string DisplayName => displayName;
        public QuestRewardType RewardType => rewardType;
        public int Amount => Mathf.Max(1, amount);
        public Sprite Icon => icon;
    }
}

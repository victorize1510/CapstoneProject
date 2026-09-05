using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestType {
        Main = 0,
        Side = 1,
        Daily = 2,
        Other = 3,
        Event = 4,
        Companion = 5
    }

    [CreateAssetMenu(fileName = "QuestDefinition", menuName = "Capstone/Quest/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject {
        [SerializeField] string questId = string.Empty;
        [SerializeField] string title = string.Empty;
        [SerializeField] Sprite icon = null;
        [SerializeField, TextArea(3, 8)] string description = string.Empty;
        [SerializeField] QuestType questType = QuestType.Main;
        [SerializeField] QuestSubType questSubType = QuestSubType.None;
        [SerializeField] List<string> tags = new List<string>();
        [SerializeField] int recommendedLevel = 1;
        [SerializeField] List<QuestObjectiveDefinition> objectives = new List<QuestObjectiveDefinition>();
        [SerializeField] string locationName = string.Empty;
        [SerializeField] bool hasWorldPosition;
        [SerializeField] Vector3 worldPosition = Vector3.zero;
        [SerializeField] List<string> prerequisiteQuestIds = new List<string>();
        [SerializeField] List<QuestRequirementDefinition> requirements = new List<QuestRequirementDefinition>();
        [SerializeField] List<string> unlockQuestIds = new List<string>();
        [SerializeField] List<QuestRewardDefinition> rewards = new List<QuestRewardDefinition>();
        [SerializeField] float timeLimit;
        [SerializeField] bool canAbandon = true;
        [SerializeField] bool canReacceptAfterAbandon = true;

        public string StableId => questId;
        public string QuestId => questId;
        public string Title => title;
        public Sprite Icon => icon;
        public string Description => description;
        public QuestType QuestType => questType;
        public QuestCategory Category => QuestCategoryUtility.FromLegacyType(questType);
        public QuestSubType QuestSubType => questSubType;
        public IReadOnlyList<string> Tags => tags;
        public int RecommendedLevel => Mathf.Max(1, recommendedLevel);
        public IReadOnlyList<QuestObjectiveDefinition> Objectives => objectives;
        public string LocationName => locationName;
        public bool HasWorldPosition => hasWorldPosition;
        public Vector3 WorldPosition => worldPosition;
        public IReadOnlyList<string> PrerequisiteQuestIds => prerequisiteQuestIds;
        public IReadOnlyList<QuestRequirementDefinition> Requirements => requirements;
        public IReadOnlyList<string> UnlockQuestIds => unlockQuestIds;
        public IReadOnlyList<QuestRewardDefinition> Rewards => rewards;
        public float TimeLimit => Mathf.Max(0f, timeLimit);
        public bool HasTimeLimit => TimeLimit > 0f;
        public bool CanAbandon => canAbandon;
        public bool CanReacceptAfterAbandon => canReacceptAfterAbandon;

        public bool TryGetWorldPosition(out Vector3 position) {
            position = worldPosition;
            return hasWorldPosition;
        }

        void OnValidate() {
            recommendedLevel = Mathf.Max(1, recommendedLevel);
            timeLimit = Mathf.Max(0f, timeLimit);
            objectives.RemoveAll(objective => objective == null);
            requirements.RemoveAll(requirement => requirement == null);
            rewards.RemoveAll(reward => reward == null);
        }
    }
}

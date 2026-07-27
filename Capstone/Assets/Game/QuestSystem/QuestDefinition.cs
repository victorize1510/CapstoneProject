using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestType {
        Main,
        Side,
        Daily,
        Other
    }

    [CreateAssetMenu(fileName = "QuestDefinition", menuName = "Capstone/Quest/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject {
        [SerializeField] string questId = string.Empty;
        [SerializeField] string title = string.Empty;
        [SerializeField, TextArea(3, 8)] string description = string.Empty;
        [SerializeField] QuestType questType = QuestType.Main;
        [SerializeField] int recommendedLevel = 1;
        [SerializeField] List<QuestObjectiveDefinition> objectives = new List<QuestObjectiveDefinition>();
        [SerializeField] string locationName = string.Empty;
        [SerializeField] Vector3 worldPosition = Vector3.zero;
        [SerializeField] List<QuestRewardDefinition> rewards = new List<QuestRewardDefinition>();
        [SerializeField] float timeLimit;
        [SerializeField] bool canAbandon = true;

        public string QuestId => questId;
        public string Title => title;
        public string Description => description;
        public QuestType QuestType => questType;
        public int RecommendedLevel => Mathf.Max(1, recommendedLevel);
        public IReadOnlyList<QuestObjectiveDefinition> Objectives => objectives;
        public string LocationName => locationName;
        public Vector3 WorldPosition => worldPosition;
        public IReadOnlyList<QuestRewardDefinition> Rewards => rewards;
        public float TimeLimit => Mathf.Max(0f, timeLimit);
        public bool HasTimeLimit => TimeLimit > 0f;
        public bool CanAbandon => canAbandon;

        void OnValidate() {
            recommendedLevel = Mathf.Max(1, recommendedLevel);
            timeLimit = Mathf.Max(0f, timeLimit);
        }
    }
}

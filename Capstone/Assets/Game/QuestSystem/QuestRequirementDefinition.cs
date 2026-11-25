using System;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestRequirementType {
        None = 0,
        PrerequisiteQuest = 1,
        TrainerLevel = 2,
        CreatureLevel = 3,
        CreatureOwned = 4,
        ItemOwned = 5,
        RegionUnlocked = 6,
        Custom = 100
    }

    public enum QuestRequirementComparison {
        AtLeast = 0,
        AtMost = 1,
        Equal = 2,
        NotEqual = 3
    }

    [Serializable]
    public sealed class QuestRequirementDefinition {
        [SerializeField] string requirementId = string.Empty;
        [SerializeField] QuestRequirementType requirementType = QuestRequirementType.None;
        [SerializeField] QuestRequirementComparison comparison = QuestRequirementComparison.AtLeast;
        [SerializeField] string targetId = string.Empty;
        [SerializeField] int requiredAmount = 1;
        [SerializeField] string customKey = string.Empty;

        public string RequirementId => requirementId;
        public QuestRequirementType RequirementType => requirementType;
        public QuestRequirementComparison Comparison => comparison;
        public string TargetId => targetId;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        public string CustomKey => customKey;
        public bool IsEmpty => requirementType == QuestRequirementType.None;

        public QuestRequirementDefinition() {
        }

        internal QuestRequirementDefinition(QuestRequirementType requirementType, string targetId) {
            this.requirementType = requirementType;
            this.targetId = targetId;
        }

        public bool Compare(int value) {
            switch (comparison) {
                case QuestRequirementComparison.AtMost:
                    return value <= RequiredAmount;
                case QuestRequirementComparison.Equal:
                    return value == RequiredAmount;
                case QuestRequirementComparison.NotEqual:
                    return value != RequiredAmount;
                default:
                    return value >= RequiredAmount;
            }
        }
    }

    public interface IQuestRequirementProvider {
        bool TryEvaluateRequirement(QuestRequirementDefinition requirement, out bool isMet);
    }
}

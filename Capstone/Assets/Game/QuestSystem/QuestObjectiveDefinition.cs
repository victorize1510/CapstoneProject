using System;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestObjectiveType {
        Custom,
        ReachLocation,
        Defeat,
        Collect,
        Interact,
        Talk,
        UseItem
    }

    [Serializable]
    public sealed class QuestObjectiveDefinition {
        [SerializeField] string objectiveId = string.Empty;
        [SerializeField] string title = string.Empty;
        [SerializeField, TextArea] string description = string.Empty;
        [SerializeField] QuestObjectiveType objectiveType = QuestObjectiveType.Custom;
        [SerializeField] int requiredAmount = 1;
        [SerializeField] bool optional = false;

        public string ObjectiveId => objectiveId;
        public string Title => title;
        public string Description => description;
        public QuestObjectiveType ObjectiveType => objectiveType;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        public bool Optional => optional;
    }
}

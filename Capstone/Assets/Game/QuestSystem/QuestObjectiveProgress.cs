using System;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [Serializable]
    public sealed class QuestObjectiveProgress {
        [SerializeField] string objectiveId;
        [SerializeField] int currentAmount;
        [SerializeField] int requiredAmount = 1;
        [SerializeField] bool optional;
        [SerializeField] bool completed;

        public string ObjectiveId => objectiveId;
        public int CurrentAmount => currentAmount;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        public bool Optional => optional;
        public bool IsComplete => completed;

        public QuestObjectiveProgress() {
        }

        public QuestObjectiveProgress(string objectiveId, int requiredAmount, bool optional) {
            this.objectiveId = objectiveId;
            this.requiredAmount = Mathf.Max(1, requiredAmount);
            this.optional = optional;
            currentAmount = 0;
            completed = false;
        }

        public void Add(int amount) {
            Set(currentAmount + amount);
        }

        public void Set(int amount) {
            currentAmount = Mathf.Clamp(amount, 0, RequiredAmount);
            completed = currentAmount >= RequiredAmount;
        }

        public void MarkComplete() {
            currentAmount = RequiredAmount;
            completed = true;
        }

        public void Restore(int currentAmount, bool completed) {
            this.currentAmount = Mathf.Clamp(currentAmount, 0, RequiredAmount);
            this.completed = completed || this.currentAmount >= RequiredAmount;
        }
    }
}

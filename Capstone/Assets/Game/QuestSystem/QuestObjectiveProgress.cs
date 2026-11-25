using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [Serializable]
    public sealed class QuestObjectiveProgress {
        [SerializeField] string objectiveId;
        [SerializeField] int currentAmount;
        [SerializeField] int requiredAmount = 1;
        [SerializeField] bool optional;
        [SerializeField] bool completed;
        [SerializeField] List<string> receipts = new List<string>();

        public string ObjectiveId => objectiveId;
        public int CurrentAmount => currentAmount;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        public bool Optional => optional;
        public bool IsComplete => completed;
        internal IReadOnlyList<string> Receipts => receipts;
        internal bool AddReceipt(string id) {
            if (string.IsNullOrEmpty(id) || receipts.Contains(id)) return false;
            receipts.Add(id);
            return true;
        }

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

        public void Restore(int currentAmount, bool completed, List<string> savedReceipts = null) {
            receipts = savedReceipts != null ? new List<string>(savedReceipts) : new List<string>();
            this.currentAmount = Mathf.Clamp(currentAmount, 0, RequiredAmount);
            this.completed = completed || this.currentAmount >= RequiredAmount;
        }
    }
}

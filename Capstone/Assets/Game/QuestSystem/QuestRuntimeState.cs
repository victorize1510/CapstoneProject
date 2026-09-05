using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestStatus {
        Active,
        Completed,
        Failed,
        Abandoned
    }

    [Serializable]
    public sealed class QuestRuntimeState {
        [SerializeField] QuestDefinition definition;
        [SerializeField] string questId;
        [SerializeField] QuestStatus status;
        [SerializeField] bool tracked;
        [SerializeField] bool rewardsClaimed;
        [SerializeField] float acceptedTime;
        [SerializeField] float completedTime = -1f;
        [SerializeField] List<QuestObjectiveProgress> objectives = new List<QuestObjectiveProgress>();

        public QuestDefinition Definition => definition;
        public string QuestId => questId;
        public QuestStatus Status => status;
        public bool IsTracked => tracked;
        public bool RewardsClaimed => rewardsClaimed;
        public float AcceptedTime => acceptedTime;
        public float CompletedTime => completedTime;
        public IReadOnlyList<QuestObjectiveProgress> Objectives => objectives;
        public bool IsFinished => status == QuestStatus.Completed || status == QuestStatus.Failed || status == QuestStatus.Abandoned;

        public QuestRuntimeState() {
        }

        public QuestRuntimeState(QuestDefinition definition, float acceptedTime) {
            this.definition = definition;
            questId = definition != null ? definition.QuestId : string.Empty;
            status = QuestStatus.Active;
            tracked = false;
            rewardsClaimed = false;
            this.acceptedTime = acceptedTime;
            completedTime = -1f;
            objectives = CreateObjectiveProgress(definition);
        }

        public QuestRuntimeState(QuestDefinition definition, QuestRuntimeSaveData saveData) {
            this.definition = definition;
            questId = definition != null ? definition.QuestId : saveData?.questId ?? string.Empty;
            status = saveData != null ? saveData.status : QuestStatus.Active;
            tracked = saveData != null && saveData.tracked && status == QuestStatus.Active;
            rewardsClaimed = saveData != null && saveData.rewardsClaimed;
            acceptedTime = saveData != null ? saveData.acceptedTime : 0f;
            completedTime = saveData != null ? saveData.completedTime : -1f;
            objectives = CreateObjectiveProgress(definition);
            RestoreObjectiveProgress(saveData);
            if (status == QuestStatus.Completed) {
                MarkAllObjectivesComplete();
            }
        }

        public QuestObjectiveProgress GetObjective(string objectiveId) {
            return objectives.FirstOrDefault(objective => objective.ObjectiveId == objectiveId);
        }

        public bool HasRequiredObjectivesComplete() {
            return objectives
                .Where(objective => !objective.Optional)
                .All(objective => objective.IsComplete);
        }

        public void SetTracked(bool value) {
            tracked = value;
        }

        public void SetRewardsClaimed(bool value) {
            rewardsClaimed = value;
        }

        public void Complete(float time) {
            MarkAllObjectivesComplete();
            status = QuestStatus.Completed;
            completedTime = time;
            tracked = false;
        }

        public void Fail(float time) {
            status = QuestStatus.Failed;
            completedTime = time;
            tracked = false;
        }

        public void Abandon(float time) {
            status = QuestStatus.Abandoned;
            completedTime = time;
            tracked = false;
        }

        static List<QuestObjectiveProgress> CreateObjectiveProgress(QuestDefinition definition) {
            if (definition == null) return new List<QuestObjectiveProgress>();

            return definition.Objectives
                .Where(objective => objective != null)
                .Select(objective => new QuestObjectiveProgress(
                    objective.ObjectiveId,
                    objective.RequiredAmount,
                    objective.Optional))
                .ToList();
        }

        void RestoreObjectiveProgress(QuestRuntimeSaveData saveData) {
            if (saveData == null || saveData.objectives == null) return;

            foreach (var objectiveData in saveData.objectives) {
                if (objectiveData == null || string.IsNullOrWhiteSpace(objectiveData.objectiveId)) continue;

                var objective = GetObjective(objectiveData.objectiveId);
                objective?.Restore(objectiveData.currentAmount, objectiveData.completed);
            }
        }

        void MarkAllObjectivesComplete() {
            foreach (var objective in objectives) {
                objective?.MarkComplete();
            }
        }
    }
}

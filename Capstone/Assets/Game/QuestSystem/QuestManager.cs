using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class QuestManager : MonoBehaviour {
        [SerializeField] List<QuestDefinition> questCatalog = new List<QuestDefinition>();
        [SerializeField] List<QuestRuntimeState> activeQuests = new List<QuestRuntimeState>();
        [SerializeField, Min(0f)] float autoCompleteDelay = 0.65f;

        readonly HashSet<string> pendingCompletionQuestIds = new HashSet<string>();
        readonly List<IQuestProgressSource> progressSources = new List<IQuestProgressSource>();

        public event Action QuestsChanged;
        public event Action<QuestRuntimeState> QuestChanged;
        public event Action<QuestRuntimeState> QuestAccepted;
        public event Action<QuestRuntimeState> QuestCompleted;
        public event Action<QuestRuntimeState> QuestFailed;
        public event Action<QuestRuntimeState> QuestAbandoned;
        public event Action<QuestRuntimeState> TrackedQuestChanged;

        public IReadOnlyList<QuestRuntimeState> GetActiveQuests() {
            return activeQuests
                .Where(quest => quest != null && quest.Status == QuestStatus.Active)
                .ToList();
        }

        public IReadOnlyList<QuestRuntimeState> GetAllQuests() {
            return activeQuests
                .Where(quest => quest != null)
                .ToList();
        }

        public QuestRuntimeState GetTrackedQuest() {
            return activeQuests.FirstOrDefault(quest => quest != null && quest.Status == QuestStatus.Active && quest.IsTracked);
        }

        public bool AcceptQuest(QuestDefinition definition) {
            if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId)) return false;
            if (FindQuest(definition.QuestId) != null) return false;

            var state = new QuestRuntimeState(definition, Time.time);
            activeQuests.Add(state);
            if (definition.QuestType == QuestType.Main) {
                SetOnlyTrackedQuest(state);
                TrackedQuestChanged?.Invoke(state);
            }

            QuestAccepted?.Invoke(state);
            PublishQuestChanged(state);
            return true;
        }

        public bool AcceptQuest(string questId) {
            var definition = FindDefinition(questId);
            return AcceptQuest(definition);
        }

        public bool TrackQuest(string questId) {
            var state = FindActiveQuest(questId);
            if (state == null) return false;

            SetOnlyTrackedQuest(state);
            TrackedQuestChanged?.Invoke(state);
            PublishQuestChanged(state);
            return true;
        }

        public bool UntrackQuest(string questId) {
            var state = FindActiveQuest(questId);
            if (state == null || !state.IsTracked) return false;

            state.SetTracked(false);
            TrackedQuestChanged?.Invoke(null);
            PublishQuestChanged(state);
            return true;
        }

        public bool UpdateObjectiveProgress(string questId, string objectiveId, int amount, QuestProgressMode mode = QuestProgressMode.Add) {
            var state = FindActiveQuest(questId);
            if (state == null) return false;

            return UpdateObjectiveProgress(state, objectiveId, amount, mode);
        }

        public bool UpdateObjectiveProgress(string objectiveId, int amount, QuestProgressMode mode = QuestProgressMode.Add) {
            var changed = false;
            foreach (var state in GetActiveQuests()) {
                changed |= UpdateObjectiveProgress(state, objectiveId, amount, mode);
            }

            return changed;
        }

        public bool CompleteQuest(string questId) {
            pendingCompletionQuestIds.Remove(questId);
            var state = FindActiveQuest(questId);
            if (state == null) return false;

            state.Complete(Time.time);
            QuestCompleted?.Invoke(state);
            TrackedQuestChanged?.Invoke(GetTrackedQuest());
            PublishQuestChanged(state);
            return true;
        }

        public bool FailQuest(string questId) {
            pendingCompletionQuestIds.Remove(questId);
            var state = FindActiveQuest(questId);
            if (state == null) return false;

            state.Fail(Time.time);
            QuestFailed?.Invoke(state);
            TrackedQuestChanged?.Invoke(GetTrackedQuest());
            PublishQuestChanged(state);
            return true;
        }

        public bool AbandonQuest(string questId) {
            pendingCompletionQuestIds.Remove(questId);
            var state = FindActiveQuest(questId);
            if (state == null || state.Definition == null || !state.Definition.CanAbandon) return false;

            state.Abandon(Time.time);
            QuestAbandoned?.Invoke(state);
            TrackedQuestChanged?.Invoke(GetTrackedQuest());
            PublishQuestChanged(state);
            return true;
        }

        public void RegisterProgressSource(IQuestProgressSource source) {
            if (source == null || progressSources.Contains(source)) return;

            progressSources.Add(source);
            source.ProgressReported += HandleProgressReported;
        }

        public void UnregisterProgressSource(IQuestProgressSource source) {
            if (source == null || !progressSources.Remove(source)) return;

            source.ProgressReported -= HandleProgressReported;
        }

        public QuestSaveData CreateSaveData() {
            var saveData = new QuestSaveData {
                trackedQuestId = GetTrackedQuest()?.QuestId
            };

            foreach (var state in activeQuests.Where(quest => quest != null)) {
                AddStateToSaveData(saveData, state);
            }

            return saveData;
        }

        void HandleProgressReported(QuestProgressReport report) {
            if (!string.IsNullOrWhiteSpace(report.QuestId)) {
                UpdateObjectiveProgress(report.QuestId, report.ObjectiveId, report.Amount, report.Mode);
                return;
            }

            UpdateObjectiveProgress(report.ObjectiveId, report.Amount, report.Mode);
        }

        bool UpdateObjectiveProgress(QuestRuntimeState state, string objectiveId, int amount, QuestProgressMode mode) {
            if (state == null || state.Status != QuestStatus.Active || string.IsNullOrWhiteSpace(objectiveId)) return false;

            var objective = state.GetObjective(objectiveId);
            if (objective == null || objective.IsComplete) return false;

            switch (mode) {
                case QuestProgressMode.Set:
                    objective.Set(amount);
                    break;
                case QuestProgressMode.Complete:
                    objective.MarkComplete();
                    break;
                default:
                    objective.Add(Mathf.Max(1, amount));
                    break;
            }

            PublishQuestChanged(state);

            if (state.HasRequiredObjectivesComplete()) {
                BeginAutoCompleteQuest(state);
            }

            return true;
        }

        void BeginAutoCompleteQuest(QuestRuntimeState state) {
            if (state == null || string.IsNullOrWhiteSpace(state.QuestId)) return;
            if (!pendingCompletionQuestIds.Add(state.QuestId)) return;

            if (autoCompleteDelay <= 0f) {
                FinishPendingQuest(state.QuestId);
                return;
            }

            StartCoroutine(CompleteAfterDelay(state.QuestId, autoCompleteDelay));
        }

        IEnumerator CompleteAfterDelay(string questId, float delay) {
            yield return new WaitForSecondsRealtime(delay);
            FinishPendingQuest(questId);
        }

        void FinishPendingQuest(string questId) {
            pendingCompletionQuestIds.Remove(questId);
            CompleteQuest(questId);
        }

        QuestRuntimeState FindQuest(string questId) {
            return activeQuests.FirstOrDefault(quest => quest != null && quest.QuestId == questId);
        }

        QuestRuntimeState FindActiveQuest(string questId) {
            return activeQuests.FirstOrDefault(quest => quest != null && quest.QuestId == questId && quest.Status == QuestStatus.Active);
        }

        QuestDefinition FindDefinition(string questId) {
            return questCatalog.FirstOrDefault(quest => quest != null && quest.QuestId == questId);
        }

        void SetOnlyTrackedQuest(QuestRuntimeState trackedState) {
            foreach (var quest in activeQuests) {
                quest?.SetTracked(false);
            }

            trackedState?.SetTracked(true);
        }

        void PublishQuestChanged(QuestRuntimeState state) {
            QuestChanged?.Invoke(state);
            QuestsChanged?.Invoke();
        }

        static void AddStateToSaveData(QuestSaveData saveData, QuestRuntimeState state) {
            var stateData = new QuestRuntimeSaveData {
                questId = state.QuestId,
                status = state.Status,
                tracked = state.IsTracked,
                acceptedTime = state.AcceptedTime,
                completedTime = state.CompletedTime
            };

            foreach (var objective in state.Objectives) {
                stateData.objectives.Add(new QuestObjectiveSaveData {
                    objectiveId = objective.ObjectiveId,
                    currentAmount = objective.CurrentAmount,
                    completed = objective.IsComplete
                });
            }

            switch (state.Status) {
                case QuestStatus.Completed:
                    saveData.completedQuestIds.Add(state.QuestId);
                    break;
                case QuestStatus.Failed:
                    saveData.failedQuestIds.Add(state.QuestId);
                    break;
                case QuestStatus.Abandoned:
                    saveData.abandonedQuestIds.Add(state.QuestId);
                    break;
                default:
                    saveData.activeQuests.Add(stateData);
                    break;
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class QuestManager : MonoBehaviour, IQuestCompletionLookup {
        [Header("Catalog")]
        [SerializeField] QuestDatabase questDatabase;
        [SerializeField] List<QuestDefinition> questCatalog = new List<QuestDefinition>();

        [Header("Runtime")]
        [SerializeField] List<QuestRuntimeState> activeQuests = new List<QuestRuntimeState>();
        [SerializeField] bool autoTrackMainQuest = true;
        [SerializeField, Min(0f)] float autoCompleteDelay = 0.65f;

        [Header("Integration")]
        [SerializeField] QuestEventBus questEventBus;
        [SerializeField] List<MonoBehaviour> requirementProviderBehaviours = new List<MonoBehaviour>();

        readonly HashSet<string> pendingCompletionQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> unlockedQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly List<IQuestProgressSource> progressSources = new List<IQuestProgressSource>();
        readonly List<IQuestProgressEventSource> progressEventSources = new List<IQuestProgressEventSource>();
        readonly List<IQuestRequirementProvider> requirementProviders = new List<IQuestRequirementProvider>();
        readonly List<IQuestRequirementProvider> runtimeRequirementProviders = new List<IQuestRequirementProvider>();
        readonly List<QuestDefinition> runtimeQuestDefinitions = new List<QuestDefinition>();
        readonly QuestRegistry registry = new QuestRegistry();

        QuestRequirementEvaluator requirementEvaluator;
        bool subscribedToEventBus;
        bool registryBuilt;
        bool requirementProvidersBuilt;

        public event Action QuestsChanged;
        public event Action<QuestRuntimeState> QuestChanged;
        public event Action<QuestRuntimeState> QuestAccepted;
        public event Action<QuestRuntimeState> QuestCompleted;
        public event Action<QuestRuntimeState> QuestFailed;
        public event Action<QuestRuntimeState> QuestAbandoned;
        public event Action<QuestRuntimeState> TrackedQuestChanged;
        public event Action<QuestDefinition> QuestUnlocked;
        public event Action<QuestRuntimeState, IReadOnlyList<QuestRewardDefinition>> QuestRewardsReady;

        public IReadOnlyList<QuestDefinition> GetQuestDefinitions() {
            EnsureReady();
            return registry.Quests;
        }

        public IReadOnlyList<QuestDefinition> GetAvailableQuestDefinitions() {
            EnsureReady();
            RefreshUnlockedQuests(false);

            return registry.Quests
                .Where(definition => definition != null && GetQuestAvailability(definition.QuestId) == QuestAvailabilityState.Available)
                .ToList();
        }

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

        public QuestRuntimeState GetQuestState(string questId) {
            return FindQuest(questId);
        }

        public QuestDefinition GetQuestDefinition(string questId) {
            EnsureReady();
            return registry.Find(questId);
        }

        public QuestTrackingInfo GetTrackedQuestInfo(Transform distanceOrigin = null) {
            var trackedQuest = GetTrackedQuest();
            TryGetQuestTarget(trackedQuest, out QuestTargetInfo target);
            return new QuestTrackingInfo(trackedQuest, target, distanceOrigin);
        }

        public bool TryGetQuestTarget(string questId, out QuestTargetInfo target) {
            return TryGetQuestTarget(FindQuest(questId), out target);
        }

        public bool TryGetQuestTarget(QuestRuntimeState quest, out QuestTargetInfo target) {
            target = default;
            if (quest == null || quest.Definition == null) return false;

            foreach (var progress in quest.Objectives) {
                if (progress == null || progress.IsComplete) continue;

                var definition = quest.Definition.Objectives
                    .FirstOrDefault(objective => objective != null && objective.ObjectiveId == progress.ObjectiveId);
                if (definition != null && definition.TryGetTargetPosition(out Vector3 objectivePosition)) {
                    target = new QuestTargetInfo(
                        quest.QuestId,
                        definition.ObjectiveId,
                        string.IsNullOrWhiteSpace(definition.Title) ? quest.Definition.Title : definition.Title,
                        objectivePosition);
                    return true;
                }
            }

            if (quest.Definition.TryGetWorldPosition(out Vector3 questPosition)) {
                target = new QuestTargetInfo(quest.QuestId, string.Empty, quest.Definition.Title, questPosition);
                return true;
            }

            return false;
        }

        public QuestAvailabilityState GetQuestAvailability(string questId) {
            EnsureReady();
            if (string.IsNullOrWhiteSpace(questId)) return QuestAvailabilityState.Locked;

            var state = FindQuest(questId);
            if (state != null) {
                switch (state.Status) {
                    case QuestStatus.Completed:
                        return QuestAvailabilityState.Completed;
                    case QuestStatus.Failed:
                        return QuestAvailabilityState.Failed;
                    case QuestStatus.Abandoned:
                        return state.Definition != null
                            && state.Definition.CanReacceptAfterAbandon
                            && requirementEvaluator.AreMet(state.Definition, out _)
                                ? QuestAvailabilityState.Available
                                : QuestAvailabilityState.Abandoned;
                    default:
                        return QuestAvailabilityState.Active;
                }
            }

            var definition = registry.Find(questId);
            if (definition != null && requirementEvaluator.AreMet(definition, out _)) {
                unlockedQuestIds.Add(questId);
            }

            return unlockedQuestIds.Contains(questId) ? QuestAvailabilityState.Available : QuestAvailabilityState.Locked;
        }

        public void RefreshQuestAvailability() {
            EnsureReady();
            RefreshUnlockedQuests(true);
        }

        public bool CanAcceptQuest(QuestDefinition definition) {
            return CanAcceptQuest(definition, out _);
        }

        public bool RegisterQuestDefinition(QuestDefinition definition) {
            if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId)) return false;

            if (!runtimeQuestDefinitions.Contains(definition)) {
                runtimeQuestDefinitions.Add(definition);
            }

            registry.Register(definition);
            return true;
        }

        public bool CanAcceptQuest(QuestDefinition definition, out QuestRequirementDefinition failedRequirement) {
            EnsureReady();
            failedRequirement = null;
            if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId)) return false;
            QuestRuntimeState existing = FindQuest(definition.QuestId);
            if (existing != null
                && (existing.Status != QuestStatus.Abandoned || !definition.CanReacceptAfterAbandon)) return false;

            return unlockedQuestIds.Contains(definition.QuestId) || requirementEvaluator.AreMet(definition, out failedRequirement);
        }

        public bool AcceptQuest(QuestDefinition definition) {
            if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId)) return false;

            RegisterQuestDefinition(definition);
            EnsureReady();

            if (!CanAcceptQuest(definition, out _)) return false;

            registry.Register(definition);
            QuestRuntimeState abandonedState = FindQuest(definition.QuestId);
            if (abandonedState != null && abandonedState.Status == QuestStatus.Abandoned) {
                activeQuests.Remove(abandonedState);
            }
            var state = new QuestRuntimeState(definition, Time.time);
            activeQuests.Add(state);
            unlockedQuestIds.Remove(definition.QuestId);

            if (autoTrackMainQuest && definition.Category == QuestCategory.Main) {
                SetOnlyTrackedQuest(state);
                TrackedQuestChanged?.Invoke(state);
            }

            QuestAccepted?.Invoke(state);
            PublishQuestChanged(state);
            RefreshUnlockedQuests(true);
            return true;
        }

        public bool AcceptQuest(string questId) {
            EnsureReady();
            return AcceptQuest(registry.Find(questId));
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
            return ReportProgress(new QuestProgressEvent(
                QuestObjectiveType.CustomObjective,
                amount: amount,
                mode: mode,
                questId: questId,
                objectiveId: objectiveId));
        }

        public bool UpdateObjectiveProgress(string objectiveId, int amount, QuestProgressMode mode = QuestProgressMode.Add) {
            return ReportProgress(new QuestProgressEvent(
                QuestObjectiveType.CustomObjective,
                amount: amount,
                mode: mode,
                objectiveId: objectiveId));
        }

        public bool ReportProgress(QuestProgressEvent progressEvent) {
            var changed = false;

            if (!string.IsNullOrWhiteSpace(progressEvent.QuestId)) {
                changed = TryApplyProgress(FindActiveQuest(progressEvent.QuestId), progressEvent);
            } else {
                foreach (var state in GetActiveQuests()) {
                    changed |= TryApplyProgress(state, progressEvent);
                }
            }

            if (changed) {
                RefreshUnlockedQuests(true);
            }

            return changed;
        }

        public bool CompleteQuest(string questId) {
            pendingCompletionQuestIds.Remove(questId);
            var state = FindActiveQuest(questId);
            if (state == null) return false;

            state.Complete(Time.time);
            QuestCompleted?.Invoke(state);
            QuestRewardsReady?.Invoke(state, state.Definition.Rewards);
            TrackedQuestChanged?.Invoke(GetTrackedQuest());
            PublishQuestChanged(state);

            UnlockLinkedQuests(state.Definition, true);
            ReportProgress(QuestProgressEvent.QuestCompleted(questId));
            RefreshUnlockedQuests(true);
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
            RefreshUnlockedQuests(true);
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
            RefreshUnlockedQuests(true);
            return true;
        }

        public void RegisterProgressSource(IQuestProgressSource source) {
            if (source == null || progressSources.Contains(source)) return;

            progressSources.Add(source);
            source.ProgressReported += HandleLegacyProgressReported;
        }

        public void UnregisterProgressSource(IQuestProgressSource source) {
            if (source == null || !progressSources.Remove(source)) return;

            source.ProgressReported -= HandleLegacyProgressReported;
        }

        public void RegisterProgressEventSource(IQuestProgressEventSource source) {
            if (source == null || progressEventSources.Contains(source)) return;

            progressEventSources.Add(source);
            source.ProgressReported += HandleProgressEventReported;
        }

        public void UnregisterProgressEventSource(IQuestProgressEventSource source) {
            if (source == null || !progressEventSources.Remove(source)) return;

            source.ProgressReported -= HandleProgressEventReported;
        }

        public void RegisterRequirementProvider(IQuestRequirementProvider provider) {
            if (provider == null || runtimeRequirementProviders.Contains(provider)) return;

            runtimeRequirementProviders.Add(provider);
            requirementProvidersBuilt = false;
            EnsureReady();
            RefreshUnlockedQuests(true);
        }

        public void UnregisterRequirementProvider(IQuestRequirementProvider provider) {
            if (provider == null || !runtimeRequirementProviders.Remove(provider)) return;

            requirementProvidersBuilt = false;
            EnsureReady();
            RefreshUnlockedQuests(true);
        }

        public bool IsQuestCompleted(string questId) {
            var state = FindQuest(questId);
            return state != null && state.Status == QuestStatus.Completed;
        }

        public bool IsQuestKnown(string questId) {
            return FindQuest(questId) != null;
        }

        public bool SetRewardsClaimed(QuestRuntimeState state, bool claimed) {
            if (state == null || state.Status != QuestStatus.Completed || !activeQuests.Contains(state)) return false;
            if (state.RewardsClaimed == claimed) return true;

            state.SetRewardsClaimed(claimed);
            PublishQuestChanged(state);
            return true;
        }

        public QuestSaveData CreateSaveData() {
            var saveData = new QuestSaveData {
                trackedQuestId = GetTrackedQuest()?.QuestId
            };

            saveData.unlockedQuestIds.AddRange(unlockedQuestIds.Where(questId => registry.Find(questId) != null));

            foreach (var state in activeQuests.Where(quest => quest != null)) {
                AddStateToSaveData(saveData, state);
            }

            return saveData;
        }

        public bool CanRestoreSaveData(QuestSaveData data, out string error) {
            EnsureReady();
            error = string.Empty;
            if (data == null) return true;
            if (data.version != 1) { error = "Unsupported quest save version."; return false; }
            if (registry.DuplicateIds.Count > 0) { error = "Duplicate quest definition IDs."; return false; }
            var seen = new HashSet<string>();
            var states = data.GetSavedQuestStates();
            if (states != null) foreach (var state in states) {
                if (state == null || string.IsNullOrWhiteSpace(state.questId) || !seen.Add(state.questId)) {
                    error = "Invalid or duplicate saved quest ID."; return false;
                }
                var definition = registry.Find(state.questId);
                if (definition == null) { error = "Missing quest definition: " + state.questId; return false; }
                var objectiveIds = new HashSet<string>();
                foreach (var objective in definition.Objectives) {
                    if (objective == null || !objectiveIds.Add(objective.ObjectiveId)) { error = "Duplicate quest objective ID."; return false; }
                }
                var savedObjectives = new HashSet<string>();
                if (state.objectives != null) foreach (var objective in state.objectives) {
                    if (objective == null || !objectiveIds.Contains(objective.objectiveId) || !savedObjectives.Add(objective.objectiveId)) {
                        error = "Missing or duplicate objective in quest " + state.questId; return false;
                    }
                }
            }
            foreach (var ids in new[] { data.unlockedQuestIds, data.completedQuestIds, data.failedQuestIds, data.abandonedQuestIds }) {
                if (ids == null) continue;
                foreach (string id in ids) if (registry.Find(id) == null) { error = "Missing quest definition: " + id; return false; }
            }
            return true;
        }

        public void RestoreFromSaveData(QuestSaveData saveData) {
            EnsureReady();
            if (!CanRestoreSaveData(saveData, out string restoreError)) throw new System.InvalidOperationException(restoreError);
            StopAllCoroutines();
            pendingCompletionQuestIds.Clear();
            activeQuests.Clear();
            unlockedQuestIds.Clear();

            if (saveData != null) {
                if (saveData.unlockedQuestIds != null) {
                    foreach (var questId in saveData.unlockedQuestIds) {
                        if (!string.IsNullOrWhiteSpace(questId) && registry.Find(questId) != null) {
                            unlockedQuestIds.Add(questId);
                        }
                    }
                }

                RestoreQuestStates(saveData);
                RestoreLegacyFinishedQuestIds(saveData.completedQuestIds, QuestStatus.Completed);
                RestoreLegacyFinishedQuestIds(saveData.failedQuestIds, QuestStatus.Failed);
                RestoreLegacyFinishedQuestIds(saveData.abandonedQuestIds, QuestStatus.Abandoned);
                RestoreLinkedUnlocks();

                if (!string.IsNullOrWhiteSpace(saveData.trackedQuestId)) {
                    SetOnlyTrackedQuest(FindActiveQuest(saveData.trackedQuestId));
                }
            }

            RefreshUnlockedQuests(false);
            ResumePendingCompletions();
            TrackedQuestChanged?.Invoke(GetTrackedQuest());
            QuestsChanged?.Invoke();
        }

        public void ResetForNewGame() {
            RestoreFromSaveData(null);
        }

        void Awake() {
            EnsureReady();
        }

        void OnEnable() {
            EnsureReady();
            SubscribeEventBus();
        }

        void OnDisable() {
            UnsubscribeEventBus();
        }

        void OnValidate() {
            questCatalog.RemoveAll(quest => quest == null);
            requirementProviderBehaviours.RemoveAll(provider => provider == null);
            autoCompleteDelay = Mathf.Max(0f, autoCompleteDelay);
            registryBuilt = false;
            requirementProvidersBuilt = false;
        }

        void EnsureReady() {
            if (requirementEvaluator == null) {
                requirementEvaluator = new QuestRequirementEvaluator(this);
                requirementProvidersBuilt = false;
            }

            if (!registryBuilt) RebuildQuestRegistry();
            if (!requirementProvidersBuilt) RebuildRequirementProviders();
        }

        void RebuildQuestRegistry() {
            registry.Rebuild(GetCatalogDefinitions());
            registryBuilt = true;

            foreach (var duplicateId in registry.DuplicateIds) {
                Debug.LogWarning($"{nameof(QuestManager)} has duplicate quest id '{duplicateId}'. Only the first definition is used.", this);
            }
        }

        IEnumerable<QuestDefinition> GetCatalogDefinitions() {
            if (questDatabase != null) {
                foreach (var quest in questDatabase.Quests) {
                    yield return quest;
                }
            }

            foreach (var quest in questCatalog) {
                yield return quest;
            }

            foreach (var quest in runtimeQuestDefinitions) {
                yield return quest;
            }
        }

        void RebuildRequirementProviders() {
            requirementProviders.Clear();
            foreach (var behaviour in requirementProviderBehaviours) {
                if (behaviour is IQuestRequirementProvider provider && !requirementProviders.Contains(provider)) {
                    requirementProviders.Add(provider);
                }
            }

            foreach (var provider in runtimeRequirementProviders) {
                if (provider != null && !requirementProviders.Contains(provider)) {
                    requirementProviders.Add(provider);
                }
            }

            requirementEvaluator.SetProviders(requirementProviders);
            requirementProvidersBuilt = true;
        }

        void SubscribeEventBus() {
            if (subscribedToEventBus) return;

            if (questEventBus == null) {
                questEventBus = GetComponent<QuestEventBus>();
            }

            if (questEventBus == null) return;

            questEventBus.ProgressReported += HandleProgressEventReported;
            subscribedToEventBus = true;
        }

        void UnsubscribeEventBus() {
            if (!subscribedToEventBus || questEventBus == null) return;

            questEventBus.ProgressReported -= HandleProgressEventReported;
            subscribedToEventBus = false;
        }

        void HandleLegacyProgressReported(QuestProgressReport report) {
            ReportProgress(QuestProgressEvent.FromLegacyReport(report));
        }

        void HandleProgressEventReported(QuestProgressEvent progressEvent) {
            ReportProgress(progressEvent);
        }

        bool TryApplyProgress(QuestRuntimeState state, QuestProgressEvent progressEvent) {
            if (state == null || state.Status != QuestStatus.Active || state.Definition == null) return false;

            var changed = false;
            foreach (var definition in state.Definition.Objectives) {
                if (definition == null || !definition.Matches(progressEvent)) continue;

                var objective = state.GetObjective(definition.ObjectiveId);
                if (objective == null || objective.IsComplete) continue;

                ApplyProgress(objective, progressEvent);
                changed = true;
            }

            if (!changed) return false;

            PublishQuestChanged(state);

            if (state.HasRequiredObjectivesComplete()) {
                BeginAutoCompleteQuest(state);
            }

            return true;
        }

        static void ApplyProgress(QuestObjectiveProgress objective, QuestProgressEvent progressEvent) {
            switch (progressEvent.Mode) {
                case QuestProgressMode.Set:
                    objective.Set(progressEvent.Amount);
                    break;
                case QuestProgressMode.Complete:
                    objective.MarkComplete();
                    break;
                default:
                    objective.Add(progressEvent.Amount);
                    break;
            }
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

        void ResumePendingCompletions() {
            foreach (var state in activeQuests) {
                if (state == null || state.Status != QuestStatus.Active) continue;
                if (!state.HasRequiredObjectivesComplete()) continue;

                BeginAutoCompleteQuest(state);
            }
        }

        void RefreshUnlockedQuests(bool publishEvents) {
            EnsureRequirementEvaluator();

            foreach (var definition in registry.Quests) {
                if (definition == null || FindQuest(definition.QuestId) != null) continue;
                if (!requirementEvaluator.AreMet(definition, out _)) continue;
                if (!unlockedQuestIds.Add(definition.QuestId)) continue;

                if (publishEvents) {
                    QuestUnlocked?.Invoke(definition);
                }
            }
        }

        void UnlockLinkedQuests(QuestDefinition completedQuest, bool publishEvents) {
            if (completedQuest == null) return;

            foreach (var questId in completedQuest.UnlockQuestIds) {
                var definition = registry.Find(questId);
                if (definition == null || FindQuest(questId) != null) continue;
                if (!unlockedQuestIds.Add(questId)) continue;

                if (publishEvents) {
                    QuestUnlocked?.Invoke(definition);
                }
            }
        }

        void RestoreLinkedUnlocks() {
            foreach (var state in activeQuests) {
                if (state != null && state.Status == QuestStatus.Completed) {
                    UnlockLinkedQuests(state.Definition, false);
                }
            }
        }

        void EnsureRequirementEvaluator() {
            if (requirementEvaluator == null) {
                requirementEvaluator = new QuestRequirementEvaluator(this);
            }
        }

        QuestRuntimeState FindQuest(string questId) {
            return activeQuests.FirstOrDefault(quest =>
                quest != null && string.Equals(quest.QuestId, questId, StringComparison.OrdinalIgnoreCase));
        }

        QuestRuntimeState FindActiveQuest(string questId) {
            return activeQuests.FirstOrDefault(quest =>
                quest != null
                && string.Equals(quest.QuestId, questId, StringComparison.OrdinalIgnoreCase)
                && quest.Status == QuestStatus.Active);
        }

        void SetOnlyTrackedQuest(QuestRuntimeState trackedState) {
            foreach (var quest in activeQuests) {
                quest?.SetTracked(false);
            }

            if (trackedState != null && trackedState.Status == QuestStatus.Active) {
                trackedState.SetTracked(true);
            }
        }

        void PublishQuestChanged(QuestRuntimeState state) {
            QuestChanged?.Invoke(state);
            QuestsChanged?.Invoke();
        }

        void RestoreQuestStates(QuestSaveData saveData) {
            var states = saveData.GetSavedQuestStates();
            if (states == null) return;
            foreach (var stateData in states) {
                RestoreQuestState(stateData);
            }
        }

        void RestoreLegacyFinishedQuestIds(IEnumerable<string> questIds, QuestStatus status) {
            if (questIds == null) return;

            foreach (var questId in questIds) {
                if (FindQuest(questId) != null) continue;

                RestoreQuestState(new QuestRuntimeSaveData {
                    questId = questId,
                    status = status,
                    completedTime = -1f
                });
            }
        }

        void RestoreQuestState(QuestRuntimeSaveData stateData) {
            if (stateData == null || string.IsNullOrWhiteSpace(stateData.questId)) return;

            var definition = registry.Find(stateData.questId);
            if (definition == null) {
                Debug.LogWarning($"{nameof(QuestManager)} could not restore missing quest id '{stateData.questId}'.", this);
                return;
            }

            activeQuests.Add(new QuestRuntimeState(definition, stateData));
        }

        static void AddStateToSaveData(QuestSaveData saveData, QuestRuntimeState state) {
            var stateData = new QuestRuntimeSaveData {
                questId = state.QuestId,
                status = state.Status,
                tracked = state.IsTracked,
                rewardsClaimed = state.RewardsClaimed,
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

            saveData.questStates.Add(stateData);

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

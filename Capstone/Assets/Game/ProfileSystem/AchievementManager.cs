using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.QuestSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.ProfileSystem {
    public readonly struct AchievementSnapshot {
        public readonly string AchievementId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly Sprite Icon;
        public readonly int CurrentProgress;
        public readonly int RequiredProgress;
        public readonly bool IsUnlocked;

        public AchievementSnapshot(
            string achievementId,
            string displayName,
            string description,
            Sprite icon,
            int currentProgress,
            int requiredProgress,
            bool isUnlocked) {
            AchievementId = achievementId;
            DisplayName = displayName;
            Description = description;
            Icon = icon;
            CurrentProgress = currentProgress;
            RequiredProgress = requiredProgress;
            IsUnlocked = isUnlocked;
        }

        public float Progress01 => RequiredProgress > 0
            ? Mathf.Clamp01((float)CurrentProgress / RequiredProgress)
            : 0f;
    }

    [DisallowMultipleComponent]
    public sealed class AchievementManager : MonoBehaviour {
        [Header("Definitions")]
        [SerializeField] List<AchievementDefinition> definitions = new List<AchievementDefinition>();
        [SerializeField] bool useFallbackDefinitionsWhenEmpty = true;

        [Header("Quest integration")]
        [SerializeField] QuestManager questManager;

        readonly Dictionary<string, int> progressByMetric = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly HashSet<string> unlockedIds = new HashSet<string>(StringComparer.Ordinal);
        readonly List<AchievementDefinition> runtimeFallbackDefinitions = new List<AchievementDefinition>();
        bool loaded;

        public event Action AchievementsChanged;

        public IReadOnlyList<AchievementDefinition> Definitions => ActiveDefinitions;

        List<AchievementDefinition> ActiveDefinitions {
            get {
                EnsureDefinitions();
                return definitions.Count > 0 ? definitions : runtimeFallbackDefinitions;
            }
        }

        void Awake() {
            EnsureLoaded();
            ResolveQuestManager();
            SyncQuestProgress();
        }

        void OnEnable() {
            EnsureLoaded();
            ResolveQuestManager();
            SubscribeQuestManager();
            SyncQuestProgress();
        }

        void OnDisable() {
            UnsubscribeQuestManager();
        }

        void OnValidate() {
            definitions.RemoveAll(definition => definition == null);
        }

        public AchievementSnapshot[] GetAchievements() {
            EnsureLoaded();
            return ActiveDefinitions
                .Where(definition => definition != null && definition.IsValid)
                .OrderBy(definition => definition.DisplayPriority)
                .ThenBy(definition => definition.DisplayName)
                .Select(CreateSnapshot)
                .ToArray();
        }

        public AchievementSnapshot[] GetUnlockedBadges(int maximum = 4) {
            int count = Mathf.Max(0, maximum);
            return GetAchievements()
                .Where(snapshot => snapshot.IsUnlocked)
                .Take(count)
                .ToArray();
        }

        public float GetCompletion01() {
            AchievementSnapshot[] snapshots = GetAchievements();
            if (snapshots.Length == 0) return 0f;
            return (float)snapshots.Count(snapshot => snapshot.IsUnlocked) / snapshots.Length;
        }

        public int GetMetricProgress(string metricId) {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(metricId)) return 0;
            return progressByMetric.TryGetValue(metricId, out int value) ? value : 0;
        }

        public void AddProgress(string metricId, int amount = 1) {
            if (amount <= 0 || string.IsNullOrWhiteSpace(metricId)) return;
            SetProgress(metricId, GetMetricProgress(metricId) + amount);
        }

        public void SetProgress(string metricId, int value) {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(metricId)) return;

            if (!SetProgressValue(metricId, value)) return;
            EvaluateUnlocks();
            AchievementsChanged?.Invoke();
        }

        public void SetProgressBatch(IEnumerable<KeyValuePair<string, int>> updates) {
            EnsureLoaded();
            if (updates == null) return;

            bool changed = false;
            foreach (KeyValuePair<string, int> update in updates) {
                changed |= SetProgressValue(update.Key, update.Value);
            }

            if (!changed) return;
            EvaluateUnlocks();
            AchievementsChanged?.Invoke();
        }

        bool SetProgressValue(string metricId, int value) {
            if (string.IsNullOrWhiteSpace(metricId)) return false;

            int clamped = Mathf.Max(0, value);
            if (progressByMetric.TryGetValue(metricId, out int current) && current == clamped) return false;

            progressByMetric[metricId] = clamped;
            return true;
        }

        AchievementSnapshot CreateSnapshot(AchievementDefinition definition) {
            int current = GetMetricProgress(definition.MetricId);
            return new AchievementSnapshot(
                definition.AchievementId,
                definition.DisplayName,
                definition.Description,
                definition.Icon,
                current,
                definition.RequiredProgress,
                unlockedIds.Contains(definition.AchievementId));
        }

        void EnsureLoaded() {
            if (loaded) return;
            loaded = true;
            EnsureDefinitions();
            EvaluateUnlocks();
        }

        public AchievementSaveData CreateSaveData() {
            EnsureLoaded();
            var data = new AchievementSaveData { captured = true };
            foreach (KeyValuePair<string, int> pair in progressByMetric) {
                data.metrics.Add(new AchievementMetricSaveData {
                    metricId = pair.Key,
                    progress = pair.Value
                });
            }

            data.unlockedAchievementIds.AddRange(unlockedIds.OrderBy(id => id));
            return data;
        }

        public void RestoreFromSaveData(AchievementSaveData saveData) {
            loaded = true;
            EnsureDefinitions();
            progressByMetric.Clear();
            unlockedIds.Clear();

            if (saveData != null && saveData.captured) {
                foreach (AchievementMetricSaveData metric in saveData.metrics ?? new List<AchievementMetricSaveData>()) {
                    if (metric == null || string.IsNullOrWhiteSpace(metric.metricId)) continue;
                    progressByMetric[metric.metricId] = Mathf.Max(0, metric.progress);
                }

                foreach (string achievementId in saveData.unlockedAchievementIds ?? new List<string>()) {
                    if (!string.IsNullOrWhiteSpace(achievementId)) unlockedIds.Add(achievementId);
                }
            }

            EvaluateUnlocks();
            AchievementsChanged?.Invoke();
        }

        public void ResetForNewGame() {
            RestoreFromSaveData(new AchievementSaveData { captured = true });
        }

        void EnsureDefinitions() {
            definitions.RemoveAll(definition => definition == null);
            if (definitions.Count > 0 || !useFallbackDefinitionsWhenEmpty || runtimeFallbackDefinitions.Count > 0) return;

            runtimeFallbackDefinitions.Add(AchievementDefinition.CreateRuntime(
                "first_capture", "First Bond", "Capture the first creature.",
                AchievementMetricIds.SpeciesCaptured, 1, 0));
            runtimeFallbackDefinitions.Add(AchievementDefinition.CreateRuntime(
                "field_researcher", "Field Researcher", "Discover 10 creatures.",
                AchievementMetricIds.CreaturesSeen, 10, 10));
            runtimeFallbackDefinitions.Add(AchievementDefinition.CreateRuntime(
                "story_begins", "A New Journey", "Complete the first main quest.",
                AchievementMetricIds.MainQuestsCompleted, 1, 20));
            runtimeFallbackDefinitions.Add(AchievementDefinition.CreateRuntime(
                "boss_hunter", "Boss Hunter", "Defeat the first boss.",
                AchievementMetricIds.BossesDefeated, 1, 30));
            runtimeFallbackDefinitions.Add(AchievementDefinition.CreateRuntime(
                "seasoned_trainer", "Seasoned Trainer", "Finish 50 battles.",
                AchievementMetricIds.TotalBattles, 50, 40));
            runtimeFallbackDefinitions.Add(AchievementDefinition.CreateRuntime(
                "codex_beginner", "Codex Beginner", "Register 10 Codex entries.",
                AchievementMetricIds.CodexEntries, 10, 50));
        }

        void EvaluateUnlocks() {
            foreach (AchievementDefinition definition in ActiveDefinitions) {
                if (definition == null || !definition.IsValid) continue;
                if (GetMetricProgress(definition.MetricId) >= definition.RequiredProgress) {
                    unlockedIds.Add(definition.AchievementId);
                }
            }
        }

        void ResolveQuestManager() {
            if (questManager != null) return;
            questManager = FindFirstObjectByType<QuestManager>();
        }

        void SubscribeQuestManager() {
            if (questManager == null) return;
            questManager.QuestsChanged -= HandleQuestsChanged;
            questManager.QuestsChanged += HandleQuestsChanged;
        }

        void UnsubscribeQuestManager() {
            if (questManager == null) return;
            questManager.QuestsChanged -= HandleQuestsChanged;
        }

        void HandleQuestsChanged() {
            SyncQuestProgress();
        }

        void SyncQuestProgress() {
            ResolveQuestManager();
            if (questManager == null) return;

            int completedMainQuests = questManager.GetQuestDefinitions()
                .Where(definition => definition != null && definition.QuestType == QuestType.Main)
                .Count(definition => {
                    QuestRuntimeState state = questManager.GetQuestState(definition.QuestId);
                    return state != null && state.Status == QuestStatus.Completed;
                });

            SetProgress(AchievementMetricIds.MainQuestsCompleted, completedMainQuests);
        }

    }
}

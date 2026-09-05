using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.QuestSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.ProfileSystem {
    [DisallowMultipleComponent]
    public sealed class PlayerProfileRuntimeProvider : MonoBehaviour, IPlayerProfileProvider {
        const float SaveIntervalSeconds = 30f;

        [Header("Identity")]
        [SerializeField] string displayName = string.Empty;
        [SerializeField] string playerId = string.Empty;
        [SerializeField] string dateStarted = string.Empty;
        [SerializeField] Sprite[] avatarOptions = Array.Empty<Sprite>();
        [SerializeField, Min(0)] int avatarIndex;

        [Header("Progress (connect Player Stats later)")]
        [SerializeField, Min(0)] int level;
        [SerializeField, Min(0)] int currentExperience;
        [SerializeField, Min(0)] int requiredExperience;

        [Header("Statistics (-1 means unavailable)")]
        [SerializeField] int creaturesSeen = -1;
        [SerializeField] int speciesCaptured = -1;
        [SerializeField] int codexEntries = -1;
        [SerializeField] int codexTotal = -1;
        [SerializeField] int bossesDefeated = -1;
        [SerializeField] int totalBattles = -1;

        [Header("References")]
        [SerializeField] QuestManager questManager;
        [SerializeField] AchievementManager achievementManager;
        [SerializeField] PlayerSaveController saveController;

        double storedPlayTimeSeconds;
        float saveTimer;
        readonly HashSet<string> capturedSpeciesIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event Action ProfileChanged;

        public bool CanChangeAvatar => avatarOptions != null && avatarOptions.Any(sprite => sprite != null);
        public IReadOnlyList<Sprite> AvatarOptions => avatarOptions ?? Array.Empty<Sprite>();
        public int SelectedAvatarIndex {
            get {
                if (!CanChangeAvatar) return -1;
                int current = Mathf.Clamp(avatarIndex, 0, avatarOptions.Length - 1);
                if (avatarOptions[current] != null) return current;
                for (int i = 0; i < avatarOptions.Length; i++) {
                    if (avatarOptions[i] != null) return i;
                }
                return -1;
            }
        }

        void Awake() {
            EnsureIdentity();
            ResolveQuestManager();
            ResolveAchievementManager();
            SyncAchievementMetrics();
        }

        void OnEnable() {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SubscribeQuestManager();
            SubscribeAchievementManager();
        }

        void Update() {
            storedPlayTimeSeconds += Time.unscaledDeltaTime;
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer < SaveIntervalSeconds) return;

            saveTimer = 0f;
            RequestSave();
        }

        void OnDisable() {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            UnsubscribeQuestManager();
            UnsubscribeAchievementManager();
            RequestSave();
        }

        void OnApplicationQuit() {
            ResolveSaveController();
            saveController?.SaveNow();
        }

        public PlayerProfileSnapshot GetSnapshot() {
            ResolveQuestManager();
            ResolveAchievementManager();
            GetStoryProgress(out int storyCompleted, out int storyTotal);

            AchievementSnapshot[] badges = achievementManager != null
                ? achievementManager.GetUnlockedBadges(4)
                : Array.Empty<AchievementSnapshot>();
            float achievementCompletion = achievementManager != null
                ? achievementManager.GetCompletion01()
                : 0f;

            return new PlayerProfileSnapshot(
                displayName,
                playerId,
                level,
                currentExperience,
                requiredExperience,
                GetCurrentAvatar(),
                storedPlayTimeSeconds,
                dateStarted,
                SceneManager.GetActiveScene().name,
                creaturesSeen,
                speciesCaptured,
                codexEntries,
                codexTotal,
                storyCompleted,
                storyTotal,
                bossesDefeated,
                totalBattles,
                badges,
                achievementCompletion);
        }

        public bool TrySetDisplayName(string nextDisplayName, out string error) {
            string cleanName = (nextDisplayName ?? string.Empty).Trim();
            if (cleanName.Length < 2 || cleanName.Length > 20) {
                error = "Tên phải có từ 2 đến 20 ký tự.";
                return false;
            }

            displayName = cleanName;
            error = string.Empty;
            RequestSave();
            ProfileChanged?.Invoke();
            return true;
        }

        public bool TrySelectNextAvatar() {
            if (!CanChangeAvatar) return false;

            int startIndex = Mathf.Max(0, SelectedAvatarIndex);
            for (int offset = 1; offset <= avatarOptions.Length; offset++) {
                int candidate = (startIndex + offset) % avatarOptions.Length;
                if (avatarOptions[candidate] == null) continue;
                return TrySelectAvatar(candidate);
            }

            return false;
        }

        public bool TrySelectAvatar(int nextAvatarIndex) {
            if (avatarOptions == null
                || nextAvatarIndex < 0
                || nextAvatarIndex >= avatarOptions.Length
                || avatarOptions[nextAvatarIndex] == null) {
                return false;
            }

            avatarIndex = nextAvatarIndex;
            RequestSave();
            ProfileChanged?.Invoke();
            return true;
        }

        public void SetLevelProgress(int nextLevel, int experience, int required) {
            level = Mathf.Max(0, nextLevel);
            currentExperience = Mathf.Max(0, experience);
            requiredExperience = Mathf.Max(0, required);
            NotifyChanged();
        }

        public void AddExperience(int amount) {
            if (amount <= 0) return;

            long nextExperience = (long)currentExperience + amount;
            if (requiredExperience > 0) {
                while (nextExperience >= requiredExperience && level < int.MaxValue) {
                    nextExperience -= requiredExperience;
                    level++;
                }
            }

            currentExperience = (int)Math.Min(int.MaxValue, Math.Max(0L, nextExperience));
            NotifyChanged();
        }

        public void SetCodexEntries(int count) {
            codexEntries = Mathf.Max(0, count);
            SyncAchievementMetric(AchievementMetricIds.CodexEntries, codexEntries);
            NotifyChanged();
        }

        public void SetCodexProgress(int count, int total) {
            codexEntries = Mathf.Max(0, count);
            codexTotal = Mathf.Max(0, total);
            SyncAchievementMetric(AchievementMetricIds.CodexEntries, codexEntries);
            NotifyChanged();
        }

        public void RecordCreatureSeen() {
            creaturesSeen = Mathf.Max(0, creaturesSeen) + 1;
            SyncAchievementMetric(AchievementMetricIds.CreaturesSeen, creaturesSeen);
            NotifyChanged();
        }

        public void RecordSpeciesCaptured() {
            speciesCaptured = Mathf.Max(0, speciesCaptured) + 1;
            SyncAchievementMetric(AchievementMetricIds.SpeciesCaptured, speciesCaptured);
            NotifyChanged();
        }

        public bool RecordSpeciesCaptured(string speciesId) {
            string normalizedId = (speciesId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedId)) {
                RecordSpeciesCaptured();
                return true;
            }
            if (!capturedSpeciesIds.Add(normalizedId)) return false;

            speciesCaptured = Mathf.Max(0, speciesCaptured) + 1;
            SyncAchievementMetric(AchievementMetricIds.SpeciesCaptured, speciesCaptured);
            NotifyChanged();
            return true;
        }

        public void RecordBossDefeated() {
            bossesDefeated = Mathf.Max(0, bossesDefeated) + 1;
            SyncAchievementMetric(AchievementMetricIds.BossesDefeated, bossesDefeated);
            NotifyChanged();
        }

        public void RecordBattle() {
            totalBattles = Mathf.Max(0, totalBattles) + 1;
            SyncAchievementMetric(AchievementMetricIds.TotalBattles, totalBattles);
            NotifyChanged();
        }

        void NotifyChanged() {
            RequestSave();
            ProfileChanged?.Invoke();
        }

        public PlayerProfileSaveData CreateSaveData() {
            EnsureIdentity();
            var saveData = new PlayerProfileSaveData {
                captured = true,
                displayName = displayName ?? string.Empty,
                playerId = playerId ?? string.Empty,
                dateStarted = dateStarted ?? string.Empty,
                avatarIndex = avatarIndex,
                level = Mathf.Max(0, level),
                currentExperience = Mathf.Max(0, currentExperience),
                requiredExperience = Mathf.Max(0, requiredExperience),
                playTimeSeconds = Math.Max(0d, storedPlayTimeSeconds),
                creaturesSeen = creaturesSeen,
                speciesCaptured = speciesCaptured,
                codexEntries = codexEntries,
                codexTotal = codexTotal,
                bossesDefeated = bossesDefeated,
                totalBattles = totalBattles
            };
            saveData.capturedSpeciesIds.AddRange(capturedSpeciesIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            return saveData;
        }

        public void RestoreFromSaveData(PlayerProfileSaveData saveData) {
            if (saveData == null || !saveData.captured) return;

            displayName = saveData.displayName ?? string.Empty;
            playerId = saveData.playerId ?? string.Empty;
            dateStarted = saveData.dateStarted ?? string.Empty;
            avatarIndex = Mathf.Max(0, saveData.avatarIndex);
            level = Mathf.Max(0, saveData.level);
            currentExperience = Mathf.Max(0, saveData.currentExperience);
            requiredExperience = Mathf.Max(0, saveData.requiredExperience);
            storedPlayTimeSeconds = Math.Max(0d, saveData.playTimeSeconds);
            creaturesSeen = saveData.creaturesSeen;
            speciesCaptured = saveData.speciesCaptured;
            codexEntries = saveData.codexEntries;
            codexTotal = saveData.codexTotal;
            bossesDefeated = saveData.bossesDefeated;
            totalBattles = saveData.totalBattles;
            capturedSpeciesIds.Clear();
            foreach (string speciesId in saveData.capturedSpeciesIds ?? new List<string>()) {
                if (!string.IsNullOrWhiteSpace(speciesId)) capturedSpeciesIds.Add(speciesId.Trim());
            }
            if (capturedSpeciesIds.Count > 0) {
                speciesCaptured = Mathf.Max(speciesCaptured, capturedSpeciesIds.Count);
            }
            EnsureIdentity();
            SyncAchievementMetrics();
            ProfileChanged?.Invoke();
        }

        public void ResetForNewGame() {
            displayName = string.Empty;
            playerId = string.Empty;
            dateStarted = string.Empty;
            avatarIndex = 0;
            level = 0;
            currentExperience = 0;
            requiredExperience = 0;
            storedPlayTimeSeconds = 0d;
            creaturesSeen = -1;
            speciesCaptured = -1;
            codexEntries = -1;
            codexTotal = -1;
            bossesDefeated = -1;
            totalBattles = -1;
            capturedSpeciesIds.Clear();
            EnsureIdentity();
            ProfileChanged?.Invoke();
        }

        Sprite GetCurrentAvatar() {
            if (!CanChangeAvatar) return null;
            avatarIndex = SelectedAvatarIndex;
            return avatarIndex >= 0 ? avatarOptions[avatarIndex] : null;
        }

        void GetStoryProgress(out int completed, out int total) {
            completed = 0;
            total = 0;
            if (questManager == null) return;

            var mainDefinitions = questManager.GetQuestDefinitions()
                .Where(definition => definition != null && definition.QuestType == QuestType.Main)
                .ToArray();

            total = mainDefinitions.Length;
            foreach (QuestDefinition definition in mainDefinitions) {
                QuestRuntimeState state = questManager.GetQuestState(definition.QuestId);
                if (state != null && state.Status == QuestStatus.Completed) completed++;
            }
        }

        void ResolveQuestManager() {
            if (questManager != null) return;
            questManager = FindFirstObjectByType<QuestManager>();
            SubscribeQuestManager();
        }

        void SubscribeQuestManager() {
            if (questManager == null) return;
            questManager.QuestsChanged -= OnQuestsChanged;
            questManager.QuestsChanged += OnQuestsChanged;
        }

        void UnsubscribeQuestManager() {
            if (questManager == null) return;
            questManager.QuestsChanged -= OnQuestsChanged;
        }

        void ResolveAchievementManager() {
            if (achievementManager != null) return;
            achievementManager = FindFirstObjectByType<AchievementManager>();
            if (achievementManager == null && Application.isPlaying) {
                achievementManager = GetComponent<AchievementManager>();
                if (achievementManager == null) achievementManager = gameObject.AddComponent<AchievementManager>();
            }

            SubscribeAchievementManager();
        }

        void SubscribeAchievementManager() {
            if (achievementManager == null) return;
            achievementManager.AchievementsChanged -= OnAchievementsChanged;
            achievementManager.AchievementsChanged += OnAchievementsChanged;
        }

        void UnsubscribeAchievementManager() {
            if (achievementManager == null) return;
            achievementManager.AchievementsChanged -= OnAchievementsChanged;
        }

        void SyncAchievementMetrics(int completedMainQuests = -1) {
            ResolveAchievementManager();
            if (achievementManager == null) return;

            if (completedMainQuests < 0) {
                GetStoryProgress(out completedMainQuests, out _);
            }

            var updates = new List<KeyValuePair<string, int>>(6);
            AddMetricUpdate(updates, AchievementMetricIds.CreaturesSeen, creaturesSeen);
            AddMetricUpdate(updates, AchievementMetricIds.SpeciesCaptured, speciesCaptured);
            AddMetricUpdate(updates, AchievementMetricIds.CodexEntries, codexEntries);
            AddMetricUpdate(updates, AchievementMetricIds.BossesDefeated, bossesDefeated);
            AddMetricUpdate(updates, AchievementMetricIds.TotalBattles, totalBattles);
            AddMetricUpdate(updates, AchievementMetricIds.MainQuestsCompleted, completedMainQuests);
            achievementManager.SetProgressBatch(updates);
        }

        static void AddMetricUpdate(List<KeyValuePair<string, int>> updates, string metricId, int value) {
            if (value < 0) return;
            updates.Add(new KeyValuePair<string, int>(metricId, value));
        }

        void SyncAchievementMetric(string metricId, int value) {
            ResolveAchievementManager();
            achievementManager?.SetProgress(metricId, value);
        }

        void OnQuestsChanged() {
            GetStoryProgress(out int storyCompleted, out _);
            SyncAchievementMetrics(storyCompleted);
            ProfileChanged?.Invoke();
        }

        void OnAchievementsChanged() {
            ProfileChanged?.Invoke();
        }

        void OnActiveSceneChanged(Scene previous, Scene current) {
            ResolveQuestManager();
            ProfileChanged?.Invoke();
        }

        void EnsureIdentity() {
            if (string.IsNullOrWhiteSpace(playerId)) {
                playerId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            }

            if (string.IsNullOrWhiteSpace(dateStarted)) {
                dateStarted = DateTime.Now.ToString("yyyy-MM-dd");
            }
        }

        void RequestSave() {
            ResolveSaveController();
            saveController?.RequestSave();
        }

        void ResolveSaveController() {
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>();
        }
    }
}

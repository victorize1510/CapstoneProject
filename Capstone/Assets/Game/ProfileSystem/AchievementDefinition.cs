using UnityEngine;

namespace Capstone.Game.ProfileSystem {
    public static class AchievementMetricIds {
        public const string CreaturesSeen = "creatures_seen";
        public const string SpeciesCaptured = "species_captured";
        public const string CodexEntries = "codex_entries";
        public const string MainQuestsCompleted = "main_quests_completed";
        public const string BossesDefeated = "bosses_defeated";
        public const string TotalBattles = "total_battles";
    }

    [CreateAssetMenu(fileName = "Achievement", menuName = "Capstone/Profile/Achievement Definition")]
    public sealed class AchievementDefinition : ScriptableObject {
        [SerializeField] string achievementId = string.Empty;
        [SerializeField] string displayName = string.Empty;
        [SerializeField, TextArea(2, 4)] string description = string.Empty;
        [SerializeField] Sprite icon;
        [SerializeField] string metricId = string.Empty;
        [SerializeField, Min(1)] int requiredProgress = 1;
        [SerializeField] int displayPriority;

        public string AchievementId => achievementId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public string MetricId => metricId;
        public int RequiredProgress => Mathf.Max(1, requiredProgress);
        public int DisplayPriority => displayPriority;

        public bool IsValid => !string.IsNullOrWhiteSpace(achievementId)
            && !string.IsNullOrWhiteSpace(metricId);

        public static AchievementDefinition CreateRuntime(
            string id,
            string title,
            string details,
            string metric,
            int target,
            int priority) {
            AchievementDefinition definition = CreateInstance<AchievementDefinition>();
            definition.name = "RuntimeAchievement_" + id;
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.achievementId = id;
            definition.displayName = title;
            definition.description = details;
            definition.metricId = metric;
            definition.requiredProgress = Mathf.Max(1, target);
            definition.displayPriority = priority;
            return definition;
        }
    }
}

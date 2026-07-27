using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.QuestSystem.UI {
    [RequireComponent(typeof(UIDocument))]
    public sealed class QuestTrackerHudController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] QuestManager questManager = null;
        [SerializeField] Transform localPlayer = null;
        [SerializeField] bool autoFindQuestManager = true;
        [SerializeField] bool autoFindLocalPlayer = true;
        [SerializeField] bool showOnlyMainQuest = true;
        [SerializeField] bool fallbackToFirstActiveMainQuest = true;
        [SerializeField] bool hideWhenNoQuest = true;
        [SerializeField] float refreshInterval = 0.2f;

        VisualElement root;
        VisualElement card;
        Label typeLabel;
        Label titleLabel;
        Label metaLabel;
        Label descriptionLabel;
        VisualElement objectiveList;
        ProgressBar progressBar;
        Label progressText;
        VisualElement locationRow;
        Label locationLabel;
        VisualElement distanceRow;
        Label distanceLabel;
        VisualElement timeRow;
        Label timeLabel;
        VisualElement rewardRow;
        Label rewardsLabel;
        Label emptyLabel;
        QuestRuntimeState displayedQuest;
        float nextRefreshTime;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            SubscribeToQuestManager();
            Refresh();
        }

        void OnDisable() {
            UnsubscribeFromQuestManager();
        }

        void Update() {
            if (Time.unscaledTime < nextRefreshTime) return;

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            RefreshDynamicValues();
        }

        public void Bind(UIDocument newDocument, QuestManager newQuestManager = null, Transform newLocalPlayer = null) {
            UnsubscribeFromQuestManager();
            document = newDocument != null ? newDocument : document;
            questManager = newQuestManager != null ? newQuestManager : questManager;
            localPlayer = newLocalPlayer != null ? newLocalPlayer : localPlayer;
            ResolveReferences();
            CacheElements();
            SubscribeToQuestManager();
            Refresh();
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            if (questManager == null && autoFindQuestManager) questManager = FindFirstObjectByType<QuestManager>();
            if (localPlayer == null && autoFindLocalPlayer) localPlayer = FindLocalPlayer();
        }

        void CacheElements() {
            if (document == null) return;

            root = document.rootVisualElement.Q<VisualElement>("quest-tracker-root");
            if (root == null) {
                root = document.rootVisualElement;
            }

            card = root.Q<VisualElement>("quest-tracker-card");
            typeLabel = root.Q<Label>("quest-tracker-type");
            titleLabel = root.Q<Label>("quest-tracker-title");
            metaLabel = root.Q<Label>("quest-tracker-meta");
            descriptionLabel = root.Q<Label>("quest-tracker-description");
            objectiveList = root.Q<VisualElement>("quest-tracker-objective-list");
            progressBar = root.Q<ProgressBar>("quest-tracker-progress-bar");
            progressText = root.Q<Label>("quest-tracker-progress-text");
            locationRow = root.Q<VisualElement>("quest-tracker-location-row");
            locationLabel = root.Q<Label>("quest-tracker-location");
            distanceRow = root.Q<VisualElement>("quest-tracker-distance-row");
            distanceLabel = root.Q<Label>("quest-tracker-distance");
            timeRow = root.Q<VisualElement>("quest-tracker-time-row");
            timeLabel = root.Q<Label>("quest-tracker-time");
            rewardRow = root.Q<VisualElement>("quest-tracker-reward-row");
            rewardsLabel = root.Q<Label>("quest-tracker-rewards");
            emptyLabel = root.Q<Label>("quest-tracker-empty");
        }

        void SubscribeToQuestManager() {
            if (questManager == null) return;

            questManager.QuestsChanged -= HandleQuestsChanged;
            questManager.TrackedQuestChanged -= HandleTrackedQuestChanged;
            questManager.QuestsChanged += HandleQuestsChanged;
            questManager.TrackedQuestChanged += HandleTrackedQuestChanged;
        }

        void UnsubscribeFromQuestManager() {
            if (questManager == null) return;

            questManager.QuestsChanged -= HandleQuestsChanged;
            questManager.TrackedQuestChanged -= HandleTrackedQuestChanged;
        }

        void HandleQuestsChanged() {
            Refresh();
        }

        void HandleTrackedQuestChanged(QuestRuntimeState quest) {
            Refresh();
        }

        void Refresh() {
            displayedQuest = ResolveQuestToDisplay();
            bool hasQuest = displayedQuest != null && displayedQuest.Definition != null;

            SetVisible(root, hasQuest || !hideWhenNoQuest);
            SetVisible(card, hasQuest || !hideWhenNoQuest);
            SetVisible(emptyLabel, !hasQuest && !hideWhenNoQuest);

            if (!hasQuest) {
                SetQuestContentVisible(false);
                return;
            }

            SetQuestContentVisible(true);

            QuestDefinition definition = displayedQuest.Definition;
            SetText(typeLabel, FormatType(definition.QuestType));
            SetText(titleLabel, GetQuestTitle(displayedQuest));
            SetText(metaLabel, "Lv. " + definition.RecommendedLevel + "  |  " + FormatStatus(displayedQuest.Status));
            SetText(descriptionLabel, definition.Description);
            SetVisible(descriptionLabel, !string.IsNullOrWhiteSpace(definition.Description));

            RefreshObjectives();
            RefreshProgress();
            RefreshLocation();
            RefreshTime();
            RefreshRewards();
        }

        void RefreshDynamicValues() {
            if (displayedQuest == null) return;

            QuestRuntimeState currentQuest = ResolveQuestToDisplay();
            if (currentQuest != displayedQuest) {
                Refresh();
                return;
            }

            RefreshLocation();
            RefreshTime();
        }

        QuestRuntimeState ResolveQuestToDisplay() {
            if (questManager == null) return null;

            QuestRuntimeState trackedQuest = questManager.GetTrackedQuest();
            if (IsAllowedQuest(trackedQuest)) return trackedQuest;
            if (!fallbackToFirstActiveMainQuest) return null;

            return questManager.GetActiveQuests()
                .FirstOrDefault(quest => quest != null && quest.Definition != null && quest.Definition.QuestType == QuestType.Main);
        }

        bool IsAllowedQuest(QuestRuntimeState quest) {
            if (quest == null || quest.Definition == null || quest.Status != QuestStatus.Active) return false;
            return !showOnlyMainQuest || quest.Definition.QuestType == QuestType.Main;
        }

        void RefreshObjectives() {
            if (objectiveList == null) return;

            objectiveList.Clear();
            if (displayedQuest.Objectives.Count == 0) {
                objectiveList.Add(CreateObjectiveRow("No objectives", false));
                return;
            }

            foreach (QuestObjectiveProgress progress in displayedQuest.Objectives.Where(objective => objective != null)) {
                QuestObjectiveDefinition definition = FindObjectiveDefinition(progress.ObjectiveId);
                objectiveList.Add(CreateObjectiveRow(FormatObjectiveText(definition, progress), progress.IsComplete));
            }
        }

        VisualElement CreateObjectiveRow(string text, bool complete) {
            var row = new VisualElement();
            row.AddToClassList("quest-tracker-objective");
            row.EnableInClassList("is-complete", complete);

            var bullet = new VisualElement();
            bullet.AddToClassList("quest-tracker-objective-bullet");
            row.Add(bullet);

            var label = new Label(text);
            label.AddToClassList("quest-tracker-objective-text");
            row.Add(label);

            return row;
        }

        void RefreshProgress() {
            int current = 0;
            int required = 0;

            foreach (QuestObjectiveProgress objective in displayedQuest.Objectives.Where(objective => objective != null && !objective.Optional)) {
                current += objective.CurrentAmount;
                required += objective.RequiredAmount;
            }

            if (required <= 0) {
                current = displayedQuest.HasRequiredObjectivesComplete() ? 1 : 0;
                required = 1;
            }

            float percent = required > 0 ? Mathf.Clamp01((float)current / required) * 100f : 0f;
            if (progressBar != null) {
                progressBar.value = percent;
                progressBar.title = string.Empty;
            }

            SetText(progressText, current + " / " + required);
        }

        void RefreshLocation() {
            bool hasLocation = HasLocation(displayedQuest);
            SetVisible(locationRow, hasLocation);
            SetVisible(distanceRow, hasLocation && localPlayer != null);

            if (!hasLocation || displayedQuest.Definition == null) return;

            SetText(locationLabel, GetLocationName(displayedQuest.Definition));
            if (localPlayer != null) {
                float distance = Vector3.Distance(localPlayer.position, displayedQuest.Definition.WorldPosition);
                SetText(distanceLabel, FormatDistance(distance));
            }
        }

        void RefreshTime() {
            bool hasTimeLimit = displayedQuest.Definition != null && displayedQuest.Definition.HasTimeLimit;
            SetVisible(timeRow, hasTimeLimit);
            if (!hasTimeLimit) return;

            float elapsed = Mathf.Max(0f, Time.time - displayedQuest.AcceptedTime);
            float remaining = Mathf.Max(0f, displayedQuest.Definition.TimeLimit - elapsed);
            SetText(timeLabel, FormatTime(remaining));
        }

        void RefreshRewards() {
            if (rewardRow == null || rewardsLabel == null || displayedQuest.Definition == null) return;

            var rewards = displayedQuest.Definition.Rewards
                .Where(reward => reward != null && !string.IsNullOrWhiteSpace(reward.DisplayName))
                .Select(reward => reward.DisplayName + " x" + reward.Amount)
                .ToArray();

            bool hasRewards = rewards.Length > 0;
            SetVisible(rewardRow, hasRewards);
            if (hasRewards) SetText(rewardsLabel, string.Join(", ", rewards));
        }

        void SetQuestContentVisible(bool visible) {
            SetVisible(typeLabel, visible);
            SetVisible(titleLabel, visible);
            SetVisible(metaLabel, visible);
            SetVisible(descriptionLabel, visible);
            SetVisible(objectiveList, visible);
            SetVisible(progressBar, visible);
            SetVisible(progressText, visible);
            SetVisible(locationRow, false);
            SetVisible(distanceRow, false);
            SetVisible(timeRow, false);
            SetVisible(rewardRow, false);
        }

        QuestObjectiveDefinition FindObjectiveDefinition(string objectiveId) {
            if (displayedQuest == null || displayedQuest.Definition == null) return null;
            return displayedQuest.Definition.Objectives.FirstOrDefault(objective => objective != null && objective.ObjectiveId == objectiveId);
        }

        static Transform FindLocalPlayer() {
            try {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null) return taggedPlayer.transform;
            } catch (UnityException) {
                // Some prototype scenes do not define a Player tag yet.
            }

            GameObject namedPlayer = GameObject.Find("Player");
            return namedPlayer != null ? namedPlayer.transform : null;
        }

        static bool HasLocation(QuestRuntimeState quest) {
            if (quest == null || quest.Definition == null) return false;

            return !string.IsNullOrWhiteSpace(quest.Definition.LocationName)
                || quest.Definition.WorldPosition != Vector3.zero;
        }

        static string GetLocationName(QuestDefinition definition) {
            if (definition == null) return "-";
            return string.IsNullOrWhiteSpace(definition.LocationName) ? "Marked Location" : definition.LocationName;
        }

        static string GetQuestTitle(QuestRuntimeState quest) {
            if (quest == null) return "-";
            if (quest.Definition != null && !string.IsNullOrWhiteSpace(quest.Definition.Title)) return quest.Definition.Title;
            return string.IsNullOrWhiteSpace(quest.QuestId) ? "Untitled Quest" : quest.QuestId;
        }

        static string FormatObjectiveText(QuestObjectiveDefinition definition, QuestObjectiveProgress progress) {
            string title = definition != null && !string.IsNullOrWhiteSpace(definition.Title)
                ? definition.Title
                : progress.ObjectiveId;

            string optional = progress.Optional ? " (Optional)" : string.Empty;
            return title + optional + "  " + progress.CurrentAmount + " / " + progress.RequiredAmount;
        }

        static string FormatType(QuestType type) {
            switch (type) {
                case QuestType.Side: return "Side";
                case QuestType.Daily: return "Daily";
                case QuestType.Other: return "Other";
                default: return "Main";
            }
        }

        static string FormatStatus(QuestStatus status) {
            switch (status) {
                case QuestStatus.Completed: return "Completed";
                case QuestStatus.Failed: return "Failed";
                case QuestStatus.Abandoned: return "Abandoned";
                default: return "In Progress";
            }
        }

        static string FormatDistance(float distance) {
            return distance >= 1000f
                ? (distance / 1000f).ToString("0.0") + " km"
                : Mathf.RoundToInt(distance) + " m";
        }

        static string FormatTime(float seconds) {
            TimeSpan timeSpan = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return timeSpan.TotalHours >= 1d
                ? string.Format("{0:0}:{1:00}:{2:00}", Math.Floor(timeSpan.TotalHours), timeSpan.Minutes, timeSpan.Seconds)
                : string.Format("{0:0}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
        }

        static void SetVisible(VisualElement element, bool visible) {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetText(Label label, string text) {
            if (label == null) return;
            label.text = string.IsNullOrWhiteSpace(text) ? "-" : text;
        }
    }
}

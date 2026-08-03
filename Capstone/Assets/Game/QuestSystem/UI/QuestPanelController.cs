using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.QuestSystem.UI {
    [RequireComponent(typeof(UIDocument))]
    public sealed class QuestPanelController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] QuestManager questManager = null;
        [SerializeField] Transform localPlayer = null;
        [SerializeField] bool autoFindQuestManager = true;
        [SerializeField] bool autoFindLocalPlayer = true;
        [SerializeField] float liveValueRefreshInterval = 0.25f;

        readonly List<QuestRuntimeState> filteredQuests = new List<QuestRuntimeState>();
        readonly List<Button> tabButtons = new List<Button>();

        VisualElement rootElement;
        ScrollView questListScroll;
        Label questEmptyLabel;
        Label trackedQuestLabel;
        Label detailTitle;
        Label typePill;
        Label levelPill;
        Label descriptionLabel;
        VisualElement objectiveList;
        Label progressValue;
        ProgressBar progressBar;
        VisualElement statusRow;
        Label statusValue;
        VisualElement locationRow;
        Label locationValue;
        VisualElement distanceRow;
        Label distanceValue;
        VisualElement timeRow;
        Label timeValue;
        Label rewardsTitle;
        VisualElement rewardList;
        Button trackToggleButton;
        Button showMapButton;
        Button abandonButton;
        Action<Vector3> showOnMapRequested;

        QuestPanelTab currentTab = QuestPanelTab.Main;
        QuestRuntimeState selectedQuest;
        string selectedQuestId;
        float nextLiveValueRefreshTime;
        bool controlsRegistered;

        public event Action<QuestRuntimeState> QuestSelected;
        public event Action<Vector3> ShowOnMapRequested {
            add {
                showOnMapRequested += value;
                RefreshActionButtons();
            }
            remove {
                showOnMapRequested -= value;
                RefreshActionButtons();
            }
        }

        public QuestRuntimeState SelectedQuest => selectedQuest;

        public void SelectNextTab() {
            ShiftTab(1);
        }

        public void SelectPreviousTab() {
            ShiftTab(-1);
        }

        public void SelectNextQuest() {
            ShiftSelectedQuest(1);
        }

        public void SelectPreviousQuest() {
            ShiftSelectedQuest(-1);
        }

        public void ConfirmSelectedQuest() {
            if (selectedQuest == null || selectedQuest.Status != QuestStatus.Active) return;
            ToggleTrackSelectedQuest();
        }

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            RegisterControls();
            SubscribeToQuestManager();
            RefreshAll();
        }

        void OnDisable() {
            UnregisterControls();
            UnsubscribeFromQuestManager();
        }

        void Update() {
            if (selectedQuest == null) return;
            if (Time.unscaledTime < nextLiveValueRefreshTime) return;

            nextLiveValueRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, liveValueRefreshInterval);
            ResolveLocalPlayer();
            RefreshLiveValues();
        }

        public void Bind(UIDocument newDocument, QuestManager newQuestManager = null, Transform newLocalPlayer = null) {
            document = newDocument != null ? newDocument : document;
            questManager = newQuestManager != null ? newQuestManager : questManager;
            localPlayer = newLocalPlayer != null ? newLocalPlayer : localPlayer;

            CacheElements();
            RegisterControls();
            SubscribeToQuestManager();
            RefreshAll();
        }

        public void SetLocalPlayer(Transform playerTransform) {
            localPlayer = playerTransform;
            RefreshLiveValues();
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            if (questManager == null && autoFindQuestManager) questManager = FindFirstObjectByType<QuestManager>();
            ResolveLocalPlayer();
        }

        void ResolveLocalPlayer() {
            if (localPlayer != null || !autoFindLocalPlayer) return;

            GameObject taggedPlayer = null;
            try {
                taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            } catch (UnityException) {
                taggedPlayer = null;
            }

            if (taggedPlayer != null) {
                localPlayer = taggedPlayer.transform;
                return;
            }

            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var behaviour in behaviours) {
                if (behaviour == null || behaviour.GetType().Name != "BasicPlayerMovement") continue;

                localPlayer = behaviour.transform;
                return;
            }
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;

            rootElement = document.rootVisualElement;
            questListScroll = rootElement.Q<ScrollView>("quest-list-scroll");
            questEmptyLabel = rootElement.Q<Label>("quest-empty-label");
            trackedQuestLabel = rootElement.Q<Label>("tracked-quest-label");
            detailTitle = rootElement.Q<Label>("quest-detail-title");
            typePill = rootElement.Q<Label>("quest-type-pill");
            levelPill = rootElement.Q<Label>("quest-level-pill");
            descriptionLabel = rootElement.Q<Label>("quest-description-label");
            objectiveList = rootElement.Q<VisualElement>("quest-objective-list");
            progressValue = rootElement.Q<Label>("quest-progress-value");
            progressBar = rootElement.Q<ProgressBar>("quest-progress-bar");
            statusRow = rootElement.Q<VisualElement>("quest-status-row");
            statusValue = rootElement.Q<Label>("quest-status-value");
            locationRow = rootElement.Q<VisualElement>("quest-location-row");
            locationValue = rootElement.Q<Label>("quest-location-value");
            distanceRow = rootElement.Q<VisualElement>("quest-distance-row");
            distanceValue = rootElement.Q<Label>("quest-distance-value");
            timeRow = rootElement.Q<VisualElement>("quest-time-row");
            timeValue = rootElement.Q<Label>("quest-time-value");
            rewardsTitle = rootElement.Q<Label>("quest-rewards-title");
            rewardList = rootElement.Q<VisualElement>("quest-reward-list");
            trackToggleButton = rootElement.Q<Button>("track-quest-button");
            showMapButton = rootElement.Q<Button>("show-map-button");
            abandonButton = rootElement.Q<Button>("abandon-quest-button");
        }

        void RegisterControls() {
            if (rootElement == null || controlsRegistered) return;

            tabButtons.Clear();
            RegisterTabButton("quest-tab-main", QuestPanelTab.Main);
            RegisterTabButton("quest-tab-side", QuestPanelTab.Side);
            RegisterTabButton("quest-tab-daily", QuestPanelTab.Daily);
            RegisterTabButton("quest-tab-completed", QuestPanelTab.Completed);

            if (trackToggleButton != null) trackToggleButton.clicked += ToggleTrackSelectedQuest;
            if (showMapButton != null) showMapButton.clicked += ShowSelectedQuestOnMap;
            if (abandonButton != null) abandonButton.clicked += AbandonSelectedQuest;

            controlsRegistered = true;
        }

        void UnregisterControls() {
            if (!controlsRegistered) return;

            foreach (var button in tabButtons) {
                button.clicked -= SelectMainTab;
                button.clicked -= SelectSideTab;
                button.clicked -= SelectDailyTab;
                button.clicked -= SelectCompletedTab;
            }

            if (trackToggleButton != null) trackToggleButton.clicked -= ToggleTrackSelectedQuest;
            if (showMapButton != null) showMapButton.clicked -= ShowSelectedQuestOnMap;
            if (abandonButton != null) abandonButton.clicked -= AbandonSelectedQuest;

            tabButtons.Clear();
            controlsRegistered = false;
        }

        void RegisterTabButton(string buttonName, QuestPanelTab tab) {
            var button = rootElement.Q<Button>(buttonName);
            if (button == null) return;

            tabButtons.Add(button);
            switch (tab) {
                case QuestPanelTab.Side:
                    button.clicked += SelectSideTab;
                    break;
                case QuestPanelTab.Daily:
                    button.clicked += SelectDailyTab;
                    break;
                case QuestPanelTab.Completed:
                    button.clicked += SelectCompletedTab;
                    break;
                default:
                    button.clicked += SelectMainTab;
                    break;
            }
        }

        void SubscribeToQuestManager() {
            if (questManager == null) return;

            questManager.QuestsChanged -= HandleQuestsChanged;
            questManager.QuestsChanged += HandleQuestsChanged;
        }

        void UnsubscribeFromQuestManager() {
            if (questManager == null) return;

            questManager.QuestsChanged -= HandleQuestsChanged;
        }

        void HandleQuestsChanged() {
            RefreshAll();
        }

        void SelectMainTab() {
            SelectTab(QuestPanelTab.Main);
        }

        void SelectSideTab() {
            SelectTab(QuestPanelTab.Side);
        }

        void SelectDailyTab() {
            SelectTab(QuestPanelTab.Daily);
        }

        void SelectCompletedTab() {
            SelectTab(QuestPanelTab.Completed);
        }

        void SelectTab(QuestPanelTab tab) {
            currentTab = tab;
            selectedQuestId = null;
            RefreshAll();
        }

        void ShiftTab(int direction) {
            var order = new[] {
                QuestPanelTab.Main,
                QuestPanelTab.Side,
                QuestPanelTab.Daily,
                QuestPanelTab.Completed
            };

            int currentIndex = Array.IndexOf(order, currentTab);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = (currentIndex + direction + order.Length) % order.Length;
            SelectTab(order[nextIndex]);
        }

        void ShiftSelectedQuest(int direction) {
            if (filteredQuests.Count == 0) return;

            int currentIndex = selectedQuest != null ? filteredQuests.IndexOf(selectedQuest) : -1;
            if (currentIndex < 0) currentIndex = direction > 0 ? -1 : 0;

            int nextIndex = Mathf.Clamp(currentIndex + direction, 0, filteredQuests.Count - 1);
            SelectQuest(filteredQuests[nextIndex]);

            if (questListScroll != null && nextIndex >= 0 && nextIndex < questListScroll.contentContainer.childCount) {
                questListScroll.ScrollTo(questListScroll.contentContainer[nextIndex]);
            }
        }

        void RefreshAll() {
            ResolveReferences();
            UpdateTabButtons();
            RefreshQuestList();
            RefreshDetail();
            RefreshTrackedLabel();
        }

        void RefreshQuestList() {
            filteredQuests.Clear();
            if (questManager != null) filteredQuests.AddRange(GetFilteredQuests());

            if (questListScroll != null) {
                questListScroll.contentContainer.Clear();
                foreach (var quest in filteredQuests) {
                    questListScroll.contentContainer.Add(CreateQuestCard(quest));
                }
            }

            var hasQuests = filteredQuests.Count > 0;
            SetVisible(questListScroll, hasQuests);
            SetVisible(questEmptyLabel, !hasQuests);
            if (questEmptyLabel != null) questEmptyLabel.text = questManager == null ? "QuestManager not found" : "No quests";

            SelectQuestAfterRefresh();
        }

        IEnumerable<QuestRuntimeState> GetFilteredQuests() {
            var quests = questManager.GetAllQuests();
            switch (currentTab) {
                case QuestPanelTab.Side:
                    return quests.Where(quest => IsQuestType(quest, QuestType.Side) && quest.Status == QuestStatus.Active);
                case QuestPanelTab.Daily:
                    return quests.Where(quest => IsQuestType(quest, QuestType.Daily) && quest.Status == QuestStatus.Active);
                case QuestPanelTab.Completed:
                    return quests.Where(quest => quest.Status == QuestStatus.Completed);
                default:
                    return quests.Where(quest => IsQuestType(quest, QuestType.Main) && quest.Status == QuestStatus.Active);
            }
        }

        VisualElement CreateQuestCard(QuestRuntimeState quest) {
            var card = new VisualElement();
            card.AddToClassList("quest-card");
            if (quest == selectedQuest) card.AddToClassList("is-selected");
            if (quest != null && quest.IsTracked) card.AddToClassList("is-tracked");

            var title = new Label(GetQuestTitle(quest));
            title.AddToClassList("quest-card-title");
            card.Add(title);

            var meta = new Label(FormatQuestMeta(quest));
            meta.AddToClassList("quest-card-meta");
            card.Add(meta);

            var status = new Label(FormatStatus(quest != null ? quest.Status : QuestStatus.Active));
            status.AddToClassList("quest-card-status");
            card.Add(status);

            var capturedQuest = quest;
            card.RegisterCallback<ClickEvent>(_ => SelectQuest(capturedQuest));
            return card;
        }

        void SelectQuestAfterRefresh() {
            if (filteredQuests.Count == 0) {
                SelectQuest(null);
                return;
            }

            var nextQuest = filteredQuests.FirstOrDefault(quest => quest != null && quest.QuestId == selectedQuestId);
            if (nextQuest == null) nextQuest = filteredQuests[0];
            SelectQuest(nextQuest);
        }

        void SelectQuest(QuestRuntimeState quest) {
            selectedQuest = quest;
            selectedQuestId = quest != null ? quest.QuestId : null;
            QuestSelected?.Invoke(selectedQuest);
            RefreshQuestCardSelection();
            RefreshDetail();
        }

        void RefreshQuestCardSelection() {
            if (questListScroll == null) return;

            foreach (var card in questListScroll.contentContainer.Children()) {
                card.RemoveFromClassList("is-selected");
            }

            int selectedIndex = selectedQuest != null ? filteredQuests.IndexOf(selectedQuest) : -1;
            if (selectedIndex < 0 || selectedIndex >= questListScroll.contentContainer.childCount) return;

            questListScroll.contentContainer[selectedIndex].AddToClassList("is-selected");
        }

        void RefreshDetail() {
            if (selectedQuest == null || selectedQuest.Definition == null) {
                SetEmptyDetail();
                return;
            }

            var definition = selectedQuest.Definition;
            if (detailTitle != null) detailTitle.text = definition.Title;
            if (typePill != null) typePill.text = FormatType(definition.QuestType);
            if (levelPill != null) levelPill.text = "Recommended Lv. " + definition.RecommendedLevel;
            if (descriptionLabel != null) descriptionLabel.text = string.IsNullOrWhiteSpace(definition.Description) ? "No description." : definition.Description;
            if (statusValue != null) statusValue.text = FormatStatus(selectedQuest.Status);

            RefreshObjectives();
            RefreshProgress();
            RefreshLocationVisibility();
            RefreshLiveValues();
            RefreshRewards();
            RefreshActionButtons();
        }

        void SetEmptyDetail() {
            if (detailTitle != null) detailTitle.text = "Select a quest";
            if (typePill != null) typePill.text = "-";
            if (levelPill != null) levelPill.text = "Lv. -";
            if (descriptionLabel != null) descriptionLabel.text = "No quest selected.";
            if (objectiveList != null) objectiveList.Clear();
            if (progressValue != null) progressValue.text = "0 / 0";
            if (progressBar != null) {
                progressBar.value = 0f;
                progressBar.title = "0%";
            }
            if (statusValue != null) statusValue.text = "-";
            if (locationValue != null) locationValue.text = "-";
            if (distanceValue != null) distanceValue.text = "-";
            if (timeValue != null) timeValue.text = "-";
            if (rewardList != null) rewardList.Clear();

            SetVisible(statusRow, true);
            SetVisible(locationValue, false);
            SetVisible(locationRow, false);
            SetVisible(distanceRow, false);
            SetVisible(timeRow, false);
            SetVisible(rewardsTitle, false);
            SetVisible(rewardList, false);
            RefreshActionButtons();
        }

        void RefreshObjectives() {
            if (objectiveList == null) return;

            objectiveList.Clear();
            if (selectedQuest.Objectives.Count == 0) {
                objectiveList.Add(CreateSimpleObjectiveRow("No objectives."));
                return;
            }

            for (int i = 0; i < selectedQuest.Objectives.Count; i++) {
                var progress = selectedQuest.Objectives[i];
                objectiveList.Add(CreateObjectiveRow(i + 1, progress));
            }
        }

        VisualElement CreateSimpleObjectiveRow(string text) {
            var row = new VisualElement();
            row.AddToClassList("objective-row");

            var label = new Label(text);
            label.AddToClassList("objective-text");
            row.Add(label);
            return row;
        }

        VisualElement CreateObjectiveRow(int index, QuestObjectiveProgress progress) {
            var row = new VisualElement();
            row.AddToClassList("objective-row");

            var bullet = new Label(index.ToString());
            bullet.AddToClassList("objective-bullet");
            row.Add(bullet);

            var objectiveDefinition = FindObjectiveDefinition(progress.ObjectiveId);
            var label = new Label(FormatObjectiveText(objectiveDefinition, progress));
            label.AddToClassList("objective-text");
            row.Add(label);

            return row;
        }

        void RefreshProgress() {
            var objectives = GetProgressObjectives();
            int current = 0;
            int required = 0;

            foreach (var objective in objectives) {
                current += Mathf.Clamp(objective.CurrentAmount, 0, objective.RequiredAmount);
                required += objective.RequiredAmount;
            }

            float percent = 0f;
            if (selectedQuest.Status == QuestStatus.Completed) {
                percent = 1f;
                current = Mathf.Max(current, required);
            } else if (required > 0) {
                percent = Mathf.Clamp01((float)current / required);
            }

            if (progressValue != null) progressValue.text = required > 0 ? current + " / " + required : "0 / 0";
            if (progressBar != null) {
                progressBar.value = percent * 100f;
                progressBar.title = Mathf.RoundToInt(percent * 100f) + "%";
            }
        }

        void RefreshLocationVisibility() {
            bool hasLocation = HasLocation(selectedQuest);

            SetVisible(locationRow, hasLocation);
            SetVisible(locationValue, hasLocation);
            SetVisible(distanceRow, hasLocation);

            if (locationValue != null) {
                locationValue.text = hasLocation
                    ? GetLocationName(selectedQuest.Definition)
                    : "-";
            }
        }

        void RefreshLiveValues() {
            if (selectedQuest == null || selectedQuest.Definition == null) return;

            var definition = selectedQuest.Definition;
            var hasLocation = HasLocation(selectedQuest);
            var hasTimeLimit = definition.HasTimeLimit;

            SetVisible(locationRow, hasLocation);
            SetVisible(distanceRow, hasLocation);
            SetVisible(timeRow, hasTimeLimit);

            if (hasLocation && distanceValue != null) {
                distanceValue.text = localPlayer != null
                    ? FormatDistance(Vector3.Distance(localPlayer.position, definition.WorldPosition))
                    : "-";
            }

            if (hasTimeLimit && timeValue != null) {
                var remaining = Mathf.Max(0f, definition.TimeLimit - Mathf.Max(0f, Time.time - selectedQuest.AcceptedTime));
                timeValue.text = FormatTime(remaining);
            }
        }

        void RefreshRewards() {
            if (rewardList == null || selectedQuest == null || selectedQuest.Definition == null) return;

            rewardList.Clear();
            var rewards = selectedQuest.Definition.Rewards
                .Where(reward => reward != null && !string.IsNullOrWhiteSpace(reward.DisplayName))
                .ToList();

            bool hasRewards = rewards.Count > 0;
            SetVisible(rewardsTitle, hasRewards);
            SetVisible(rewardList, hasRewards);

            foreach (var reward in rewards) {
                rewardList.Add(CreateRewardRow(reward));
            }
        }

        VisualElement CreateRewardRow(QuestRewardDefinition reward) {
            var row = new VisualElement();
            row.AddToClassList("reward-row");

            var icon = new VisualElement();
            icon.AddToClassList("reward-icon");
            if (reward.Icon != null) icon.style.backgroundImage = new StyleBackground(reward.Icon);
            row.Add(icon);

            var label = new Label(FormatReward(reward));
            label.AddToClassList("reward-text");
            row.Add(label);

            return row;
        }

        void RefreshActionButtons() {
            var hasQuest = selectedQuest != null && selectedQuest.Definition != null;
            var isActive = hasQuest && selectedQuest.Status == QuestStatus.Active;
            var hasLocation = hasQuest && HasLocation(selectedQuest);
            var hasMapReceiver = showOnMapRequested != null;

            if (trackToggleButton != null) {
                trackToggleButton.text = hasQuest && selectedQuest.IsTracked ? "Untrack" : "Track";
                trackToggleButton.tooltip = hasQuest && selectedQuest.IsTracked
                    ? "Stop marking this quest for future map guidance."
                    : "Mark this quest for future map and minimap guidance.";
                trackToggleButton.EnableInClassList("primary-quest-action", isActive && !selectedQuest.IsTracked);
            }

            if (showMapButton != null) {
                showMapButton.tooltip = hasMapReceiver
                    ? "Send this quest location to the map system."
                    : "Map system is not connected yet.";
            }

            SetButtonEnabled(trackToggleButton, isActive);
            SetButtonEnabled(showMapButton, hasLocation && hasMapReceiver);
            SetButtonEnabled(abandonButton, isActive && selectedQuest.Definition.CanAbandon);

            SetVisible(showMapButton, hasLocation && hasMapReceiver);
        }

        void RefreshTrackedLabel() {
            if (trackedQuestLabel == null) return;

            var trackedQuest = questManager != null ? questManager.GetTrackedQuest() : null;
            trackedQuestLabel.text = trackedQuest != null ? "Tracked: " + GetQuestTitle(trackedQuest) : "Tracked: -";
        }

        void ToggleTrackSelectedQuest() {
            if (questManager == null || selectedQuest == null) return;

            if (selectedQuest.IsTracked) {
                questManager.UntrackQuest(selectedQuest.QuestId);
            } else {
                questManager.TrackQuest(selectedQuest.QuestId);
            }
        }

        void ShowSelectedQuestOnMap() {
            if (selectedQuest == null || selectedQuest.Definition == null || !HasLocation(selectedQuest) || showOnMapRequested == null) return;

            showOnMapRequested.Invoke(selectedQuest.Definition.WorldPosition);
        }

        void AbandonSelectedQuest() {
            if (questManager == null || selectedQuest == null) return;
            questManager.AbandonQuest(selectedQuest.QuestId);
        }

        void UpdateTabButtons() {
            SetTabSelected("quest-tab-main", currentTab == QuestPanelTab.Main);
            SetTabSelected("quest-tab-side", currentTab == QuestPanelTab.Side);
            SetTabSelected("quest-tab-daily", currentTab == QuestPanelTab.Daily);
            SetTabSelected("quest-tab-completed", currentTab == QuestPanelTab.Completed);
        }

        void SetTabSelected(string buttonName, bool selected) {
            if (rootElement == null) return;

            var button = rootElement.Q<Button>(buttonName);
            if (button == null) return;

            if (selected) button.AddToClassList("is-selected");
            else button.RemoveFromClassList("is-selected");
        }

        List<QuestObjectiveProgress> GetProgressObjectives() {
            if (selectedQuest == null) return new List<QuestObjectiveProgress>();

            var requiredObjectives = selectedQuest.Objectives
                .Where(objective => objective != null && !objective.Optional)
                .ToList();

            return requiredObjectives.Count > 0
                ? requiredObjectives
                : selectedQuest.Objectives.Where(objective => objective != null).ToList();
        }

        QuestObjectiveDefinition FindObjectiveDefinition(string objectiveId) {
            if (selectedQuest == null || selectedQuest.Definition == null) return null;

            return selectedQuest.Definition.Objectives
                .FirstOrDefault(objective => objective != null && objective.ObjectiveId == objectiveId);
        }

        static bool IsQuestType(QuestRuntimeState quest, QuestType type) {
            return quest != null && quest.Definition != null && quest.Definition.QuestType == type;
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

        static string FormatQuestMeta(QuestRuntimeState quest) {
            if (quest == null || quest.Definition == null) return "-";
            return FormatType(quest.Definition.QuestType) + " Quest  |  Lv. " + quest.Definition.RecommendedLevel;
        }

        static string FormatObjectiveText(QuestObjectiveDefinition definition, QuestObjectiveProgress progress) {
            var title = definition != null && !string.IsNullOrWhiteSpace(definition.Title)
                ? definition.Title
                : progress.ObjectiveId;

            var optional = progress.Optional ? " (Optional)" : string.Empty;
            return title + optional + "  " + progress.CurrentAmount + " / " + progress.RequiredAmount;
        }

        static string FormatReward(QuestRewardDefinition reward) {
            if (reward == null) return "-";
            return reward.DisplayName + " x" + reward.Amount;
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
            var timeSpan = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return timeSpan.TotalHours >= 1d
                ? string.Format("{0:0}:{1:00}:{2:00}", Math.Floor(timeSpan.TotalHours), timeSpan.Minutes, timeSpan.Seconds)
                : string.Format("{0:0}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
        }

        static void SetVisible(VisualElement element, bool visible) {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetButtonEnabled(Button button, bool enabled) {
            if (button == null) return;
            button.SetEnabled(enabled);
        }

        enum QuestPanelTab {
            Main,
            Side,
            Daily,
            Completed
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.Inventory;
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
        readonly Dictionary<Button, Action> buttonCallbacks = new Dictionary<Button, Action>();

        VisualElement rootElement;
        ScrollView questListScroll;
        VisualElement questListColumn;
        VisualElement questDetailColumn;
        Label questPageEmpty;
        VisualElement trackedQuestStrip;
        Label trackedQuestLabel;
        VisualElement detailContent;
        VisualElement questIcon;
        Label detailTitle;
        Label typePill;
        Label levelPill;
        Label descriptionLabel;
        VisualElement objectiveList;
        VisualElement objectiveHeading;
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
        VisualElement questActions;
        Button trackToggleButton;
        Button showMapButton;
        Button abandonButton;
        Button backButton;
        Action<Vector3> showOnMapRequested;

        QuestPanelTab currentTab = QuestPanelTab.InProgress;
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
            questListColumn = rootElement.Q<VisualElement>("quest-list-column");
            questDetailColumn = rootElement.Q<VisualElement>("quest-detail-column");
            questPageEmpty = rootElement.Q<Label>("quest-page-empty");
            trackedQuestStrip = rootElement.Q<VisualElement>("quest-tracked-strip");
            trackedQuestLabel = rootElement.Q<Label>("tracked-quest-label");
            detailContent = rootElement.Q<VisualElement>("quest-detail-content");
            questIcon = rootElement.Q<VisualElement>("quest-detail-icon");
            detailTitle = rootElement.Q<Label>("quest-detail-title");
            typePill = rootElement.Q<Label>("quest-type-pill");
            levelPill = rootElement.Q<Label>("quest-level-pill");
            descriptionLabel = rootElement.Q<Label>("quest-description-label");
            objectiveList = rootElement.Q<VisualElement>("quest-objective-list");
            objectiveHeading = rootElement.Q<VisualElement>("quest-objective-heading");
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
            questActions = rootElement.Q<VisualElement>("quest-actions");
            trackToggleButton = rootElement.Q<Button>("track-quest-button");
            showMapButton = rootElement.Q<Button>("show-map-button");
            abandonButton = rootElement.Q<Button>("abandon-quest-button");
            backButton = rootElement.Q<Button>("quest-back-button");
        }

        void RegisterControls() {
            if (rootElement == null || controlsRegistered) return;

            RegisterTabButton("quest-tab-progress", QuestPanelTab.InProgress);
            RegisterTabButton("quest-tab-main", QuestPanelTab.Main);
            RegisterTabButton("quest-tab-side", QuestPanelTab.Side);
            RegisterTabButton("quest-tab-event", QuestPanelTab.Event);
            RegisterTabButton("quest-tab-other", QuestPanelTab.Other);
            RegisterButton(trackToggleButton, ToggleTrackSelectedQuest);
            RegisterButton(showMapButton, ShowSelectedQuestOnMap);
            RegisterButton(abandonButton, AbandonSelectedQuest);
            RegisterButton(backButton, CloseQuestMenu);

            controlsRegistered = true;
        }

        void UnregisterControls() {
            foreach (var pair in buttonCallbacks) {
                pair.Key.clicked -= pair.Value;
            }

            buttonCallbacks.Clear();
            controlsRegistered = false;
        }

        void RegisterTabButton(string buttonName, QuestPanelTab tab) {
            var button = rootElement.Q<Button>(buttonName);
            RegisterButton(button, () => SelectTab(tab));
        }

        void RegisterButton(Button button, Action callback) {
            if (button == null || callback == null || buttonCallbacks.ContainsKey(button)) return;

            button.clicked += callback;
            buttonCallbacks.Add(button, callback);
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

        void SelectTab(QuestPanelTab tab) {
            currentTab = tab;
            selectedQuestId = null;
            RefreshAll();
        }

        void ShiftTab(int direction) {
            var order = new[] {
                QuestPanelTab.InProgress,
                QuestPanelTab.Main,
                QuestPanelTab.Side,
                QuestPanelTab.Event,
                QuestPanelTab.Other
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

            bool hasQuests = filteredQuests.Count > 0;
            SetVisible(questListColumn, hasQuests);
            SetVisible(questDetailColumn, hasQuests);
            SetVisible(questPageEmpty, !hasQuests);
            SetVisible(trackedQuestStrip, hasQuests && questManager != null && questManager.GetTrackedQuest() != null);
            if (questPageEmpty != null) questPageEmpty.text = "NONE";

            SelectQuestAfterRefresh();
        }

        IEnumerable<QuestRuntimeState> GetFilteredQuests() {
            var quests = questManager.GetAllQuests();
            switch (currentTab) {
                case QuestPanelTab.Main:
                    return SortQuests(quests.Where(quest => IsType(quest, QuestType.Main)));
                case QuestPanelTab.Side:
                    return SortQuests(quests.Where(quest => IsType(quest, QuestType.Side)));
                case QuestPanelTab.Event:
                    return SortQuests(quests.Where(quest => IsType(quest, QuestType.Event)));
                case QuestPanelTab.Other:
                    return SortQuests(quests.Where(IsOtherType));
                default:
                    return SortQuests(quests.Where(quest => quest != null && quest.Status == QuestStatus.Active));
            }
        }

        static IEnumerable<QuestRuntimeState> SortQuests(IEnumerable<QuestRuntimeState> quests) {
            return quests
                .OrderByDescending(quest => quest != null && quest.IsTracked)
                .ThenBy(quest => quest != null && quest.Status == QuestStatus.Active ? 0 : 1)
                .ThenBy(quest => GetQuestTitle(quest), StringComparer.CurrentCultureIgnoreCase);
        }

        VisualElement CreateQuestCard(QuestRuntimeState quest) {
            var card = new VisualElement();
            card.AddToClassList("quest-card");
            if (quest == selectedQuest) card.AddToClassList("is-selected");
            if (quest != null && quest.IsTracked) card.AddToClassList("is-tracked");
            if (quest != null && quest.Status == QuestStatus.Completed) card.AddToClassList("is-completed");

            var icon = new VisualElement();
            icon.AddToClassList("quest-card-icon");
            if (quest != null && quest.Definition != null && quest.Definition.Icon != null) {
                icon.style.backgroundImage = new StyleBackground(quest.Definition.Icon);
            }
            card.Add(icon);

            var textGroup = new VisualElement();
            textGroup.AddToClassList("quest-card-text");
            card.Add(textGroup);

            var title = new Label(GetQuestTitle(quest));
            title.AddToClassList("quest-card-title");
            textGroup.Add(title);

            var meta = new Label(FormatQuestCardMeta(quest));
            meta.AddToClassList("quest-card-meta");
            textGroup.Add(meta);

            var right = new VisualElement();
            right.AddToClassList("quest-card-right");
            card.Add(right);

            string progressText = GetQuestProgressText(quest);
            if (!string.IsNullOrWhiteSpace(progressText)) {
                var progress = new Label(progressText);
                progress.AddToClassList("quest-card-progress");
                right.Add(progress);
            }

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

            SetVisible(detailContent, true);

            var definition = selectedQuest.Definition;
            if (questIcon != null) {
                questIcon.ClearClassList();
                questIcon.AddToClassList("quest-detail-icon");
                questIcon.AddToClassList(GetQuestIconClass(definition.QuestType));
                if (definition.Icon != null) {
                    questIcon.style.backgroundImage = new StyleBackground(definition.Icon);
                } else {
                    questIcon.style.backgroundImage = StyleKeyword.None;
                }
            }
            if (detailTitle != null) detailTitle.text = definition.Title;
            if (typePill != null) typePill.text = FormatType(definition.QuestType);
            if (levelPill != null) levelPill.text = "Lv. " + definition.RecommendedLevel;
            if (descriptionLabel != null) descriptionLabel.text = definition.Description ?? string.Empty;
            if (statusValue != null) statusValue.text = FormatStatus(selectedQuest.Status);

            RefreshObjectives();
            RefreshProgress();
            RefreshLocationVisibility();
            RefreshLiveValues();
            RefreshRewards();
            RefreshActionButtons();
        }

        void SetEmptyDetail() {
            SetVisible(detailContent, false);
            if (detailTitle != null) detailTitle.text = string.Empty;
            if (typePill != null) typePill.text = string.Empty;
            if (levelPill != null) levelPill.text = string.Empty;
            if (descriptionLabel != null) descriptionLabel.text = string.Empty;
            if (objectiveList != null) objectiveList.Clear();
            if (progressValue != null) progressValue.text = string.Empty;
            if (progressBar != null) {
                progressBar.value = 0f;
                progressBar.title = string.Empty;
            }
            if (statusValue != null) statusValue.text = string.Empty;
            if (locationValue != null) locationValue.text = string.Empty;
            if (distanceValue != null) distanceValue.text = string.Empty;
            if (timeValue != null) timeValue.text = string.Empty;
            if (rewardList != null) rewardList.Clear();

            SetVisible(statusRow, false);
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
            bool hasObjectives = selectedQuest.Objectives.Count > 0;
            SetVisible(objectiveHeading, hasObjectives);
            SetVisible(objectiveList, hasObjectives);
            SetVisible(progressBar, hasObjectives);
            if (!hasObjectives) return;

            for (int i = 0; i < selectedQuest.Objectives.Count; i++) {
                var progress = selectedQuest.Objectives[i];
                objectiveList.Add(CreateObjectiveRow(progress));
            }
        }

        VisualElement CreateObjectiveRow(QuestObjectiveProgress progress) {
            var row = new VisualElement();
            row.AddToClassList("objective-row");
            if (progress != null && progress.IsComplete) row.AddToClassList("is-complete");

            var objectiveDefinition = FindObjectiveDefinition(progress.ObjectiveId);
            var label = new Label(FormatObjectiveText(objectiveDefinition, progress));
            label.AddToClassList("objective-text");
            row.Add(label);

            var count = new Label(progress != null ? progress.CurrentAmount + " / " + progress.RequiredAmount : string.Empty);
            count.AddToClassList("objective-count");
            row.Add(count);
            return row;
        }

        void RefreshProgress() {
            CalculateProgress(selectedQuest, out int current, out int required, out float percent);

            if (progressValue != null) progressValue.text = required > 0 ? current + " / " + required : string.Empty;
            if (progressBar != null) {
                progressBar.value = percent * 100f;
                progressBar.title = string.Empty;
            }
        }

        void RefreshLocationVisibility() {
            bool hasLocationName = selectedQuest != null
                && selectedQuest.Definition != null
                && !string.IsNullOrWhiteSpace(selectedQuest.Definition.LocationName);
            bool hasDistance = TryGetTarget(selectedQuest, out _) && localPlayer != null;

            SetVisible(locationRow, hasLocationName);
            SetVisible(distanceRow, hasDistance);

            if (locationValue != null) {
                locationValue.text = hasLocationName ? selectedQuest.Definition.LocationName : string.Empty;
            }
        }

        void RefreshLiveValues() {
            if (selectedQuest == null || selectedQuest.Definition == null) return;

            var definition = selectedQuest.Definition;
            bool hasLocationName = !string.IsNullOrWhiteSpace(definition.LocationName);
            bool hasTarget = TryGetTarget(selectedQuest, out QuestTargetInfo target);
            bool hasDistance = hasTarget && localPlayer != null;
            var hasTimeLimit = definition.HasTimeLimit;

            SetVisible(locationRow, hasLocationName);
            SetVisible(distanceRow, hasDistance);
            SetVisible(timeRow, hasTimeLimit);

            if (hasDistance && distanceValue != null) {
                distanceValue.text = FormatDistance(Vector3.Distance(localPlayer.position, target.Position));
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
            var hasTarget = hasQuest && TryGetTarget(selectedQuest, out _);
            var hasMapReceiver = showOnMapRequested != null;

            if (trackToggleButton != null) {
                trackToggleButton.text = hasQuest && selectedQuest.IsTracked ? "Hủy theo dõi" : "Theo dõi nhiệm vụ";
                trackToggleButton.tooltip = hasQuest && selectedQuest.IsTracked
                    ? "Ngừng đánh dấu nhiệm vụ này cho bản đồ/minimap."
                    : "Đánh dấu nhiệm vụ này cho bản đồ/minimap.";
                trackToggleButton.EnableInClassList("is-tracked-action", hasQuest && selectedQuest.IsTracked);
            }

            if (showMapButton != null) {
                showMapButton.tooltip = hasMapReceiver
                    ? "Gửi vị trí nhiệm vụ sang hệ thống bản đồ."
                    : "Map system is not connected yet.";
            }

            SetButtonEnabled(trackToggleButton, isActive);
            SetButtonEnabled(showMapButton, hasTarget && hasMapReceiver);
            SetButtonEnabled(abandonButton, isActive && selectedQuest.Definition.CanAbandon);

            SetVisible(showMapButton, hasTarget);
            SetVisible(questActions, isActive);
        }

        void RefreshTrackedLabel() {
            if (trackedQuestLabel == null) return;

            var trackedQuest = questManager != null ? questManager.GetTrackedQuest() : null;
            trackedQuestLabel.text = trackedQuest != null ? "Đang theo dõi: " + GetQuestTitle(trackedQuest) : string.Empty;
            SetVisible(trackedQuestStrip, filteredQuests.Count > 0 && trackedQuest != null);
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
            if (selectedQuest == null || selectedQuest.Definition == null || showOnMapRequested == null) return;
            if (!TryGetTarget(selectedQuest, out QuestTargetInfo target)) return;

            showOnMapRequested.Invoke(target.Position);
        }

        void AbandonSelectedQuest() {
            if (questManager == null || selectedQuest == null) return;
            questManager.AbandonQuest(selectedQuest.QuestId);
        }

        void CloseQuestMenu() {
            var inventoryController = GetComponent<MonsterInventoryController>();
            if (inventoryController != null) inventoryController.Close();
        }

        void UpdateTabButtons() {
            SetTabSelected("quest-tab-progress", currentTab == QuestPanelTab.InProgress);
            SetTabSelected("quest-tab-main", currentTab == QuestPanelTab.Main);
            SetTabSelected("quest-tab-side", currentTab == QuestPanelTab.Side);
            SetTabSelected("quest-tab-event", currentTab == QuestPanelTab.Event);
            SetTabSelected("quest-tab-other", currentTab == QuestPanelTab.Other);
        }

        void SetTabSelected(string buttonName, bool selected) {
            if (rootElement == null) return;

            var button = rootElement.Q<Button>(buttonName);
            if (button == null) return;

            if (selected) button.AddToClassList("is-selected");
            else button.RemoveFromClassList("is-selected");
        }

        QuestObjectiveDefinition FindObjectiveDefinition(string objectiveId) {
            if (selectedQuest == null || selectedQuest.Definition == null) return null;

            return selectedQuest.Definition.Objectives
                .FirstOrDefault(objective => objective != null && objective.ObjectiveId == objectiveId);
        }

        static void CalculateProgress(QuestRuntimeState quest, out int current, out int required, out float percent) {
            current = 0;
            required = 0;
            percent = 0f;

            if (quest == null) return;

            var objectives = quest.Objectives
                .Where(objective => objective != null && !objective.Optional)
                .ToList();

            if (objectives.Count == 0) {
                objectives = quest.Objectives.Where(objective => objective != null).ToList();
            }

            foreach (var objective in objectives) {
                current += Mathf.Clamp(objective.CurrentAmount, 0, objective.RequiredAmount);
                required += objective.RequiredAmount;
            }

            if (quest.Status == QuestStatus.Completed) {
                current = Mathf.Max(current, required);
                percent = 1f;
            } else if (required > 0) {
                percent = Mathf.Clamp01((float)current / required);
            }
        }

        static bool IsType(QuestRuntimeState quest, QuestType type) {
            return quest != null
                && quest.Definition != null
                && quest.Definition.QuestType == type;
        }

        static bool IsOtherType(QuestRuntimeState quest) {
            if (quest == null || quest.Definition == null) return false;

            var type = quest.Definition.QuestType;
            return type == QuestType.Other
                || type == QuestType.Daily
                || type == QuestType.Companion;
        }

        bool TryGetTarget(QuestRuntimeState quest, out QuestTargetInfo target) {
            target = default;
            return questManager != null && questManager.TryGetQuestTarget(quest, out target);
        }

        static string GetQuestTitle(QuestRuntimeState quest) {
            if (quest == null) return "-";
            if (quest.Definition != null && !string.IsNullOrWhiteSpace(quest.Definition.Title)) return quest.Definition.Title;
            return string.IsNullOrWhiteSpace(quest.QuestId) ? "Untitled Quest" : quest.QuestId;
        }

        string FormatQuestCardMeta(QuestRuntimeState quest) {
            if (quest == null || quest.Definition == null) return string.Empty;
            var type = FormatType(quest.Definition.QuestType);
            if (string.IsNullOrWhiteSpace(quest.Definition.LocationName)) return type;

            var location = quest.Definition.LocationName;
            return string.IsNullOrWhiteSpace(location) ? type : type + "  |  " + location;
        }

        static string GetQuestProgressText(QuestRuntimeState quest) {
            CalculateProgress(quest, out int current, out int required, out _);
            return required > 0 ? current + " / " + required : string.Empty;
        }

        static string FormatObjectiveText(QuestObjectiveDefinition definition, QuestObjectiveProgress progress) {
            if (progress == null) return "-";
            var title = definition != null && !string.IsNullOrWhiteSpace(definition.Title)
                ? definition.Title
                : progress.ObjectiveId;

            var optional = progress.Optional ? " (Optional)" : string.Empty;
            return title + optional;
        }

        static string FormatReward(QuestRewardDefinition reward) {
            if (reward == null) return "-";
            var name = string.IsNullOrWhiteSpace(reward.DisplayName)
                ? reward.RewardType.ToString()
                : reward.DisplayName;
            return name + " x" + reward.Amount;
        }

        static string FormatType(QuestType type) {
            switch (type) {
                case QuestType.Side: return "Nhiệm vụ phụ";
                case QuestType.Event: return "Sự kiện";
                case QuestType.Other: return "Khác";
                case QuestType.Daily:
                case QuestType.Companion:
                    return "Khác";
                default: return "Nhiệm vụ chính";
            }
        }

        static string FormatStatus(QuestStatus status) {
            switch (status) {
                case QuestStatus.Completed: return "Hoàn thành";
                case QuestStatus.Failed: return "Thất bại";
                case QuestStatus.Abandoned: return "Đã hủy";
                default: return "Đang tiến hành";
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

        static string GetQuestIconClass(QuestType type) {
            switch (type) {
                case QuestType.Main: return "quest-main-icon";
                case QuestType.Side: return "quest-side-icon";
                case QuestType.Event: return "quest-event-icon";
                case QuestType.Daily:
                case QuestType.Companion:
                    return "quest-other-icon";
                default: return "quest-other-icon";
            }
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
            InProgress,
            Main,
            Side,
            Event,
            Other
        }
    }
}

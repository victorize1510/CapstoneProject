using System;
using System.Collections.Generic;
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
        [SerializeField] bool hideWhenNoQuest = false;
        [SerializeField, Min(0.05f)] float refreshInterval = 0.2f;

        readonly List<DistanceBinding> distanceBindings = new List<DistanceBinding>();

        VisualElement root;
        VisualElement list;
        ScrollView scrollView;
        Label emptyLabel;
        float nextRefreshTime;
        string lastQuestSignature = string.Empty;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            SubscribeToQuestManager();
            Refresh(true);
        }

        void OnDisable() {
            UnsubscribeFromQuestManager();
        }

        void Update() {
            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + refreshInterval;

            ResolveLocalPlayer();
            string signature = BuildQuestSignature();
            if (signature != lastQuestSignature) {
                Refresh(true);
                return;
            }

            RefreshDistanceLabels();
        }

        public void Bind(UIDocument newDocument, QuestManager newQuestManager = null, Transform newLocalPlayer = null) {
            UnsubscribeFromQuestManager();
            document = newDocument != null ? newDocument : document;
            questManager = newQuestManager != null ? newQuestManager : questManager;
            localPlayer = newLocalPlayer != null ? newLocalPlayer : localPlayer;
            ResolveReferences();
            CacheElements();
            SubscribeToQuestManager();
            Refresh(true);
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            if (questManager == null && autoFindQuestManager) questManager = FindFirstObjectByType<QuestManager>();
            ResolveLocalPlayer();
        }

        void ResolveLocalPlayer() {
            if (localPlayer != null || !autoFindLocalPlayer) return;

            try {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null) {
                    localPlayer = taggedPlayer.transform;
                    return;
                }
            } catch (UnityException) {
            }

            GameObject namedPlayer = GameObject.Find("Player");
            if (namedPlayer != null) localPlayer = namedPlayer.transform;
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;

            root = document.rootVisualElement.Q<VisualElement>("quest-tracker-root");
            if (root == null) root = document.rootVisualElement;

            scrollView = root.Q<ScrollView>("quest-tracker-scroll");
            list = root.Q<VisualElement>("quest-tracker-list");
            if (list == null && scrollView != null) list = scrollView.contentContainer;
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
            Refresh(true);
        }

        void HandleTrackedQuestChanged(QuestRuntimeState quest) {
            Refresh(true);
        }

        void Refresh(bool rebuild) {
            if (root == null) return;

            List<QuestRuntimeState> quests = GetVisibleQuests();
            lastQuestSignature = BuildQuestSignature(quests);

            SetVisible(root, quests.Count > 0 || !hideWhenNoQuest);
            SetVisible(scrollView, quests.Count > 0);
            SetVisible(emptyLabel, quests.Count == 0);

            if (!rebuild) return;

            RebuildQuestList(quests);
        }

        void RebuildQuestList(List<QuestRuntimeState> quests) {
            if (list == null) return;

            list.Clear();
            distanceBindings.Clear();

            foreach (QuestRuntimeState quest in quests) {
                list.Add(CreateQuestCard(quest));
            }

            RefreshDistanceLabels();
        }

        VisualElement CreateQuestCard(QuestRuntimeState quest) {
            var card = new VisualElement();
            card.AddToClassList("quest-tracker-card");
            card.EnableInClassList("is-tracked", quest.IsTracked);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("quest-tracker-title-row");

            var titleBackground = new VisualElement();
            titleBackground.AddToClassList("quest-tracker-title-background");
            for (int i = 0; i < 16; i++) {
                var fade = new VisualElement();
                fade.AddToClassList("quest-tracker-title-fade");
                fade.AddToClassList("quest-tracker-title-fade-" + i);
                titleBackground.Add(fade);
            }
            titleRow.Add(titleBackground);

            var title = new Label(GetQuestTitle(quest));
            title.AddToClassList("quest-tracker-title");
            titleRow.Add(title);
            card.Add(titleRow);

            var objective = new Label(GetPrimaryObjectiveText(quest));
            objective.AddToClassList("quest-tracker-objective");
            card.Add(objective);

            int current;
            int required;
            float percent = CalculateProgress(quest, out current, out required);

            var progressRow = new VisualElement();
            progressRow.AddToClassList("quest-tracker-progress-row");

            var progressBar = new ProgressBar {
                lowValue = 0f,
                highValue = 100f,
                value = percent * 100f,
                title = string.Empty
            };
            progressBar.AddToClassList("quest-tracker-progress-bar");
            progressRow.Add(progressBar);
            card.Add(progressRow);

            if (HasLocation(quest)) {
                var distance = new Label("-");
                distance.AddToClassList("quest-tracker-distance");
                card.Add(distance);
                distanceBindings.Add(new DistanceBinding(distance, quest));
            }

            return card;
        }

        List<QuestRuntimeState> GetVisibleQuests() {
            if (questManager == null) return new List<QuestRuntimeState>();

            return questManager.GetActiveQuests()
                .Where(quest => quest != null && quest.Definition != null)
                .OrderBy(quest => GetQuestTypeSortOrder(quest.Definition.QuestType))
                .ThenByDescending(quest => quest.IsTracked)
                .ThenBy(quest => GetQuestTitle(quest))
                .ToList();
        }

        void RefreshDistanceLabels() {
            foreach (DistanceBinding binding in distanceBindings) {
                if (binding.Label == null || binding.Quest == null || binding.Quest.Definition == null) continue;

                if (localPlayer == null) {
                    binding.Label.text = GetLocationName(binding.Quest.Definition);
                    continue;
                }

                float distance = Vector3.Distance(localPlayer.position, binding.Quest.Definition.WorldPosition);
                binding.Label.text = GetLocationName(binding.Quest.Definition) + "  " + FormatDistance(distance);
            }
        }

        string BuildQuestSignature() {
            return BuildQuestSignature(GetVisibleQuests());
        }

        static string BuildQuestSignature(List<QuestRuntimeState> quests) {
            return string.Join("|", quests.Select(FormatQuestSignature));
        }

        static string FormatQuestSignature(QuestRuntimeState quest) {
            if (quest == null) return string.Empty;

            string objectives = string.Join(",", quest.Objectives
                .Where(objective => objective != null)
                .Select(objective => objective.ObjectiveId + ":" + objective.CurrentAmount + "/" + objective.RequiredAmount + ":" + objective.IsComplete));

            return quest.QuestId + ":" + quest.Status + ":" + quest.IsTracked + ":" + objectives;
        }

        static float CalculateProgress(QuestRuntimeState quest, out int current, out int required) {
            current = 0;
            required = 0;

            foreach (QuestObjectiveProgress objective in quest.Objectives.Where(objective => objective != null && !objective.Optional)) {
                current += Mathf.Clamp(objective.CurrentAmount, 0, objective.RequiredAmount);
                required += objective.RequiredAmount;
            }

            if (required <= 0) {
                current = quest.HasRequiredObjectivesComplete() ? 1 : 0;
                required = 1;
            }

            return required > 0 ? Mathf.Clamp01((float)current / required) : 0f;
        }

        static string GetPrimaryObjectiveText(QuestRuntimeState quest) {
            QuestObjectiveProgress progress = quest.Objectives.FirstOrDefault(objective => objective != null && !objective.IsComplete)
                ?? quest.Objectives.FirstOrDefault(objective => objective != null);

            if (progress == null) return "No objectives";

            QuestObjectiveDefinition definition = quest.Definition.Objectives.FirstOrDefault(objective => objective != null && objective.ObjectiveId == progress.ObjectiveId);
            string title = definition != null && !string.IsNullOrWhiteSpace(definition.Title)
                ? definition.Title
                : progress.ObjectiveId;

            string optional = progress.Optional ? " (Optional)" : string.Empty;
            return title + optional + "  " + progress.CurrentAmount + " / " + progress.RequiredAmount;
        }

        static int GetQuestTypeSortOrder(QuestType type) {
            switch (type) {
                case QuestType.Main: return 0;
                case QuestType.Side: return 1;
                case QuestType.Daily: return 2;
                default: return 3;
            }
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

        static string FormatDistance(float distance) {
            return distance >= 1000f
                ? (distance / 1000f).ToString("0.0") + " km"
                : Mathf.RoundToInt(distance) + " m";
        }

        static void SetVisible(VisualElement element, bool visible) {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        readonly struct DistanceBinding {
            public readonly Label Label;
            public readonly QuestRuntimeState Quest;

            public DistanceBinding(Label label, QuestRuntimeState quest) {
                Label = label;
                Quest = quest;
            }
        }
    }
}

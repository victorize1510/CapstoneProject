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
        [SerializeField, Min(0.05f)] float distanceRefreshInterval = 0.2f;

        VisualElement root;
        VisualElement list;
        ScrollView scrollView;
        QuestRuntimeState trackedQuest;
        Label distanceLabel;
        float nextDistanceRefreshTime;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            SubscribeToQuestManager();
            RefreshTrackedQuest();
        }

        void OnDisable() {
            UnsubscribeFromQuestManager();
        }

        void Update() {
            if (trackedQuest == null || Time.unscaledTime < nextDistanceRefreshTime) return;

            nextDistanceRefreshTime = Time.unscaledTime + distanceRefreshInterval;
            ResolveLocalPlayer();
            RefreshDistance();
        }

        public void Bind(UIDocument newDocument, QuestManager newQuestManager = null, Transform newLocalPlayer = null) {
            UnsubscribeFromQuestManager();
            document = newDocument != null ? newDocument : document;
            questManager = newQuestManager != null ? newQuestManager : questManager;
            localPlayer = newLocalPlayer != null ? newLocalPlayer : localPlayer;
            ResolveReferences();
            CacheElements();
            SubscribeToQuestManager();
            RefreshTrackedQuest();
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

            root = document.rootVisualElement.Q<VisualElement>("quest-tracker-root") ?? document.rootVisualElement;
            scrollView = root.Q<ScrollView>("quest-tracker-scroll");
            list = root.Q<VisualElement>("quest-tracker-list");
            if (list == null && scrollView != null) list = scrollView.contentContainer;
        }

        void SubscribeToQuestManager() {
            if (questManager == null) return;

            questManager.QuestChanged -= HandleQuestChanged;
            questManager.TrackedQuestChanged -= HandleTrackedQuestChanged;
            questManager.QuestsChanged -= HandleQuestsChanged;
            questManager.QuestChanged += HandleQuestChanged;
            questManager.TrackedQuestChanged += HandleTrackedQuestChanged;
            questManager.QuestsChanged += HandleQuestsChanged;
        }

        void UnsubscribeFromQuestManager() {
            if (questManager == null) return;

            questManager.QuestChanged -= HandleQuestChanged;
            questManager.TrackedQuestChanged -= HandleTrackedQuestChanged;
            questManager.QuestsChanged -= HandleQuestsChanged;
        }

        void HandleQuestChanged(QuestRuntimeState quest) {
            if (trackedQuest == null || quest == null || quest.QuestId == trackedQuest.QuestId) {
                RefreshTrackedQuest();
            }
        }

        void HandleTrackedQuestChanged(QuestRuntimeState quest) {
            RefreshTrackedQuest();
        }

        void HandleQuestsChanged() {
            QuestRuntimeState current = questManager != null ? questManager.GetTrackedQuest() : null;
            if (current != trackedQuest) RefreshTrackedQuest();
        }

        void RefreshTrackedQuest() {
            trackedQuest = questManager != null ? questManager.GetTrackedQuest() : null;
            bool hasTrackedQuest = trackedQuest != null && trackedQuest.Definition != null;

            SetVisible(root, hasTrackedQuest);
            SetVisible(scrollView, hasTrackedQuest);
            if (!hasTrackedQuest) {
                list?.Clear();
                distanceLabel = null;
                return;
            }

            RebuildTrackedQuest();
        }

        void RebuildTrackedQuest() {
            if (list == null || trackedQuest == null) return;

            list.Clear();
            distanceLabel = null;

            var card = new VisualElement();
            card.AddToClassList("quest-tracker-card");
            card.AddToClassList("is-tracked");

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

            var title = new Label(GetQuestTitle(trackedQuest));
            title.AddToClassList("quest-tracker-title");
            titleRow.Add(title);
            card.Add(titleRow);

            QuestObjectiveProgress primaryObjective = GetPrimaryObjective(trackedQuest);
            if (primaryObjective != null) {
                var objective = new Label(FormatObjective(trackedQuest, primaryObjective));
                objective.AddToClassList("quest-tracker-objective");
                card.Add(objective);
            }

            CalculateProgress(trackedQuest, out int current, out int required, out float percent);
            if (required > 0) {
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
            }

            if (questManager != null && questManager.TryGetQuestTarget(trackedQuest, out _)) {
                distanceLabel = new Label();
                distanceLabel.AddToClassList("quest-tracker-distance");
                card.Add(distanceLabel);
            }

            list.Add(card);
            RefreshDistance();
        }

        void RefreshDistance() {
            if (distanceLabel == null || questManager == null || trackedQuest == null) return;

            if (!questManager.TryGetQuestTarget(trackedQuest, out QuestTargetInfo target) || localPlayer == null) {
                SetVisible(distanceLabel, false);
                return;
            }

            string location = trackedQuest.Definition != null ? trackedQuest.Definition.LocationName : string.Empty;
            string prefix = string.IsNullOrWhiteSpace(location) ? string.Empty : location + "  ";
            distanceLabel.text = prefix + FormatDistance(Vector3.Distance(localPlayer.position, target.Position));
            SetVisible(distanceLabel, true);
        }

        static QuestObjectiveProgress GetPrimaryObjective(QuestRuntimeState quest) {
            if (quest == null) return null;
            return quest.Objectives.FirstOrDefault(objective => objective != null && !objective.IsComplete)
                ?? quest.Objectives.FirstOrDefault(objective => objective != null);
        }

        static string FormatObjective(QuestRuntimeState quest, QuestObjectiveProgress progress) {
            QuestObjectiveDefinition definition = quest.Definition.Objectives
                .FirstOrDefault(objective => objective != null && objective.ObjectiveId == progress.ObjectiveId);
            string title = definition != null && !string.IsNullOrWhiteSpace(definition.Title)
                ? definition.Title
                : progress.ObjectiveId;
            string optional = progress.Optional ? " (Tùy chọn)" : string.Empty;
            return title + optional + "  " + progress.CurrentAmount + " / " + progress.RequiredAmount;
        }

        static void CalculateProgress(QuestRuntimeState quest, out int current, out int required, out float percent) {
            current = 0;
            required = 0;
            percent = 0f;
            if (quest == null) return;

            foreach (QuestObjectiveProgress objective in quest.Objectives.Where(objective => objective != null && !objective.Optional)) {
                current += Mathf.Clamp(objective.CurrentAmount, 0, objective.RequiredAmount);
                required += objective.RequiredAmount;
            }

            if (required > 0) percent = Mathf.Clamp01((float)current / required);
        }

        static string GetQuestTitle(QuestRuntimeState quest) {
            if (quest == null) return string.Empty;
            if (quest.Definition != null && !string.IsNullOrWhiteSpace(quest.Definition.Title)) return quest.Definition.Title;
            return quest.QuestId ?? string.Empty;
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
    }
}

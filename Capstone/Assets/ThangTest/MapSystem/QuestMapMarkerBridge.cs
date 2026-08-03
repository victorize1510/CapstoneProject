using Capstone.Game.QuestSystem;
using UnityEngine;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class QuestMapMarkerBridge : MonoBehaviour {
        [SerializeField] QuestManager questManager = null;
        [SerializeField] MapMarker trackedQuestMarker = null;
        [SerializeField] Texture trackedQuestIcon = null;
        [SerializeField] Color trackedQuestColor = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] bool showOnlyTrackedQuest = true;

        void Awake() {
            EnsureMarker();
            ResolveQuestManager();
        }

        void OnEnable() {
            ResolveQuestManager();
            if (questManager != null) {
                questManager.TrackedQuestChanged += HandleTrackedQuestChanged;
                questManager.QuestChanged += HandleQuestChanged;
                questManager.QuestsChanged += Refresh;
            }

            Refresh();
        }

        void OnDisable() {
            if (questManager != null) {
                questManager.TrackedQuestChanged -= HandleTrackedQuestChanged;
                questManager.QuestChanged -= HandleQuestChanged;
                questManager.QuestsChanged -= Refresh;
            }
        }

        public void Refresh() {
            EnsureMarker();
            ResolveQuestManager();

            QuestRuntimeState quest = questManager != null ? questManager.GetTrackedQuest() : null;
            ApplyQuest(quest);
        }

        void HandleTrackedQuestChanged(QuestRuntimeState quest) {
            ApplyQuest(quest);
        }

        void HandleQuestChanged(QuestRuntimeState quest) {
            if (!showOnlyTrackedQuest || quest == null || quest.IsTracked) Refresh();
        }

        void ApplyQuest(QuestRuntimeState quest) {
            EnsureMarker();
            if (trackedQuestMarker == null) return;

            if (quest == null || quest.Definition == null || !HasLocation(quest)) {
                trackedQuestMarker.SetVisible(false);
                return;
            }

            QuestDefinition definition = quest.Definition;
            trackedQuestMarker.transform.position = definition.WorldPosition;
            trackedQuestMarker.ConfigureRuntime(
                MapMarkerType.QuestTarget,
                definition.QuestId,
                string.IsNullOrWhiteSpace(definition.Title) ? "Tracked Quest" : definition.Title,
                trackedQuestIcon,
                trackedQuestColor,
                true,
                true);
            trackedQuestMarker.SetVisible(true);
        }

        void EnsureMarker() {
            if (trackedQuestMarker != null) return;

            Transform markerTransform = transform.Find("Tracked Quest Marker");
            GameObject markerObject = markerTransform != null
                ? markerTransform.gameObject
                : new GameObject("Tracked Quest Marker");

            markerObject.transform.SetParent(transform, false);
            trackedQuestMarker = markerObject.GetComponent<MapMarker>();
            if (trackedQuestMarker == null) trackedQuestMarker = markerObject.AddComponent<MapMarker>();
            trackedQuestMarker.SetVisible(false);
        }

        void ResolveQuestManager() {
            if (questManager == null) questManager = FindFirstObjectByType<QuestManager>();
        }

        static bool HasLocation(QuestRuntimeState quest) {
            return quest != null
                && quest.Definition != null
                && (!string.IsNullOrWhiteSpace(quest.Definition.LocationName)
                    || quest.Definition.WorldPosition != Vector3.zero);
        }
    }
}

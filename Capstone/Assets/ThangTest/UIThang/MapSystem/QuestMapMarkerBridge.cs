using System;
using Capstone.Game.QuestSystem;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class QuestMapMarkerBridge : MonoBehaviour {
        [SerializeField] QuestManager questManager = null;
        [SerializeField] MapMarker trackedQuestMarker = null;
        [SerializeField] Texture trackedQuestIcon = null;
        [SerializeField] Color trackedQuestColor = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] bool showOnlyTrackedQuest = true;
        NavMeshPath route;
        MapSystemController mapSystem;
        float nextRefresh;
        Vector3 routeStart;
        QuestTargetInfo routeTarget;
        internal Vector3[] RouteCorners { get; private set; } = Array.Empty<Vector3>();
        internal float RouteDistance { get; private set; }
        internal string TargetLabel => routeTarget.Label;

        void Awake() {
            route = new NavMeshPath();
            mapSystem = GetComponent<MapSystemController>();
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
            if (trackedQuestMarker != null) trackedQuestMarker.SetVisible(false);
            ClearRoute();
        }

        void Update() {
            if (Time.unscaledTime >= nextRefresh) Refresh();
        }

        public void Refresh() {
            nextRefresh = Time.unscaledTime + .35f;
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

            if (quest == null
                || quest.Definition == null
                || questManager == null
                || !questManager.TryGetQuestTarget(quest, out QuestTargetInfo target)) {
                trackedQuestMarker.SetVisible(false);
                ClearRoute();
                return;
            }

            QuestDefinition definition = quest.Definition;
            trackedQuestMarker.transform.position = target.Position;
            string markerTitle = string.IsNullOrWhiteSpace(target.Label)
                ? definition.Title
                : target.Label;
            string markerId = definition.QuestId + ":" + target.ObjectiveId;
            if (trackedQuestMarker.MarkerType != MapMarkerType.QuestTarget || trackedQuestMarker.MarkerId != markerId ||
                trackedQuestMarker.DisplayName != markerTitle)
                trackedQuestMarker.ConfigureRuntime(MapMarkerType.QuestTarget, markerId, markerTitle,
                    trackedQuestIcon, trackedQuestColor, true, true);
            trackedQuestMarker.SetVisible(true);
            UpdateRoute(target);
        }

        void UpdateRoute(QuestTargetInfo target) {
            bool newTarget = target.QuestId != routeTarget.QuestId || target.ObjectiveId != routeTarget.ObjectiveId ||
                (target.Position - routeTarget.Position).sqrMagnitude > .04f;
            routeTarget = target;
            var player = mapSystem != null ? mapSystem.PlayerTarget : null;
            if (player == null) return;
            if (!newTarget && RouteCorners.Length > 0 && (player.position - routeStart).sqrMagnitude < 1f) return;

            routeStart = player.position;
            RouteCorners = TryCalculateRoute(routeStart, target.Position, route) ? route.corners : Array.Empty<Vector3>();
            RouteDistance = 0f;
            for (int i = 1; i < RouteCorners.Length; i++)
                RouteDistance += Vector3.Distance(RouteCorners[i - 1], RouteCorners[i]);
        }

        internal static bool TryCalculateRoute(Vector3 from, Vector3 to, NavMeshPath path) {
            if (!NavMesh.SamplePosition(from, out var start, 3.5f, NavMesh.AllAreas)) return false;
            // Keep both projected endpoints close to the actual player and destination.
            if (!NavMesh.SamplePosition(to, out var finish, 3.5f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(start.position, finish.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        void ClearRoute() {
            routeTarget = default;
            RouteCorners = Array.Empty<Vector3>();
            RouteDistance = 0f;
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
    }
}

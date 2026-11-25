using System.Linq;
using Capstone.Game.CreatureSystem;
using Capstone.Game.MapSystem;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class ChapterWorldLayout : MonoBehaviour {
        [Header("Trưởng làng ở hai khu vực")]
        [SerializeField] Transform grassElder, caveElder;
        [SerializeField] Transform grassGuide;
        [Header("Điểm đến khi dịch chuyển")]
        [SerializeField] Transform grassPortal, cavePortal, entryPoint, duskPoint, bossPoint;
        [SerializeField] DummyEnemy boss;
        [SerializeField] Sprite grassMap, caveMap;
        [SerializeField] Vector4 grassBounds, caveBounds;
        [SerializeField] Vector4 caveRegionBounds;
        CreatureEncounterZone[] zones;
        Transform player;
        MinimapController minimap;
        WorldMapController map;
        QuestManager quests;
        MapMarker grassPortalMarker, cavePortalMarker;
        bool currentCave, hasRegion;
        float nextZoneRefresh;

        public DummyEnemy Boss => boss != null && boss.IsBoss ? boss : null;
        public const float ElderTalkDistance = 3.5f;
        public const string CaveElderName = "Peasant Nolant(Free Version) (1)";
        public const string GrassGuideName = "Peasant Nolant(Free Version) (2)";
        internal Transform GrassElder => grassElder;
        internal Transform CaveElder => caveElder;
        internal Transform TravelGuideFor(Vector3 position) => InCave(position) ? caveElder : grassGuide;

        void Awake() {
            if (grassElder != null && caveElder != null && grassGuide != null) return;
            // Older versions of DemoScene 3 have the actors and arrival points before these two references were serialized.
            foreach (var root in gameObject.scene.GetRootGameObjects()) {
                if (grassElder == null && root.GetComponentInChildren<VillageChiefCutscene>(true) is VillageChiefCutscene intro)
                    grassElder = intro.Chief;
                if (caveElder == null)
                    caveElder = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(actor => actor.name == CaveElderName);
                if (grassGuide == null)
                    grassGuide = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(actor => actor.name == GrassGuideName);
                if (grassElder != null && caveElder != null && grassGuide != null) break;
            }
        }

        void Start() {
            player = FindFirstObjectByType<PetCommandInput>()?.transform;
            quests = FindFirstObjectByType<QuestManager>();
            minimap = FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            map = FindFirstObjectByType<WorldMapController>(FindObjectsInactive.Include);
            grassPortalMarker = AddPortalMarker(grassGuide, "chapter-cave-entrance", "Lối vào hang động");
            cavePortalMarker = AddPortalMarker(caveElder, "chapter-cave-exit", "Lối ra đồng cỏ");
            RefreshMap();
        }

        static MapMarker AddPortalMarker(Transform point, string id, string label) {
            if (point == null) return null;
            var marker = point.GetComponent<MapMarker>() ?? point.gameObject.AddComponent<MapMarker>();
            marker.ConfigureRuntime(MapMarkerType.CaveTravel, id, label, null, Color.white, true, true);
            return marker;
        }

        void Update() {
            if (player == null) return;
            RefreshMap();
            if (Boss != null && quests != null) {
                var finale = quests.GetQuestState("main_quest_09");
                bool enabled = finale != null && finale.Status == QuestStatus.Active && finale.GetObjective("mq09_obj02")?.IsComplete != true;
                if (Boss.gameObject.activeSelf != enabled && (enabled || Boss.IsAlive || Boss.DeathVisualComplete))
                    Boss.gameObject.SetActive(enabled);
            }
        }

        internal bool CanTravelFrom(Transform elder) => elder != null && elder.gameObject.activeInHierarchy &&
            (elder == grassGuide && caveElder != null && caveElder.gameObject.activeInHierarchy && cavePortal != null ||
                elder == caveElder && grassGuide != null && grassGuide.gameObject.activeInHierarchy && grassPortal != null);

        internal bool TryTravelFromElder(Transform elder) {
            if (player == null || !CanTravelFrom(elder) || TravelGuideFor(player.position) != elder) return false;
            var arrival = elder == grassGuide ? cavePortal : grassPortal;
            var destinationElder = elder == grassGuide ? caveElder : grassGuide;
            Vector3 point = arrival.position;
            if (Vector3.Distance(point, destinationElder.position) > ElderTalkDistance &&
                !TryFindElderArrival(destinationElder, destinationElder.position, destinationElder.rotation, out point)) return false;
            Vector3 facing = Vector3.ProjectOnPlane(destinationElder.position - point, Vector3.up);
            var rotation = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : arrival.rotation;
            Teleport(point, rotation);
            return true;
        }

        public static bool TryFindElderArrival(Transform elder, Vector3 origin, Quaternion rotation, out Vector3 point,
            Vector3? pathReference = null) {
            var front = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
            NavMeshPath path = pathReference.HasValue ? new NavMeshPath() : null;
            foreach (float radius in new[] { 2.4f, 1.7f, 3.0f }) {
                foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f }) {
                    var candidate = origin + Quaternion.AngleAxis(angle, Vector3.up) * front * radius;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 1.1f, NavMesh.AllAreas) ||
                        Mathf.Abs(hit.position.y - origin.y) > 1.1f ||
                        Vector3.Distance(hit.position, origin) > ElderTalkDistance) continue;
                    if (Vector3.ProjectOnPlane(hit.position - origin, Vector3.up).sqrMagnitude < 1.2f) continue;
                    var look = origin + Vector3.up * 1.15f - (hit.position + Vector3.up * 1.35f);
                    if (Physics.RaycastAll(hit.position + Vector3.up * 1.35f, look.normalized,
                        look.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        .Any(obstacle => !obstacle.transform.IsChildOf(elder))) continue;
                    if (path != null && (!NavMesh.CalculatePath(hit.position, pathReference.Value, NavMesh.AllAreas, path) ||
                        path.status != NavMeshPathStatus.PathComplete)) continue;
                    point = hit.position;
                    return true;
                }
            }
            point = default;
            return false;
        }

        void Teleport(Vector3 point, Quaternion rotation) {
            Vector3 departure = player.position;
            player.TryGetComponent<PetCommandInput>(out var input);
            if (input != null && input.activePet != null && input.activePet.IsSummoned) input.activePet.Withdraw();
            if (player.TryGetComponent<BasicPlayerMovement>(out var movement))
                movement.WarpTo(point, rotation);
            else {
                player.SetPositionAndRotation(point, rotation);
                Physics.SyncTransforms();
            }
            if (input != null) input.ClearSelectionAfterWarp(player.position - departure, rotation.eulerAngles.y);
            RefreshMap();
            quests?.RefreshChapterTracking();
        }

        bool InCave(Vector3 position) => BoundsRect(caveRegionBounds.z > 0 ? caveRegionBounds : caveBounds)
            .Contains(new Vector2(position.x, position.z));

        static Rect BoundsRect(Vector4 bounds) => new Rect(bounds.x, bounds.y, bounds.z, bounds.w);

        void RefreshMap() {
            if (player == null) return;
            bool cave = InCave(player.position);
            if (hasRegion && cave == currentCave) return;
            hasRegion = true;
            currentCave = cave;
            var rect = BoundsRect(cave ? caveBounds : grassBounds);
            var sprite = cave ? caveMap : grassMap;
            if (sprite != null) {
                string group = cave ? MapEncounterZone.CaveGroup : MapEncounterZone.GrassGroup;
                minimap?.UseMapArt(sprite, rect, cave ? 22 : 38, group);
                map?.UseMapArt(sprite, rect, cave ? "Hang động" : "Đồng cỏ", group);
            }
            grassPortalMarker?.SetVisible(!cave);
            cavePortalMarker?.SetVisible(cave);
        }

        internal bool PointForMap(Vector3 from, Vector3 destination, bool cave, string quest, string objective,
            string label, out QuestTargetInfo target) {
            target = default;
            bool playerInCave = InCave(from);
            if (playerInCave != cave) {
                var elder = TravelGuideFor(from);
                if (!CanTravelFrom(elder)) return false;
                target = new QuestTargetInfo(quest, objective, playerInCave ? "Gặp người dẫn đường để ra hang" :
                    "Gặp người dẫn đường để vào hang", elder.position);
                return true;
            }
            target = new QuestTargetInfo(quest, objective, label, destination);
            return true;
        }

        internal bool TryGetPoint(QuestObjectiveDefinition objective, Vector3 from, out Vector3 position, out bool cave,
            out string zoneLabel) {
            position = default;
            cave = false;
            zoneLabel = null;
            string id = objective.MarkerId;
            Transform point = id switch {
                "cave" => cavePortal,
                "loc_cave_entrance_01" => entryPoint,
                "loc_cave_dusk_01" => duskPoint,
                "loc_cave_boss_01" => Boss != null ? Boss.transform : bossPoint,
                _ => null
            };
            if (point != null) {
                position = point.position;
                cave = true;
                return true;
            }
            if (zones == null || Time.unscaledTime >= nextZoneRefresh) {
                zones = MapEncounterZone.Find(gameObject.scene);
                nextZoneRefresh = Time.unscaledTime + 1f;
            }
            CreatureEncounterZone closest = null;
            float distance = float.PositiveInfinity;
            foreach (var zone in zones) {
                if (!MapEncounterZone.IsActive(zone) ||
                    (id != zone.zoneId && !objective.AllowedZones.Contains(zone.zoneId))) continue;
                float length = (from - zone.zoneBounds.bounds.ClosestPoint(from)).sqrMagnitude;
                if (length >= distance) continue;
                closest = zone;
                distance = length;
            }
            if (closest == null) return false;
            position = closest.zoneBounds.bounds.center;
            cave = InCave(position);
            zoneLabel = MapEncounterZone.DisplayName(closest);
            return true;
        }

        internal bool TryGetHuntingPoint(QuestDefinition definition, Vector3 from, out Vector3 position, out bool cave,
            out string zoneLabel) {
            foreach (var objective in definition.Objectives)
                if (objective.ObjectiveType == QuestObjectiveType.CaptureCreature
                    && TryGetPoint(objective, from, out position, out cave, out zoneLabel)) return true;
            position = default;
            cave = false;
            zoneLabel = null;
            return false;
        }

        internal void ReportLocations(QuestManager manager, QuestRuntimeState active, Vector3 playerPosition) {
            if (!InCave(playerPosition)) return;
            foreach (var objective in active.Definition.Objectives) {
                if (active.GetObjective(objective.ObjectiveId)?.IsComplete == true) continue;
                if (objective.ObjectiveType == QuestObjectiveType.ReachScene || objective.ObjectiveType == QuestObjectiveType.ReachLocation &&
                    TryGetPoint(objective, playerPosition, out var destination, out _, out _) && Vector3.Distance(playerPosition, destination) <= 13f)
                    manager.ReportProgress(new QuestProgressEvent(objective.ObjectiveType, objective.TargetId,
                        questId: active.QuestId, sceneId: gameObject.scene.name));
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Capstone.Game.CreatureSystem;
using Capstone.Game.MapSystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class ChapterWorldLayout : MonoBehaviour {
        static readonly Dictionary<int, (ChapterWorldLayout world, Bounds? rockBounds)> surfaces = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSceneSurfaces() {
            surfaces.Clear();
            SceneManager.sceneUnloaded -= RemoveSceneSurface;
            SceneManager.sceneUnloaded += RemoveSceneSurface;
        }

        static void RemoveSceneSurface(Scene scene) => surfaces.Remove(scene.handle);

        internal static bool IsCaveRegion(Scene scene, Vector3 position) {
            if (!scene.IsValid() || !scene.isLoaded) return false;
            var (layout, caveRockBounds) = ResolveSceneSurface(scene);
            return layout != null ? layout.InCave(position)
                : caveRockBounds?.Contains(position) == true
                    || scene.name.IndexOf("cave", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static (ChapterWorldLayout, Bounds?) ResolveSceneSurface(Scene scene) {
            if (surfaces.TryGetValue(scene.handle, out var existing)) return existing;
            ChapterWorldLayout layout = null;
            Transform cave = null;
            foreach (var root in scene.GetRootGameObjects()) {
                layout ??= root.GetComponentInChildren<ChapterWorldLayout>(true);
                if (layout != null) break;
                if (cave != null) continue;
                foreach (var node in root.GetComponentsInChildren<Transform>(true)) {
                    if (node.name != "MapCave") continue;
                    cave = node;
                    break;
                }
            }
            Bounds? rockBounds = null;
            if (layout == null && cave != null) {
                foreach (var collider in cave.GetComponentsInChildren<Collider>()) {
                    if (!collider.enabled || collider.isTrigger || collider.bounds.size.sqrMagnitude < .01f) continue;
                    var combined = rockBounds ?? collider.bounds;
                    combined.Encapsulate(collider.bounds);
                    rockBounds = combined;
                }
                if (rockBounds is Bounds combinedBounds) {
                    combinedBounds.Expand(new Vector3(2f, 6f, 2f));
                    rockBounds = combinedBounds;
                }
            }
            return surfaces[scene.handle] = (layout, rockBounds);
        }

        [Header("Trưởng làng ở hai khu vực")]
        [SerializeField] Transform grassElder, caveElder;
        [SerializeField] Transform grassGuide;
        [Header("Điểm đến khi dịch chuyển")]
        [SerializeField] Transform grassPortal, cavePortal, entryPoint, duskPoint, bossPoint;
        [SerializeField] Transform bossEntranceWaypoint;
        [SerializeField] DummyEnemy boss;
        [SerializeField] Sprite grassMap, caveMap;
        [SerializeField] Vector4 grassBounds, caveBounds;
        [SerializeField] Vector4 caveRegionBounds;
        CreatureEncounterZone[] zones;
        Transform player;
        BasicPlayerMovement playerMovement;
        BoxCollider caveCatchFloor;
        Vector3? lastSafeCavePosition;
        float nextCaveSafetyCheck;
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
            playerMovement = player != null ? player.GetComponent<BasicPlayerMovement>() : null;
            quests = FindFirstObjectByType<QuestManager>();
            minimap = FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            map = FindFirstObjectByType<WorldMapController>(FindObjectsInactive.Include);
            grassPortalMarker = AddPortalMarker(grassGuide, "chapter-cave-entrance", "Lối vào hang động");
            cavePortalMarker = AddPortalMarker(caveElder, "chapter-cave-exit", "Lối ra đồng cỏ");
            if (gameObject.scene.name == ChapterQuestFlow.GameplayScene) InstallCaveSafety();
            RefreshMap();
        }

        void InstallCaveSafety() {
            var guards = new GameObject("Map Cave - Collider an toàn").transform;
            guards.SetParent(transform, false);
            AddPortalBackWall(guards, grassGuide, grassPortal, 12f, "Vách sau NPC vào hang");
            AddPortalBackWall(guards, caveElder, cavePortal, 10f, "Vách sau NPC trong hang");

            var cave = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(node => node.name == "MapCave");
            if (cave == null) return;

            Bounds rockBounds = default;
            bool foundRock = false;
            foreach (var rock in cave.GetComponentsInChildren<Collider>()) {
                if (!rock.enabled || rock.isTrigger || rock.bounds.size.sqrMagnitude < .001f) continue;
                if (foundRock) rockBounds.Encapsulate(rock.bounds);
                else rockBounds = rock.bounds;
                foundRock = true;
            }
            if (!foundRock) return;

            var region = BoundsRect(caveRegionBounds.z > 0 ? caveRegionBounds : caveBounds);
            const float margin = 8f;
            Vector3 min = new(Mathf.Min(rockBounds.min.x, region.xMin) - margin, rockBounds.min.y - 3f,
                Mathf.Min(rockBounds.min.z, region.yMin) - margin);
            Vector3 max = new(Mathf.Max(rockBounds.max.x, region.xMax) + margin, rockBounds.min.y - 2f,
                Mathf.Max(rockBounds.max.z, region.yMax) + margin);
            caveCatchFloor = AddSafetyBox(guards, "Sàn chặn rơi MapCave", (min + max) * .5f, Quaternion.identity, max - min);
        }

        static void AddPortalBackWall(Transform parent, Transform npc, Transform arrival, float width, string label) {
            if (npc == null || arrival == null) return;
            var back = Vector3.ProjectOnPlane(npc.position - arrival.position, Vector3.up);
            if (back.sqrMagnitude < .01f) back = -npc.forward;
            back = Vector3.ProjectOnPlane(back, Vector3.up).normalized;
            var center = npc.position + back * 3.7f + Vector3.up * 3f;
            AddSafetyBox(parent, label, center, Quaternion.LookRotation(back), new Vector3(width, 8f, 1f));
        }

        static BoxCollider AddSafetyBox(Transform parent, string label, Vector3 center, Quaternion rotation, Vector3 size) {
            var guard = new GameObject(label);
            guard.transform.SetPositionAndRotation(center, rotation);
            guard.transform.SetParent(parent, true);
            var box = guard.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        static MapMarker AddPortalMarker(Transform point, string id, string label) {
            if (point == null) return null;
            var marker = point.GetComponent<MapMarker>() ?? point.gameObject.AddComponent<MapMarker>();
            marker.ConfigureRuntime(MapMarkerType.CaveTravel, id, label, null, Color.white, true, true);
            return marker;
        }

        void Update() {
            if (player == null) return;
            CheckCaveFall();
            RefreshMap();
            if (Boss != null && quests != null) {
                var finale = quests.GetQuestState("main_quest_09");
                bool enabled = finale != null && finale.Status == QuestStatus.Active && finale.GetObjective("mq09_obj02")?.IsComplete != true;
                if (Boss.gameObject.activeSelf != enabled && (enabled || Boss.IsAlive || Boss.DeathVisualComplete))
                    Boss.gameObject.SetActive(enabled);
            }
        }

        void CheckCaveFall() {
            if (playerMovement == null || caveCatchFloor == null || Time.time < nextCaveSafetyCheck) return;
            nextCaveSafetyCheck = Time.time + .12f;
            Vector3 position = player.position;
            if (!InCave(position)) {
                if (lastSafeCavePosition is Vector3 edge) {
                    if (Vector3.ProjectOnPlane(position - edge, Vector3.up).sqrMagnitude > 18f * 18f)
                        lastSafeCavePosition = null;
                    else if (position.y < edge.y - 4.5f) Teleport(edge, player.rotation);
                }
                return;
            }

            if (position.y < caveCatchFloor.bounds.min.y - 1f && cavePortal != null) {
                Teleport(lastSafeCavePosition ?? cavePortal.position, player.rotation);
                return;
            }
            bool foundGround = Physics.Raycast(position + Vector3.up * .15f, Vector3.down, out var ground, 12f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (foundGround && ground.collider == caveCatchFloor && ground.distance < .85f &&
                lastSafeCavePosition == null && cavePortal != null) {
                Teleport(cavePortal.position, cavePortal.rotation);
                return;
            }
            bool foundLanding = foundGround && ground.collider != caveCatchFloor &&
                NavMesh.SamplePosition(ground.point, out var floor, .65f, NavMesh.AllAreas) &&
                Mathf.Abs(ground.point.y - floor.position.y) < .5f;
            if (playerMovement.IsGroundedForPetThrow && foundLanding && ground.distance < .8f &&
                NavMesh.FindClosestEdge(ground.point, out var boundary, NavMesh.AllAreas) && boundary.distance >= .4f) {
                lastSafeCavePosition = position;
                return;
            }

            if (foundLanding || lastSafeCavePosition is not Vector3 safe || position.y > safe.y - 4.5f) return;
            Teleport(safe, player.rotation);
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
            lastSafeCavePosition = null;
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

        internal bool InCave(Vector3 position) => BoundsRect(caveRegionBounds.z > 0 ? caveRegionBounds : caveBounds)
            .Contains(new Vector2(position.x, position.z));

        static Rect BoundsRect(Vector4 bounds) => new Rect(bounds.x, bounds.y, bounds.z, bounds.w);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DummyEnemy developerBoss;
        internal Transform DeveloperBossEntrance => bossEntranceWaypoint;

        internal Transform DeveloperWaypoint(int index) => index switch {
            0 => grassElder,
            4 => cavePortal,
            5 => duskPoint,
            7 => DeveloperBossEntrance != null ? DeveloperBossEntrance : bossPoint,
            _ => null
        };

        internal void DeveloperTeleport(Vector3 position, Quaternion orientation) => Teleport(position, orientation);

        internal bool TryBindDeveloperBoss(out string error) {
            error = string.Empty;
            if (Boss != null) return true;
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Trung/Detale.prefab");
            if (bossPoint != null && prefab != null && prefab.TryGetComponent<DummyEnemy>(out var template) && template.IsBoss &&
                NavMesh.SamplePosition(bossPoint.position, out var floor, 6f, NavMesh.AllAreas)) {
                var actor = Instantiate(prefab, floor.position, bossPoint.rotation, transform);
                developerBoss = boss = actor.GetComponent<DummyEnemy>();
                actor.SetActive(false);
                return true;
            }
#endif
            error = "Chưa có Boss hợp lệ tại điểm đấu trường. Kiểm tra prefab Detale, mốc Boss và NavMesh của hang.";
            return false;
        }

        internal void ClearDeveloperBossFixture() {
            if (developerBoss == null) return;
            if (boss == developerBoss) boss = null;
            developerBoss.gameObject.SetActive(false);
            Destroy(developerBoss.gameObject);
            developerBoss = null;
        }
#endif

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

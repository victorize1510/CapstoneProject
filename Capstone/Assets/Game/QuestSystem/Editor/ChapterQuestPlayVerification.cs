using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.ProfileSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Capstone.Game.QuestSystem.Editor {
    public static class ChapterQuestPlayVerification {
        const string SessionKey = "Capstone.ChapterI.IsolatedVerification";
        static string Folder => Path.Combine(QaArtifactPaths.Root, "Chapter01");

        [MenuItem("Tools/Capstone/Chapter I/Verify Transactions in Isolated Play Mode")]
        public static void Start() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
                string.IsNullOrEmpty(scene.path) || SceneManager.sceneCount != 1 ||
                PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Mở một scene đã lưu, ở Edit Mode, trước khi chạy.");
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "play-checks.txt"), "ENTERING ISOLATED PLAY");
            SessionState.SetString(SessionKey, scene.path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Register() => EditorApplication.playModeStateChanged += StateChanged;

        static void StateChanged(PlayModeStateChange change) {
            string scene = SessionState.GetString(SessionKey, "");
            if (scene.Length == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode) Run();
            if (change == PlayModeStateChange.EnteredEditMode) {
                SessionState.EraseString(SessionKey);
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            }
        }

        static async void Run() {
            var lines = new List<string>();
            void Check(bool ok, string description) {
                lines.Add((ok ? "PASS " : "FAIL ") + description);
                if (!ok) throw new InvalidOperationException(description);
            }
            string testSlot = "chapter-test-" + Guid.NewGuid().ToString("N");
            var store = new PlayerJsonFileSaveStore(Folder);
            try {
                Check(UnityEngine.Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                    "Isolated scene has no live save controller");
                var fixture = new GameObject("Isolated Chapter I");
                var manager = fixture.AddComponent<QuestManager>();
                var player = fixture.AddComponent<PetCommandInput>();
                var roster = fixture.AddComponent<PetBoxRuntimeProvider>();
                roster.Bind(player);
                var bag = fixture.AddComponent<MonsterInventoryAdapter>();
                var wallet = fixture.AddComponent<PlayerCurrencyWallet>();
                var profile = fixture.AddComponent<PlayerProfileRuntimeProvider>();
                var saves = fixture.AddComponent<PlayerSaveController>();
                saves.enabled = false;
                saves.SetSaveStore(store);
                Set(saves, "slotId", testSlot);
                Set(saves, "loadOnStart", false);
                Set(saves, "saveOnApplicationQuit", false);
                Set(saves, "saveOnApplicationPause", false);
                var rewards = fixture.GetComponent<QuestRewardService>() ?? fixture.AddComponent<QuestRewardService>();
                rewards.Bind(manager, bag, wallet, profile, saves);
                var flowType = typeof(QuestManager).Assembly.GetType("Capstone.Game.QuestSystem.ChapterQuestFlow", true);
                var flow = fixture.AddComponent(flowType);
                await Task.Delay(170);
                Check(saves.SavePath.StartsWith(Folder, StringComparison.OrdinalIgnoreCase), "Save writes target the drive E QA folder");
                var first = manager.GetQuestDefinition("main_quest_01");
                Check(!(bool)Invoke(flow, "TryAccept", first), "NV01 cannot be accepted before tutorial");
                manager.MarkElderIntroductionComplete();
                Check(!(bool)Invoke(flow, "TryAccept", first), "The elder introduction alone does not unlock NV01");
                manager.MarkStarterChosen();
                Check((bool)Invoke(flow, "TryAccept", first), "Accepting NV01 commits its starter items and progress");
                Check((string)Invoke(flow, "QuestOption") == "Xem nhiệm vụ", "An unfinished quest opens the progress dialogue");
                var balls = InventoryDefinitionCatalog.Find("capture-ball-basic-01");
                Check(bag.GetQuantity(balls) == 15 && !(bool)Invoke(flow, "TryAccept", first) && saves.LoadNow() &&
                    bag.GetQuantity(balls) == 15 && manager.GetQuestState(first.QuestId).IsTracked, "15 balls once, with tracking persisted across load");

                VerifyElderTravel(Check, player, manager);
                VerifyPetHandover(Check, manager, flow, player, roster, bag, wallet, saves);
                await Task.Delay(70);
                var chapterSnapshot = manager.CreateSaveData();
                chapterSnapshot.questStates.Add(new QuestRuntimeSaveData { questId = "main_quest_02", status = QuestStatus.Completed, rewardsClaimed = true });
                manager.RestoreFromSaveData(chapterSnapshot);
                var wolf = InventoryDefinitionCatalog.Find("loot-wolf-01");
                var cat = InventoryDefinitionCatalog.Find("loot-cat-electric-01");
                bag.AddItem(wolf, 7);
                bag.AddItem(cat, 6);
                var third = manager.GetQuestDefinition("main_quest_03");
                Check((bool)Invoke(flow, "TryAccept", third), "NV03 can be accepted after NV02 and uses materials already in the bag");
                for (int i = 0; i < 5; i++) {
                    manager.ReportProgress(new QuestProgressEvent(QuestObjectiveType.DefeatCreature, targetTypeId: "wolf",
                        formId: "01", regionId: "capstone-grass-mid", receiptId: "wolf-" + i, startedAtNight: true));
                    manager.ReportProgress(new QuestProgressEvent(QuestObjectiveType.DefeatCreature, targetTypeId: "cat-electric",
                        formId: "01", regionId: "capstone-grass-hard", receiptId: "cat-" + i));
                }
                var state = manager.GetQuestState(third.QuestId);
                Check((bool)Invoke(flow, "IsReady", state), "Five night wolves, five cats and both materials make NV03 ready");
                bag.RemoveItem(cat, 2);
                Check(!(bool)Invoke(flow, "IsReady", state), "Selling an owned material makes the objective incomplete");
                bag.AddItem(cat, 2);
                Set(saves, "saveBlockedReason", "QA intentional failure");
                object[] failed = { state, Array.Empty<PetController>(), "" };
                Check(!(bool)Invoke(flow, "TryTurnIn", failed) && bag.GetQuantity(wolf) == 7 && bag.GetQuantity(cat) == 6 &&
                    wallet.Gold == 150 && manager.GetQuestState(third.QuestId).Status == QuestStatus.Active,
                    "Failed save rolls back turn-in, gold and material removal");
                Set(saves, "saveBlockedReason", "");
                state = manager.GetQuestState(third.QuestId);
                object[] success = { state, Array.Empty<PetController>(), "" };
                Check((bool)Invoke(flow, "TryTurnIn", success), "NV03 turn-in saves all rewards and deductions together");
                Check(wallet.Gold == 500 && bag.GetQuantity(wolf) == 2 && bag.GetQuantity(cat) == 1 && state.RewardsClaimed &&
                    bag.GetQuantity(InventoryDefinitionCatalog.Find("hp-potion-small")) == 3, "Only required materials deducted; exact rewards claimed");
                Check(saves.LoadNow() && wallet.Gold == 500 && !(bool)Invoke(flow, "TryTurnIn", success), "Reload preserves the claim and cannot replay the reward");

                VerifyMapArt(Check);
                VerifyCapturePicker(fixture, Check);
                lines.Insert(0, "COMPLETE isolated Chapter I Play Mode");
            } catch (Exception error) {
                lines.Insert(0, "FAILED isolated Chapter I Play Mode");
                lines.Add(error.ToString());
                Debug.LogException(error);
            } finally {
                store.TryDelete(testSlot, out _);
                File.WriteAllLines(Path.Combine(Folder, "play-checks.txt"), lines);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        static void VerifyElderTravel(Action<bool, string> check, PetCommandInput player, QuestManager manager) {
            var root = new GameObject("QA two elder locations");
            var layout = root.AddComponent<ChapterWorldLayout>();
            Transform Point(string name, Vector3 position) {
                var point = new GameObject(name).transform;
                point.SetParent(root.transform);
                point.position = position;
                return point;
            }
            var grass = Point("Grass chief", new Vector3(0, 0, 3));
            var cave = Point("Cave chief", new Vector3(100, -44, -97));
            var grassArrival = Point("Grass arrival", Vector3.zero);
            var caveArrival = Point("Cave arrival", new Vector3(100, -44, -100));
            Set(layout, "grassGuide", grass);
            Set(layout, "grassElder", Point("Quest chief", new Vector3(8, 0, 0)));
            Set(layout, "caveElder", cave);
            Set(layout, "grassPortal", grassArrival);
            Set(layout, "cavePortal", caveArrival);
            Set(layout, "caveRegionBounds", new Vector4(90, -110, 20, 20));
            Set(layout, "player", player.transform);
            Set(layout, "quests", manager);
            var tracked = manager.GetTrackedQuest();
            check(!(bool)Invoke(layout, "CanTravelFrom", root.transform.Find("Quest chief")), "The quest chief cannot teleport the player");
            check(!(bool)Invoke(layout, "TryTravelFromElder", cave), "The remote elder cannot trigger a trip from the grass region");
            check((bool)Invoke(layout, "TryTravelFromElder", grass) && player.transform.position == caveArrival.position
                && manager.GetTrackedQuest() == tracked, "Nolant at the grass entrance sends the player to Nolant with the same tracked quest");
            check((bool)Invoke(layout, "TryTravelFromElder", cave) && player.transform.position == grassArrival.position
                && manager.GetTrackedQuest() == tracked, "Nolant sends the player back to the entrance guide and quest state");
            UnityEngine.Object.Destroy(root);
        }

        static void VerifyPetHandover(Action<bool, string> check, QuestManager manager, Component flow,
            PetCommandInput player, PetBoxRuntimeProvider roster, MonsterInventoryAdapter bag, PlayerCurrencyWallet wallet, PlayerSaveController saves) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Chicks/OwnedPet/OwnedPet_chick_01.prefab");
            PetController Chick(string id) {
                var pet = UnityEngine.Object.Instantiate(prefab).GetComponent<PetController>();
                pet.AssignOwner(player.transform);
                pet.GetComponent<PetCollectionMetadata>().AssignPersistentId(id);
                pet.Withdraw();
                return pet;
            }
            var starter = Chick("qa-starter-not-captured");
            player.petSlots[0] = starter;
            var captures = Enumerable.Range(0, 5).Select(i => Chick("qa-chick-" + i)).ToArray();
            player.petSlots[1] = captures[0];
            player.petSlots[2] = captures[1];
            for (int i = 2; i < captures.Length; i++)
                check(roster.TryStoreCapturedPet(captures[i], out _), "Captured pet placed in Box fixture " + i);
            player.SetActivePet(starter);
            for (int i = 0; i < 5; i++) {
                manager.ReportProgress(new QuestProgressEvent(QuestObjectiveType.DefeatCreature, targetTypeId: "chick",
                    formId: "01", regionId: "capstone-grass-easy", receiptId: "qa-defeat-chick-" + i));
                manager.ReportProgress(new QuestProgressEvent(QuestObjectiveType.CaptureCreature, targetTypeId: "chick",
                    formId: "01", regionId: "capstone-grass-easy", receiptId: "qa-chick-" + i));
            }
            var quest = manager.GetQuestState("main_quest_01");
            check((string)Invoke(flow, "QuestOption") == "Trả nhiệm vụ" &&
                ((string)Invoke(flow, "PetLabel", captures[0])).EndsWith("Đội ô 2", StringComparison.Ordinal) &&
                ((string)Invoke(flow, "PetLabel", captures[2])).EndsWith("Kho ô 1", StringComparison.Ordinal),
                "NV01 offers turn-in and identifies each delivery pet by party or box slot");
            var invalid = captures.ToArray();
            invalid[0] = starter;
            check((bool)Invoke(flow, "IsReady", quest) &&
                !(bool)Invoke(flow, "TryTurnIn", new object[] { quest, invalid, "" }) &&
                roster.Contains(starter) && captures.All(roster.Contains), "A starter with the same family and form cannot be delivered");
            player.petSlots[0] = null;
            check(!(bool)Invoke(flow, "TryTurnIn", new object[] { quest, captures, "" }) &&
                captures.All(roster.Contains), "Handover cannot leave the party without a battle-capable pet");
            player.petSlots[0] = starter;
            Set(saves, "saveBlockedReason", "QA intentional pet handover failure");
            check(!(bool)Invoke(flow, "TryTurnIn", new object[] { quest, captures, "" }) &&
                captures.All(roster.Contains) && wallet.Gold == 0, "Failed save restores all staged Party and Box pets");
            Set(saves, "saveBlockedReason", "");
            quest = manager.GetQuestState("main_quest_01");
            check((bool)Invoke(flow, "TryTurnIn", new object[] { quest, captures, "" }) &&
                captures.All(pet => !roster.Contains(pet)) && roster.PartyCount == 1 && roster.Contains(starter) &&
                wallet.Gold == 150 && bag.GetQuantity(InventoryDefinitionCatalog.Find("capture-ball-basic-01")) == 20,
                "Five selected individuals from Party and Box leave the roster exactly once; the starter and rewards remain");
            check((Invoke(flow, "Available") as QuestDefinition)?.QuestId == "main_quest_02" &&
                (string)Invoke(flow, "QuestOption") == "Nhận nhiệm vụ",
                "Returning NV01 immediately makes NV02 available for the next dialogue offer");
        }

        static void VerifyMapArt(Action<bool, string> check) {
            var camera = new GameObject("QA projection camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographicSize = 5;
            camera.aspect = 1;
            camera.transform.position = new Vector3(110, 70, -25);
            var image = new GameObject("QA map texture").AddComponent<RawImage>();
            var projection = typeof(QuestManager).Assembly.GetType("Capstone.Game.MapSystem.MapArtProjection", true);
            var apply = projection.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var name in new[] { "Grassland_Fantasy", "Cave_Fantasy" }) {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Game/MapSystem/Sprites/Chapter01/{name}.png");
                apply.Invoke(null, new object[] { image, camera, sprite, new Rect(100, -35, 20, 20) });
                check(sprite != null && image.texture == sprite.texture && image.uvRect == new Rect(.25f, .25f, .5f, .5f),
                    name + " uses the sprite and maps the projection center to the correct texture coordinates");
            }
            var target = new GameObject("QA minimap target").transform;
            target.position = new Vector3(111, 0, -25);
            var minimap = new GameObject("QA minimap controller").AddComponent<MinimapController>();
            minimap.SetReferences(null, camera, target);
            minimap.UseMapArt(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/MapSystem/Sprites/Chapter01/Cave_Fantasy.png"),
                new Rect(100, -35, 20, 20), 5);
            Set(minimap, "artDisplay", image);
            Set(minimap, "followSharpness", 0f);
            Invoke(minimap, "LateUpdate");
            check(Mathf.Approximately(image.uvRect.x, .30f) && Mathf.Approximately(image.uvRect.y, .25f),
                "Minimap refreshes its sprite crop when the player moves");

            var manager = minimap.gameObject.AddComponent<MapMarkerManager>();
            Set(minimap, "markerManager", manager);
            Set(manager, "minimap", minimap);
            Set(manager, "bossIcon", Texture2D.whiteTexture);
            MapMarker PlaceMarker(MapMarkerType type) {
                var marker = new GameObject("QA " + type).AddComponent<MapMarker>();
                marker.transform.position = new Vector3(111, 0, -25);
                marker.ConfigureRuntime(type, type.ToString(), type.ToString(), null, Color.white, true, true);
                return marker;
            }
            var boss = PlaceMarker(MapMarkerType.Boss);
            var enemy = PlaceMarker(MapMarkerType.Enemy);
            var pet = PlaceMarker(MapMarkerType.Pet);
            var visibleMarkers = new List<MapMarker>();
            manager.CopyMarkersTo(visibleMarkers);
            check(visibleMarkers.Contains(boss) && !visibleMarkers.Contains(enemy) && !visibleMarkers.Contains(pet),
                "A boss is registered and ordinary creatures are excluded from map icons");
            var bossRenderer = manager.transform.Find("QA Boss Map Icon/Visuals").GetComponent<Renderer>();
            var playerCamera = new GameObject("QA gameplay camera").AddComponent<Camera>();
            playerCamera.enabled = false;
            playerCamera.transform.rotation = Quaternion.Euler(-85f, 0f, 0f);
            Invoke(manager, "BeginMapCamera", default(ScriptableRenderContext), playerCamera);
            check(bossRenderer.forceRenderingOff, "The gameplay camera looking up cannot render a map icon");
            Invoke(manager, "EndMapCamera", default(ScriptableRenderContext), playerCamera);
            Set(minimap, "mapArt", null);
            Invoke(manager, "BeginMapCamera", default(ScriptableRenderContext), camera);
            bool renderedByMap = !bossRenderer.forceRenderingOff;
            Invoke(manager, "EndMapCamera", default(ScriptableRenderContext), camera);
            check(renderedByMap && bossRenderer.forceRenderingOff, "Legacy icons are enabled only during the minimap render");

            image.rectTransform.sizeDelta = new Vector2(240, 240);
            minimap.UseMapArt(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/MapSystem/Sprites/Chapter01/Cave_Fantasy.png"),
                new Rect(100, -35, 20, 20), 5, "ZoneCave");
            var zoneType = typeof(WorldMapController).Assembly.GetType("Capstone.Game.MapSystem.MapEncounterZone", true);
            var zones = Array.CreateInstance(zoneType, 4);
            void AddZone(int index, Vector3 point, string name, string group) =>
                zones.SetValue(Activator.CreateInstance(zoneType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { new Bounds(point, new Vector3(4, 8, 4)), name, group }, null), index);
            AddZone(0, new Vector3(111, -49, -25), "Vùng 4", "ZoneCave");
            AddZone(1, new Vector3(118, 100, -25), "Vùng 5", "ZoneCave");
            AddZone(2, new Vector3(110, 0, -26), "Vùng 6", "ZoneCave");
            AddZone(3, new Vector3(110, 0, -26), "Vùng 1", "ZoneGrass");
            Invoke(minimap, "SetEncounterZones", zones);
            boss.transform.position += Vector3.up * 500;
            Invoke(minimap, "LateUpdate");
            var icon = image.transform.Find("Map Art Icons").GetComponentsInChildren<RawImage>().Single();
            check(icon.rectTransform.anchorMin == new Vector2(.5f, .5f),
                "The sprite minimap preserves the boss icon while ignoring altitude");
            var projectedOutside = (Vector2)projection.GetMethod("ViewportPoint", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { camera, new Vector3(118, 500, -25) });
            check(projectedOutside.x > 1, "An off-map icon stays outside the world map viewport");
            var overlay = image.transform.Find("Encounter Zones");
            using var mesh = new VertexHelper();
            var graphic = overlay.GetComponent<Graphic>();
            graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Invoke(graphic, new object[] { mesh });
            check(overlay.Cast<Transform>().Select(label => label.GetComponent<Text>().text)
                .SequenceEqual(new[] { "Vùng 4", "Vùng 5", "Vùng 6" }) && !overlay.Find("Vùng 5").gameObject.activeSelf
                && mesh.currentVertCount > 0, "Encounter overlays use authored numbers for the current region and clip to the visible map");
        }

        static void VerifyCapturePicker(GameObject fixture, Action<bool, string> check) {
            var host = new GameObject("QA capture picker", typeof(RectTransform), typeof(Canvas));
            var hud = fixture.AddComponent<HubCaptureHud>();
            hud.Build(host.GetComponent<RectTransform>());
            hud.OpenPicker();
            var picker = host.transform.Find("CaptureBallPicker");
            var cards = Enumerable.Range(0, 3)
                .Select(index => picker?.Find("BallChoice" + index) as RectTransform).ToArray();
            check(hud.IsPickerOpen && cards.All(card => card != null) &&
                Mathf.Approximately(cards[0].anchoredPosition.y, cards[1].anchoredPosition.y) &&
                Mathf.Approximately(cards[1].anchoredPosition.y, cards[2].anchoredPosition.y) &&
                Mathf.Approximately(cards[1].anchoredPosition.x - cards[0].anchoredPosition.x,
                    cards[2].anchoredPosition.x - cards[1].anchoredPosition.x),
                "All three ball cards are level and equally spaced in the actual HUD");
            hud.ClosePicker();
        }

        static object Invoke(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        static void Set(Component target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}

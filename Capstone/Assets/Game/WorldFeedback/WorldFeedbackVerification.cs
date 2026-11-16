#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.PetSummon;
using Capstone.Game.QuestSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.SaveSystem;
using Capstone.Game.ShopSystem;
using Capstone.Game.UISystem;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Capstone.Game.WorldFeedback {
    /// <summary>Opt-in, bounded engine checks. All fixtures and saves are external or Play-only.</summary>
    public static class WorldFeedbackVerification {
        const string Qa = QaArtifactPaths.Root + "pve-model-camera-feedback-2026-11-10/";
        const string Session = "Capstone.WorldFeedback.Qa";
        const string AssetsRoot = "Assets/Game/WorldFeedback/";
        const string CommonBallPath = "Assets/Game/PetSummon/Prefabs/BallGreenClose.prefab";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static readonly List<string> checks = new List<string>();
        static bool running;
        static double nextPoll;
        [InitializeOnLoadMethod] static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        static void Poll() {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(Qa + "qa.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Qa + "qa.request"); Begin();
        }
        static void Write(string file, string value) {
            Directory.CreateDirectory(Qa); File.WriteAllText(Qa + file, value);
            File.AppendAllText(Qa + "qa-created-paths.txt", Qa + file + "\n");
        }
        static void Check(bool pass, string message) {
            checks.Add((pass ? "PASS " : "FAIL ") + message); Write("engine-checks.txt", string.Join("\n", checks));
            if (!pass) throw new InvalidOperationException(message);
        }
        [MenuItem("Tools/Capstone/Advanced/World Feedback/2. Verify isolated runtime")]
        public static void Begin() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            checks.Clear();
            try {
                var common = AssetDatabase.LoadAssetAtPath<GameObject>(CommonBallPath);
                Check(common != null && common.GetComponent<Rigidbody>() != null && common.GetComponent<SphereCollider>() != null,
                    "existing ball GUID/physics wrapper retained");
                Check(AssetDatabase.AssetPathToGUID(CommonBallPath) == "3af14d9d9cd3a5349b28963a15ffa405", "ball prefab GUID unchanged");
                foreach (string id in new[] { "BallCommon", "BallRare", "BallUltra", "RecoveryTable" }) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(id == "BallCommon" ? CommonBallPath : AssetsRoot + "Prefabs/" + id + ".prefab");
                    Check(prefab != null && prefab.GetComponent<LODGroup>()?.GetLODs().Length == 3, id + " three LODs imported");
                    var visual = prefab.transform.Find("Visual/LOD0");
                    int triangles = visual.GetComponentsInChildren<MeshFilter>(true).Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
                    Check(triangles <= (id == "RecoveryTable" ? 40500 : 8100), id + " runtime triangle budget " + triangles);
                }
                var ball = CaptureAssets.LoadBall();
                Check(ball != null && ball.Icon != null && ball.CaptureRules.Multiplier == 1, "basic ball icon changed without capture-rule change");
                var shader = Resources.Load<Shader>("WorldFeedback/CameraDitherLit");
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), "dither shader imports without reported errors");
                SessionState.SetBool(Session, true);
                SessionState.SetString(Session + ".save", Qa + "test-saves/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                SessionState.SetBool(Session + ".reload", EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetInt(Session + ".options", (int)EditorSettings.enterPlayModeOptions);
                EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
                Write("qa-status.txt", "ENTERING_PLAY isolated saves; original scene is not saved by verification");
                EditorApplication.EnterPlaymode();
            } catch (Exception error) { Write("qa-status.txt", "FAILED before Play\n" + error); Debug.LogException(error); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
            if (!SessionState.GetBool(Session, false) || running) return;
            foreach (var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                save.SetSaveStore(new PlayerJsonFileSaveStore(SessionState.GetString(Session + ".save", "")));
            foreach (var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                save.SetSaveStore(new QuestJsonFileSaveStore(SessionState.GetString(Session + ".save", "")));
            Run();
        }
        static async Task Gap(int milliseconds = 100) { await Task.Delay(milliseconds); if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play Mode ended"); }
        static async void Run() {
            running = true; GameObject fixtures = null; EditorWindow view = null; PropertyInfo sizeProperty = null;
            int oldSize = -1; var requests = new List<string>();
            try {
                await Gap(650);
                Write("qa-status.txt", "RUNNING engine checks");
                var save = Object.FindFirstObjectByType<PlayerSaveController>();
                Check(save != null && save.SavePath.StartsWith(Qa, StringComparison.OrdinalIgnoreCase), "real save isolated on E");
                var party = Object.FindFirstObjectByType<PetCommandInput>();
                var camera = Camera.main;
                Check(party != null && camera != null, "DemoScene player/camera available");
                fixtures = new GameObject("WorldFeedback QA Play-only"); fixtures.hideFlags = HideFlags.DontSave;
                var cameraFollow = Object.FindFirstObjectByType<BasicCameraFollow>();
                Check(cameraFollow != null && cameraFollow.GetComponent<CameraVisibility>() != null, "camera visibility installed on existing rig");
                var table = GameObject.Find("PetRecoveryTable_VisualOnly");
                Check(table != null && table.GetComponentInChildren<MeshCollider>() != null && table.GetComponent<NavMeshObstacle>() != null,
                    "recovery table placed with simplified collision/navigation footprint");
                TestCamera(cameraFollow, fixtures.transform);
                var inventory = Object.FindFirstObjectByType<MonsterInventoryAdapter>();
                var wallet = Object.FindFirstObjectByType<PlayerCurrencyWallet>();
                Check(inventory != null && wallet != null, "runtime inventory/wallet bound");
                var toast = AcquisitionToastView.Ensure();
                var sourceObject = new GameObject("QA merchant"); sourceObject.transform.SetParent(fixtures.transform);
                var source = sourceObject.AddComponent<ShopSource>();
                var offers = (List<ShopOffer>)typeof(ShopSource).GetField("offers", Any).GetValue(source);
                offers.Clear();
                var potion = AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>("Assets/Game/Inventory/Potion.asset");
                offers.Add(new ShopOffer { Id = "qa-potion", Item = potion, GoldPrice = 2, Stock = 10, MaxPerPurchase = 5, Unlocked = true });
                offers.Add(new ShopOffer { Id = "qa-ball", Item = CaptureAssets.LoadBall(), GoldPrice = 0, Stock = 3, MaxPerPurchase = 3, Unlocked = true });
                wallet.AddGold(100);
                double clock = 0;
                var service = new ShopPurchaseService(inventory, wallet, () => clock);
                int receipts = 0; Action<ItemAcquisitionReceipt> observer = _ => receipts++;
                ItemAcquisitionFeedback.Received += observer;
                int owned = service.Owned(offers[0]), gold = wallet.Gold;
                Check(service.TryBuy(source, "qa-potion", 2, 2, out _), "real purchase service commits item delivery");
                Check(service.Owned(offers[0]) == owned + 2 && wallet.Gold == gold - 4 && receipts == 1, "purchase receipt follows committed quantities");
                clock += 1;
                Check(!service.TryBuy(source, "qa-potion", 1, 9, out _) && receipts == 1, "price-change rejection emits no receipt");
                clock += 1; offers[0].Stock = 0;
                Check(!service.TryBuy(source, "qa-potion", 1, 2, out _) && receipts == 1, "sold-out rejection emits no receipt");
                clock += 1; offers[0].Stock = 8; offers[0].Unlocked = false;
                Check(!service.TryBuy(source, "qa-potion", 1, 2, out _) && receipts == 1, "locked offer emits no receipt");
                clock += 1; offers[0].Unlocked = true;
                Check(service.TryBuy(source, "qa-ball", 1, 0, out _) && receipts == 2, "free valid offer commits one receipt");
                string duplicateId = "qa-dedup";
                Check(ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(duplicateId, potion, 1, AcquisitionSource.Pickup))
                    && !ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(duplicateId, potion, 1, AcquisitionSource.Pickup)), "duplicate receipt suppressed");
                ItemAcquisitionFeedback.Received -= observer;
                await Gap(200);
                Check(toast.VisibleCount > 0 && toast.VisibleCount <= 3 && toast.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null, "toast rows pooled/visible/non-interactive");
                TestQuestReceipts(save, inventory, potion);
                var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab");
                var actor = new List<DummyEnemy>();
                for (int i = 0; i < 3; i++) {
                    var enemyObject = Object.Instantiate(enemyPrefab, fixtures.transform); enemyObject.name = "QA moving bat " + i;
                    Vector3 point = party.transform.position + party.transform.forward * (5 + i) + party.transform.right * (i - 1) * 2;
                    if (UnityEngine.AI.NavMesh.SamplePosition(point, out var ground, 3, UnityEngine.AI.NavMesh.AllAreas)) point = ground.position;
                    enemyObject.transform.position = point;
                    var enemy = enemyObject.GetComponent<DummyEnemy>(); enemy.autoRevive = false;
                    actor.Add(enemy);
                }
                await Gap(500);
                var healthBars = Object.FindFirstObjectByType<EnemyHealthBars>();
                Check(healthBars != null && healthBars.PooledCount == 32, "one fixed pool of 32 enemy bars");
                var first = actor[0]; float hp = first.CurrentHealth;
                first.ApplyCombatHit(new CombatHit(party.gameObject, "qa-health", 1, 1, CreatureDamageKind.Physical, false, false, first.TargetPosition));
                Check(first.CurrentHealth < hp, "health notification uses real damage path");
                await Gap(250);
                Check(first.GetComponent<EnemyHealthAnchor>() != null, "visual top anchor registered on real enemy");
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                view = EditorWindow.GetWindow(type); sizeProperty = type.GetProperty("selectedSizeIndex", Any); oldSize = (int)sizeProperty.GetValue(view);
                var selectType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t => t != null);
                var selectSize = selectType.GetMethod("SelectSize", Any);
                foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) }) {
                    selectSize.Invoke(null, new object[] { view, sizeProperty, resolution }); await Gap(450);
                    Check(Screen.width == resolution.x && Screen.height == resolution.y, "actual Game view " + resolution);
                    var visibility = cameraFollow.GetComponent<CameraVisibility>();
                    healthBars.enabled = false; visibility.enabled = false; if (table != null) table.SetActive(false);
                    await Gap(200); await Capture("before-" + resolution.x + ".png");
                    if (table != null) table.SetActive(true); healthBars.enabled = true; visibility.enabled = true;
                    clock += 1; service.TryBuy(source, "qa-potion", 1, 2, out _);
                    await Gap(350); await Capture("after-" + resolution.x + ".png");
                    Check(healthBars.VisibleCount > 0 && healthBars.VisibleCount <= 32, "real enemy bars visible and bounded at " + resolution);
                    Write("last-runtime-state.txt", "visibleBars=" + healthBars.VisibleCount + "\nfadeRenderers=" + visibility.FadedRendererCount
                        + "\nunsupportedFade=" + visibility.UnsupportedRendererCount + "\ntable=" + table.transform.position);
                }
                await TestSummonAndTable(party, table, cameraFollow, fixtures.transform);
                var visibilityProfiler = cameraFollow.GetComponent<CameraVisibility>();
                string profile = "environment,features,samples,gc_bytes_per_observed_frame,main_thread_ms\n";
                foreach (bool enabled in new[] { false, true }) {
                healthBars.enabled = enabled; visibilityProfiler.enabled = enabled; await Gap(200);
                using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 32))
                using (var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 32)) {
                    long gcTotal = 0, cpuTotal = 0; int n = 0;
                    for (int i = 0; i < 20; i++) { await Gap(100); gcTotal += gc.LastValue; cpuTotal += cpu.LastValue; n++; }
                    profile += "Editor," + enabled + "," + n + "," + gcTotal / n + "," + (cpuTotal / (double)n / 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n";
                    Write("editor-profiler.csv", profile);
                    Check(gc.Valid && cpu.Valid, "actual Editor ProfilerRecorder sample collected (whole-scene, not standalone)");
                }
                }
                healthBars.enabled = true; visibilityProfiler.enabled = true;
                Write("qa-status.txt", "COMPLETE " + checks.Count + " checks; native mouse/keyboard and standalone not verified");
            } catch (Exception error) { Write("qa-status.txt", "FAILED\n" + error); Debug.LogException(error); }
            finally {
                if (fixtures != null) Object.Destroy(fixtures);
                if (view != null && sizeProperty != null && oldSize >= 0) sizeProperty.SetValue(view, oldSize);
                SessionState.SetBool(Session, false); running = false;
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Session + ".reload", false);
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Session + ".options", 0);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
        static void TestQuestReceipts(PlayerSaveController save, MonsterInventoryAdapter inventory, MonsterItemDefinition potion) {
            var manager = Object.FindFirstObjectByType<QuestManager>();
            var rewards = Object.FindFirstObjectByType<QuestRewardService>();
            Check(manager != null && rewards != null, "real quest reward service bound");
            var definition = ScriptableObject.CreateInstance<QuestDefinition>(); definition.name = "QA receipt only";
            string id = "qa-world-feedback-" + Guid.NewGuid().ToString("N");
            typeof(QuestDefinition).GetField("questId", Any).SetValue(definition, id);
            var reward = new QuestRewardDefinition();
            typeof(QuestRewardDefinition).GetField("rewardType", Any).SetValue(reward, QuestRewardType.Item);
            typeof(QuestRewardDefinition).GetField("amount", Any).SetValue(reward, 1);
            typeof(QuestRewardDefinition).GetField("itemDefinition", Any).SetValue(reward, potion);
            ((List<QuestRewardDefinition>)typeof(QuestDefinition).GetField("rewards", Any).GetValue(definition)).Add(reward);
            string savedRoot = SessionState.GetString(Session + ".save", "");
            string blocker = savedRoot + "/blocked-store"; Directory.CreateDirectory(savedRoot); File.WriteAllText(blocker, "intentional QA save failure");
            File.AppendAllText(Qa + "qa-created-paths.txt", blocker + "\n");
            int receipts = 0; Action<ItemAcquisitionReceipt> observer = r => { if (r.Source == AcquisitionSource.QuestReward && r.TransactionId.Contains(id)) receipts++; };
            ItemAcquisitionFeedback.Received += observer;
            int before = inventory.GetQuantity(potion);
            // Preserve the original store's load authorization; changing a store deliberately clears it.
            var loadedPathField = typeof(PlayerSaveController).GetField("loadedSlotPath", Any);
            var retainedField = typeof(PlayerSaveController).GetField("retainedData", Any);
            object loadedPath = loadedPathField.GetValue(save), retained = retainedField.GetValue(save);
            void RestoreStore() {
                save.SetSaveStore(new PlayerJsonFileSaveStore(savedRoot));
                loadedPathField.SetValue(save, loadedPath); retainedField.SetValue(save, retained);
            }
            try {
                Check(manager.AcceptQuest(definition), "temporary quest accepted in isolated save");
                save.SetSaveStore(new PlayerJsonFileSaveStore(blocker));
                Check(manager.CompleteQuest(id), "temporary quest completes through real callback");
                Check(receipts == 0 && inventory.GetQuantity(potion) == before && !manager.GetQuestState(id).RewardsClaimed,
                    "failed quest save rolls back item and emits no acquisition receipt");
                RestoreStore();
                rewards.RetryPendingRewards();
                Check(receipts == 1 && inventory.GetQuantity(potion) == before + 1 && manager.GetQuestState(id).RewardsClaimed,
                    "successful saved quest emits exactly one acquisition receipt");
                rewards.RetryPendingRewards(); Check(receipts == 1, "claimed quest retry does not duplicate receipt");
            } finally { ItemAcquisitionFeedback.Received -= observer; RestoreStore(); }
        }
        static async Task TestSummonAndTable(PetCommandInput party, GameObject table, BasicCameraFollow follow, Transform fixtures) {
            var hand = party.GetComponent<PetSummonHandSocket>(); var director = party.GetComponent<PetSummonDirector>();
            Check(hand != null && director != null && hand.ShowBallInActiveHand(), "existing hand socket displays common ball");
            Check(hand.HeldBall.GetComponent<LODGroup>() != null && hand.HeldBall.GetComponentsInChildren<Renderer>(true)
                .All(r => r.sharedMaterial == AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "Materials/BallCommon.mat")), "held ball uses new common model/material");
            hand.DestroyHeldBall();
            var bodies = new List<(BasicCreatureEnemy brain, bool enabled)>();
            foreach (var brain in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)) { bodies.Add((brain, brain.enabled)); brain.enabled = false; }
            Vector3 old = party.transform.position;
            var yawField = typeof(BasicCameraFollow).GetField("yaw", Any);
            var pitchField = typeof(BasicCameraFollow).GetField("pitch", Any);
            object oldYaw = yawField.GetValue(follow), oldPitch = pitchField.GetValue(follow);
            try {
                int index = Enumerable.Range(0, 6).First(i => party.TryGetPetInSlot(i, out _));
                Check(director.RequestSummonSlot(index), "real summon director accepts existing slot");
                for (int i = 0; i < 100 && director.IsSequenceRunning; i++) await Gap(100);
                Check(!director.IsSequenceRunning && party.activePet != null && party.activePet.CanReceiveCommands,
                    "new ball summon completes with real pet/provider confirmation: " + director.LastFailureReason);
                Check(director.RequestRecall(), "real recall director accepts summoned pet");
                for (int i = 0; i < 100 && director.IsSequenceRunning; i++) await Gap(100);
                Check(!director.IsSequenceRunning && !party.activePet.IsSummoned, "recall completes without replacing ball physics/callbacks");
                // Inspect the authored prop from the existing gameplay camera, only in this Play/save fixture.
                Vector3 point = old;
                // Use an unobstructed walking position, not a fixed offset inside the neighboring shop.
                float closest = float.PositiveInfinity;
                foreach (var delta in new[] { new Vector3(0,0,-3.2f), new Vector3(3.2f,0,0), new Vector3(-3.2f,0,0), new Vector3(0,0,3.2f) }) {
                    Vector3 candidate = table.transform.position + delta;
                    if (NavMesh.SamplePosition(candidate, out var ground, .75f, NavMesh.AllAreas)) candidate = ground.position;
                    else if (Physics.Raycast(candidate + Vector3.up, Vector3.down, out var floor, 3, ~0, QueryTriggerInteraction.Ignore)) candidate = floor.point;
                    else continue;
                    if (Physics.CheckCapsule(candidate + Vector3.up * .3f, candidate + Vector3.up * 1.5f, .25f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 direction = (table.transform.position - candidate).normalized;
                    if (Physics.Linecast(candidate + Vector3.up * 1.2f, candidate - direction * 3 + Vector3.up * 2, ~0, QueryTriggerInteraction.Ignore)) continue;
                    float distance = (candidate - old).sqrMagnitude;
                    if (distance < closest) { closest = distance; point = candidate; }
                }
                var movement = party.GetComponent<CharacterController>(); bool controllerOn = movement != null && movement.enabled;
                if (movement != null) movement.enabled = false; party.transform.position = point;
                if (movement != null) movement.enabled = controllerOn;
                Vector3 toward = table.transform.position - point; toward.y = 0;
                yawField.SetValue(follow, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg); pitchField.SetValue(follow, 24f);
                await Gap(1000);
                var camera = Camera.main;
                Vector3 focus = party.transform.position + Vector3.up;
                var obstruction = Physics.RaycastAll(camera.transform.position, (focus-camera.transform.position).normalized, Vector3.Distance(focus,camera.transform.position), ~0, QueryTriggerInteraction.Ignore);
                Write("table-camera-diagnostics.txt", "camera=" + camera.transform.position + " player=" + party.transform.position + " table=" + table.transform.position + "\n" + string.Join("\n", obstruction.Select(h => h.collider.name + " parent=" + h.collider.transform.parent?.name + " shaders=" + string.Join(",", h.collider.GetComponentsInParent<Renderer>(true).Select(r => r.sharedMaterial?.shader?.name)))));
                await Capture("runtime-table.png");
                Vector3 viewport = camera.WorldToViewportPoint(table.transform.position + Vector3.up * .55f);
                Check(viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1, "authored table is inside actual gameplay camera viewport");
                Check(table.GetComponentsInChildren<MeshCollider>().All(c => !c.convex && !c.isTrigger), "table remains a static solid prop without healing interaction");
            } finally { party.transform.position = old; yawField.SetValue(follow, oldYaw); pitchField.SetValue(follow, oldPitch); foreach (var item in bodies) if (item.brain != null) item.brain.enabled = item.enabled; }
        }
        static void TestCamera(BasicCameraFollow source, Transform parent) {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "QA wall"; wall.transform.SetParent(parent);
            Vector3 origin = new Vector3(1000, 20, 1000), desired = origin - Vector3.forward * 5;
            wall.transform.position = origin - Vector3.forward * .55f; wall.transform.localScale = new Vector3(4, 4, .1f); Physics.SyncTransforms();
            Vector3 result = source.ConstrainCameraPosition(origin, desired);
            Check(result.z > wall.transform.position.z, "camera preferred min-distance does not cross a close wall");
            Check(Vector3.Distance(result, origin) < .45f, "camera may shorten below preferred 0.45m for safety");
            wall.transform.position = origin - Vector3.forward * 2; Physics.SyncTransforms(); result = source.ConstrainCameraPosition(origin, desired);
            Check(result.z > wall.transform.position.z + .25f, "camera collision reserves near-plane space");
            Object.Destroy(wall);
        }
        static async Task Capture(string name) {
            ScreenCapture.CaptureScreenshot(Qa + name); await Gap(250);
            File.AppendAllText(Qa + "qa-created-paths.txt", Qa + name + "\n");
            Check(File.Exists(Qa + name), "engine screenshot " + name);
        }
    }
}
#endif

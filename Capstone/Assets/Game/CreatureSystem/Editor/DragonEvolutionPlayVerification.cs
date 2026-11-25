using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.PetSummon;
using Capstone.Game.ProfileSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.ShopSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    // Only armed by the explicit menu; one empty temporary scene and a bounded 180s session.
    public static class DragonEvolutionPlayVerification {
        const string Key = "Capstone.DragonEvolution.Play.20261121";
        static bool MilestoneRun => SessionState.GetBool(Key + ".milestones", false);
        static bool ItemRun => SessionState.GetBool(Key + ".items", false);
        static string Root => ItemRun ? Path.Combine(QaArtifactPaths.Root, "ShopProgression")
            : MilestoneRun ? Path.Combine(QaArtifactPaths.Root, "EvolutionPromptMilestones") : DragonEvolutionVerification.Root;
        static string Report => Path.Combine(Root, "play-checks.txt");
        static readonly List<string> lines = new();
        static double deadline;
        static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        static void Check(bool passed, string message) {
            lines.Add((passed ? "PASS " : "FAIL ") + message); File.WriteAllLines(Report, lines);
            if (!passed) throw new InvalidOperationException(message);
        }
        static async Task Until(Func<bool> predicate, float seconds, string description) {
            double expires = Math.Min(deadline, EditorApplication.timeSinceStartup + seconds);
            while (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < expires && !predicate())
                await Awaitable.NextFrameAsync();
            Check(EditorApplication.isPlaying && predicate(), description);
        }
        [MenuItem("Tools/Capstone/Creatures/Play verify dragon evolution (isolated, 180s)")]
        static void Begin() {
            SessionState.EraseString(Key + ".reload");
            SessionState.EraseBool(Key + ".milestones");
            BeginScene();
        }
        [MenuItem("Tools/Capstone/Creatures/Verify evolution milestones and camera (isolated)")]
        static void BeginMilestones() {
            SessionState.EraseString(Key + ".reload");
            SessionState.SetBool(Key + ".milestones", true);
            BeginScene();
        }
        [MenuItem("Tools/Capstone/Creatures/Verify expanded item use (isolated)")]
        static void BeginItems() {
            SessionState.EraseString(Key + ".reload");
            SessionState.SetBool(Key + ".items", true);
            try { BeginScene(); }
            catch { SessionState.EraseBool(Key + ".items"); throw; }
        }
        static void BeginScene() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1
                || scene.isDirty || string.IsNullOrEmpty(scene.path) || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Cần một scene đã lưu ở Edit Mode, không có chỉnh sửa chưa lưu.");
            Directory.CreateDirectory(Root);
            bool reloading = SessionState.GetString(Key + ".reload", "").Length > 0;
            File.WriteAllText(reloading ? Path.Combine(Root, "play-reload-checks.txt") : Report,
                "Entering isolated empty-scene Play; max " + (reloading ? "30" : "180") + "s; no original save/controller loaded.\n");
            SessionState.SetString(Key, scene.path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("Tools/Capstone/Creatures/Play verify persisted forms (isolated, 30s)")]
        static void BeginReload() {
            SessionState.EraseBool(Key + ".milestones");
            string folder = Path.Combine(Root, "play-saves");
            string path = Directory.Exists(folder) ? Directory.GetFiles(folder, "player_qa-evo-*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
            if (path == null) throw new InvalidOperationException("Chạy lượt Play evolution trước để tạo save thử trên E.");
            SessionState.SetString(Key + ".reload", path);
            BeginScene();
        }
        [InitializeOnLoadMethod]
        static void Listen() {
            EditorApplication.playModeStateChanged -= StateChanged;
            EditorApplication.playModeStateChanged += StateChanged;
        }
        static void StateChanged(PlayModeStateChange state) {
            string previous = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(previous)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) {
                string reload = SessionState.GetString(Key + ".reload", "");
                if (reload.Length > 0) RunReload(reload); else Run();
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseString(Key);
            SessionState.EraseString(Key + ".reload");
            SessionState.EraseBool(Key + ".milestones");
            SessionState.EraseBool(Key + ".items");
            EditorApplication.delayCall += () => EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
        static IEnumerator FinishRoutine(IEnumerator routine, Action complete) {
            yield return routine;
            complete();
        }
        static async Task Effect(PetSummonReleaseEffect effect, IEnumerator routine, string name) {
            bool done = false;
            effect.StartCoroutine(FinishRoutine(routine, () => done = true));
            await Until(() => done, 15, name);
        }
        static async void Run() {
            lines.Clear(); deadline = EditorApplication.timeSinceStartup + 180;
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            var errors = new List<string>();
            void Log(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
            Application.logMessageReceived += Log;
            try {
                Check(Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                    "No original save controller in temporary scene");
                var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
                var catalog = AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>("Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "QA Floor";
                floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(100, 1, 100);
                var camera = new GameObject("QA Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>(); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(6, 5, -9); camera.transform.LookAt(Vector3.forward * 2);
                var owner = new GameObject("QA Owner"); owner.SetActive(false);
                var input = owner.AddComponent<PetCommandInput>();
                var box = owner.AddComponent<PetBoxRuntimeProvider>(); box.Bind(input);
                var save = owner.AddComponent<PlayerSaveController>();
                save.SetSaveStore(new PlayerJsonFileSaveStore(Path.Combine(Root, "play-saves")));
                Set(save, "slotId", "qa-evo-" + Guid.NewGuid().ToString("N"));
                Set(save, "autoFindReferences", false); Set(save, "loadOnStart", false);
                Set(save, "saveOnApplicationQuit", false); Set(save, "saveOnDisable", false); Set(save, "saveOnApplicationPause", false);
                Set(save, "migrateLegacyQuestSave", false); Set(save, "autoSaveDelay", 9999f);
                Set(save, "petCommandInput", input); Set(save, "petBoxProvider", box); Set(save, "petPrefabCatalog", catalog);
                MonsterInventoryAdapter bag = null;
                if (ItemRun) {
                    bag = owner.AddComponent<MonsterInventoryAdapter>();
                    bag.SetInventory(new GDS.Core.ListBag { Size = 24 });
                    Set(save, "inventoryAdapter", bag);
                }
                owner.SetActive(true);
                var service = owner.AddComponent<PetEvolutionService>(); Set(service, "saveController", save);
                if (ItemRun) service.Bind(bag);
                var presenter = owner.AddComponent<PetEvolutionPresentationController>();
                presenter.gameplayCamera = camera;
                presenter.whiteOverlayMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("6aa8d41eaab07fe47b792e7bfad3b5a4"));
                presenter.showStartText = presenter.showCongratsText = false; // No interactive congrats wait in the bounded fixture.
                var effect = owner.AddComponent<PetSummonReleaseEffect>();
                effect.ConfigureProfile(AssetDatabase.LoadAssetAtPath<PetVFXProfile>(PetSummon.Editor.PetSummonVfxSetup.ProfilePath));
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "QA Ball";
                ball.transform.localScale = Vector3.one * .22f; ball.GetComponent<Collider>().enabled = false;
                ball.transform.position = new Vector3(0, .5f, 2);
                await Awaitable.NextFrameAsync();
                PetController Create(string family, int slot, int level) {
                    var prefab = library.families.Single(f => f.definitionId == family).forms[0].petPrefab;
                    var pet = Object.Instantiate(prefab).GetComponent<PetController>();
                    pet.GetComponent<PetCollectionMetadata>().AssignPersistentId("qa-evo-" + Guid.NewGuid().ToString("N"));
                    pet.AssignOwner(owner.transform); pet.roamAroundOwner = false;
                    var stats = pet.GetComponent<PetHudRuntimeStats>();
                    stats.SetIdentity(stats.DisplayName, level, stats.Icon);
                    pet.GetComponent<PetCollectionMetadata>().SyncRuntimeHealth(stats);
                    stats.SetStatus(stats.MaxHealth, stats.MaxHealth, 0, 0);
                    input.petSlots[slot] = pet; pet.Withdraw(); return pet;
                }
                if (ItemRun) {
                    await VerifyItems(Create("Dragon Dusk", 0, 5), Create("bat", 1, 5), input, save, service, bag);
                    Check(errors.Count == 0, "No errors during isolated item use");
                    lines.Add("COMPLETE isolated item use");
                    return;
                }
                if (MilestoneRun) {
                    await VerifyMilestones(Create("bat", 0, 17), input, save, service, presenter, camera);
                    Check(errors.Count == 0, "No errors during prompt, save reload, and camera transitions");
                    lines.Add("COMPLETE isolated evolution milestone and camera test.");
                    return;
                }
                var dragon = Create("Dragon Dusk", 0, 40); input.SetActivePet(dragon);
                await Awaitable.NextFrameAsync();
                for (int cycle = 1; cycle <= 10; cycle++) {
                    bool released = false;
                    await Effect(effect, effect.PlaySummon(dragon, Vector3.forward * 4, ball.transform,
                        () => released = dragon.TrySummonAt(Vector3.forward * 4, Vector3.forward, true)), "Dusk release VFX cycle " + cycle);
                    await Until(() => dragon.CanReceiveCommands, 6, "Dusk Idle/Follow cycle " + cycle);
                    Check(released && !dragon.IsSummonPresentationPending && dragon.GetComponentsInChildren<Renderer>(true)
                        .Any(r => r.enabled && r.gameObject.activeInHierarchy), "Dusk visible cycle " + cycle);
                    Check(!dragon.animator.GetCurrentAnimatorStateInfo(0).IsName("Cast Spell"), "No lifecycle Cast Spell cycle " + cycle);
                    await Effect(effect, effect.PlayRecall(dragon, ball.transform, dragon.Withdraw), "Dusk recall VFX cycle " + cycle);
                    Check(!dragon.IsSummoned, "Dusk withdrawn cycle " + cycle);
                }
                async Task Evolve(PetController pet, string target, string family) {
                    var metadata = pet.GetComponent<PetCollectionMetadata>();
                    string id = metadata.PetId, genetics = JsonUtility.ToJson(metadata.IndividualValues) + JsonUtility.ToJson(metadata.EffortValues) + metadata.Nature;
                    var before = pet.GetComponent<PetHudRuntimeStats>().CreateSaveData();
                    bool done = false, success = false; string message = "";
                    PetEvolutionPresentationController.Present(owner.scene, service, pet, PetEvolutionMethod.Level, null, (ok, text) => { success = ok; message = text; done = true; });
                    await Until(() => done, 25, family + " evolution presentation → " + target);
                    Check(success, family + " committed → " + target + ": " + message);
                    Check(metadata.CurrentFormId == target && metadata.PetId == id, family + " Form/PetId → " + target);
                    var after = pet.GetComponent<PetHudRuntimeStats>().CreateSaveData();
                    Check(after.level == before.level && before.equippedSkillIds.SequenceEqual(after.equippedSkillIds), family + " level/loadout preserved");
                    Check(genetics == JsonUtility.ToJson(metadata.IndividualValues) + JsonUtility.ToJson(metadata.EffortValues) + metadata.Nature, family + " genetics preserved");
                    var visual = pet.GetComponent<PetEvolutionVisualController>();
                    Check(visual.ActiveAnimator.GetComponent<CreatureVisualBinding>().animationProfile.formId == target, family + " active visual matches form");
                    Check(!visual.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Cast Spell"), family + " no Cast Spell after evolution");
                    await Until(() => !SceneManager.GetSceneByName(presenter.evolutionSceneName).isLoaded, 4, "Evolution scene unloaded");
                }
                await Evolve(dragon, "02", "Dragon Dusk");
                string dragonId = dragon.GetComponent<PetCollectionMetadata>().PetId;
                var form2 = dragon.GetComponent<PetHudRuntimeStats>().CreateSaveData();
                Object.Destroy(dragon.gameObject); input.petSlots[0] = null; input.activePet = null;
                await Awaitable.NextFrameAsync();
                Check(save.LoadNow(), "Full roster load Form02 from isolated disk save");
                dragon = input.petSlots[0];
                Check(dragon.GetComponent<PetCollectionMetadata>().CurrentFormId == "02"
                    && dragon.GetComponent<PetCollectionMetadata>().PetId == dragonId
                    && dragon.GetComponent<PetHudRuntimeStats>().CreateSaveData().equippedSkillIds.SequenceEqual(form2.equippedSkillIds), "Loaded Form02 identity and loadout");
                await Evolve(dragon, "03", "Dragon Dusk");
                foreach (var pair in new[] { (family: "chick", slot: 1), (family: "wolf", slot: 2) }) {
                    var pet = Create(pair.family, pair.slot, 40); await Awaitable.NextFrameAsync();
                    await Evolve(pet, "02", pair.family); await Evolve(pet, "03", pair.family);
                }
                Check(save.SaveNow(), "Save three evolved Form03 actors");
                var expected = input.petSlots.Take(3).Select(p => (id: p.GetComponent<PetCollectionMetadata>().PetId,
                    data: p.GetComponent<PetHudRuntimeStats>().CreateSaveData(), genetics: p.GetComponent<PetCollectionMetadata>().Nature)).ToArray();
                foreach (var pet in input.petSlots.Take(3)) if (pet != null) Object.Destroy(pet.gameObject);
                input.petSlots = new PetController[6]; input.activePet = null;
                await Awaitable.NextFrameAsync();
                Check(save.LoadNow(), "Full roster reload three Form03 actors");
                for (int i = 0; i < 3; i++) {
                    var pet = input.petSlots[i]; var metadata = pet.GetComponent<PetCollectionMetadata>();
                    var stats = pet.GetComponent<PetHudRuntimeStats>().CreateSaveData();
                    Check(metadata.CurrentFormId == "03" && metadata.PetId == expected[i].id && stats.level == expected[i].data.level
                        && Mathf.Approximately(stats.health, expected[i].data.health) && stats.equippedSkillIds.SequenceEqual(expected[i].data.equippedSkillIds)
                        && metadata.Nature == expected[i].genetics, "Form03 disk reload identity/HP/level/skills/nature slot " + i);
                }
                var first = Create("chick", 3, 18); var second = Create("wolf", 4, 18);
                await Awaitable.NextFrameAsync();
                var canvas = new GameObject("QA HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
                var prompt = canvas.AddComponent<EvolutionHudPromptController>(); Set(prompt, "service", service); prompt.Build((RectTransform)canvas.transform);
                await Until(() => prompt.IsOpen, 5, "HUD queue first prompt");
                var view = canvas.GetComponentInChildren<EvolutionHudPromptView>(true);
                Check(Get<PetController>(prompt, "current") == first, "Prompt queue order and single view");
                await Awaitable.NextFrameAsync(); view.Later.onClick.Invoke();
                Check(first.GetComponent<PetCollectionMetadata>().CurrentFormId == "01" && !InventoryInputController.GameplayInputBlocked, "Later keeps Form01 and releases owned input gate");
                await Until(() => prompt.IsOpen && Get<PetController>(prompt, "current") == second, 5, "Next eligible pet queued");
                await Until(() => view.Confirm.interactable, 5, "Confirm valid and enabled");
                await Awaitable.NextFrameAsync(); view.Confirm.onClick.Invoke();
                await Until(() => second.GetComponent<PetCollectionMetadata>().CurrentFormId == "02" && !PetEvolutionPresentationController.AnyPlaying, 25, "HUD Confirm commits via real presentation/service");
                Check(!InventoryInputController.GameplayInputBlocked, "Confirm/scene cleanup releases input gate");
                await Until(() => !prompt.IsOpen, 3, "No repeated same-level Later prompt");
                Check(errors.Count == 0, "No Error/Exception logged in isolated Play session (environment logs included)");
                lines.Add("PLAY CALLBACK/API VERIFIED: Dragon01 summon-release/recall x10; Dragon/chick/wolf 01→02→03; disk Form02/03; popup Later/Confirm/queue. Manual player throw, mouse/gamepad, visuals at both resolutions remain unverified.");
            } catch (Exception error) {
                lines.Add("FAILED/INTERRUPTED: " + error); Debug.LogWarning("Isolated evolution Play verification stopped: " + error.Message);
            } finally {
                Application.logMessageReceived -= Log;
                Application.runInBackground = background;
                File.WriteAllLines(Report, lines);
                if (errors.Count > 0) File.WriteAllLines(Path.Combine(Root, "play-errors.txt"), errors);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
        static async Task VerifyItems(PetController pet, PetController reserve, PetCommandInput input,
            PlayerSaveController save, PetEvolutionService evolution, MonsterInventoryAdapter bag) {
            var heal = input.gameObject.AddComponent<PetHealService>();
            heal.Bind(bag);
            var stats = pet.GetComponent<PetHudRuntimeStats>();
            var combat = pet.GetComponent<CreatureCombatant>();
            var reserveStats = reserve.GetComponent<PetHudRuntimeStats>();
            MonsterItemDefinition Add(string id) {
                var item = InventoryDefinitionCatalog.Find(id);
                Check(item != null && bag.TryAddItemAtomic(item, 1), "Add test item " + id);
                return item;
            }
            async Task Wait(float delay) {
                float until = Time.unscaledTime + delay;
                while (Time.unscaledTime < until && EditorApplication.isPlaying) await Awaitable.NextFrameAsync();
            }
            input.SetActivePet(pet);
            Check(pet.TrySummonAt(Vector3.forward * 4, Vector3.forward), "Summon living pet");
            await Until(() => pet.CanReceiveCommands, 8, "Summoned pet can act");
            float maxHp = stats.MaxHealth, maxMp = stats.MaxEnergy;
            stats.SetStatus(maxHp * .4f, maxHp, 15, maxMp);
            float oldHp = stats.Health;
            var potion = Add("hp-potion-small");
            Check(heal.TryHeal(pet, potion, out var message) && Mathf.Approximately(stats.Health, Mathf.Min(maxHp, oldHp + 25))
                && bag.GetQuantity(potion) == 0, "Fixed 25 HP and one consumed potion: " + message);
            await Wait(.2f);
            var mana = Add("mana-potion");
            stats.SetStatus(stats.Health, maxHp, 15, maxMp);
            float oldMp = stats.Energy;
            Check(heal.TryHeal(pet, mana, out message) && Mathf.Approximately(stats.Energy, oldMp + 25)
                && bag.GetQuantity(mana) == 0, "Fixed 25 MP and one consumed potion: " + message);
            await Wait(.2f);
            var hit = new CombatHit(reserve.gameObject, "qa-bleed", 1, 5, CreatureDamageKind.Physical, false, false, pet.transform.position);
            CombatBleed.ApplyTarget(new CreatureCombatTarget(combat), hit, 5);
            var ailments = pet.GetComponent<CombatBleed>();
            var burnRemedy = Add("fruit-cure-burn");
            Check((ailments.ActiveAilments & CombatAilment.Bleed) != 0 && !heal.CreatePreview(pet, burnRemedy).CanHeal,
                "Burn fruit rejects a target with bleed only");
            var bleedRemedy = Add("remedy-bleed");
            Check(heal.TryHeal(pet, bleedRemedy, out message) && ailments.ActiveAilments == CombatAilment.None,
                "Selective medicine clears the matching bleed: " + message);
            await Wait(.2f);
            var effectiveStats = typeof(CreatureCombatant).GetProperty("Snapshot", BindingFlags.NonPublic | BindingFlags.Instance);
            int speed = ((PetStatSnapshot)effectiveStats.GetValue(combat)).Speed;
            Check(heal.TryHeal(pet, Add("fruit-speed"), out message)
                && ((PetStatSnapshot)effectiveStats.GetValue(combat)).Speed == Mathf.RoundToInt(speed * 1.15f),
                "Speed fruit increases the combat stat by 15 percent before the movement cap: " + message);
            reserveStats.SetStatus(0, reserveStats.MaxHealth, reserveStats.Energy, reserveStats.MaxEnergy);
            await Wait(.2f);
            Check(heal.TryHeal(reserve, Add("revive-small"), out message)
                && Mathf.Approximately(reserveStats.Health, reserveStats.MaxHealth * .25f), "Revive an inactive party pet at 25 percent: " + message);

            stats.SetStatus(maxHp * .45f, maxHp, maxMp, maxMp);
            typeof(PetHudRuntimeStats).GetMethod("SpendMana", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(stats, new object[] { 60f });
            oldHp = stats.Health; oldMp = stats.Energy;
            await Wait(.45f);
            Check(stats.Health == oldHp && stats.Energy == oldMp, "Neither passive resource recovers in the first half-second");
            await Until(() => stats.Health > oldHp && stats.Energy > oldMp, 2, "Living summoned pet passively recovers HP and MP");
            Check(Mathf.Approximately(stats.Health - oldHp, maxHp * .05f)
                && Mathf.Approximately(stats.Energy - oldMp, maxMp * .1f), "First recovery tick is 5 percent HP and 10 percent MP");
            pet.Withdraw();
            var wrongStone = Add("stone-dragon");
            var matchingStone = Add("dusk-stone");
            var metadata = pet.GetComponent<PetCollectionMetadata>();
            Check(!evolution.CreatePreview(pet, PetEvolutionMethod.Item, wrongStone.StableId).CanEvolve && bag.GetQuantity(wrongStone) == 1,
                "Dragon secondary type cannot use a Dragon stone");
            Check(evolution.TryEvolve(pet, out message, PetEvolutionMethod.Item, matchingStone.StableId)
                && metadata.CurrentFormId == "02" && bag.GetQuantity(matchingStone) == 0 && stats.Level == 5,
                "Primary Dark stone advances exactly one form below the level milestone: " + message);
            var disk = PlayerSaveMigration.Read(File.ReadAllText(save.SavePath), out _);
            Check(disk.pets.petStates.Single(x => x.petId == metadata.PetId).customization.currentFormId == "02",
                "Stone evolution persists in the test save");
            var profile = input.gameObject.AddComponent<PlayerProfileRuntimeProvider>();
            profile.ResetForNewGame();
            Set(save, "profileProvider", profile);
            var wallet = input.gameObject.AddComponent<PlayerCurrencyWallet>();
            Set(save, "currencyWallet", wallet);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab");
            var enemyObject = Object.Instantiate(enemyPrefab, Vector3.right * 8, Quaternion.identity);
            enemyObject.GetComponent<BasicCreatureEnemy>().enabled = false;
            var enemy = enemyObject.GetComponent<DummyEnemy>();
            int trainerExperience = 10 + 2 * enemyObject.GetComponent<PetHudRuntimeStats>().Level;
            var finisher = new CombatHit(reserve.gameObject, "qa-reward", 2, 100000, CreatureDamageKind.Physical,
                false, false, enemy.TargetPosition);
            enemy.ApplyCombatHit(finisher);
            await Until(() => profile.CreateSaveData().currentExperience == trainerExperience, 20, "Defeating a real enemy grants trainer XP");
            enemy.ApplyCombatHit(finisher);
            disk = PlayerSaveMigration.Read(File.ReadAllText(save.SavePath), out _);
            Check(profile.CreateSaveData().currentExperience == trainerExperience && disk.profile.currentExperience == trainerExperience,
                "A repeated hit cannot grant trainer XP again, and XP survives in the saved journal");
            await Until(() => wallet.Gold >= 2 && wallet.Gold <= 5, 8, "The defeated enemy delivers the authored gold range");

            var shopSource = input.gameObject.AddComponent<ShopSource>();
            var shopScreen = input.gameObject.AddComponent<ShopScreenController>();
            var shopUi = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ThangTest/UIThang/Inventory/UI/ShopScreen.uxml").CloneTree();
            profile.SetLevelProgress(9, 40, 100);
            shopScreen.Bind(shopUi, bag, wallet);
            shopScreen.Open(shopSource);
            var shownOffers = Get<List<ShopOffer>>(shopScreen, "filtered");
            Check(shownOffers.Count == 3 && shownOffers.All(offer => offer.RequiredPlayerLevel == 0),
                "Shop grid hides every locked consumable at level nine");
            var search = shopUi.Q<TextField>("shop-search");
            search.SetValueWithoutNotify(shopSource.Find("hp-potion-medium").Item.Name);
            typeof(ShopScreenController).GetMethod("ApplyQuery", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shopScreen, null);
            Check(shownOffers.Count == 0, "Shop search cannot reveal a locked potion");
            var profileCanvas = new GameObject("QA Profile Canvas", typeof(RectTransform), typeof(Canvas));
            profileCanvas.SetActive(false);
            var profilePanel = profileCanvas.AddComponent<ProfilePanelController>();
            profilePanel.Configure(profileCanvas.GetComponent<Canvas>(), profile);
            profileCanvas.SetActive(true);
            profilePanel.Open();
            var debugButton = profileCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(false)
                .Single(button => button.name == "DebugPlayerLevelUp");
            debugButton.onClick.Invoke();
            disk = PlayerSaveMigration.Read(File.ReadAllText(save.SavePath), out _);
            Check(profile.Level == 10 && disk.profile.level == 10 && disk.profile.currentExperience == 40 && stats.Level == 5,
                "The real Profile button adds and saves one player level, preserving player XP and pet level");
            Check(shownOffers.Count == 1 && shownOffers[0].Id == "hp-potion-medium",
                "An open shop refreshes its search immediately when the Profile button unlocks a tier");
            profilePanel.Close();
            shopScreen.Close();

            var petLevels = save.GetComponent<PetLevelUpService>();
            int required = petLevels.CalculateExperienceRequirement(stats.Level);
            metadata.SetProgress(required - 1, required);
            float beforeGrowth = stats.MaxHealth;
            Check(petLevels.GrantBattleExperience(pet, 2, out _) && stats.Level == 6 && metadata.Experience == 1
                && stats.MaxHealth > beforeGrowth && profile.Level == 10, "Pet XP levels the pet, grows its stats, and leaves player level unchanged");
            Check(petLevels.GrantBattleExperience(pet, int.MaxValue, out _) && stats.Level == 100 && metadata.ExperienceToNextLevel == 0
                && !petLevels.GrantBattleExperience(pet, 1, out _), "Pet progression caps at level 100");
            Check(save.SaveNow(), "Save both player and pet progression");
            disk = PlayerSaveMigration.Read(File.ReadAllText(save.SavePath), out _);
            Check(disk.profile.level == 10 && disk.pets.petStates.Single(x => x.petId == metadata.PetId).runtimeStats.level == 100,
                "The normal save stores independent player and pet levels");
            stats.SetStatus(20, stats.MaxHealth, stats.Energy, stats.MaxEnergy);
            oldHp = stats.Health;
            potion = Add("hp-potion-small");
            using (var locked = new FileStream(save.SavePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
                Check(!heal.TryHeal(pet, potion, out _) && stats.Health == oldHp && bag.GetQuantity(potion) == 1,
                    "Failed save rolls back potion use and HP");
            }
        }

        static async Task VerifyMilestones(PetController pet, PetCommandInput input, PlayerSaveController save,
            PetEvolutionService service, PetEvolutionPresentationController presenter, Camera gameplayCamera) {
            var hud = new GameObject("QA evolution HUD", typeof(RectTransform), typeof(Canvas));
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prompt = hud.AddComponent<EvolutionHudPromptController>();
            Set(prompt, "service", service);
            prompt.Build((RectTransform)hud.transform);
            var view = hud.GetComponentInChildren<EvolutionHudPromptView>(true);
            var level = input.gameObject.AddComponent<PetLevelUpService>();
            int xpTo18 = PetLevelUpService.CalculateDefaultExperienceRequirement(17);
            pet.GetComponent<PetCollectionMetadata>().SetProgress(0, xpTo18);

            async Task Quiet(string description) {
                double until = EditorApplication.timeSinceStartup + 1.25;
                while (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < until && !prompt.IsOpen)
                    await Awaitable.NextFrameAsync();
                Check(EditorApplication.isPlaying && !prompt.IsOpen, description);
            }
            async Task ReloadDeferred() {
                string id = pet.GetComponent<PetCollectionMetadata>().PetId;
                Object.Destroy(pet.gameObject);
                input.petSlots[0] = null;
                input.activePet = null;
                await Awaitable.NextFrameAsync();
                Check(save.LoadNow(), "Reload succeeds from isolated disk save");
                pet = input.petSlots[0];
                Check(pet != null && pet.GetComponent<PetCollectionMetadata>().PetId == id, "Reload keeps the same individual pet");
            }
            void SetLevel(int value) {
                var stats = pet.GetComponent<PetHudRuntimeStats>();
                stats.SetIdentity(stats.DisplayName, value, stats.Icon);
                prompt.Enqueue(pet);
            }
            async Task ConfirmNextForm(string form) {
                bool sawStage = false;
                await Awaitable.NextFrameAsync();
                view.Confirm.onClick.Invoke();
                Check(presenter.IsPlaying && gameplayCamera.isActiveAndEnabled,
                    "Gameplay camera remains enabled while the evolution scene starts loading");
                await Until(() => {
                    var stage = SceneManager.GetSceneByName(presenter.evolutionSceneName);
                    bool stageRenders = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(output =>
                        output.gameObject.scene == stage && output.isActiveAndEnabled && output.targetTexture == null
                        && output.targetDisplay == gameplayCamera.targetDisplay);
                    if (!gameplayCamera.isActiveAndEnabled && stageRenders) sawStage = true;
                    if (!gameplayCamera.isActiveAndEnabled && !stageRenders)
                        throw new InvalidOperationException("Display has no rendering camera during evolution.");
                    return !presenter.IsPlaying;
                }, 12, "No frame without gameplay or evolution display camera");
                Check(sawStage && gameplayCamera.isActiveAndEnabled, "Stage camera takes over and gameplay camera returns");
                Check(pet.GetComponent<PetCollectionMetadata>().CurrentFormId == form, "Confirmed only the next form " + form);
                await Until(() => !SceneManager.GetSceneByName(presenter.evolutionSceneName).isLoaded, 4, "Stage scene unloads");
            }

            await Quiet("Lv17 has no automatic offer");
            Check(level.GrantBattleExperience(pet, xpTo18, out var gained)
                && gained.CurrentLevel == 18, "Actual battle XP reaches Lv18");
            await Until(() => prompt.IsShowing(pet) && view.Confirm.interactable, 5, "Lv18 offers Form01 to Form02");
            await Awaitable.NextFrameAsync();
            view.Later.onClick.Invoke();
            await ReloadDeferred();
            await Quiet("Lv18 Later stays dismissed after a disk reload");
            SetLevel(35);
            await Quiet("No new offer between Lv18 and Lv36");
            SetLevel(36);
            await Until(() => prompt.IsShowing(pet) && view.Confirm.interactable, 5, "Lv36 asks again while the pet is still Form01");
            var offer = Get<PetEvolutionPreview>(prompt, "offeredPreview");
            Check(offer.Rule.SourceStage == 0 && offer.Rule.TargetStage == 1, "Lv36 Form01 offer targets Form02");
            await Awaitable.NextFrameAsync();
            view.Later.onClick.Invoke();
            await ReloadDeferred();
            await Quiet("Lv36 Later also stays dismissed after disk reload");

            presenter.glowStartDelay = 0;
            presenter.spinUpDuration = presenter.spinDownDuration = .35f;
            presenter.whiteoutFadeInDuration = presenter.whiteoutFadeOutDuration = .35f;
            prompt.RequestManual(pet);
            await Until(() => prompt.IsShowing(pet) && view.Confirm.interactable, 5, "Manual evolution remains available after Later");
            await ConfirmNextForm("02");
            await Until(() => prompt.IsShowing(pet) && view.Confirm.interactable, 5, "Lv36 Form02 gets its own offer for Form03");
            await ConfirmNextForm("03");
            await Quiet("Final form has no further automatic offer");
        }
        static async void RunReload(string path) {
            string report = Path.Combine(Root, "play-reload-checks.txt");
            var results = new List<string>();
            void Verify(bool passed, string message) {
                results.Add((passed ? "PASS " : "FAIL ") + message); File.WriteAllLines(report, results);
                if (!passed) throw new InvalidOperationException(message);
            }
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            double expires = EditorApplication.timeSinceStartup + 30;
            try {
                Verify(Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, "Fresh Play has no original save controller");
                var expected = PlayerSaveMigration.Read(File.ReadAllText(path), out _);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(100, 1, 100);
                new GameObject("Reload QA camera", typeof(Camera), typeof(AudioListener));
                var owner = new GameObject("Reload QA owner"); owner.SetActive(false);
                var input = owner.AddComponent<PetCommandInput>();
                var box = owner.AddComponent<PetBoxRuntimeProvider>(); box.Bind(input);
                var save = owner.AddComponent<PlayerSaveController>();
                save.SetSaveStore(new PlayerJsonFileSaveStore(Path.GetDirectoryName(path)));
                Set(save, "slotId", Path.GetFileNameWithoutExtension(path).Substring("player_".Length));
                Set(save, "autoFindReferences", false); Set(save, "loadOnStart", false);
                Set(save, "saveOnApplicationQuit", false); Set(save, "saveOnDisable", false); Set(save, "saveOnApplicationPause", false);
                Set(save, "migrateLegacyQuestSave", false); Set(save, "autoSaveDelay", 9999f);
                Set(save, "petCommandInput", input); Set(save, "petBoxProvider", box);
                Set(save, "petPrefabCatalog", AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>("Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset"));
                owner.SetActive(true); await Awaitable.NextFrameAsync();
                Verify(save.LoadNow(), "Disk save from previous Play session loaded");
                for (int slot = 0; slot < expected.pets.partyPetIds.Count; slot++) {
                    string id = expected.pets.partyPetIds[slot]; if (string.IsNullOrEmpty(id)) continue;
                    var source = expected.pets.petStates.Single(p => p.petId == id);
                    var pet = input.petSlots[slot]; var metadata = pet.GetComponent<PetCollectionMetadata>();
                    var stats = pet.GetComponent<PetHudRuntimeStats>().CreateSaveData();
                    Verify(metadata.PetId == id && metadata.CurrentFormId == source.customization.currentFormId
                        && metadata.EvolutionStage == source.customization.evolutionStage && stats.level == source.runtimeStats.level
                        && Mathf.Approximately(stats.health, source.runtimeStats.health)
                        && stats.equippedSkillIds.SequenceEqual(source.runtimeStats.equippedSkillIds)
                        && metadata.Experience == source.customization.experience && metadata.Nickname == source.customization.nickname
                        && metadata.Nature == source.customization.nature,
                        "Cross-Play restore identity/form/stage/level/HP/loadout/EXP/nickname/nature " + source.definitionId + "/" + source.customization.currentFormId);
                    Verify(pet.TrySummonAt(new Vector3(slot * 4, 0, 4), Vector3.forward), "Cross-Play summon " + source.definitionId);
                    while (EditorApplication.isPlaying && !pet.CanReceiveCommands && EditorApplication.timeSinceStartup < expires)
                        await Awaitable.NextFrameAsync();
                    Verify(pet.CanReceiveCommands && pet.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy), "Cross-Play visible and ready " + source.definitionId);
                    var visual = pet.animator.GetComponent<CreatureVisualBinding>();
                    Verify(visual != null && visual.animationProfile.formId == metadata.CurrentFormId, "Cross-Play correct actual visual " + source.definitionId + "/" + metadata.CurrentFormId);
                    pet.Withdraw(); await Awaitable.NextFrameAsync();
                }
                results.Add("PLAY VERIFIED across separate Play sessions; isolated save only. No manual player throw/input test.");
            } catch (Exception error) { results.Add("FAILED/INTERRUPTED: " + error); Debug.LogWarning("Cross-Play verification: " + error.Message); }
            finally {
                Application.runInBackground = background;
                File.WriteAllLines(report, results);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
    }
}

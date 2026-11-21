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
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    // Only armed by the explicit menu; one empty temporary scene and a bounded 180s session.
    public static class DragonEvolutionPlayVerification {
        const string Key = "Capstone.DragonEvolution.Play.20261121";
        static string Root => DragonEvolutionVerification.Root;
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
            BeginScene();
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
                owner.SetActive(true);
                var service = owner.AddComponent<PetEvolutionService>(); Set(service, "saveController", save);
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

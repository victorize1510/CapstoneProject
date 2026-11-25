using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    // Explicit menu only. Uses the real QA scene bootstrap with a copied profile.
    public static class EvolutionLiveIntegrationVerification {
        const string Key = "Capstone.Evolution.LiveIntegration.20261121";
        static string Root => Path.Combine(UISystem.QaArtifactPaths.Root, "evolution-live-integration-fix-2026-11-21");
        static readonly List<string> lines = new();
        static double deadline;
        static bool FormTwo => SessionState.GetBool(Key + ".formTwo", false);
        static bool NativeInput => SessionState.GetBool(Key + ".native", false);
        static string Report => Path.Combine(Root, FormTwo ? "live-form02-checks.txt" : "live-play-checks.txt");
        static FieldInfo Field(object value, string name) => value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
        static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
        static string PromptKey(PetController pet) => (string)typeof(EvolutionHudPromptController).GetMethod("PromptKey", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { pet });
        static void Check(bool passed, string message) {
            lines.Add((passed ? "PASS " : "FAIL ") + message);
            File.WriteAllLines(Report, lines);
            if (!passed) throw new InvalidOperationException(message);
        }
        static async Task Until(Func<bool> predicate, double seconds, string message) {
            double end = Math.Min(deadline, EditorApplication.timeSinceStartup + seconds);
            while (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < end && !predicate())
                await Awaitable.NextFrameAsync();
            Check(EditorApplication.isPlaying && predicate(), message);
        }
        [MenuItem("Tools/Capstone/Creatures/Verify LIVE evolution HUD and auto restore (copied save, 90s)")]
        static void Begin() {
            SessionState.SetBool(Key + ".formTwo", false);
            SessionState.SetBool(Key + ".native", false);
            BeginScene();
        }
        [MenuItem("Tools/Capstone/Creatures/Verify LIVE evolution with keyboard (copied save, 90s)")]
        static void BeginNative() {
            SessionState.SetBool(Key + ".formTwo", false);
            SessionState.SetBool(Key + ".native", true);
            BeginScene();
        }
        [MenuItem("Tools/Capstone/Creatures/Verify LIVE Form02 auto restore (copied save, 90s)")]
        static void BeginFormTwo() {
            SessionState.SetBool(Key + ".formTwo", true);
            SessionState.SetBool(Key + ".native", false);
            BeginScene();
        }
        static void BeginScene() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || SceneManager.sceneCount != 1
                || scene.name != DemoSceneOneSaveIsolation.CreatureQaSceneName)
                throw new InvalidOperationException("Cần Creature QA scene đã lưu, một scene, Edit Mode; không tự lưu thay đổi của người dùng.");
            var isolation = Object.FindFirstObjectByType<DemoSceneOneSaveIsolation>();
            var save = Object.FindFirstObjectByType<PlayerSaveController>();
            if (isolation == null || save == null) throw new InvalidOperationException("Thiếu QA save isolation.");
            Directory.CreateDirectory(Root);
            string source = Path.Combine(isolation.ResolvedDevelopmentProfile, "player_" + save.SlotId + ".json");
            var data = PlayerSaveMigration.Read(File.ReadAllText(source), out _);
            var dragon = data.pets.petStates.First(p => p.definitionId == "Dragon Dusk");
            if (FormTwo) { dragon.customization.currentFormId = "02"; dragon.customization.evolutionStage = 1; }
            data.pets.activePetId = dragon.petId; data.pets.activePetSummoned = true;
            string profile = Path.Combine(Root, "live-profile"); Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile, "player_" + save.SlotId + ".json"), JsonUtility.ToJson(data, true));
            File.WriteAllText(Path.Combine(Root, "source-save-before.sha256"), BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(source))).Replace("-", ""));
            SessionState.SetString(Key, scene.path); SessionState.SetString(Key + ".source", source);
            // In-memory override only. Reload the unchanged scene asset when leaving Play.
            isolation.developmentProfile = profile;
            Field(save, "saveOnApplicationQuit").SetValue(save, false);
            Field(save, "saveOnDisable").SetValue(save, false);
            Field(save, "saveOnApplicationPause").SetValue(save, false);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Listen() {
            EditorApplication.playModeStateChanged -= StateChanged;
            EditorApplication.playModeStateChanged += StateChanged;
        }
        static void StateChanged(PlayModeStateChange state) {
            string scene = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(scene)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Run();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseString(Key);
            EditorApplication.delayCall += () => EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        }
        [MenuItem("Tools/Capstone/Creatures/Inspect LIVE evolution HUD (read-only)")]
        static void Inspect() {
            if (!EditorApplication.isPlaying) return;
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "live-state.txt"), Snapshot());
            Debug.Log("Evolution integration state: " + Root);
        }
        static string Snapshot() {
            var rows = new List<string>();
            var save = Object.FindFirstObjectByType<PlayerSaveController>();
            rows.Add("Save loaded=" + save?.IsCurrentSlotLoaded + " blocked=" + save?.SaveBlockedReason);
            rows.Add("Paused=" + UISystem.GameplayPause.IsPaused + " input=" + InventoryInputController.GameplayInputBlocked);
            var blockers = typeof(InventoryInputController).GetField("ExternalGameplayInputBlockers", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var item in (System.Collections.IEnumerable)blockers.GetValue(null)) rows.Add("Input owner=" + item);
            foreach (var manager in Object.FindObjectsByType<EvolutionHudPromptController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                rows.Add("Manager=" + manager.name + " enabled=" + manager.isActiveAndEnabled + " open=" + manager.IsOpen
                    + " view=" + Get<EvolutionHudPromptView>(manager, "view") + " globalBlocked=" + Call(manager, "GloballyBlocked"));
            }
            foreach (var pet in Object.FindObjectsByType<PetController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (pet.IsWildEncounter) continue;
                var metadata = pet.GetComponent<PetCollectionMetadata>();
                var stats = pet.GetComponent<PetHudRuntimeStats>();
                var binding = pet.animator != null ? pet.animator.GetComponent<CreatureVisualBinding>() : null;
                rows.Add("Pet=" + pet.name + " id=" + metadata?.PetId + " form=" + metadata?.CurrentFormId + " lv=" + stats?.Level
                    + " visual=" + binding?.animationProfile?.formId + " summoned=" + pet.IsSummoned + " ready=" + pet.CanReceiveCommands
                    + " hp=" + stats?.Health + " target=" + pet.HasCombatTarget + " eligible=" + PromptKey(pet));
                if (metadata?.DefinitionId == "Dragon Dusk") {
                    var visual = pet.GetComponentInChildren<PetEvolutionVisualController>(true);
                    rows.Add("  Visual host=" + visual?.name + " base=" + (visual != null ? Get<GameObject>(visual, "baseVisual") : null));
                    if (visual != null) foreach (var item in Get<Renderer[]>(visual, "baseRenderers") ?? Array.Empty<Renderer>())
                        rows.Add("  Cached base=" + (item != null ? item.name : "destroyed"));
                    var presence = Get<Dictionary<Renderer, bool>>(pet, "rendererEnabledStates");
                    foreach (var renderer in pet.GetComponentsInChildren<Renderer>(true))
                        rows.Add("  Renderer=" + renderer.name + " type=" + renderer.GetType().Name + " parent=" + renderer.transform.parent?.name
                            + " enabled=" + renderer.enabled + " active=" + renderer.gameObject.activeInHierarchy
                            + " current=" + (visual != null && visual.IsCurrentVisualRenderer(renderer))
                            + " cached=" + (presence.TryGetValue(renderer, out bool enabled) ? enabled.ToString() : "missing"));
                }
            }
            return string.Join("\n", rows);
        }
        static async void Run() {
            lines.Clear(); deadline = EditorApplication.timeSinceStartup + 90;
            bool background = Application.runInBackground; Application.runInBackground = true;
            var errors = new List<string>();
            void Log(string message, string stack, LogType type) {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            }
            Application.logMessageReceived += Log;
            try {
                var save = Object.FindFirstObjectByType<PlayerSaveController>();
                await Until(() => save != null && save.IsCurrentSlotLoaded, 20, "Actual QA bootstrap loaded copied save");
                var isolation = Object.FindFirstObjectByType<DemoSceneOneSaveIsolation>();
                Check(Path.GetFullPath(isolation.ActiveProfile) == Path.GetFullPath(Path.Combine(Root, "live-profile")), "Actual scene save store is isolated on E");
                var party = Object.FindFirstObjectByType<PetCommandInput>();
                var dragon = party.activePet;
                await Until(() => dragon != null && dragon.IsSummoned, 12, "Saved-summoned actor restored automatically, no recall/manual summon");
                var metadata = dragon.GetComponent<PetCollectionMetadata>();
                var binding = dragon.animator.GetComponent<CreatureVisualBinding>();
                Check(metadata.DefinitionId == "Dragon Dusk" && binding != null
                    && binding.animationProfile.formId == metadata.CurrentFormId, "First reveal actual dragon Animator matches saved form " + metadata.CurrentFormId);
                var visual = dragon.GetComponentInChildren<PetEvolutionVisualController>(true);
                Check(dragon.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy)
                    .All(r => r.GetComponentInParent<AAMAP.MapIcon>() != null || visual.IsCurrentVisualRenderer(r)), "No visible base-form renderer after auto restore");
                var hud = Object.FindFirstObjectByType<GameplayHudController>();
                var prompt = hud.GetComponent<EvolutionHudPromptController>();
                Check(prompt != null && Get<EvolutionHudPromptView>(prompt, "view") != null, "Popup installed by actual GameplayHudController.Awake");
                if (FormTwo) {
                    Check(errors.Count == 0, "No error/exception during Form02 bootstrap");
                    lines.Add("PLAY VERIFIED: second fresh Play, Form02 actual saved-summoned first reveal."); return;
                }
                File.WriteAllText(Path.Combine(Root, "bootstrap-state.txt"), Snapshot());
                // Suppress already eligible roster entries in this isolated session only.
                var shown = Get<HashSet<string>>(prompt, "shown");
                foreach (var pet in Object.FindObjectsByType<PetController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                    if (pet.IsWildEncounter) continue;
                    string key = PromptKey(pet); if (key != null) shown.Add(key);
                }
                Call(prompt, "Hide", true);
                var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
                var petFixture = Object.Instantiate(library.families.First(f => f.definitionId == "Dragon Dusk").forms[0].petPrefab).GetComponent<PetController>();
                petFixture.GetComponent<PetCollectionMetadata>().AssignPersistentId("qa-live-" + Guid.NewGuid().ToString("N"));
                petFixture.AssignOwner(party.transform);
                var stats = petFixture.GetComponent<PetHudRuntimeStats>(); stats.SetIdentity(stats.DisplayName, 17, stats.Icon);
                petFixture.GetComponent<PetCollectionMetadata>().SetProgress(0, PetLevelUpService.CalculateDefaultExperienceRequirement(17));
                petFixture.GetComponent<PetCollectionMetadata>().SyncRuntimeHealth(stats); stats.SetStatus(stats.MaxHealth, stats.MaxHealth, 0, 0);
                int slot = Array.FindIndex(party.petSlots, p => p == null); Check(slot >= 0, "Isolated copied roster has a free test slot");
                party.petSlots[slot] = petFixture; petFixture.Withdraw();
                var inventory = Object.FindFirstObjectByType<MonsterInventoryController>();
                inventory.OpenPetEvolutionPanel(petFixture);
                await Until(() => prompt.IsShowing(petFixture), 12, "Actual Pet evolution button routes to compact text-only HUD prompt");
                var view = Get<EvolutionHudPromptView>(prompt, "view");
                Check(!view.Confirm.interactable && !inventory.IsOpen, "Under-level manual confirmation disabled; menu closed");
                Check(!Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(d => d.name == "Pet Evolution Screen" && d.rootVisualElement.style.display.value != UnityEngine.UIElements.DisplayStyle.None), "Old portrait screen never opens");
                await Awaitable.NextFrameAsync(); view.Later.onClick.Invoke();
                Check(!prompt.IsOpen, "Manual Later closes prompt");
                var level = inventory.GetComponent<PetLevelUpService>() ?? inventory.gameObject.AddComponent<PetLevelUpService>();
                Check(level.GrantBattleExperience(petFixture, PetLevelUpService.CalculateDefaultExperienceRequirement(17), out var gained)
                    && gained.CurrentLevel == 18, "Real EXP service/event raised fixture from Lv17 to Lv18");
                await Until(() => prompt.IsShowing(petFixture), 12, "Automatic level evolution popup appears on actual HUB");
                Check(view.gameObject.activeInHierarchy && hud.PresentationRoot.gameObject.activeInHierarchy && view.Confirm.interactable,
                    "Automatic popup visible and enabled, not merely queued");
                ScreenCapture.CaptureScreenshot(Path.Combine(Root, "actual-hub-popup.png"));
                await Awaitable.NextFrameAsync();
                if (NativeInput) {
                    lines.Add("WAIT native keyboard: Enter on selected Later (or N)"); File.WriteAllLines(Report, lines);
                    await Until(() => !prompt.IsOpen, 30, "Native keyboard deferred automatic popup");
                } else view.Later.onClick.Invoke();
                Check(!prompt.IsOpen && petFixture.GetComponent<PetCollectionMetadata>().CurrentFormId == "01", "Later preserves form and releases popup");
                await Until(() => !InventoryInputController.GameplayInputBlocked, 5, "Input restored after Later");
                await Awaitable.NextFrameAsync(); prompt.Enqueue(petFixture);
                var deferred = petFixture.GetComponent<PetCollectionMetadata>();
                Check(PromptKey(petFixture) == null && deferred.TryGetNextEvolutionRule(out var deferredRule)
                    && deferred.WasEvolutionPrompted(deferredRule.EvolutionId), "Later records the first evolution milestone");
                // Manual access remains available after Later; use the same real presentation/save pipeline.
                inventory.OpenPetEvolutionPanel(petFixture);
                await Until(() => prompt.IsShowing(petFixture), 8, "Manual reopen after Later remains available");
                await Awaitable.NextFrameAsync();
                var presenter = PetEvolutionPresentationController.FindFor(party.gameObject.scene);
                presenter.showStartText = presenter.showCongratsText = false; // Avoid interactive text waits in this bounded callback check.
                if (NativeInput) {
                    lines.Add("WAIT native keyboard: Y to confirm"); File.WriteAllLines(Report, lines);
                    await Until(() => !prompt.IsOpen, 30, "Native keyboard confirmed manual popup");
                } else view.Confirm.onClick.Invoke();
                await Until(() => petFixture.GetComponent<PetCollectionMetadata>().CurrentFormId == "02"
                    && !PetEvolutionPresentationController.AnyPlaying, 35, "Actual HUD Confirm completes presentation and saves Form02");
                Check(petFixture.GetComponent<PetCollectionMetadata>().PetId.StartsWith("qa-live-") && stats.Level == 18,
                    "HUD confirmation preserves individual ID and level");
                await Until(() => !InventoryInputController.GameplayInputBlocked, 5, "Input restored after real evolution confirmation");
                // Repeat saved restore with a base-form instance that has already hydrated a different form.
                dragon.Withdraw(); await Awaitable.NextFrameAsync();
                Check(dragon.TryRestoreSummonedNearOwner(), "Repeat auto restore path succeeds");
                Check(dragon.animator.GetComponent<CreatureVisualBinding>().animationProfile.formId == metadata.CurrentFormId,
                    "Repeat restore preserves actual visual and Animator binding");
                File.WriteAllText(Path.Combine(Root, "final-state.txt"), Snapshot());
                Check(errors.Count == 0, "No error/exception during actual HUD and evolution test");
                lines.Add("PLAY VERIFIED: actual QA scene boot, copied save, automatic first reveal and actual manual/automatic HUD integration. "
                    + (NativeInput ? "Native keyboard Later/Confirm tested; pointer separate." : "Button callbacks automated; native pointer/keyboard separate."));
            } catch (Exception error) {
                lines.Add("FAILED: " + error); File.WriteAllText(Path.Combine(Root, "failure-state.txt"), Snapshot());
                Debug.LogWarning("Evolution live integration: " + error.Message);
            } finally {
                Application.logMessageReceived -= Log;
                if (errors.Count > 0) File.WriteAllLines(Path.Combine(Root, "live-runtime-errors.txt"), errors);
                Application.runInBackground = background;
                string source = SessionState.GetString(Key + ".source", "");
                if (File.Exists(source)) File.WriteAllText(Path.Combine(Root, "source-save-after.sha256"), BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(source))).Replace("-", ""));
                File.WriteAllLines(Report, lines);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
    }
}

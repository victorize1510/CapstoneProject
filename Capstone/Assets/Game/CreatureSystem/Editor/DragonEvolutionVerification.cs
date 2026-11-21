using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Capstone.Game.CreatureSystem.Editor {
    // Explicit menu only; no InitializeOnLoad, scene patching or real-save access.
    public static class DragonEvolutionVerification {
        public static string Root => Path.Combine(UISystem.QaArtifactPaths.Root, "dragon-spawn-evolution-popup-2026-11-21");
        static readonly List<string> checks = new();
        static int failed;
        static void Check(bool condition, string description) {
            checks.Add((condition ? "PASS " : "FAIL ") + description);
            if (!condition) failed++;
        }
        static string Cell(object value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        static void Row(StringBuilder rows, params object[] values) => rows.AppendLine(string.Join(",", values.Select(Cell)));

        [MenuItem("Tools/Capstone/Creatures/Verify dragon summon and evolution (isolated)")]
        public static void Verify() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play trước khi kiểm Editor.");
            Directory.CreateDirectory(Root); checks.Clear(); failed = 0;
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>("Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset");
            Check(library != null && catalog != null, "Library and runtime catalog exist");
            if (library == null || catalog == null) return;
            var rows = new StringBuilder();
            Row(rows, "Family", "Form", "Role", "Prefab", "Profile", "Controller", "Default", "Idle", "Move", "Spawn", "Cast", "EvolutionClip", "Transitions", "Result");
            int forms = 0, prefabs = 0;
            foreach (var family in library.families) {
                foreach (var form in family.forms) {
                    forms++;
                    foreach (bool owned in new[] { true, false }) {
                        var prefab = owned ? form.petPrefab : form.wildPrefab;
                        string key = family.definitionId + "/" + form.formId + (owned ? " Owned" : " Wild");
                        Check(prefab != null, key + " prefab"); if (prefab == null) continue;
                        prefabs++;
                        var pet = prefab.GetComponent<PetController>();
                        var metadata = prefab.GetComponent<PetCollectionMetadata>();
                        var animator = prefab.GetComponentInChildren<Animator>(true);
                        var profile = animator != null ? animator.GetComponent<CreatureVisualBinding>()?.animationProfile : null;
                        var raw = animator != null ? animator.runtimeAnimatorController : null;
                        var controller = raw as AnimatorController;
                        if (raw is AnimatorOverrideController over) controller = over.runtimeAnimatorController as AnimatorController;
                        Check(pet != null && metadata != null && profile != null && controller != null, key + " runtime references");
                        if (controller == null || profile == null) continue;
                        var states = States(controller.layers[0].stateMachine).ToArray();
                        string State(CreatureAction action) => profile.actions.FirstOrDefault(a => a != null && a.action == action)?.state;
                        string Motion(string state) {
                            if (string.IsNullOrEmpty(state)) return "thiếu clip/mapping";
                            var found = states.FirstOrDefault(s => s.name == state || state.EndsWith("." + s.name, StringComparison.Ordinal));
                            var motion = found?.motion;
                            if (motion is AnimationClip clip && raw is AnimatorOverrideController overrides) motion = overrides[clip] ?? clip;
                            return state + " → " + (motion != null ? motion.name : "MISSING");
                        }
                        string idle = State(CreatureAction.Idle), move = State(CreatureAction.Move), spawn = State(CreatureAction.Spawn);
                        Check(states.Any(s => s.name == idle || idle == "Base Layer." + s.name), key + " Idle state");
                        Check(states.Any(s => s.name == move || move == "Base Layer." + s.name), key + " Move state");
                        var defaultState = controller.layers[0].stateMachine.defaultState;
                        Check(defaultState != null && (defaultState.name == idle || idle == "Base Layer." + defaultState.name), key + " default Idle");
                        int automatic = states.Sum(s => s.transitions.Count(t => t.conditions.Length == 0));
                        Check(automatic == 0 && controller.layers[0].stateMachine.anyStateTransitions.Length == 0,
                            key + " no automatic demo chain/AnyState");
                        Check(!animator.applyRootMotion, key + " root motion off");
                        Row(rows, family.definitionId, form.formId, owned ? "Owned" : "Wild", AssetDatabase.GetAssetPath(prefab),
                            AssetDatabase.GetAssetPath(profile), AssetDatabase.GetAssetPath(raw), defaultState?.name,
                            Motion(idle), Motion(move), Motion(spawn), Motion(State(CreatureAction.Cast)), "Shared evolution presentation; no dedicated clip required",
                            automatic, "EDITOR VERIFIED config; PLAY UNVERIFIED");
                    }
                }
                for (int stage = 0; stage < family.forms.Count - 1; stage++) {
                    var source = family.forms[stage]; var destination = family.forms[stage + 1];
                    var metadata = source.petPrefab != null ? source.petPrefab.GetComponent<PetCollectionMetadata>() : null;
                    var rule = metadata?.EvolutionRules.FirstOrDefault(r => r != null && r.SourceStage == stage);
                    Check(rule != null && rule.TargetFormId == destination.formId
                        && rule.TargetVisualPrefab == destination.visualPrefab, family.definitionId + " evolution " + stage + " target visual");
                    Check(catalog.TryGetPrefab(family.definitionId, destination.formId, out var resolved)
                        && resolved.gameObject == destination.petPrefab, family.definitionId + "/" + destination.formId + " catalog Owned");
                }
            }
            Check(forms == 33 && prefabs == 66, "33 forms / 66 role prefabs inspected");
            File.WriteAllText(Path.Combine(Root, "evolution-66-editor.csv"), rows.ToString());
            var preview = EditorSceneManager.NewPreviewScene();
            try {
                for (int stage = 0; stage <= 2; stage++) {
                    string form = (stage + 1).ToString("00");
                    Check(catalog.TryGetPrefab("Dragon Dusk", form, out var prefab), "Dragon " + form + " prefab");
                    if (prefab == null) continue;
                    var holder = new GameObject("MetadataOnly_QA");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, preview);
                    var metadata = holder.AddComponent<PetCollectionMetadata>();
                    EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(prefab.GetComponent<PetCollectionMetadata>()), metadata);
                    var data = metadata.CreateSaveData();
                    metadata.RestoreFromSaveData(data);
                    Check(metadata.CurrentFormId == form && metadata.EvolutionStage == stage, "Dragon " + form + " metadata roundtrip");
                    if (stage > 0) {
                        data.currentFormId = "base";
                        metadata.RestoreFromSaveData(data);
                        Check(metadata.CurrentFormId == form, "Legacy base + stage " + stage + " restores committed form");
                        Check(catalog.TryGetSavedPrefab("Dragon Dusk", "base", stage, out var saved) && saved == prefab,
                            "Legacy factory stage " + stage + " selects Owned form");
                        data.currentFormId = stage == 1 ? "dragon-dusk-winged" : "dragon-dusk-elder";
                        metadata.RestoreFromSaveData(data);
                        Check(metadata.CurrentFormId == form, "Dragon alias normalizes " + form);
                    }
                    UnityEngine.Object.DestroyImmediate(holder);
                }
                VerifyJson();
                var prompt = Resources.Load<EvolutionHudPromptView>("UI/EvolutionHudPrompt");
                Check(prompt != null, "Text-only HUD prompt prefab resource");
                if (prompt != null) {
                    var instance = (EvolutionHudPromptView)UnityEngine.Object.Instantiate(prompt);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance.gameObject, preview);
                    instance.Build(); instance.Display("Rồng Chạng Vạng tên dài để thử", true, "");
                    Check(instance.Confirm != null && instance.Later != null, "Both prompt buttons");
                    Check(instance.GetComponentsInChildren<UnityEngine.UI.Text>(true).All(t => t.font != null), "HUD font references");
                    Check(instance.Confirm.navigation.selectOnRight == instance.Later, "Keyboard/gamepad navigation targets");
                    Check(instance.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length == 0, "No pet portrait/Image in prompt");
                    UnityEngine.Object.DestroyImmediate(instance.gameObject);
                }
            } finally { EditorSceneManager.ClosePreviewScene(preview); }
            checks.Add("PLAY UNVERIFIED: no scene/save mutation or Play entry by this menu.");
            File.WriteAllLines(Path.Combine(Root, "editor-checks.txt"), checks);
            Debug.Log($"Evolution Editor checks={checks.Count - 1}, failed={failed}; {Root}. PLAY UNVERIFIED.");
        }
        static void VerifyJson() {
            var data = new PlayerSaveData { quest = null, inventory = null, currency = null, profile = null, achievements = null };
            data.pets = new PetRosterSaveData { captured = true };
            data.pets.petStates.Add(new PetInstanceSaveData {
                petId = "qa-evolution-only", definitionId = "Dragon Dusk",
                customization = new PetCustomizationSaveData { currentFormId = "03", evolutionStage = 2, nickname = "Tên riêng", experience = 900 },
                runtimeStats = new PetRuntimeStatsSaveData { captured = true, level = 40, maxHealth = 300, health = 123 }
            });
            data.pets.partyPetIds.Add("qa-evolution-only");
            string json = PlayerSaveMigration.Write(data);
            var restored = PlayerSaveMigration.Read(json, out _);
            var pet = restored.pets.petStates[0];
            Check(pet.customization.currentFormId == "03" && pet.customization.evolutionStage == 2
                && pet.runtimeStats.level == 40 && pet.runtimeStats.health == 123, "JSON Form3/level/HP persistence");
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            var custom = (Newtonsoft.Json.Linq.JObject)root["pets"]["petStates"][0]["customization"];
            custom.Remove("currentFormId"); custom.Remove("evolutionStage");
            restored = PlayerSaveMigration.Read(root.ToString(), out _);
            Check(restored.pets.petStates[0].customization.evolutionStage == 0, "Old save without form/stage does not infer evolution from level40");
            var store = new PlayerJsonFileSaveStore(Path.Combine(Root, "test-saves"));
            Check(store.TrySave("evolution-roundtrip", data, out var error), "Isolated file save " + error);
            Check(store.TryLoad("evolution-roundtrip", out var disk, out error)
                && disk.pets.petStates[0].customization.currentFormId == "03", "Isolated file load " + error);
        }
        static IEnumerable<AnimatorState> States(AnimatorStateMachine root) {
            foreach (var item in root.states) yield return item.state;
            foreach (var child in root.stateMachines) foreach (var state in States(child.stateMachine)) yield return state;
        }

        [MenuItem("Tools/Capstone/Creatures/Inspect live dragon summon (read-only)")]
        static void InspectLive() {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Lệnh này chỉ đọc state khi đang Play."); return; }
            Directory.CreateDirectory(Root);
            var rows = new StringBuilder();
            Row(rows, "Pet", "Form", "Stage", "Summoned", "RevealPending", "ActorActive", "AnimatorEnabled", "VisualProfileForm", "StateHash", "Normalized", "VisibleRenderers", "SaveLoaded");
            var save = UnityEngine.Object.FindFirstObjectByType<PlayerSaveController>();
            foreach (var pet in UnityEngine.Object.FindObjectsByType<PetController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                var metadata = pet.GetComponent<PetCollectionMetadata>();
                if (metadata == null || metadata.DefinitionId != "Dragon Dusk" || pet.IsWildEncounter) continue;
                var animator = pet.animator;
                var state = animator != null && animator.isActiveAndEnabled ? animator.GetCurrentAnimatorStateInfo(0) : default;
                var profile = animator != null ? animator.GetComponent<CreatureVisualBinding>()?.animationProfile : null;
                Row(rows, pet.name, metadata.CurrentFormId, metadata.EvolutionStage, pet.IsSummoned, pet.IsSummonPresentationPending,
                    pet.gameObject.activeInHierarchy, animator != null && animator.enabled, profile?.formId,
                    state.fullPathHash, state.normalizedTime, pet.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy), save != null && save.IsCurrentSlotLoaded);
            }
            File.WriteAllText(Path.Combine(Root, "live-dragon-state.csv"), rows.ToString());
            var director = UnityEngine.Object.FindFirstObjectByType<PetSummon.PetSummonDirector>();
            File.WriteAllText(Path.Combine(Root, "live-summon-state.txt"), "State=" + director?.State + "\nRunning=" + director?.IsSequenceRunning + "\nReason=" + director?.LastFailureReason);
            Debug.Log("Live dragon snapshot saved: " + Root);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor
{
    public static class CreatureAnimationLibraryVerification
    {
        [MenuItem("Tools/Capstone/Creatures/Verify animation library (read-only)")]
        public static void Verify() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dung Play truoc khi kiem tra.");
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
            if (library == null) throw new InvalidOperationException("Khong tim thay CreatureLibrary.");
            var lines = new StringBuilder("Family,Form,Animator,Avatar,MissingStates,MissingBindings,LoopingOneShots,Typing\n");
            int checkedForms = 0, errors = 0;
            foreach (var family in library.families) {
                if (family == null) continue;
                foreach (var form in family.forms) {
                    if (form == null) continue;
                    var scene = EditorSceneManager.NewPreviewScene();
                    GameObject instance = null;
                    try {
                        if (form.visualPrefab == null || form.animationProfile == null) throw new InvalidOperationException("Thieu Visual/Profile.");
                        instance = (GameObject)PrefabUtility.InstantiatePrefab(form.visualPrefab, scene);
                        var animators = instance.GetComponentsInChildren<Animator>(true);
                        if (animators.Length != 1) throw new InvalidOperationException("Can dung mot Animator.");
                        var animator = animators[0];
                        if (animator.avatar == null || !animator.avatar.isValid || animator.isHuman) throw new InvalidOperationException("Avatar Generic chua hop le.");
                        animator.applyRootMotion = false;
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animator.Rebind(); animator.Update(0);
                        var driver = instance.AddComponent<CreatureAnimationDriver>();
                        driver.Bind(animator);
                        var missing = new List<string>();
                        foreach (var action in form.animationProfile.actions)
                            if (action != null && !driver.TryGetStates(action.action, out _)) missing.Add(action.action + ":" + action.state);
                        foreach (var skill in form.animationProfile.skills)
                            if (skill != null && !driver.TryGetSkillStates(skill.skillId, out _)) missing.Add("Skill:" + skill.skillId);
                        var missingBindings = new HashSet<string>();
                        foreach (var clip in animator.runtimeAnimatorController.animationClips) {
                            if (clip == null) { missingBindings.Add("Null motion clip"); continue; }
                            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                                if (!string.IsNullOrEmpty(binding.path) && animator.transform.Find(binding.path) == null) missingBindings.Add(binding.path);
                        }
                        var oneShots = new List<string>();
                        RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
                        if (runtime is AnimatorOverrideController overrides) {
                            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                            overrides.GetOverrides(pairs);
                            foreach (var pair in pairs) if (pair.Key == null) missingBindings.Add("Invalid override key");
                            runtime = overrides.runtimeAnimatorController;
                        }
                        if (runtime is AnimatorController controller) {
                            foreach (var action in form.animationProfile.actions) {
                                if (action == null || !IsOneShot(action.action)) continue;
                                string stateName = action.state.StartsWith("Base Layer.", StringComparison.Ordinal) ? action.state.Substring(11) : action.state;
                                foreach (var child in controller.layers[0].stateMachine.states)
                                    if (child.state.name == stateName && child.state.motion is AnimationClip clip && clip.isLooping) oneShots.Add(action.action + ":" + clip.name);
                            }
                        }
                        if (missing.Count > 0 || missingBindings.Count > 0 || oneShots.Count > 0) errors++;
                        checkedForms++;
                        lines.AppendLine(string.Join(",", Csv(family.definitionId), Csv(form.formId), "1", "Generic valid",
                            Csv(string.Join(";", missing)), Csv(string.Join(";", missingBindings)), Csv(string.Join(";", oneShots)), Csv(form.typingStatus.ToString())));
                    } catch (Exception error) {
                        errors++;
                        lines.AppendLine(Csv(family.definitionId) + "," + Csv(form.formId) + ",ERROR," + Csv(error.Message));
                    } finally {
                        if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                        EditorSceneManager.ClosePreviewScene(scene);
                    }
                }
            }
            var factory = AssetDatabase.LoadAssetAtPath<Capstone.Game.HudSystem.PetPrefabCatalog>("Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset");
            foreach (string form in new[] { "01", "02", "03" }) {
                bool resolved = factory != null && factory.TryGetPrefab("Dragon Dusk", form, out var pet) && pet != null;
                if (!resolved) errors++;
                lines.AppendLine("Factory,Dragon Dusk/" + form + "," + (resolved ? "RESOLVED" : "MISSING"));
            }
            string directory = UISystem.QaArtifactPaths.Root + "creature-animation-types-2026-11-18/";
            Directory.CreateDirectory(directory);
            string output = Path.Combine(directory, "editor-bindings-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
            File.WriteAllText(output, lines.ToString(), new UTF8Encoding(false));
            File.AppendAllText(Path.Combine(directory, "editor-created-paths.txt"), output + Environment.NewLine);
            Debug.Log("[Creature Animation] Editor binding check: " + checkedForms + " forms, " + errors + " issues. " + output
                + ". Khong phai Play verification; khong sua scene, prefab hay save.");
        }

        [MenuItem("Tools/Capstone/Creatures/Verify pre-B2 animation and evolution (read-only)")]
        public static void VerifyPreB2()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play trước khi kiểm tra.");
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>("Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset");
            if (library == null || catalog == null) throw new InvalidOperationException("Thiếu Creature Library hoặc runtime catalog.");

            var results = new List<string>();
            int failures = 0;
            void Check(bool passed, string name)
            {
                if (!passed) failures++;
                results.Add((passed ? "PASS " : "FAIL ") + name);
            }

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var dragons = library.families.Single(f => f.definitionId == "Dragon Dusk");
                foreach (string formId in new[] { "02", "03" })
                    VerifyDragonGraph(dragons.forms.Single(f => f.formId == formId), scene, Check);

                var cats = library.families.Single(f => f.definitionId == "cat-electric");
                VerifyEvolutionScale(catalog, cats.forms.Single(f => f.formId == "02"), "01", scene, Check);
                VerifyEvolutionScale(catalog, cats.forms.Single(f => f.formId == "03"), "02", scene, Check);
            }
            catch (Exception error) { Check(false, error.ToString()); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }

            string directory = Path.Combine(UISystem.QaArtifactPaths.Root, "pre-b2-animation-evolution-2026-11-19");
            Directory.CreateDirectory(directory);
            string output = Path.Combine(directory, "editor-verification.txt");
            File.WriteAllLines(output, new[] { "EDITOR PREVIEW. Checks=" + results.Count + ", failures=" + failures }.Concat(results));
            string summary = "[Pre-B2] " + results.Count + " Editor checks, " + failures + " failures. " + output;
            if (failures > 0) Debug.LogError(summary); else Debug.Log(summary);
        }

        static void VerifyDragonGraph(CreatureFormDefinition form, Scene scene, Action<bool, string> check)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(form.visualPrefab, scene);
            var animator = visual.GetComponentInChildren<Animator>(true);
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var graph = controller.layers[0].stateMachine;
            string label = "Dragon " + form.formId + ": ";
            string idle = form.animationProfile.actions.Single(a => a.action == CreatureAction.Idle).state;
            check(graph.defaultState != null && graph.defaultState.name == idle, label + "Idle is default");
            check(controller.layers.Length == 1 && graph.stateMachines.Length == 0
                && graph.entryTransitions.Length == 0 && graph.anyStateTransitions.Length == 0
                && graph.states.All(s => s.state.transitions.Length == 0), label + "no Animator-driven transitions");

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0);
            var driver = visual.AddComponent<CreatureAnimationDriver>();
            driver.Bind(animator);
            foreach (var action in form.animationProfile.actions)
            {
                bool resolved = driver.TryGetStates(action.action, out _);
                var state = graph.states.SingleOrDefault(s => s.state.name == action.state).state;
                var clip = state != null ? state.motion as AnimationClip : null;
                check(resolved && clip != null && clip.length > 0, label + action.action + " resolves to a real clip");
                if (!resolved || clip == null) continue;
                if (IsOneShot(action.action)) check(!clip.isLooping, label + action.action + " is one-shot");
                if (action.action == CreatureAction.Idle || action.action == CreatureAction.Move || action.action == CreatureAction.Fly)
                    check(clip.isLooping, label + action.action + " loops");
                bool played = driver.TryPlay(action.action);
                animator.Update(.12f);
                animator.Update(clip.length + .25f);
                check(played && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + action.state),
                    label + action.action + " stays in its state until the next explicit command");
            }

            foreach (var skill in form.animationProfile.skills)
                check(driver.TryGetSkillStates(skill.skillId, out _), label + "skill " + skill.skillId + " resolves");
            check(driver.TryPlay(CreatureAction.Idle), label + "driver can return to Idle explicitly");
            animator.Update(.12f);
            animator.Update(8f);
            check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + idle), label + "Idle does not start a demo cycle");
        }

        static void VerifyEvolutionScale(PetPrefabCatalog catalog, CreatureFormDefinition form, string previousId,
            Scene scene, Action<bool, string> check)
        {
            string label = "Electric Cat " + form.formId + ": ";
            if (!catalog.TryGetPrefab("cat-electric", previousId, out var previous)
                || !catalog.TryGetPrefab("cat-electric", form.formId, out var target))
                throw new InvalidOperationException(label + "catalog lookup failed");

            var authored = form.visualPrefab.transform.localScale;
            var direct = (GameObject)PrefabUtility.InstantiatePrefab(target.gameObject, scene);
            CheckVisual(direct, "direct/factory prefab");
            var evolved = (GameObject)PrefabUtility.InstantiatePrefab(previous.gameObject, scene);
            var metadata = evolved.GetComponent<PetCollectionMetadata>();
            var stats = evolved.GetComponent<PetHudRuntimeStats>();
            var evolution = evolved.GetComponent<PetEvolutionVisualController>();
            var rule = metadata.EvolutionRules.Single(r => r.TargetFormId == form.formId);
            check(rule.TargetVisualPrefab == form.visualPrefab && metadata.ApplyEvolution(rule), label + "applied correct form rule");
            check(evolution.TryApply(rule.TargetVisualPrefab, out var error), label + "live visual swap " + error);
            CheckVisual(evolved, "evolved");
            Transform applied = evolution.ActiveVisualTransform;
            check(evolution.TryApply(rule.TargetVisualPrefab, out _) && evolution.ActiveVisualTransform == applied
                && applied.localScale == authored, label + "reapply is idempotent");

            var loaded = (GameObject)PrefabUtility.InstantiatePrefab(target.gameObject, scene);
            var loadedMetadata = loaded.GetComponent<PetCollectionMetadata>();
            var loadedStats = loaded.GetComponent<PetHudRuntimeStats>();
            loadedMetadata.AssignPersistentId(metadata.PersistentId);
            loadedMetadata.RestoreFromSaveData(metadata.CreateSaveData());
            loadedStats.RestoreFromSaveData(stats.CreateSaveData());
            loadedMetadata.RestoreEvolutionPresentation(loadedStats);
            check(loadedMetadata.CurrentFormId == form.formId && loadedMetadata.PersistentId == metadata.PersistentId,
                label + "snapshot restore preserves form and PetId");
            CheckVisual(loaded, "restored snapshot");

            var capsule = evolved.GetComponent<CapsuleCollider>();
            var agent = evolved.GetComponent<NavMeshAgent>();
            var binding = stats.VisualAnimator.GetComponent<CreatureVisualBinding>();
            var presentation = evolved.GetComponent<PetSkillPresentationPlayer>();
            check(capsule != null && agent != null && capsule.height > 0 && Mathf.Approximately(capsule.height, agent.height)
                && Mathf.Approximately(capsule.radius, agent.radius), label + "evolved capsule and NavMeshAgent match");
            check(presentation.sockets.Count > 0 && presentation.sockets.SequenceEqual(binding.sockets)
                && presentation.sockets.All(s => s.anchor != null && s.anchor.IsChildOf(applied)), label + "sockets use evolved rig");

            void CheckVisual(GameObject actor, string step)
            {
                var visual = actor.GetComponent<PetEvolutionVisualController>().ActiveVisualTransform;
                var animator = visual.GetComponentInChildren<Animator>(true);
                var visualBinding = animator.GetComponent<CreatureVisualBinding>();
                check(visual.localScale == authored && visual.lossyScale == direct.GetComponent<PetEvolutionVisualController>().ActiveVisualTransform.lossyScale,
                    label + step + " matches authored scale " + authored);
                check(visualBinding != null && visualBinding.animationProfile == form.animationProfile,
                    label + step + " uses the authored animation profile");
            }
        }

        static bool IsOneShot(CreatureAction action) => action == CreatureAction.Spawn || action == CreatureAction.Attack
            || action == CreatureAction.Bite || action == CreatureAction.Cast || action == CreatureAction.Blast
            || action == CreatureAction.Projectile || action == CreatureAction.WingStrike || action == CreatureAction.Hit || action == CreatureAction.Death;
        static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}

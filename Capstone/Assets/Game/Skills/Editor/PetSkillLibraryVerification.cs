using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.Skills.Editor {
    /// <summary>Explicit tests in an empty scene: no PlayerSaveController and no access to the user's save store.</summary>
    public static class PetSkillLibraryVerification {
        public const string Root = QaArtifactPaths.Root + "pet-skill-library-2026-10-22/";
        const string PlayFlag = "Capstone.PetSkillLibrary.PlayQA";
        const string LiveFlag = "Capstone.PetSkillLibrary.LiveQA";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> results = new List<string>();
        static bool running;
        static void Check(bool valid, string message) {
            results.Add((valid ? "PASS " : "FAIL ") + message);
            Directory.CreateDirectory(Root); File.WriteAllLines(Root + "checks.txt", results);
            if (!valid) throw new InvalidOperationException(message);
        }
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static PetHudRuntimeStats Stats(string name, PetSpeciesSkillSet set) {
            var go = new GameObject(name); var stats = go.AddComponent<PetHudRuntimeStats>(); stats.ConfigureSkillSet(set, false); return stats;
        }
        public static void RunEditor() {
            results.Clear();
            var catalog = AssetDatabase.LoadAssetAtPath<PetSkillCatalog>(PetSkillLibrarySetup.CatalogPath);
            Check(catalog != null && catalog.skills.Count == 4, "four existing skill definitions in shared catalog");
            Check(catalog.speciesSets.Count == 3, "three species/form learnsets connected");
            Check(catalog.TryGet(" WING STRIKE ", out var wing) && wing.Id == "wing strike", "saved ID with spaces retained, trim/case lookup");
            foreach (var set in catalog.speciesSets) Check(set.ValidateSet().Count == 0, "valid species " + set.speciesId);
            foreach (var path in PetSkillLibrarySetup.PrefabPaths) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var stats = root.GetComponentInChildren<PetHudRuntimeStats>(true);
                    Check(stats.SpeciesSkillSet != null && stats.GetSkills().Count == 4, "prefab bound without losing slots: " + root.name);
                    root.GetComponentInChildren<PetCollectionMetadata>(true)?.SyncRuntimeHealth(stats);
                    var before = stats.CreateSaveData();
                    stats.RestoreFromSaveData(JsonUtility.FromJson<PetRuntimeStatsSaveData>(JsonUtility.ToJson(before)));
                    File.WriteAllText(Root + root.name + "-roundtrip.json", "{\"before\":" + JsonUtility.ToJson(before) + ",\"after\":" + JsonUtility.ToJson(stats.CreateSaveData()) + "}");
                    Check(JsonUtility.ToJson(before) == JsonUtility.ToJson(stats.CreateSaveData()), "existing save schema round trip: " + root.name);
                    Check(root.GetComponentInChildren<PetSkillPresentationPlayer>(true) != null, "presentation component on " + root.name);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var a = ScriptableObject.CreateInstance<PetSkillDefinition>(); a.skillId = "qa.shared"; a.displayName = "Shared"; a.cooldownSeconds = 2; a.legacyIds = new[] { "qa.old" };
            var b = ScriptableObject.CreateInstance<PetSkillDefinition>(); b.skillId = "qa.unlock"; b.displayName = "Unlock";
            var library = ScriptableObject.CreateInstance<PetSkillCatalog>(); library.skills.Add(a); library.skills.Add(b);
            var setA = ScriptableObject.CreateInstance<PetSpeciesSkillSet>(); setA.speciesId = "QA A"; setA.catalog = library;
            setA.learnset.Add(new PetSpeciesSkillEntry { skill = a }); setA.learnset.Add(new PetSpeciesSkillEntry { skill = b, learnAtLevel = 5 }); setA.defaultLoadout.Add(a);
            var setB = ScriptableObject.CreateInstance<PetSpeciesSkillSet>(); setB.speciesId = "QA B"; setB.catalog = library;
            setB.learnset.Add(new PetSpeciesSkillEntry { skill = a }); setB.defaultLoadout.Add(a);
            PetHudRuntimeStats first = null, second = null, restored = null;
            try {
                first = Stats("QA A", setA); second = Stats("QA B", setB); restored = Stats("QA restored", setA);
                Check(first.GetLearnedSkills().Count == 1 && second.GetLearnedSkills().Count == 1, "species learn-level gate and no foreign skills");
                a.displayName = "Edited once";
                Check(first.GetSkills()[0].displayName == a.displayName && second.GetSkills()[0].displayName == a.displayName, "one definition update reaches two species");
                first.UnlockSkillsUpToLevel(5, out _); first.SetIdentity("QA A", 5);
                Check(first.GetLearnedSkills().Count == 2 && second.GetLearnedSkills().Count == 1, "individual unlock does not mutate another pet");
                Check(first.TryEquipLearnedSkill(1, 1), "equip species skill into second slot");
                var snapshot = first.CreateSaveData(); snapshot.equippedSkillIds[0] = "qa.old"; snapshot.learnedSkillProgress[0].skillId = "qa.old";
                Check(restored.CanRestoreSaveData(snapshot, out _), "legacy aliases accepted by unchanged save data");
                restored.RestoreFromSaveData(snapshot);
                Check(restored.GetSkills()[0].skillId == "qa.shared" && restored.GetSkills()[1].skillId == "qa.unlock", "restore order resolves canonical IDs");
                var empty = restored.CreateSaveData(); empty.equippedSkillIds = new List<string> { "", "" };
                restored.RestoreFromSaveData(empty);
                Check(restored.GetSkills().Count == 2 && restored.GetSkills().All(s => string.IsNullOrEmpty(s.skillId)), "explicit empty saved slots not refilled by species defaults");
                var invalid = first.CreateSaveData(); invalid.equippedSkillIds.Add("nonexistent");
                Check(!first.CanRestoreSaveData(invalid, out _), "unknown saved ID rejected before restore");
                var duplicate = ScriptableObject.CreateInstance<PetSkillDefinition>(); duplicate.skillId = "qa.shared"; library.skills.Add(duplicate); library.Invalidate();
                Check(library.ValidateCatalog().Count > 0, "duplicate ID detected");
                library.skills.Remove(duplicate); Object.DestroyImmediate(duplicate); library.Invalidate();
                Check(a.CreateHudData().skillLevel == 1 && a.CreateHudData().cooldownRemainingSeconds == 0, "definition holds no individual progress/cooldown");
            } finally {
                if (first != null) Object.DestroyImmediate(first.gameObject); if (second != null) Object.DestroyImmediate(second.gameObject); if (restored != null) Object.DestroyImmediate(restored.gameObject);
                Object.DestroyImmediate(setA); Object.DestroyImmediate(setB); Object.DestroyImmediate(library); Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            }
            results.Add("COMPLETE Editor data/save fixtures; no gameplay save files opened."); File.WriteAllLines(Root + "checks.txt", results);
        }

        public static void SetupAndPlayBatch() {
            try {
                PetSkillLibrarySetup.Setup(); PetSkillLibrarySetup.Validate(); RunEditor();
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Refuse replacing a dirty scene.");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(PlayFlag, true); EditorApplication.EnterPlaymode();
            } catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        [InitializeOnLoadMethod] static void Initialize() {
            EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
        }
        static void Poll() {
            if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Root + "request.txt")) return;
            string request;
            try { request = File.ReadAllText(Root + "request.txt").Trim(); File.Delete(Root + "request.txt"); }
            catch (IOException) { return; } // An external writer may still be completing its atomic request.
            try {
                PetSkillLibrarySetup.Setup(); PetSkillLibrarySetup.Validate(); RunEditor();
                if (request == "play") {
                    File.WriteAllText(Root + "isolate.flag", "isolated QA");
                    SessionState.SetBool(LiveFlag, true); SessionState.SetBool(PlayFlag, true);
                    EditorApplication.EnterPlaymode();
                } else File.WriteAllText(Root + "status.txt", "COMPLETE Editor checks");
            } catch (Exception exception) { File.WriteAllText(Root + "status.txt", "ERROR " + exception); Debug.LogException(exception); }
        }
        static void OnPlay(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayFlag, false) && !running) RunPlay();
        }
        static async void RunPlay() {
            running = true; bool passed = false;
            results.Clear(); if (File.Exists(Root + "checks.txt")) results.AddRange(File.ReadAllLines(Root + "checks.txt"));
            var owned = new List<Object>();
            try {
                bool live = SessionState.GetBool(LiveFlag, false);
                var saves = Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(saves.Length == 0 || (live && File.Exists(Root + "isolation.txt") && saves.All(s => Path.GetFullPath(s.SavePath).StartsWith(Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase))), "runtime fixture is isolated from all live player save stores");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(PetSkillLibrarySetup.PrefabPaths[0]);
                var root = Object.Instantiate(source); owned.Add(root);
                var pet = root.GetComponentInChildren<PetController>(true);
                var stats = root.GetComponentInChildren<PetHudRuntimeStats>(true);
                var player = stats.GetComponent<PetSkillPresentationPlayer>();
                var second = Object.Instantiate(source); owned.Add(second);
                var other = second.GetComponentInChildren<PetHudRuntimeStats>(true);
                await Task.Delay(250);
                pet.owner = null; // Do not bind a test pet to the scene player's active-pet input gate.
                Check(stats.SpeciesSkillSet != null && stats.GetSkills().Count == 4, "runtime prefab resolves shared skill data");
                int rejected = 0; stats.SkillAnimationAccepted += _ => rejected++;
                stats.RequestSkill(0); Check(rejected == 0, "dormant pet emits no accepted presentation");
                // Activate only this isolated fixture; no world/navmesh/save mutation.
                typeof(PetController).GetProperty("IsSummoned").SetValue(pet, true);
                Set(pet, "state", Enum.Parse(typeof(PetController).GetField("state", Private).FieldType, "FollowOwner"));
                var profile = ScriptableObject.CreateInstance<PetSkillPresentationProfile>(); owned.Add(profile);
                profile.releaseDelaySeconds = 0.02f; profile.cast.lifetimeSeconds = 0.3f; profile.release.lifetimeSeconds = 0.3f; profile.impact.lifetimeSeconds = 0.3f;
                var effect = new GameObject("QA particle prefab"); owned.Add(effect);
                var particle = effect.AddComponent<ParticleSystem>(); particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var main = particle.main; main.playOnAwake = false; main.duration = 0.2f; main.startLifetime = 0.1f; effect.SetActive(false);
                var clip = AudioClip.Create("QA silence", 4410, 1, 44100, false); owned.Add(clip);
                profile.cast.effectPrefab = effect; profile.release.effectPrefab = effect; profile.release.sound = clip; profile.impact.effectPrefab = effect;
                // Use a transient species variant, never mutate the project definitions.
                var variant = Object.Instantiate(stats.SpeciesSkillSet); owned.Add(variant);
                variant.learnset = stats.SpeciesSkillSet.learnset.Select(e => new PetSpeciesSkillEntry {
                    skill = e.skill, learnAtLevel = e.learnAtLevel, overrideAnimation = e.overrideAnimation, animatorStates = e.animatorStates,
                    animationDuration = e.animationDuration, animationFade = e.animationFade, windupSeconds = e.windupSeconds, recoverySeconds = e.recoverySeconds,
                    presentationOverride = profile
                }).ToList(); variant.Invalidate(); stats.ConfigureSkillSet(variant);
                stats.RequestSkill(0); int token = player.ActiveCastId;
                Check(rejected == 1 && token > 0 && player.LiveEffectCount > 0, "accepted actual skill request starts cast cue");
                Check(stats.GetSkills()[0].IsCoolingDown && !other.GetSkills()[0].IsCoolingDown, "cooldown is per pet, not shared asset");
                int releaseBefore = player.ReleasedCount; stats.RequestSkill(0);
                Check(player.ActiveCastId == token && rejected == 1, "cooldown rejection cannot emit another cast");
                await Task.Delay(450);
                Check(player.ReleasedCount == releaseBefore + 1, "timed release fires once after pre-animation windup");
                Check(player.NotifyImpact(token, Vector3.one, Vector3.up) && !player.NotifyImpact(token, Vector3.one, Vector3.up), "real-impact token accepted once, duplicate blocked");
                await Task.Delay(450);
                Check(player.LiveEffectCount == 0 && player.PooledEffectCount > 0, "finite cues return to bounded pool");
                profile.releaseMode = PetSkillReleaseMode.AnimationEvent;
                stats.ConfigureSkillSet(variant); await Task.Delay(70); stats.RequestSkill(0); token = player.ActiveCastId;
                await Task.Delay(200);
                Check(player.NotifyRelease(token) && !player.NotifyRelease(token), "explicit animation-event release de-duplicates");
                stats.ConfigureSkillSet(variant); await Task.Delay(70); stats.RequestSkill(0); token = player.ActiveCastId;
                player.CancelPresentation();
                Check(player.LiveEffectCount == 0 && !player.NotifyRelease(token) && !player.NotifyImpact(token, Vector3.zero, Vector3.up), "cancel invalidates release/impact and all active cues");
                stats.ConfigureSkillSet(variant); await Task.Delay(70); stats.RequestSkill(0);
                typeof(PetController).GetProperty("IsSummoned").SetValue(pet, false); await Task.Delay(90);
                Check(player.LiveEffectCount == 0 && player.ActiveCastId == 0, "recall/dormant transition cleans presentation");
                Check(Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == saves.Length, "runtime fixtures did not create a save controller");
                passed = true; results.Add("COMPLETE Play Mode: actual request/animation acceptance + ParticleSystem/AudioSource lifecycle. Not an artistic VFX/audio approval.");
            } catch (Exception exception) { results.Add("ERROR " + exception); Debug.LogException(exception); }
            finally {
                foreach (var item in owned) if (item != null) Object.Destroy(item);
                await Task.Delay(100); File.WriteAllLines(Root + "checks.txt", results);
                SessionState.SetBool(PlayFlag, false); running = false;
                bool live = SessionState.GetBool(LiveFlag, false); SessionState.SetBool(LiveFlag, false);
                if (File.Exists(Root + "isolate.flag")) File.Delete(Root + "isolate.flag");
                File.WriteAllText(Root + "status.txt", passed ? "COMPLETE Editor + Play Mode" : "FAILED see checks.txt");
                EditorApplication.ExitPlaymode();
                if (!live) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
            }
        }
    }
}

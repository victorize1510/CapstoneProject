using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.CreatureSystem.Editor {
    public static class CreatureMotionAudioAudit {
        public static string Root => Path.Combine(UISystem.QaArtifactPaths.Root, "animation-return-skill-audit-2026-11-21");
        static string Csv(object value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        static void Row(StringBuilder csv, params object[] cells) => csv.AppendLine(string.Join(",", cells.Select(Csv)));

        [MenuItem("Tools/Capstone/Creatures/Audit 33 locomotion and audio (read-only)")]
        public static void Audit() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play trước khi audit.");
            Directory.CreateDirectory(Root);
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
            if (library == null) throw new InvalidOperationException("Missing CreatureLibrary.");
            var rows = new StringBuilder();
            Row(rows, "Family", "Form", "Role", "Prefab", "Profile", "Controller", "Idle", "Walk", "Run", "Move/Fly/Crawl", "RootMotion", "Avatar", "Issues", "ActualSpeed", "Status");
            foreach (var family in library.families) foreach (var form in family.forms) {
                foreach (bool owned in new[] { true, false }) {
                    var prefab = owned ? form.petPrefab : form.wildPrefab;
                    var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
                    var binding = animator != null ? animator.GetComponent<CreatureVisualBinding>() : null;
                    var profile = binding != null ? binding.animationProfile : null;
                    var issues = new List<string>();
                    var raw = animator != null ? animator.runtimeAnimatorController : null;
                    var controller = raw as AnimatorController;
                    var overrides = raw as AnimatorOverrideController;
                    if (overrides != null) controller = overrides.runtimeAnimatorController as AnimatorController;
                    var states = controller != null ? Enumerate(controller.layers[0].stateMachine).ToArray() : Array.Empty<AnimatorState>();
                    string Motion(CreatureAction action) {
                        var entry = profile?.actions.FirstOrDefault(a => a.action == action);
                        if (entry == null) return "thiếu mapping";
                        var name = entry.state.StartsWith("Base Layer.") ? entry.state.Substring(11) : entry.state;
                        var state = states.FirstOrDefault(s => s.name == name || entry.state.EndsWith("." + s.name, StringComparison.Ordinal));
                        var motion = state?.motion as AnimationClip;
                        if (motion != null && overrides != null) motion = overrides[motion] ?? motion;
                        if (state == null || motion == null) { issues.Add("Missing state/clip " + entry.state); return entry.state + " (missing clip)"; }
                        return entry.state + " → " + motion.name + " | " + motion.length.ToString("0.###") + "s | loop=" + motion.isLooping;
                    }
                    if (animator == null || profile == null || controller == null) issues.Add("Missing animator/profile/controller");
                    if (controller != null && controller.layers[0].stateMachine.defaultState?.name != profile?.actions.FirstOrDefault(a => a.action == CreatureAction.Idle)?.state)
                        issues.Add("Default state differs from Idle mapping (inspect lifecycle)");
                    foreach (var state in states) {
                        if (state.transitions.Any(t => t.conditions.Length == 0)) issues.Add("Automatic transition from " + state.name);
                        if (state.motion is AnimationClip clip && clip.events.Length > 0)
                            issues.Add("AnimationEvents " + state.name + ": " + string.Join("/", clip.events.Select(e => e.functionName)));
                    }
                    string idle = Motion(CreatureAction.Idle), walk = Motion(CreatureAction.Walk), run = Motion(CreatureAction.Run);
                    string move = Motion(CreatureAction.Move);
                    Row(rows, family.definitionId, form.formId, owned ? "Owned" : "Wild", AssetDatabase.GetAssetPath(prefab),
                        AssetDatabase.GetAssetPath(profile), AssetDatabase.GetAssetPath(raw), idle, walk, run, move,
                        animator != null && animator.applyRootMotion, animator?.avatar != null ? "valid=" + animator.avatar.isValid + ", human=" + animator.isHuman : "missing",
                        string.Join("; ", issues), "CHƯA ĐO PLAY", "EDITOR VERIFIED configuration");
                }
            }
            File.WriteAllText(Path.Combine(Root, "animation-66.csv"), rows.ToString());
            var audioRows = new StringBuilder();
            Row(audioRows, "Path", "DurationSeconds", "Bytes", "Channels", "Frequency", "LoadType", "Compression", "ForceToMono", "Preload", "Guid", "Issues");
            var clips = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds" })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (clip == null || importer == null) continue;
                clips.Add(clip);
                var setting = importer.defaultSampleSettings;
                string issue = clip.channels > 1 && clip.length < 10 ? "Short stereo SFX: review Mono if spatial sound" : "";
                if (clip.length < 2 && !setting.preloadAudioData) issue += "; short clip without preload: check first-play latency";
                if (new FileInfo(path).Length > 1024 * 1024) issue += "; >1 MiB";
                Row(audioRows, path, clip.length, new FileInfo(path).Length, clip.channels, clip.frequency, setting.loadType,
                    setting.compressionFormat, importer.forceToMono, setting.preloadAudioData, guid, issue);
            }
            File.WriteAllText(Path.Combine(Root, "audio-all.csv"), audioRows.ToString());
            var cryRows = new StringBuilder();
            Row(cryRows, "Family", "Form", "DisplayName", "Candidate", "Status");
            foreach (var family in library.families) foreach (var form in family.forms) {
                var match = clips.Where(c => AssetDatabase.GetAssetPath(c).Contains("/Pet Cry/")
                    && string.Equals(c.name, form.displayName, StringComparison.OrdinalIgnoreCase)).ToArray();
                Row(cryRows, family.definitionId, form.formId, form.displayName,
                    string.Join(";", match.Select(AssetDatabase.GetAssetPath)), match.Length == 1 ? "NAME MATCH only" : "MANUAL NAME REVIEW");
            }
            File.WriteAllText(Path.Combine(Root, "pet-cry-33.csv"), cryRows.ToString());
            var catalog = Resources.Load<Skills.PetSkillCatalog>("PetSkills/PetSkillCatalog");
            var skillRows = new StringBuilder();
            Row(skillRows, "Id", "Name", "Presentation", "CastSound", "ReleaseSound", "ImpactSound", "OverrideSet", "OverrideSkill");
            if (catalog != null) {
                foreach (var skill in catalog.skills) if (skill != null) Add(skill.Id, skill.displayName, skill.presentation, "", "");
                foreach (var set in catalog.speciesSets) if (set != null)
                    foreach (var entry in set.learnset) if (entry?.skill != null && entry.presentationOverride != null)
                        Add(entry.skill.Id, entry.skill.displayName, entry.presentationOverride, AssetDatabase.GetAssetPath(set), entry.skill.Id);
            }
            void Add(string id, string name, Skills.PetSkillPresentationProfile p, string set, string overrideId) {
                Row(skillRows, id, name, AssetDatabase.GetAssetPath(p), AssetDatabase.GetAssetPath(p?.cast.sound),
                    AssetDatabase.GetAssetPath(p?.release.sound), AssetDatabase.GetAssetPath(p?.impact.sound), set, overrideId);
            }
            File.WriteAllText(Path.Combine(Root, "skill-audio-bindings.csv"), skillRows.ToString());
            Debug.Log("Animation/audio audit finished: " + Root);
        }
        static IEnumerable<AnimatorState> Enumerate(AnimatorStateMachine machine) {
            foreach (var state in machine.states) yield return state.state;
            foreach (var nested in machine.stateMachines) foreach (var state in Enumerate(nested.stateMachine)) yield return state;
        }
    }
}

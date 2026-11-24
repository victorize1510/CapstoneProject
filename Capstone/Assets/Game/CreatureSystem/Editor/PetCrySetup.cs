using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Capstone.Game.CreatureSystem.EditorTools
{
    public static class PetCrySetup
    {
        const string Menu = "Tools/Capstone/Audio/Setup 33 Pet Cries/";
        const string LibraryPath = "Assets/Game/CreatureSystem/Data/CreatureLibrary.asset";
        const string SoundRoot = "Assets/Sounds/Trung/SFX";
        const string BatFbx = "Assets/UnityAssset/Trung/Monsters Ultimate Pack 01 Cute Series/Bat Cute Series/FBX/Bat@";

        // These names belong to the source recordings. The library continues to own all display names and IDs.
        static readonly Dictionary<string, string> SourceNames = new Dictionary<string, string>
        {
            ["dragon-fire/01"] = "Rồng Trẻ Con",
            ["Dragon Dusk/03"] = "Rồng Ác Mộng",
            ["snake-fire/03"] = "Rắn Ba Đầu",
            ["wolf/03"] = "Ma Sói"
        };

        [MenuItem(Menu + "Scan")]
        public static void Scan() => Run(false, false);

        [MenuItem(Menu + "Apply")]
        public static void Apply() => Run(true, true);

        [MenuItem(Menu + "Validate")]
        public static void Validate() => Run(false, true);

        public static void ApplyBatch() => Apply();
        public static void ValidateBatch() => Validate();

        static void Run(bool apply, bool validate)
        {
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(LibraryPath);
            if (library == null)
                throw new InvalidOperationException("Cannot load the creature authoring library.");

            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { SoundRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .GroupBy(path => Normalize(Path.GetFileNameWithoutExtension(path)))
                .ToDictionary(group => group.Key, group => group.ToArray());
            var rows = new List<string>();
            var errors = new List<string>();
            var clipsUsed = new HashSet<string>(StringComparer.Ordinal);
            var visualsUsed = new HashSet<string>(StringComparer.Ordinal);
            var alternateVisuals = new HashSet<string>(StringComparer.Ordinal);
            int configured = 0;
            int roles = 0;
            int embeddedVisuals = 0;
            bool migrateCast = apply && HasBatCryEvent("Cast Spell");
            bool migrateDeath = apply && HasBatCryEvent("Die");
            bool batConfigured = false;

            foreach (var family in library.families)
            foreach (var form in family.forms)
            {
                string key = family.definitionId + "/" + form.formId;
                string soundName = SourceNames.TryGetValue(key, out var alias) ? alias : form.displayName;
                if (!clips.TryGetValue(Normalize(soundName), out var paths) || paths.Length != 1)
                {
                    errors.Add($"{key} {form.displayName}: {(paths == null ? "no recording" : string.Join(", ", paths))}");
                    continue;
                }
                string path = paths[0];
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || !clipsUsed.Add(path))
                {
                    errors.Add($"{key}: invalid or reused recording {path}");
                    continue;
                }

                string visualPath = AssetDatabase.GetAssetPath(form.visualPrefab);
                try
                {
                    if (string.IsNullOrEmpty(visualPath) || !visualsUsed.Add(visualPath))
                        throw new InvalidOperationException("Missing or reused visual prefab.");
                    var binding = FindBinding(form.visualPrefab, form.animationProfile);
                    var animator = binding.GetComponent<Animator>();
                    CreatureAction expression = key == "ghost/01" ? CreatureAction.Spawn : CreatureAction.Cast;
                    string expressionMotion = MotionPath(animator, form.animationProfile, expression);
                    string deathMotion = MotionPath(animator, form.animationProfile, CreatureAction.Death);
                    string projectileMotion = MotionPath(animator, form.animationProfile, CreatureAction.Projectile, false);
                    bool bat = key == "bat/01";
                    var roleAssets = new List<(string path, CreatureAnimationProfile profile)>();
                    var embedded = new List<string>();
                    foreach (var actor in new[] { form.petPrefab, form.wildPrefab, form.enemyPrefab })
                    {
                        if (actor == null) continue; // Most families use their Wild asset for encounter actors.
                        string actorPath = AssetDatabase.GetAssetPath(actor);
                        var roleBinding = actor.GetComponentsInChildren<CreatureVisualBinding>(true).SingleOrDefault(candidate =>
                            candidate.animationProfile != null && candidate.animationProfile.definitionId == family.definitionId
                            && candidate.animationProfile.formId == form.formId);
                        if (roleBinding == null)
                            throw new InvalidOperationException($"{actor.name} has no matching visual for {key}.");
                        var roleAnimator = roleBinding.GetComponent<Animator>();
                        if (roleBinding.animationProfile == form.animationProfile)
                        {
                            if (roleAnimator.runtimeAnimatorController != animator.runtimeAnimatorController
                                || roleAnimator.avatar != animator.avatar)
                                throw new InvalidOperationException($"{actor.name} overrides the verified visual.");
                            // Older Chick and Electric Cat actors embed a copy of the visual in each role.
                            // A reference in their evolution list doesn't make the active Animator a nested instance.
                            if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(roleBinding, visualPath) == null)
                                embedded.Add(actorPath);
                        }
                        else
                        {
                            // Dragon Dusk's authored Enemy prefabs still use the separate Pilot visuals.
                            var profile = roleBinding.animationProfile;
                            var alternate = AssetDatabase.GetDependencies(actorPath, false)
                                .Where(candidate => candidate != actorPath && candidate.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                                .Single(candidate => candidate != null && candidate.GetComponentsInChildren<CreatureVisualBinding>(true)
                                    .Any(binding => binding.animationProfile == profile));
                            string alternatePath = AssetDatabase.GetAssetPath(alternate);
                            var alternateAnimator = FindBinding(alternate, profile).GetComponent<Animator>();
                            if (roleAnimator.runtimeAnimatorController != alternateAnimator.runtimeAnimatorController)
                                throw new InvalidOperationException($"{actor.name} overrides the alternate Animator Controller.");
                            string altExpression = MotionPath(roleAnimator, profile, expression);
                            string altDeath = MotionPath(roleAnimator, profile, CreatureAction.Death);
                            string altProjectile = MotionPath(roleAnimator, profile, CreatureAction.Projectile, false);
                            if (alternateVisuals.Add(alternatePath))
                            {
                                if (apply) Configure(alternatePath, profile, clip, expression, false, false);
                                rows.Add($"{key} Enemy\t{form.displayName}\t{path}\t{clip.length:F3}s\t{alternatePath}"
                                    + $"\t{altExpression}\t{altProjectile}\t{altDeath}");
                            }
                        }
                        roleAssets.Add((actorPath, roleBinding.animationProfile));
                        roles++;
                    }

                    if (apply)
                        Configure(visualPath, form.animationProfile, clip, expression, bat && migrateCast, bat && migrateDeath);
                    foreach (string actorPath in embedded)
                    {
                        if (apply) Configure(actorPath, form.animationProfile, clip, expression, false, false);
                        embeddedVisuals++;
                        rows.Add($"{key} Embedded\t{form.displayName}\t{path}\t{clip.length:F3}s\t{actorPath}"
                            + $"\t{expressionMotion}\t{projectileMotion}\t{deathMotion}");
                    }
                    if (validate)
                    {
                        var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
                        ValidateAudio(FindBinding(reloaded, form.animationProfile), clip);
                        foreach (var role in roleAssets)
                        {
                            var current = AssetDatabase.LoadAssetAtPath<GameObject>(role.path);
                            ValidateAudio(FindBinding(current, role.profile), clip);
                        }
                        configured++;
                        if (bat) batConfigured = true;
                    }

                    rows.Add($"{key}\t{form.displayName}\t{path}\t{clip.length:F3}s\t{visualPath}"
                        + $"\t{expressionMotion}\t{projectileMotion}\t{deathMotion}");
                }
                catch (Exception error)
                {
                    errors.Add($"{key} {form.displayName}: {error.Message}");
                }
            }

            if (batConfigured && apply)
            {
                RemoveSharedBatEvents("Cast Spell");
                RemoveSharedBatEvents("Die");
            }
            if (validate && (HasBatCryEvent("Cast Spell") || HasBatCryEvent("Die")))
                errors.Add("The shared vendor Bat clips still contain events requiring a receiver from the gameplay visual.");
            if (clipsUsed.Count != 33 || visualsUsed.Count != 33 || roles < 66 || validate && configured != 33)
                errors.Add("Expected 33 distinct recordings and visual prefabs and at least 66 Owned/Wild role prefabs.");

            string summary = $"Recordings {clipsUsed.Count}/33, validated {configured}/33, role prefabs {roles}, alternate visuals {alternateVisuals.Count}, embedded visuals {embeddedVisuals}.";
            string report = summary + "\nForm\tDisplay name\tAudioClip\tLength\tVisual\tExpression\tProjectile\tDeath\n"
                + string.Join("\n", rows) + "\n" + string.Join("\n", errors);
            Debug.Log("[Pet Cries] " + report);
            string output = Path.Combine(QaArtifactPaths.Root, "PetCries");
            try
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, apply ? "apply.tsv" : validate ? "validate.tsv" : "scan.tsv"), report);
            }
            catch (IOException error) { Debug.LogWarning(error.Message); }
            catch (UnauthorizedAccessException error) { Debug.LogWarning(error.Message); }
            if (errors.Count > 0)
            {
                string message = summary + "\n" + string.Join("\n", errors);
                if (Application.isBatchMode)
                    throw new InvalidOperationException(message);
                Debug.LogError(message);
            }
        }

        static string Normalize(string value)
        {
            var result = new StringBuilder();
            foreach (char letter in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            {
                if (letter == 'đ') result.Append('d');
                else if (char.IsLetterOrDigit(letter) && CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark)
                    result.Append(letter);
            }
            return result.ToString();
        }

        static CreatureVisualBinding FindBinding(GameObject root, CreatureAnimationProfile profile)
        {
            if (profile == null)
                throw new InvalidOperationException("Missing animation profile.");
            var bindings = root.GetComponentsInChildren<CreatureVisualBinding>(true)
                .Where(binding => binding.animationProfile == profile).ToArray();
            if (bindings.Length != 1)
                throw new InvalidOperationException($"{root.name}: expected exactly one visual for {profile.name}.");
            return bindings[0];
        }

        static string MotionPath(Animator animator, CreatureAnimationProfile profile, CreatureAction action, bool required = true)
        {
            var binding = profile.actions.SingleOrDefault(entry => entry.action == action);
            if (binding == null && !required) return "";
            if (binding == null || animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException($"Missing {action} on {profile.name}.");
            var runtime = animator.runtimeAnimatorController;
            var overrides = runtime as AnimatorOverrideController;
            var controller = (overrides != null ? overrides.runtimeAnimatorController : runtime) as AnimatorController;
            string path = binding.state.Contains(".") ? binding.state : "Base Layer." + binding.state;
            var layer = controller?.layers.FirstOrDefault(item => path.StartsWith(item.name + ".", StringComparison.Ordinal));
            var motion = layer != null ? FindMotion(layer.stateMachine, path.Substring(layer.name.Length + 1)) : null;
            if (!(motion is AnimationClip clip))
                throw new InvalidOperationException($"State {path} on {profile.name} has no AnimationClip.");
            if (overrides != null)
                clip = overrides[clip] ?? clip;
            return path + " → " + AssetDatabase.GetAssetPath(clip);
        }

        static Motion FindMotion(AnimatorStateMachine machine, string path)
        {
            foreach (var state in machine.states)
                if (state.state.name == path) return state.state.motion;
            foreach (var child in machine.stateMachines)
            {
                string prefix = child.stateMachine.name + ".";
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                    return FindMotion(child.stateMachine, path.Substring(prefix.Length));
            }
            return null;
        }

        static void Configure(string path, CreatureAnimationProfile profile, AudioClip clip, CreatureAction expression,
            bool migrateCast, bool migrateDeath)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var host = FindBinding(root, profile).gameObject;
                var sources = host.GetComponents<AudioSource>();
                if (sources.Length > 1)
                    throw new InvalidOperationException("Multiple AudioSources on the Animator. Review manually.");
                var source = sources.FirstOrDefault();
                if (source != null && source.clip != null && source.clip != clip)
                    throw new InvalidOperationException("Existing AudioSource is assigned to a different clip. Review manually.");
                var receiver = host.GetComponent<PetCryEventReceiver>();
                bool addedReceiver = receiver == null;
                bool changed = addedReceiver || source == null;
                if (source == null)
                {
                    source = host.AddComponent<AudioSource>();
                    source.spatialBlend = 1;
                    source.volume = .8f;
                    source.pitch = 1;
                    source.minDistance = 2;
                    source.maxDistance = 20;
                    source.dopplerLevel = 0;
                }
                if (source.clip != clip || source.playOnAwake || source.loop)
                {
                    source.clip = clip;
                    source.playOnAwake = source.loop = false;
                    changed = true;
                }
                if (addedReceiver)
                    receiver = host.AddComponent<PetCryEventReceiver>();
                var settings = new SerializedObject(receiver);
                var voice = settings.FindProperty("crySource");
                if (voice.objectReferenceValue != null && voice.objectReferenceValue != source)
                    throw new InvalidOperationException("Existing receiver references a different AudioSource. Review manually.");
                voice.objectReferenceValue = source;
                if (addedReceiver)
                {
                    settings.FindProperty("ambientAction").intValue = (int)expression;
                    settings.FindProperty("summonAction").intValue = (int)expression;
                }
                if (migrateCast)
                {
                    SetClipSeconds(settings, "ambient", .50884354f);
                    SetClipSeconds(settings, "skill", .50884354f);
                    source.dopplerLevel = 0;
                    changed = true;
                }
                if (migrateDeath)
                    SetClipSeconds(settings, "death", .13061221f);
                changed |= settings.ApplyModifiedPropertiesWithoutUndo();
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetClipSeconds(SerializedObject settings, string name, float seconds)
        {
            var timing = settings.FindProperty(name);
            timing.FindPropertyRelative("mode").enumValueIndex = 1;
            timing.FindPropertyRelative("clipSeconds").floatValue = seconds;
        }

        static void ValidateAudio(CreatureVisualBinding binding, AudioClip clip)
        {
            var host = binding.gameObject;
            var receivers = host.GetComponents<PetCryEventReceiver>();
            var sources = host.GetComponents<AudioSource>();
            if (receivers.Length != 1 || !receivers[0].enabled || sources.Length != 1 || !sources[0].enabled
                || sources[0].clip != clip || sources[0].playOnAwake || sources[0].loop
                || new SerializedObject(receivers[0]).FindProperty("crySource").objectReferenceValue != sources[0])
                throw new InvalidOperationException($"{AssetDatabase.GetAssetPath(binding)} / {host.name}: found "
                    + $"{receivers.Length} cry receivers and {sources.Length} AudioSources. Expected one enabled, wired pair.");
        }

        static bool IsSharedBatEvent(AnimationEvent marker) =>
            marker.functionName == "NewEvent" || marker.functionName == "PlayPetCry";

        static bool HasBatCryEvent(string animation)
        {
            var importer = AssetImporter.GetAtPath(BatFbx + animation + ".FBX") as ModelImporter;
            return importer != null && importer.clipAnimations.Any(clip => clip.events.Any(IsSharedBatEvent));
        }

        static void RemoveSharedBatEvents(string animation)
        {
            var importer = AssetImporter.GetAtPath(BatFbx + animation + ".FBX") as ModelImporter;
            if (importer == null || !HasBatCryEvent(animation)) return;
            var clips = importer.clipAnimations;
            foreach (var clip in clips)
                clip.events = clip.events.Where(marker => !IsSharedBatEvent(marker)).ToArray();
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.HudSystem;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.Skills.Editor {
    public static class PetSkillLibrarySetup {
        public const string DataRoot = "Assets/Game/Skills/Data";
        public const string CatalogPath = "Assets/Game/Skills/Resources/PetSkills/PetSkillCatalog.asset";
        public static readonly string[] PrefabPaths = {
            "Assets/Game/HudSystem/Prefabs/Dragon01Gameplay.prefab",
            "Assets/Game/HudSystem/Prefabs/Dragon02Gameplay.prefab",
            "Assets/Game/HudSystem/Prefabs/Dragon03Gameplay.prefab"
        };
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/Capstone/Pet Skills/1. Create or Connect Existing Dragon Library")]
        public static void Setup() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before editing the skill library.");
            var catalog = LoadOrCreate<PetSkillCatalog>(CatalogPath);
            foreach (string path in PrefabPaths) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var stats = root.GetComponentInChildren<PetHudRuntimeStats>(true);
                    var metadata = root.GetComponentInChildren<PetCollectionMetadata>(true);
                    if (stats == null || metadata == null) throw new InvalidOperationException(path + " lacks stats/metadata.");
                    var oldSkills = ((List<SkillHudData>)typeof(PetHudRuntimeStats).GetField("skills", Private).GetValue(stats)).ToArray();
                    var oldLearned = ((List<SkillHudData>)typeof(PetHudRuntimeStats).GetField("learnedSkills", Private).GetValue(stats)).ToArray();
                    var set = stats.SpeciesSkillSet;
                    bool createSet = set == null;
                    if (createSet) set = LoadOrCreate<PetSpeciesSkillSet>(DataRoot + "/Species/" + Path.GetFileNameWithoutExtension(path).Replace("Gameplay", "") + ".asset");
                    bool initializeSet = string.IsNullOrWhiteSpace(set.speciesId);
                    if (initializeSet) {
                        set.speciesId = metadata.Species; set.catalog = catalog;
                        foreach (var legacy in oldLearned.Concat(oldSkills)) {
                            if (string.IsNullOrWhiteSpace(legacy.skillId)) continue;
                            if (!catalog.TryGet(legacy.skillId, out var skill)) {
                                string filename = legacy.skillId.Replace(' ', '_');
                                string definitionPath = DataRoot + "/Definitions/Skill_" + filename + ".asset";
                                bool newDefinition = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>(definitionPath) == null;
                                skill = LoadOrCreate<PetSkillDefinition>(definitionPath);
                                if (newDefinition) {
                                    skill.skillId = legacy.skillId; skill.legacyIds = legacy.legacySkillIds ?? Array.Empty<string>();
                                    skill.displayName = legacy.displayName; skill.description = legacy.description;
                                    skill.icon = legacy.icon; skill.element = legacy.element; skill.cooldownSeconds = legacy.cooldownSeconds;
                                    skill.criticalRateBonus = legacy.criticalRateBonus; skill.criticalDamageBonus = legacy.criticalDamageBonus;
                                    skill.animatorStates = legacy.animatorStates; skill.animationDuration = legacy.animationDuration;
                                    skill.animationFade = legacy.animationFade; skill.windupSeconds = legacy.windupSeconds; skill.recoverySeconds = legacy.recoverySeconds;
                                    var profile = LoadOrCreate<PetSkillPresentationProfile>(DataRoot + "/Presentation/Presentation_" + filename + ".asset");
                                    profile.cast.socket = "Body"; profile.release.socket = "Mouth";
                                    profile.releaseDelaySeconds = Mathf.Min(0.25f, legacy.animationDuration * 0.5f);
                                    skill.presentation = profile; EditorUtility.SetDirty(skill); EditorUtility.SetDirty(profile);
                                }
                                if (!catalog.skills.Contains(skill)) catalog.skills.Add(skill);
                                catalog.Invalidate();
                            }
                            if (set.learnset.Any(e => e?.skill == skill)) continue;
                            set.learnset.Add(new PetSpeciesSkillEntry {
                                skill = skill, learnAtLevel = Mathf.Max(1, legacy.requiredPetLevel),
                                overrideAnimation = true, animatorStates = legacy.animatorStates,
                                animationDuration = legacy.animationDuration, animationFade = legacy.animationFade,
                                windupSeconds = legacy.windupSeconds, recoverySeconds = legacy.recoverySeconds
                            });
                        }
                        foreach (var legacy in oldSkills) if (catalog.TryGet(legacy.skillId, out var skill) && !set.defaultLoadout.Contains(skill)) set.defaultLoadout.Add(skill);
                        set.Invalidate(); EditorUtility.SetDirty(set);
                    }
                    if (!catalog.speciesSets.Contains(set)) catalog.speciesSets.Add(set);
                    stats.ConfigureSkillSet(set);
                    var player = stats.GetComponent<PetSkillPresentationPlayer>();
                    if (player == null) player = stats.gameObject.AddComponent<PetSkillPresentationPlayer>();
                    if (player.sockets.Count == 0) {
                        player.sockets.Add(new PetSkillSocket { key = "Body", anchor = stats.transform });
                        player.sockets.Add(new PetSkillSocket { key = "Ground", anchor = stats.transform });
                        player.sockets.Add(new PetSkillSocket { key = "Mouth" }); // Author must choose the real bone, never guess it.
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("Pet skill library connected. Existing IDs, slots and progress preserved; VFX/audio await author-supplied assets. See Assets/Game/Skills/README_VI.md.");
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            string current = "Assets";
            foreach (var part in folder.Split('/').Skip(1)) { if (!AssetDatabase.IsValidFolder(current + "/" + part)) AssetDatabase.CreateFolder(current, part); current += "/" + part; }
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        [MenuItem("Tools/Capstone/Pet Skills/2. Validate Library")]
        public static void Validate() {
            var errors = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:PetSkillCatalog")) {
                var catalog = AssetDatabase.LoadAssetAtPath<PetSkillCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                catalog.Invalidate(); foreach (var error in catalog.ValidateCatalog()) errors.Add(catalog.name + ": " + error);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:PetSpeciesSkillSet")) {
                var set = AssetDatabase.LoadAssetAtPath<PetSpeciesSkillSet>(AssetDatabase.GUIDToAssetPath(guid));
                set.Invalidate(); foreach (var error in set.ValidateSet()) errors.Add(set.name + ": " + error);
            }
            foreach (var path in PrefabPaths) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var stats = prefab != null ? prefab.GetComponentInChildren<PetHudRuntimeStats>(true) : null;
                if (stats == null || stats.SpeciesSkillSet == null) { errors.Add(path + ": missing species binding."); continue; }
                foreach (string field in new[] { "skills", "learnedSkills" }) {
                    var legacy = (List<SkillHudData>)typeof(PetHudRuntimeStats).GetField(field, Private).GetValue(stats);
                    foreach (var skill in legacy) if (!string.IsNullOrWhiteSpace(skill.skillId) && !stats.SpeciesSkillSet.TryGet(skill.skillId, out _)) errors.Add(path + ": unmapped legacy skill ID " + skill.skillId);
                }
            }
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("Pet skill catalogs and species learnsets valid.");
        }
        [MenuItem("Tools/Capstone/Pet Skills/3. Open Guide")]
        public static void OpenGuide() => Application.OpenURL(new Uri(Path.GetFullPath("Assets/Game/Skills/README_VI.md")).AbsoluteUri);

        public static void SetupBatch() {
            try { Setup(); Validate(); PetSkillLibraryVerification.RunEditor(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }

    [CustomEditor(typeof(PetSkillDefinition))]
    sealed class PetSkillDefinitionInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            EditorGUILayout.HelpBox("Shared definition: edit here once for every species using this skill. Never store cooldown/progress on this asset. Do not rename a saved ID without a legacy alias.", MessageType.Info);
            DrawDefaultInspector();
            var skill = (PetSkillDefinition)target;
            if (string.IsNullOrWhiteSpace(skill.Id)) EditorGUILayout.HelpBox("Set a unique, stable Skill ID.", MessageType.Error);
        }
    }
    [CustomEditor(typeof(PetSpeciesSkillSet))]
    sealed class PetSpeciesSkillSetInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            EditorGUILayout.HelpBox("Learnset = skills this species can learn. Default Loadout applies only to a new pet, never over a loaded save. Animator/socket configuration can differ per species.", MessageType.Info);
            DrawDefaultInspector();
            if (GUILayout.Button("Validate This Species")) {
                var set = (PetSpeciesSkillSet)target; set.Invalidate(); var errors = set.ValidateSet();
                if (errors.Count == 0) Debug.Log("Species valid: " + set.speciesId, set);
                else foreach (var error in errors) Debug.LogError(error, set);
            }
        }
    }
}

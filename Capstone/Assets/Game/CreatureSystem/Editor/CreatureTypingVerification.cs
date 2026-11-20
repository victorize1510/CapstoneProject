using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.CodexSystem;
using Capstone.Game.Elements;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor
{
    static class CreatureTypingVerification
    {
        const string CatalogPath = "Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset";

        [MenuItem("Tools/Capstone/Creatures/Verify canonical typing (read-only)")]
        static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Dừng Play Mode trước khi kiểm tra hệ.");

            var results = new List<string>();
            int failures = 0;
            void Check(bool passed, string message)
            {
                results.Add((passed ? "PASS " : "FAIL ") + message);
                if (!passed) failures++;
            }

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(CreatureLibraryAuthoring.LibraryPath);
                var factory = AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>(CatalogPath);
                var projection = AssetDatabase.LoadAssetAtPath<CreatureAvatarCatalog>(CreatureLibraryAuthoring.AvatarPath);
                Check(library != null && factory != null && projection != null, "Library, factory, projection");
                if (library == null || factory == null || projection == null)
                    throw new InvalidOperationException("Thiếu asset danh mục.");

                var firstOwned = library.families.SelectMany(f => f.forms).First(f => f.petPrefab != null).petPrefab;
                var root = (GameObject)PrefabUtility.InstantiatePrefab(firstOwned, scene);
                root.SetActive(false);
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int runtimeForms = 0;
                Check(library.families.Count == 11 && projection.entries.Count == 33, "11 families and 33 projected entries");
                foreach (var family in library.families)
                {
                    Check(family != null && family.forms.Count == 3, "Three forms: " + family?.definitionId);
                    if (family == null) continue;
                    foreach (var form in family.forms)
                    {
                        string key = family.definitionId + "/" + form.formId;
                        Check(keys.Add(key), key + " unique key");
                        Check(form.TryGetTyping(out var primary, out var secondary), key + " valid canonical pair");
                        foreach (var id in new[] { family.definitionId }.Concat(family.legacyDefinitionIds))
                        {
                            Check(CreatureAvatarCatalog.TryResolveTyping(id, form.formId, out var p, out var s)
                                && (p, s) == (primary, secondary), key + " runtime typing via " + id);
                            Check(CreatureDisplayNames.Species(id, form.formId, string.Empty) == form.displayName,
                                key + " official display name via " + id);
                        }
                        Check(projection.TryResolve(family.definitionId, form.formId, out var avatar) && avatar == form.avatar,
                            key + " existing avatar");
                        bool hasFactory = factory.TryGetPrefab(family.definitionId, form.formId, out var prefab);
                        if (form.petPrefab == null && form.wildPrefab == null)
                        {
                            Check(!hasFactory && form.enemyPrefab == null, key + " data only");
                            continue;
                        }

                        runtimeForms++;
                        Check(hasFactory && prefab.gameObject == form.petPrefab && form.wildPrefab != null, key + " factory and Wild");
                        if (form.petPrefab != null) VerifyActor(form.petPrefab, family, form, "Owned", Check);
                        if (form.wildPrefab != null) VerifyActor(form.wildPrefab, family, form, "Wild", Check);
                        if (form.enemyPrefab != null) VerifyActor(form.enemyPrefab, family, form, "Enemy", Check);
                        if (!hasFactory) continue;
                        bool created = factory.TryInstantiate(family.definitionId, form.formId, root.transform, out var instance);
                        Check(created, key + " factory instantiated");
                        if (!created) continue;
                        VerifySnapshot(instance, family, form, Check);
                    }
                }
                Check(keys.Count == 33, $"33 form keys, {runtimeForms} runtime and {33 - runtimeForms} data only");
                Check(!CreatureAvatarCatalog.TryResolveTyping("Dragon Dusk", "99", out _, out _), "Unknown form stays unresolved");
                Check(CreatureAvatarCatalog.TryResolveTyping("Dragon Dusk Elder", "dragon-dusk-elder", out var dark, out var dragon)
                    && (dark, dragon) == (PetElement.Dark, PetElement.Dragon), "Legacy Dragon ID and form alias resolve");
                VerifyCodex(Check);
                var table = ElementCombatSettings.Lookup;
                Check(table.IsValid, "Runtime matchup asset is valid");
                Check(ElementDamageMath.Effectiveness(table, PetElement.Fighting, PetElement.Flying, PetElement.Normal) == 1f,
                    "Chick: Fighting x0.5 against Flying and x2 against Normal");
                Check(ElementDamageMath.Multiplier(table, new CombatElementSnapshot(PetElement.Electric, PetElement.Electric, PetElement.Unknown),
                    PetElement.Flying, PetElement.Normal) == 3f, "Electric against Chick plus same-type bonus = x3");
            }
            catch (Exception e) { Check(false, e.ToString()); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }

            string dir = Path.Combine(QaArtifactPaths.Root, "creature-canonical-typing-2026-11-19");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "editor-verification.txt");
            File.WriteAllLines(path, new[] { $"EDITOR PREVIEW. Checks={results.Count}, failures={failures}" }.Concat(results));
            string summary = $"[Creature Typing] {results.Count} Editor checks, {failures} failures. {path}";
            if (failures == 0) Debug.Log(summary); else Debug.LogError(summary);
        }

        static void VerifyActor(GameObject prefab, CreatureFamilyDefinition family, CreatureFormDefinition form,
            string role, Action<bool, string> check)
        {
            string label = $"{family.definitionId}/{form.formId} {role}";
            var meta = prefab.GetComponentInChildren<PetCollectionMetadata>(true);
            check(HasTyping(meta, family, form), label + " serialized identity and typing");
            check(prefab.GetComponentsInChildren<Transform>(true)
                .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
                label + " no Missing Script");
            if (meta == null) return;
            foreach (var rule in meta.EvolutionRules)
            {
                var target = family.forms.Find(f => f.formId == rule.TargetFormId);
                check(target != null && (rule.TargetElement, rule.TargetSecondaryElement)
                    == (target.primaryElement, target.secondaryElement) && rule.TargetVisualPrefab != null,
                    label + " evolution target " + rule.TargetFormId);
            }
        }

        static bool HasTyping(PetCollectionMetadata meta, CreatureFamilyDefinition family, CreatureFormDefinition form) =>
            meta != null && meta.DefinitionId == family.definitionId && meta.CurrentFormId == form.formId
            && (meta.Element, meta.SecondaryElement) == (form.primaryElement, form.secondaryElement);

        static void VerifySnapshot(PetController pet, CreatureFamilyDefinition family, CreatureFormDefinition form,
            Action<bool, string> check)
        {
            string label = $"{family.definitionId}/{form.formId}";
            var meta = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            var stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            check(HasTyping(meta, family, form) && stats != null, label + " factory identity, types and stats");
            if (meta == null || stats == null) return;

            // Simulate loading a pre-change snapshot, without opening the player's save.
            meta.AssignPersistentId("typing-qa/" + label);
            meta.TrySetNickname("Bạn nhỏ", out _);
            var saved = meta.CreateSaveData();
            var runtime = stats.CreateSaveData();
            saved.secondaryElement = family.definitionId == "chick" ? PetElement.Flying : PetElement.Dark;
            var old = JsonUtility.FromJson<PetCustomizationSaveData>(JsonUtility.ToJson(saved));
            meta.RestoreFromSaveData(old);
            stats.RestoreFromSaveData(JsonUtility.FromJson<PetRuntimeStatsSaveData>(JsonUtility.ToJson(runtime)));
            saved.secondaryElement = form.secondaryElement;
            check(HasTyping(meta, family, form) && JsonUtility.ToJson(saved) == JsonUtility.ToJson(meta.CreateSaveData()),
                label + " old metadata snapshot resolves type and preserves remaining fields");
            check(meta.PersistentId == "typing-qa/" + label && meta.ResolveDisplayName(stats.DisplayName) == "Bạn nhỏ",
                label + " PetId and nickname survive");
            var actual = stats.CreateSaveData();
            check(actual.level == runtime.level && Mathf.Approximately(actual.health / Mathf.Max(1, actual.maxHealth),
                runtime.health / Mathf.Max(1, runtime.maxHealth)) && actual.equippedSkillIds.SequenceEqual(runtime.equippedSkillIds),
                label + " level, HP ratio and equipped skill IDs survive");
            meta.RestoreEvolutionPresentation(stats);
            check(HasTyping(meta, family, form), label + " typing after restoring evolution presentation");
            if (meta.TryGetNextEvolutionRule(out var rule))
            {
                var target = family.forms.Find(f => f.formId == rule.TargetFormId);
                check(target != null && meta.ApplyEvolution(rule) && HasTyping(meta, family, target)
                    && meta.PersistentId == "typing-qa/" + label, label + " evolution resolves the target type");
            }
        }

        static void VerifyCodex(Action<bool, string> check)
        {
            var catalog = PetCodexCatalog.Default;
            check(catalog != null, "Codex catalog resolves");
            if (catalog == null) return;
            foreach (var entry in catalog.species)
            {
                var meta = entry.Metadata;
                check(meta != null && CreatureAvatarCatalog.TryResolveTyping(meta.DefinitionId, meta.CurrentFormId, out var primary, out var secondary)
                    && (meta.Element, meta.SecondaryElement) == (primary, secondary), "Codex source typing: " + entry.id);
            }
        }
    }
}

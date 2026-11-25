using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor
{
    static class CreatureExpansionVerification
    {
        const string SkillCatalogPath = "Assets/Game/Skills/Resources/PetSkills/PetSkillCatalog.asset";
        const string V2 = "Assets/Game/Skills/Data/V2/";
        static string ReportDirectory => Path.Combine(QaArtifactPaths.Root, "skill-v2-owned-wild-2026-11-20");

        [MenuItem("Tools/Capstone/Creatures/Verify Skill V2 (read-only)")]
        static void VerifySkills()
        {
            Run("a-skills-editor.txt", VerifySkillCatalog);
        }

        static void Run(string file, Action<Action<bool, string>> verify)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Dừng Play Mode trước khi chạy kiểm tra.");
            var lines = new List<string>();
            int failures = 0;
            void Check(bool pass, string name)
            {
                if (!pass) failures++;
                lines.Add((pass ? "PASS " : "FAIL ") + name);
            }
            try { verify(Check); }
            catch (Exception e) { Check(false, e.ToString()); }
            Directory.CreateDirectory(ReportDirectory);
            string output = Path.Combine(ReportDirectory, file);
            lines.Insert(0, $"EDITOR. Checks={lines.Count}, failures={failures}");
            File.WriteAllLines(output, lines);
            string summary = $"[Creature Expansion] {lines[0]} {output}";
            if (failures == 0) Debug.Log(summary); else Debug.LogError(summary);
        }

        static void VerifySkillCatalog(Action<bool, string> check)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PetSkillCatalog>(SkillCatalogPath);
            if (catalog == null) throw new InvalidOperationException("Missing skill catalog");
            var issues = catalog.ValidateCatalog();
            check(issues.Count == 0, "Unique IDs and aliases: " + string.Join(" | ", issues));
            check(catalog.skills.Count >= 27, "Registered legacy and V2 definitions");
            foreach (var definition in catalog.skills.Where(s => s != null))
            {
                string id = definition.Id;
                check(catalog.TryGet(id, out var resolved) && resolved == definition, id + " factory resolves");
                var combat = definition.combat;
                check(combat != null && definition.presentation != null, id + " actual combat/presentation");
                check(definition.cooldownSeconds > 0 && definition.CreateHudData().icon == definition.icon, id + " HUD payload");
                if (combat == null) continue;
                check(combat.delivery == PetSkillDelivery.SelfBuff
                    ? combat.offenseBuffPercent > 0 && combat.buffDuration > 0
                    : combat.power > 0 && combat.range > 0, id + " effect data");
            }

            var orb = catalog.skills.Single(s => s.Id == "orb.dark");
            string orbArt = "Assets/Prefabs/Trung/Icons/Quả Cầu Bóng Tối.png";
            check(orb.icon != null && AssetDatabase.GetAssetPath(orb.icon) == orbArt, "orb.dark exact user Sprite");
            check(orb.combat.delivery == PetSkillDelivery.Projectile && orb.combat.damageKind == CreatureDamageKind.Magical
                && Mathf.Approximately(orb.combat.power, 1.2f) && orb.combat.releaseSocket == "Mouth"
                && Mathf.Approximately(orb.cooldownSeconds, 3.6f), "orb.dark preserved delivery, mouth, damage and cooldown");
            check(AssetDatabase.GetAssetPath(orb.combat.projectileVisual) == V2 + "Effects/orb.dark.prefab",
                "orb.dark uses the projectile visual derived from the supplied effect");
            for (int i = 1; i <= 3; i++)
            {
                string asset = $"Assets/Game/CreatureSystem/Runtime/Bats/Animation/Bat0{i}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<CreatureAnimationProfile>(asset);
                check(profile != null && profile.skills.Any(a => a.skillId == orb.Id && a.action == CreatureAction.Projectile),
                    "Bat0" + i + ": orb.dark animation mapping");
            }

            var effects = AssetDatabase.FindAssets("t:Prefab", new[] { V2.TrimEnd('/') + "/Effects" });
            check(effects.Length >= 14, "V2 display-only effect prefabs imported");
            foreach (string guid in effects)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (effect == null) { check(false, path + " import failed"); continue; }
                var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
                check(particles.Length > 0 && particles.All(p => p.main.stopAction == ParticleSystemStopAction.None),
                    effect.name + " particles with lifetime controlled by the pool");
                check(effect.GetComponentsInChildren<MonoBehaviour>(true).Length == 0
                    && effect.GetComponentsInChildren<Collider>(true).Length == 0, effect.name + " no gameplay script or collider");
                check(effect.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r =>
                    !r.enabled || r.sharedMaterial != null ||
                    (r.GetComponent<ParticleSystem>().trails.enabled && r.trailMaterial != null)),
                    effect.name + " particle or trail renderer material");
            }
        }

        [MenuItem("Tools/Capstone/Creatures/Verify 33 Owned-Wild (read-only)")]
        static void VerifyRoster()
        {
            Run("c-roster-editor.txt", VerifyRuntimeAssets);
        }

        static void VerifyRuntimeAssets(Action<bool, string> check)
        {
            const string zones = "Assets/Game/CreatureSystem/Runtime/SpawnZones/ApprovedSix/";
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(CreatureLibraryAuthoring.LibraryPath);
            var factory = AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>(
                "Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset");
            if (library == null || factory == null) throw new InvalidOperationException("Missing library or factory");

            typeof(PetStatCalculator).GetMethod("ResetProfiles",
                BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
            var preview = EditorSceneManager.NewPreviewScene();
            var rows = new List<string> { "Zone,EligibleCount,Configuration,Issues,PlacedInOpenScenes" };
            try
            {
                var forms = library.families.SelectMany(f => f.forms.Select(form => (family: f, form))).ToList();
                check(forms.Count == 33, "33 canonical forms");
                check(forms.Select(pair => pair.form.petPrefab).Where(p => p != null).Distinct().Count() == 33,
                    "33 unique Owned prefabs");
                check(forms.Select(pair => pair.form.wildPrefab).Where(p => p != null).Distinct().Count() == 33,
                    "33 unique Wild prefabs");
                foreach (var pair in forms)
                {
                    string label = pair.family.definitionId + "/" + pair.form.formId;
                    var form = pair.form;
                    check(factory.TryGetPrefab(pair.family.definitionId, form.formId, out var registered)
                        && registered != null && registered.gameObject == form.petPrefab, label + " factory resolves Owned");
                    check(form.TryGetTyping(out var primary, out var secondary)
                        && CreatureAvatarCatalog.TryResolveTyping(pair.family.definitionId, form.formId, out var p, out var s)
                        && (p, s) == (primary, secondary), label + " canonical typing");
                    CheckActor(form.petPrefab, pair.family, form, preview, false, check);
                    CheckActor(form.wildPrefab, pair.family, form, preview, true, check);
                }

                var zonePaths = AssetDatabase.FindAssets("t:Prefab", new[] { zones.TrimEnd('/') })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                check(zonePaths.Length == 6, "Six existing zone prefabs");
                foreach (string path in zonePaths)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                    try
                    {
                        var zone = go.GetComponent<CreatureEncounterZone>();
                        var table = zone != null ? zone.spawnTable : null;
                        check(table != null && table.ownedPetCatalog == factory, go.name + " table and Owned catalog");
                        if (table == null) continue;
                        foreach (var entry in table.species)
                        {
                            check(!new[] { "dragon-fire", "shell", "sun-blossom" }.Contains(entry.definitionId),
                                go.name + " no starter entry: " + entry.definitionId);
                            foreach (var f in entry.forms.Where(f => f.enabled && f.weight > 0))
                            {
                                var expected = forms.Single(x => x.family.definitionId == entry.definitionId && x.form.formId == f.formId).form;
                                check(f.prefab == expected.wildPrefab && f.prefab != null && string.IsNullOrWhiteSpace(f.configurationBlocker),
                                    go.name + " approved form " + entry.definitionId + "/" + f.formId);
                            }
                        }
                        bool valid = zone.ValidateConfiguration(out var reason);
                        check(valid && zone.ConfigurationIssues.Count == 0 && zone.EligibleSpeciesCount > 0,
                            go.name + " " + reason + " " + string.Join(" | ", zone.ConfigurationIssues));
                        bool placed = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                            .Where(scene => scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene))
                            .SelectMany(scene => scene.GetRootGameObjects())
                            .SelectMany(root => root.GetComponentsInChildren<CreatureEncounterZone>(true))
                            .Any(z => z.spawnTable == table);
                        rows.Add(string.Join(",", Csv(go.name), zone.EligibleSpeciesCount, Csv(reason),
                            Csv(string.Join(" | ", zone.ConfigurationIssues)), placed ? "yes" : "no"));
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllLines(Path.Combine(ReportDirectory, "c-zones-editor.csv"), rows);
        }

        static string Csv(string text) => "\"" + (text ?? "").Replace("\"", "\"\"") + "\"";

        static void CheckActor(GameObject prefab, CreatureFamilyDefinition family, CreatureFormDefinition form,
            Scene preview, bool wild, Action<bool, string> check)
        {
            string label = family.definitionId + "/" + form.formId + (wild ? " Wild" : " Owned");
            check(prefab != null, label + " exists");
            if (prefab == null) return;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            try
            {
                var meta = root.GetComponent<PetCollectionMetadata>();
                var stats = root.GetComponent<PetHudRuntimeStats>();
                var pet = root.GetComponent<PetController>();
                var driver = root.GetComponent<CreatureAnimationDriver>();
                var combat = root.GetComponent<CreatureCombatant>();
                var executor = root.GetComponent<PetSkillCombatExecutor>();
                var animator = root.GetComponentInChildren<Animator>(true);
                bool complete = meta != null && stats != null && pet != null && driver != null && combat != null
                    && executor != null && animator != null;
                check(complete, label + " AI/actor foundation");
                if (!complete) return;
                check(meta.DefinitionId == family.definitionId && meta.CurrentFormId == form.formId
                    && meta.Element == form.primaryElement && meta.SecondaryElement == form.secondaryElement
                    && pet.IsWildEncounter == wild, label + " role/identity/typing");
                check(string.IsNullOrWhiteSpace(new SerializedObject(meta).FindProperty("petId").stringValue),
                    label + " prefab carries no PetId");
                check(root.GetComponentsInChildren<Transform>(true).All(t =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0
                    && PrefabUtility.GetPrefabInstanceStatus(t.gameObject) != PrefabInstanceStatus.MissingAsset), label + " scripts and nested prefabs resolve");
                check(PetStatCalculator.TryGetProfile(meta, out var profile) && profile == form.statProfile
                    && profile.BaseCatchRate > 0 && profile.BaseCatchRate <= 255, label + " stats/catch");
                var binding = animator.GetComponent<CreatureVisualBinding>();
                check(root.GetComponentsInChildren<Animator>(true).Length == 1
                    && animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman
                    && animator.runtimeAnimatorController != null, label + " Generic rig and controller");
                check(binding != null && binding.animationProfile == form.animationProfile, label + " Form animation profile");
                check(stats.SpeciesSkillSet == form.skillSet && form.skillSet != null, label + " learnset binding");
                if (wild)
                {
                    var capture = root.GetComponent<WildPetCaptureTarget>();
                    check(capture != null && capture.CanEnterDowned && capture.Pet == pet && capture.Metadata == meta,
                        label + " capture eligibility and dependencies");
                }
                bool expanded = AssetDatabase.GetAssetPath(prefab).Contains("/Expansion/");
                if (!expanded) return;

                if (wild)
                    check(root.GetComponent<BasicCreatureEnemy>() != null && root.GetComponent<EncounterLevel>() != null,
                        label + " zone AI and level initialization");
                check(binding != null && binding.skillSet == form.skillSet
                    && new[] { "Head", "Mouth", "Body", "Ground" }.All(key => binding.sockets.Any(s => s.key == key && s.anchor != null)),
                    label + " sockets and visual skill set");
                if (binding == null || form.skillSet == null) return;
                var visual = root.GetComponent<PetEvolutionVisualController>();
                var capsule = root.GetComponent<CapsuleCollider>();
                var agent = root.GetComponent<NavMeshAgent>();
                var boundVisual = visual != null ? visual.ActiveVisualTransform : null;
                check(boundVisual != null && boundVisual.localScale == form.visualPrefab.transform.localScale,
                    label + " authoring scale");
                check(boundVisual != null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(boundVisual.gameObject) == form.visualPrefab,
                    label + " nested visual source");
                check(capsule != null && agent != null && Mathf.Approximately(capsule.radius, binding.actorRadius)
                    && Mathf.Approximately(agent.height, binding.actorHeight), label + " capsule and NavMesh dimensions");
                animator.Rebind();
                animator.Update(0);
                driver.Bind(animator);
                foreach (var action in new[] { CreatureAction.Idle, CreatureAction.Move, CreatureAction.Attack, CreatureAction.Hit, CreatureAction.Death })
                    check(driver.TryGetStates(action, out _), label + " state " + action);
                foreach (var skill in form.skillSet.learnset)
                    check(skill.skill.combat != null && skill.Presentation != null && driver.TryGetSkillStates(skill.skill.Id, out _),
                        label + " skill " + skill.skill.Id);
                check(form.skillSet.ValidateSet().Count == 0 && form.skillSet.CreateFreshDefaultLoadout(1).Count > 0,
                    label + " valid level-based loadout");
                if (wild) return;

                meta.AssignPersistentId("qa/" + label);
                meta.TrySetNickname("Bạn thử", out _);
                var runtime = stats.CreateSaveData();
                var customization = meta.CreateSaveData();
                meta.RestoreFromSaveData(JsonUtility.FromJson<PetCustomizationSaveData>(JsonUtility.ToJson(customization)));
                stats.RestoreFromSaveData(JsonUtility.FromJson<PetRuntimeStatsSaveData>(JsonUtility.ToJson(runtime)));
                check(meta.PersistentId == "qa/" + label && meta.Nickname == "Bạn thử"
                    && stats.CreateSaveData().equippedSkillIds.SequenceEqual(runtime.equippedSkillIds), label + " snapshot round trip");
                if (meta.TryGetNextEvolutionRule(out var rule))
                {
                    var next = family.forms.Single(f => f.formId == rule.TargetFormId);
                    bool evolved = meta.ApplyEvolution(rule) && visual.TryApply(rule.TargetVisualPrefab, out _);
                    check(evolved && meta.CurrentFormId == next.formId && visual.ActiveVisualTransform.localScale == next.visualPrefab.transform.localScale
                        && stats.SpeciesSkillSet == next.skillSet && meta.PersistentId == "qa/" + label && meta.Nickname == "Bạn thử",
                        label + " evolution Form, scale, learnset and individual");
                    var nextBinding = visual.ActiveAnimator != null ? visual.ActiveAnimator.GetComponent<CreatureVisualBinding>() : null;
                    check(nextBinding != null && Mathf.Approximately(capsule.radius, nextBinding.actorRadius)
                        && Mathf.Approximately(agent.height, nextBinding.actorHeight), label + " evolution recalibrates geometry");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

    }
}

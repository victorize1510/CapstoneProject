using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor
{
    static class CreatureSkillV2PlayVerification
    {
        const string SessionKey = "Capstone.SkillV2.Smoke.20261120";
        static string Out => Path.Combine(QaArtifactPaths.Root, "skill-v2-owned-wild-2026-11-20");
        static string Report => Path.Combine(Out, "c-play-smoke.txt");

        [MenuItem("Tools/Capstone/Creatures/Play smoke test Skill V2 (isolated)")]
        static void Start()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1
                || scene.isDirty || string.IsNullOrEmpty(scene.path) || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Mở một scene đã lưu, ở Edit Mode và thoát Prefab Mode trước khi chạy.");
            Directory.CreateDirectory(Out);
            File.WriteAllText(Report, "ENTERING PLAY — temporary empty scene; no save controllers.");
            SessionState.SetString(SessionKey, scene.path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Attach()
        {
            EditorApplication.playModeStateChanged -= OnStateChanged;
            EditorApplication.playModeStateChanged += OnStateChanged;
        }

        static void OnStateChanged(PlayModeStateChange state)
        {
            string previousScene = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(previousScene)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Run();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseString(SessionKey);
            EditorApplication.delayCall += () =>
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }

        static async void Run()
        {
            var lines = new List<string>();
            var damage = new List<CombatDamageResult>();
            void Record(CombatDamageResult hit) => damage.Add(hit);
            void Check(bool passed, string detail)
            {
                lines.Add((passed ? "PASS " : "FAIL ") + detail);
                File.WriteAllLines(Report, lines);
                if (!passed) throw new InvalidOperationException(detail);
            }
            async Task<bool> WaitFor(Func<bool> predicate, float timeout)
            {
                double expires = EditorApplication.timeSinceStartup + timeout;
                while (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < expires && !predicate())
                    await Task.Delay(35);
                return EditorApplication.isPlaying && predicate();
            }

            bool background = Application.runInBackground;
            Application.runInBackground = true;
            CombatDamageEvents.Applied += Record;
            try
            {
                Check(Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                    "No live or test save controller loaded");
                var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(CreatureLibraryAuthoring.LibraryPath);
                var bats = library.families.Single(f => f.definitionId == "bat");
                var chicks = library.families.Single(f => f.definitionId == "chick");
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.SetPositionAndRotation(Vector3.down * .5f, Quaternion.identity);
                ground.transform.localScale = new Vector3(30, 1, 30);
                var owner = new GameObject("QA Bat owner");
                owner.transform.position = Vector3.back * 2;
                var actor = Object.Instantiate(bats.forms[0].petPrefab);
                var stats = actor.GetComponent<PetHudRuntimeStats>();
                var pet = actor.GetComponent<PetController>();
                var executor = actor.GetComponent<PetSkillCombatExecutor>();
                pet.AssignOwner(owner.transform);
                pet.roamAroundOwner = false;
                var victim = Object.Instantiate(chicks.forms[0].wildPrefab, Vector3.forward * 6, Quaternion.identity);
                victim.GetComponent<BasicCreatureEnemy>().enabled = false;
                var enemy = victim.GetComponent<DummyEnemy>();
                await Task.Delay(120);
                Check(pet.TrySummonAt(Vector3.zero, Vector3.forward), "Real summon begins");
                Check(await WaitFor(() => pet.CanReceiveCommands, 5), "Dơi Nhỏ summoned and ready");
                int slot = stats.GetSkills().ToList().FindIndex(s => s.skillId == "orb.dark");
                Check(slot >= 0 && stats.GetSkills()[slot].icon != null
                    && AssetDatabase.GetAssetPath(stats.GetSkills()[slot].icon).EndsWith("Quả Cầu Bóng Tối.png"),
                    "Equipped HUD data resolves the supplied orb Sprite");
                float startingHealth = enemy.CurrentHealth;
                if (!stats.SpeciesSkillSet.TryGet("orb.dark", out var orb))
                    throw new InvalidOperationException("Không resolve được orb.dark từ learnset của Dơi.");
                string visualName = orb.skill.combat.projectileVisual.name + "(Clone)";
                Physics.SyncTransforms();
                stats.RequestSkill(slot);
                var driver = actor.GetComponent<PetCombatActionDriver>();
                Check(driver != null && driver.IsSkillBusy, "Real combat action accepted");
                bool visual = await WaitFor(() => executor.ActiveProjectileCount > 0
                    && Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                        .Any(t => t.name == visualName && t.GetComponentsInChildren<ParticleSystem>().Any(p => p.isPlaying)), 4);
                Check(visual, $"V2 projectile visual emitting: {visualName}, release={driver.LastReleaseNormalized:0.###}, "
                    + $"phase={driver.Phase}, hits={damage.Count}, rejection={driver.LastRejection}");
                Check(await WaitFor(() => damage.Any(r => r.Hit.Source == actor && r.Hit.SkillId == "orb.dark"), 3),
                    "Projectile emits actual damage from orb.dark");
                var result = damage.First(r => r.Hit.Source == actor && r.Hit.SkillId == "orb.dark");
                Check(enemy.CurrentHealth < startingHealth && result.HpLost > 0 && driver.LastReleaseNormalized > 0,
                    $"Animation release, impact and HP loss ({result.HpLost:0.##})");
                Check(stats.GetSkills()[slot].IsCoolingDown && !driver.Submit(slot), "Cooldown blocks immediate repeat");
                pet.Withdraw();
                Check(!pet.IsSummoned, "Real recall removes the summoned state");

                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var family in library.families)
                foreach (var form in family.forms)
                {
                    if (!AssetDatabase.GetAssetPath(form.wildPrefab).Contains("/Expansion/")) continue;
                    var wild = Object.Instantiate(form.wildPrefab, Vector3.right * 15f, Quaternion.identity);
                    var target = wild.GetComponent<WildPetCaptureTarget>();
                    Check(target != null && target.CanEnterDowned && ids.Add(target.Metadata.PersistentId)
                        && target.Metadata.CurrentFormId == form.formId, family.definitionId + "/" + form.formId + " live Wild instantiation and new PetId");
                    wild.SetActive(false);
                    Object.Destroy(wild);
                }
                Check(ids.Count == 21, "21 new Wild prefabs initialized in Play Mode");
                lines.Insert(0, "COMPLETE scoped Play smoke test. No saved scene, no save file.");
            }
            catch (Exception e)
            {
                lines.Insert(0, "FAILED scoped Play smoke test");
                lines.Add(e.ToString());
                Debug.LogException(e);
            }
            finally
            {
                CombatDamageEvents.Applied -= Record;
                Application.runInBackground = background;
                File.WriteAllLines(Report, lines);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
    }
}

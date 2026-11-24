using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Elements;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using Capstone.Game.PetSummon;
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
        static bool SkillAuditRun => SessionState.GetBool(SessionKey + ".skillAudit", false);
        static bool FollowUpRun => SessionState.GetBool(SessionKey + ".followUp", false);
        static bool HitFeedbackRun => SessionState.GetBool(SessionKey + ".hitFeedback", false);
        static bool CastVfxRun => SessionState.GetBool(SessionKey + ".castVfx", false);
        static string Out => Path.Combine(QaArtifactPaths.Root,
            CastVfxRun ? "SkillUiPartyShopFollowup" : FollowUpRun ? "PetSkillOnlyCombat" : HitFeedbackRun ? "PetHitFeedback"
                : SkillAuditRun ? "SkillAudit-2026-11-23" : "PetCries");
        static string Report => Path.Combine(Out, "playmode.txt");
        const string QaSceneName = "DemoScene 1 - Creature QA";
        const string ZoneFixtureKey = SessionKey + ".specialZones";

        [Serializable] sealed class SpecialZoneFixture
        {
            public string prefabPath;
            public string definition;
            public Vector3 position;
            public Quaternion rotation;
            public bool spawningEnabled, ignoreSpeciesTimeWindows, requireWaterNearby, allowNearPlayerFallback;
        }

        [Serializable] sealed class QaZoneFixture
        {
            public string navAsset;
            public Vector3 navPosition;
            public Quaternion navRotation;
            public SpecialZoneFixture[] zones;
        }

        [MenuItem("Tools/Capstone/Creatures/Play smoke test Skill V2 (isolated)")]
        static void Start() => Start(false);

        [MenuItem("Tools/Capstone/Pet Skills/Verify skill combat (isolated)")]
        static void StartSkillAudit() => Start(true);

        [MenuItem("Tools/Capstone/Pet Skills/Verify skill-only pet combat (isolated)")]
        static void StartFollowUp() => Start(true, true);

        [MenuItem("Tools/Capstone/Pet Skills/Verify hit feedback (isolated)")]
        static void StartHitFeedback() => Start(true, hitFeedback: true);

        [MenuItem("Tools/Capstone/Pet Skills/Verify cast and status VFX (isolated)")]
        static void StartCastVfx() => Start(false, castVfx: true);

        static void Start(bool skillAudit, bool followUp = false, bool castVfx = false, bool hitFeedback = false)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1
                || scene.isDirty || string.IsNullOrEmpty(scene.path) || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Mở một scene đã lưu, ở Edit Mode và thoát Prefab Mode trước khi chạy.");
            SessionState.SetBool(SessionKey + ".skillAudit", skillAudit);
            SessionState.SetBool(SessionKey + ".followUp", followUp);
            SessionState.SetBool(SessionKey + ".hitFeedback", hitFeedback);
            if (castVfx && scene.name == QaSceneName)
                SessionState.SetString(ZoneFixtureKey, JsonUtility.ToJson(SnapshotSpecialZones(scene)));
            else SessionState.EraseString(ZoneFixtureKey);
            SessionState.SetBool(SessionKey + ".castVfx", castVfx);
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
            SessionState.EraseBool(SessionKey + ".skillAudit");
            SessionState.EraseBool(SessionKey + ".followUp");
            SessionState.EraseBool(SessionKey + ".hitFeedback");
            SessionState.EraseBool(SessionKey + ".castVfx");
            SessionState.EraseString(ZoneFixtureKey);
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
            PetSpeciesSkillSet meleeFixture = null;
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
                owner.AddComponent<AudioListener>();
                owner.transform.position = Vector3.back * 2;
                if (CastVfxRun)
                {
                    var zoneSnapshot = SessionState.GetString(ZoneFixtureKey, "");
                    if (!string.IsNullOrEmpty(zoneSnapshot))
                        await VerifySpecialZones(JsonUtility.FromJson<QaZoneFixture>(zoneSnapshot), Check, WaitFor);
                    await VerifyCastVfx(library, owner.transform, Check, WaitFor);
                    lines.Insert(0, "COMPLETE isolated cast/status Play Mode checks. No save controller.");
                    return;
                }
                var actor = Object.Instantiate(bats.forms[0].petPrefab);
                var stats = actor.GetComponent<PetHudRuntimeStats>();
                var pet = actor.GetComponent<PetController>();
                var executor = actor.GetComponent<PetSkillCombatExecutor>();
                pet.AssignOwner(owner.transform);
                pet.roamAroundOwner = false;
                var cry = actor.GetComponentInChildren<PetCryEventReceiver>(true);
                var voice = cry.GetComponent<AudioSource>();
                var crySettings = new SerializedObject(cry);
                crySettings.FindProperty("ambientInterval").vector2Value = new Vector2(1, 1);
                crySettings.ApplyModifiedPropertiesWithoutUndo();
                var victim = Object.Instantiate(chicks.forms[0].wildPrefab, Vector3.forward * 6, Quaternion.identity);
                victim.GetComponent<BasicCreatureEnemy>().enabled = false;
                var enemy = victim.GetComponent<DummyEnemy>();
                PetCommandInput commands = null;
                await Task.Delay(120);
                if (FollowUpRun)
                {
                    commands = owner.AddComponent<PetCommandInput>();
                    commands.SetActivePet(pet);
                    enemy.maxHealth = 500;
                    enemy.Revive();
                    Check(commands.TrySelectEnemy(enemy) && commands.SelectedEnemy == enemy && !pet.HasCombatTarget &&
                        commands.GetComponent<CombatEngagement>() == null, "Enemy inspection works with a dormant pet and starts no combat");
                }
                Check(!voice.isPlaying, "Dormant Owned Pet stays silent");
                float spawnAt = Time.time;
                Check(pet.TrySummonAt(Vector3.zero, Vector3.forward), "Real summon begins");
                Check(await WaitFor(() => voice.isPlaying, 1), "Spawn cry fires during the greeting");
                Check(await WaitFor(() => pet.CanReceiveCommands, 5), "Dơi Nhỏ summoned and ready");
                lines.Add($"Direct summon to commands: {Time.time - spawnAt:F3}s");
                Check(await WaitFor(() => !voice.isPlaying, 5), "Spawn cry ends before the next expression");
                float quietMana = stats.Energy;
                Check(await WaitFor(() => voice.isPlaying, 3) && Mathf.Approximately(quietMana, stats.Energy)
                    && executor.ActiveProjectileCount == 0, "Peaceful Cast Spell plays without spending mana or spawning a projectile");
                crySettings.FindProperty("ambient.enabled").boolValue = false;
                crySettings.ApplyModifiedPropertiesWithoutUndo();
                Check(await WaitFor(() => !voice.isPlaying, 5), "Peaceful cry returns control");
                int slot = stats.GetSkills().ToList().FindIndex(s => s.skillId == "orb.dark");
                Check(slot >= 0 && stats.GetSkills()[slot].icon != null
                    && AssetDatabase.GetAssetPath(stats.GetSkills()[slot].icon).EndsWith("Quả Cầu Bóng Tối.png"),
                    "Equipped HUD data resolves the supplied orb Sprite");
                float startingHealth = enemy.CurrentHealth;
                if (!stats.SpeciesSkillSet.TryGet("orb.dark", out var orb))
                    throw new InvalidOperationException("Không resolve được orb.dark từ learnset của Dơi.");
                string visualName = orb.skill.combat.projectileVisual.name + "(Clone)";
                if (FollowUpRun)
                {
                    Check(!commands.TryCommandAttack(enemy) && !pet.HasCombatTarget, "The legacy basic attack command is disabled by default");
                    var originalPosition = victim.transform.position;
                    if (victim.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var targetAgent)) targetAgent.enabled = false;
                    victim.transform.position = actor.transform.position + Vector3.forward * (orb.skill.combat.CastRange + 5);
                    Physics.SyncTransforms();
                    var rangedDriver = actor.GetComponent<PetCombatActionDriver>() ?? actor.AddComponent<PetCombatActionDriver>();
                    Check(!rangedDriver.Submit(slot) && !rangedDriver.IsSkillBusy && !stats.GetSkills()[slot].IsCoolingDown,
                        "Selected enemy out of range does not launch a cast or order a chase");
                    victim.transform.position = originalPosition;
                    Physics.SyncTransforms();
                }
                Camera hitCamera = null;
                Transform bodySocket = null;
                Transform groundSocket = null;
                Vector3 ExpectedHitPosition()
                {
                    Vector3 point = bodySocket.position;
                    point.y = Mathf.Lerp(point.y, Mathf.Min(point.y, groundSocket.position.y), .35f);
                    return point;
                }
                if (FollowUpRun || HitFeedbackRun)
                {
                    var cameraObject = new GameObject("QA enemy inspection camera") { tag = "MainCamera" };
                    hitCamera = cameraObject.AddComponent<Camera>();
                    hitCamera.transform.position = enemy.TargetPosition + new Vector3(3, 1.3f, -2.8f);
                    hitCamera.transform.LookAt(enemy.TargetPosition);
                }
                if (FollowUpRun)
                {
                    var overhead = Object.FindFirstObjectByType<Capstone.Game.WorldFeedback.EnemyHealthBars>();
                    Check(overhead != null && await WaitFor(() => overhead.GetComponentsInChildren<UnityEngine.UI.Text>()
                        .Any(label => label.enabled && label.name == "Selected HP" && label.text == "500/500 HP"), 2),
                        "Selected enemy shows current and maximum HP above its model");
                }
                if (HitFeedbackRun)
                {
                    var visualBinding = victim.GetComponent<PetHudRuntimeStats>().VisualAnimator.GetComponent<CreatureVisualBinding>();
                    bodySocket = visualBinding.sockets.Single(socket => socket.key == "Body").anchor;
                    groundSocket = visualBinding.sockets.Single(socket => socket.key == "Ground").anchor;
                    Check(bodySocket != null && bodySocket.IsChildOf(visualBinding.transform), "Wild prefab exposes a Body socket on its current model");
                    Check(groundSocket != null && bodySocket.position.y > groundSocket.position.y + .02f,
                        "Model has enough vertical distance to test lowering the hit");
                    Vector3 textPoint = enemy.TargetPosition;
                    var fire = new CombatElementSnapshot(PetElement.Fire, PetElement.Fire, PetElement.Unknown);
                    var normal = new CombatHit(actor, "qa", 0, 501, CreatureDamageKind.Magical, false, false, textPoint, 0, fire);
                    var critical = new CombatHit(actor, "qa", 0, 503, CreatureDamageKind.Magical, true, false, textPoint, 0, fire);
                    FloatingCombatText.ShowDamage(new CombatDamageResult(normal, victim, 501, false));
                    FloatingCombatText.ShowDamage(new CombatDamageResult(critical, victim, 503, false));
                    var labels = Object.FindFirstObjectByType<FloatingCombatText>().GetComponentsInChildren<UnityEngine.UI.Text>();
                    Check(labels.Any(label => label.text == "501" && label.color == Color.white)
                        && labels.Any(label => label.text == "503" && ((Color32)label.color).Equals(new Color32(255, 77, 77, 255))),
                        "Normal elemental damage text is white and critical damage text is red");
                }
                Physics.SyncTransforms();
                Vector3 rangedOrigin = actor.transform.position;
                stats.RequestSkill(slot);
                var driver = actor.GetComponent<PetCombatActionDriver>();
                Check(driver != null && driver.IsSkillBusy, "Real combat action accepted");
                if (FollowUpRun)
                    Check(await WaitFor(() => driver.LastReleaseNormalized > 0, 3) &&
                        Vector3.Distance(rangedOrigin, actor.transform.position) < .15f,
                        "In-range projectile casts from the current position");
                Check(await WaitFor(() => voice.isPlaying, 3), "Accepted Projectile Attack plays a cry");
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
                if (HitFeedbackRun)
                {
                    var feel = Resources.Load<CombatFeelSettings>("CombatFeelSettings");
                    Check(feel != null && feel.directImpact != null, "Combined hit prefab is assigned in combat settings");
                    string hitName = feel.directImpact.name + "(Clone)";
                    Check(await WaitFor(() =>
                    {
                        var hit = GameObject.Find(hitName);
                        if (hit == null) return false;
                        var red = hit.transform.Find("Red impact").GetComponent<ParticleSystem>();
                        var white = hit.transform.Find("White sparks").GetComponent<ParticleSystem>();
                        return red.particleCount > 0 && white.particleCount > 0
                            && red.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported
                            && white.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported;
                    }, 1), "Combined direct-hit pool plays red impact and white spark particles");
                    Transform flash = GameObject.Find(hitName).transform;
                    Check(Vector3.Distance(flash.position, ExpectedHitPosition()) < .015f,
                        "Both hit layers spawn 35% below Body toward Ground on the active prefab");
                    var number = Object.FindFirstObjectByType<FloatingCombatText>();
                    const System.Reflection.BindingFlags privateField = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var entries = (Array)typeof(FloatingCombatText).GetField("entries", privateField).GetValue(number);
                    Check(entries.Cast<object>().Any(entry => (bool)entry.GetType().GetField("active").GetValue(entry)
                        && (Transform)entry.GetType().GetField("anchor").GetValue(entry) == bodySocket),
                        "Floating damage number uses the prefab Body socket");
                    Vector3 flashAtHit = flash.position;
                    Vector3 enemyAtHit = victim.transform.position;
                    victim.transform.position += Vector3.right * .28f;
                    Check(await WaitFor(() => Vector3.Distance(flash.position, flashAtHit) > .15f
                        && Vector3.Distance(flash.position, ExpectedHitPosition()) < .015f, .35f),
                        "The lowered hit emitter follows the visual as it moves");
                    victim.transform.position = enemyAtHit;
                    Physics.SyncTransforms();
                }
                if (hitCamera != null) Object.Destroy(hitCamera.gameObject);
                if (FollowUpRun)
                {
                    await Task.Delay(650);
                    Check(!pet.HasCombatTarget && damage.All(hit => hit.Hit.Source != actor || hit.Hit.SkillId != "basic"),
                        "Projectile damage never queues a basic follow-up");
                }
                Check(stats.GetSkills()[slot].IsCoolingDown && !driver.Submit(slot), "Cooldown blocks immediate repeat");
                if (SkillAuditRun)
                {
                    Check(result.Hit.Kind == CreatureDamageKind.Magical, "projectile uses its authored Special damage kind");
                    Check(await WaitFor(() => !driver.IsSkillBusy, 4), "Special action releases the combat channel");
                    var activeSet = stats.SpeciesSkillSet;
                    var melee = activeSet.catalog.FindRetired("bite.dark");
                    Check(melee != null && melee.combat != null, "Retired bite data is available for the isolated melee fixture");
                    meleeFixture = Object.Instantiate(activeSet);
                    meleeFixture.learnset = new List<PetSpeciesSkillEntry> {
                        new PetSpeciesSkillEntry { skill = melee, overrideAnimation = true, animatorStates = new[] { "Bite Attack" } }
                    };
                    meleeFixture.defaultLoadout = new List<PetSkillDefinition> { melee };
                    meleeFixture.Invalidate();
                    stats.ConfigureSkillSet(meleeFixture, false);
                    int bite = stats.GetSkills().ToList().FindIndex(skill => skill.combatProfile != null
                        && skill.combatProfile.delivery == PetSkillDelivery.Melee && skill.combatProfile.damageKind == CreatureDamageKind.Physical);
                    if (victim.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent)) agent.enabled = false;
                    Vector3 direction = Vector3.ProjectOnPlane(actor.transform.forward, Vector3.up).normalized;
                    Vector3 meleeOrigin = actor.transform.position;
                    victim.transform.position = meleeOrigin + direction * (FollowUpRun ? 5.5f : 1.35f);
                    Physics.SyncTransforms();
                    Check(bite >= 0 && enemy.IsAlive, "same pet has an equipped Physical melee skill and a live target");
                    string biteId = stats.GetSkills()[bite].skillId;
                    stats.RequestSkill(bite);
                    Check(await WaitFor(() => damage.Any(hit => hit.Hit.Source == actor && hit.Hit.SkillId == biteId
                        && hit.Hit.Kind == CreatureDamageKind.Physical && !hit.Hit.DamageOverTime && hit.HpLost > 0), 4),
                        "Physical melee inflicts HP damage using its authored damage kind");
                    if (FollowUpRun)
                    {
                        Check(Vector3.Distance(meleeOrigin, actor.transform.position) > 1f, "Melee skill runs into range before striking");
                        Check(await WaitFor(() => !driver.IsSkillBusy && Vector3.Distance(owner.transform.position, actor.transform.position) < 2.9f, 4)
                            && !pet.HasCombatTarget, "After the melee skill the pet returns to its owner");
                        owner.transform.position += Vector3.right * 6;
                        Check(await WaitFor(() => Vector3.Distance(owner.transform.position, actor.transform.position) < 2.9f, 2),
                            "Owner movement is followed quickly");
                        owner.transform.position -= Vector3.right * 6;
                    }
                    stats.ConfigureSkillSet(activeSet, false);
                }
                pet.Withdraw();
                Check(!pet.IsSummoned && !voice.isPlaying, "Real recall removes the summoned state and silences the visual");

                Check(pet.TrySummonAt(Vector3.zero, Vector3.forward)
                    && await WaitFor(() => pet.CanReceiveCommands && !voice.isPlaying, 5), "Re-summon resets the same visual and source");
                float defeatAt = Time.time;
                actor.GetComponent<CreatureCombatant>().ReceiveDamage(stats.MaxHealth * 100, CreatureDamageKind.Physical, victim);
                Check(pet.IsDefeatPresentation && !pet.CanReceiveCommands, "HP zero locks Owned Pet combat during defeat");
                Check(await WaitFor(() => voice.isPlaying, 2) && pet.IsSummoned,
                    "Owned death cry begins before Recall");
                Check(await WaitFor(() => !voice.isPlaying, 6) && pet.IsSummoned,
                    "Owned death cry completes while the pet is still in the world");
                Check(await WaitFor(() => !pet.IsSummoned && !pet.IsDefeatPresentation, 3),
                    "Emergency Recall completes after the death cry");
                lines.Add($"Owned defeat to dormant: {Time.time - defeatAt:F3}s (ownerless VFX profile fixture)");

                var wildDefeat = Object.Instantiate(bats.forms[0].wildPrefab, Vector3.right * 8f, Quaternion.identity);
                var wildHealth = wildDefeat.GetComponent<DummyEnemy>();
                var wildVoice = wildDefeat.GetComponentInChildren<AudioSource>(true);
                await Task.Delay(120);
                var captureSettings = new SerializedObject(wildDefeat.GetComponent<WildPetCaptureTarget>());
                captureSettings.FindProperty("encounterCaptureAllowed").boolValue = false;
                captureSettings.ApplyModifiedPropertiesWithoutUndo();
                wildHealth.TakeDamage(wildHealth.maxHealth * 100);
                Check(await WaitFor(() => wildVoice.isPlaying, 2), "Wild Death plays the same Form recording");
                Check(await WaitFor(() => wildHealth.DeathVisualComplete && !wildDefeat.activeSelf, 6),
                    "Wild Death finishes through encounter presentation without Party Recall");

                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var family in library.families)
                foreach (var form in family.forms)
                {
                    var wild = Object.Instantiate(form.wildPrefab, Vector3.right * 15f, Quaternion.identity);
                    var target = wild.GetComponent<WildPetCaptureTarget>();
                    var wildDriver = wild.GetComponent<CreatureAnimationDriver>();
                    Check(target != null && target.IsWild && ids.Add(target.Metadata.PersistentId)
                        && target.Metadata.CurrentFormId == form.formId && wildDriver.Cry != null
                        && wildDriver.Cry.GetComponent<AudioSource>().clip != null,
                        family.definitionId + "/" + form.formId + " live Wild instantiation, new PetId and cry clip");
                    wild.SetActive(false);
                    Object.Destroy(wild);
                }
                Check(ids.Count == 33, "33 Wild prefabs initialized in Play Mode");
                await VerifyDirector(bats, Check, WaitFor, lines);
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
                if (meleeFixture != null) Object.Destroy(meleeFixture);
                CombatDamageEvents.Applied -= Record;
                Application.runInBackground = background;
                File.WriteAllLines(Report, lines);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        static QaZoneFixture SnapshotSpecialZones(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var surface = roots.SelectMany(root => root.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>())
                .Single(item => item.name == "Demo PvE Navigation");
            var zones = roots.SelectMany(root => root.GetComponentsInChildren<CreatureEncounterZone>())
                .Where(zone => zone.zoneId is "capstone-special-dragon-fire" or "capstone-special-shell" or "capstone-special-sun-blossom")
                .Select(zone => new SpecialZoneFixture {
                    prefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(zone.gameObject)),
                    definition = zone.spawnTable.species.Single().definitionId,
                    position = zone.transform.position, rotation = zone.transform.rotation,
                    spawningEnabled = zone.spawningEnabled, ignoreSpeciesTimeWindows = zone.ignoreSpeciesTimeWindows,
                    requireWaterNearby = zone.requireWaterNearby, allowNearPlayerFallback = zone.allowNearPlayerFallback
                }).ToArray();
            return new QaZoneFixture {
                navAsset = AssetDatabase.GetAssetPath(surface.navMeshData), navPosition = surface.transform.position,
                navRotation = surface.transform.rotation, zones = zones
            };
        }

        static async Task VerifySpecialZones(QaZoneFixture snapshot, Action<bool, string> check,
            Func<Func<bool>, float, Task<bool>> waitFor)
        {
            check(snapshot.zones.Length == 3 && snapshot.zones.Select(zone => zone.definition).Distinct().Count() == 3,
                "The QA scene contains three distinct starter species zones");
            var navigation = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(snapshot.navAsset);
            check(navigation != null, "The isolated fixture loads the QA scene's baked NavMesh");
            var handle = UnityEngine.AI.NavMesh.AddNavMeshData(navigation, snapshot.navPosition, snapshot.navRotation);
            var zones = new List<CreatureEncounterZone>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "QA starter zones floor fixture";
            floor.transform.position = snapshot.zones[0].position + Vector3.down * .5f;
            floor.transform.localScale = new Vector3(80, 1, 80);
            try
            {
                foreach (var data in snapshot.zones)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(data.prefabPath);
                    var zone = Object.Instantiate(prefab, data.position, data.rotation).GetComponent<CreatureEncounterZone>();
                    zones.Add(zone);
                    zone.spawningEnabled = data.spawningEnabled;
                    zone.ignoreSpeciesTimeWindows = data.ignoreSpeciesTimeWindows;
                    zone.requireWaterNearby = data.requireWaterNearby;
                    zone.allowNearPlayerFallback = data.allowNearPlayerFallback;
                    bool valid = zone.ValidateConfiguration(out var reason);
                    check(zone.spawningEnabled && valid && !zone.requireWaterNearby &&
                        (data.definition == "shell" || zone.ignoreSpeciesTimeWindows),
                        data.definition + " inherits an active QA spawn and requires no clock or shoreline: " + reason);
                }
                Physics.SyncTransforms();
                bool spawned = await waitFor(() => zones.All(zone => zone.OccupiedSlots > 0), 8);
                foreach (var zone in zones)
                    check(zone.OccupiedSlots > 0, $"{zone.zoneId} spawned at its QA position ({zone.OccupiedSlots}/{zone.TargetPopulation}): {zone.LastStatus}. {zone.LastPlacementReport}");
                check(spawned, "All three zones populate on the QA NavMesh");
            }
            finally
            {
                foreach (var zone in zones) Object.Destroy(zone.gameObject);
                Object.Destroy(floor);
                handle.Remove();
            }
        }

        static async Task VerifyCastVfx(CreatureLibrary library, Transform owner, Action<bool, string> check,
            Func<Func<bool>, float, Task<bool>> waitFor)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "QA status target";
            target.transform.position = new Vector3(0, 1f, 11.5f);
            var enemy = target.AddComponent<DummyEnemy>();
            enemy.trainingDummy = true;
            enemy.maxHealth = 5000;
            enemy.Revive();

            async Task<PetHudRuntimeStats> Summon(string family, params string[] spellIds)
            {
                var form = library.families.Single(item => item.definitionId == family).forms
                    .First(item => spellIds.All(id => item.skillSet.TryGet(id, out _)));
                var actor = Object.Instantiate(form.petPrefab);
                var pet = actor.GetComponent<PetController>();
                var stats = actor.GetComponent<PetHudRuntimeStats>();
                stats.SetIdentity(stats.SpeciesSkillSet.speciesId, 36);
                stats.SetSkills(spellIds.Select(id => form.skillSet.learnset.First(entry => entry.skill.Id == id).CreateHudData(36)));
                pet.AssignOwner(owner);
                pet.roamAroundOwner = false;
                check(pet.TrySummonAt(Vector3.zero, Vector3.forward) && await waitFor(() => pet.CanReceiveCommands, 7),
                    family + " summoned in the isolated scene");
                Physics.SyncTransforms();
                return stats;
            }

            static Transform ActiveVisual(GameObject prefab)
            {
                var effect = GameObject.Find(prefab.name + "(Clone)");
                return effect != null ? effect.transform : null;
            }

            var fire = await Summon("snake-fire", "heatwave.fire", "shield.fire");
            var firePresentation = fire.GetComponent<PetSkillPresentationPlayer>();
            var mouth = new GameObject("QA low Mouth").transform;
            mouth.SetParent(fire.transform, false);
            mouth.position = fire.transform.position + Vector3.up * .6f + fire.transform.forward * .75f;
            firePresentation.sockets = new List<PetSkillSocket>(firePresentation.sockets.Where(socket => socket.key != "Mouth"))
                { new PetSkillSocket { key = "Mouth", anchor = mouth } };

            fire.RequestSkill(0);
            var driver = fire.GetComponent<PetCombatActionDriver>();
            var profile = fire.GetSkills()[0].combatProfile;
            check(driver != null && driver.IsSkillBusy && !fire.GetSkills()[0].IsCoolingDown,
                "Command starts windup before charging the cooldown");
            check(await waitFor(() =>
            {
                var visual = ActiveVisual(profile.projectileVisual);
                return visual != null && Vector3.Distance(visual.position, mouth.position) >= .14f;
            }, 4), "Đốt Nóng leaves a low mouth with clearance and its cloud clears the floor");
            check(await waitFor(() => target.GetComponent<CombatBleed>()?.Has(CombatAilment.Burn) == true, 4) &&
                fire.GetSkills()[0].IsCoolingDown, "Đốt Nóng reaches a target 11.5m away, applies Burn and charges cooldown");
            var wounds = target.GetComponent<CombatBleed>();
            var catalog = Resources.Load<PetSkillCatalog>("PetSkills/PetSkillCatalog");
            check(await waitFor(() => wounds.TicksApplied > 0, 2) && ActiveVisual(catalog.burnAura) != null,
                "Burn deals a real HP tick and the fire aura is still visible at that tick");

            check(await waitFor(() => !driver.IsSkillBusy, 4), "Fire actor can accept another skill");
            fire.RequestSkill(1);
            var shield = fire.GetSkillPresentation("shield.fire").release.effectPrefab;
            check(await waitFor(() => ActiveVisual(shield) != null, 4), "Khiên Lửa VFX spawns on the pet");
            var shieldVisual = ActiveVisual(shield);
            var ground = firePresentation.sockets.Find(socket => socket.key == "Ground")?.anchor;
            check(ground != null && Mathf.Abs(shieldVisual.parent.position.y - ground.position.y) < .06f &&
                shieldVisual.position.y > shieldVisual.parent.position.y,
                "Khiên Lửa's scene anchor sits by the pet's feet and its rim extends upwards");
            check(Mathf.Abs(Vector3.Dot(shieldVisual.forward, Vector3.up)) < .15f,
                "Khiên Lửa uses a vertical plane");
            await Task.Delay(250);
            await CaptureEffectFrame(shieldVisual.position, 4.5f, "fire-shield-upright.png");
            await Task.Delay(2200);
            check(ActiveVisual(shield) != null, "Khiên Lửa remains visible during the active buff");

            fire.GetComponent<PetController>().Withdraw();
            Object.Destroy(fire.gameObject);
            target.transform.position = new Vector3(0, 1f, 5f);
            var electric = await Summon("cat-electric", "thunder.electric");
            float before = enemy.CurrentHealth;
            var equipped = electric.GetSkills()[0];
            check(equipped.skillId == "thunder.electric" && equipped.unlocked, "Sấm Sét is equipped and unlocked");
            electric.RequestSkill(0);
            var electricDriver = electric.GetComponent<PetCombatActionDriver>();
            var targetFx = electric.GetSkillPresentation("thunder.electric").impact.effectPrefab;
            check(electricDriver != null && electricDriver.IsSkillBusy, "Sấm Sét is accepted: " + electricDriver?.LastRejection);
            bool shocked = await waitFor(() => target.GetComponent<CombatBleed>()?.Has(CombatAilment.Shock) == true, 5);
            check(shocked, $"Sấm Sét applies Shock to the target (HP {before:0.#} → {enemy.CurrentHealth:0.#}, " +
                $"release {electricDriver.LastReleaseNormalized:0.##}, Mana {electric.Energy:0.#}, {electricDriver.LastRejection})");
            var strike = ActiveVisual(targetFx);
            check(strike != null && Vector3.Distance(strike.position, new Vector3(target.transform.position.x, .03f,
                target.transform.position.z)) < .12f, "Sấm Sét spawns at the target's feet");
            await Task.Delay(800);
            check(ActiveVisual(catalog.shockAura) != null, "Shock aura stays visible beyond its previous half-second cutoff");

            electric.GetComponent<PetController>().Withdraw();
            Object.Destroy(electric.gameObject);
            var blossom = await Summon("sun-blossom", "leafstorm.grass");
            var leafProfile = blossom.GetSkills()[0].combatProfile;
            blossom.RequestSkill(0);
            check(await waitFor(() => ActiveVisual(leafProfile.projectileVisual) != null, 4), "Bão Lá spawns a moving prefab");
            var leaves = ActiveVisual(leafProfile.projectileVisual);
            var leafLaunch = leaves.position;
            check(await waitFor(() => leaves != null && leaves.gameObject.activeInHierarchy &&
                Vector3.Distance(leaves.position, leafLaunch) > 2f &&
                leaves.GetComponentsInChildren<ParticleSystem>().Any(system => system.particleCount > 0), 3),
                "Bão Lá particles remain visible on the same instance in flight");
            blossom.GetComponent<PetController>().Withdraw();
            Object.Destroy(blossom.gameObject);
            var flyer = await Summon("chick", "cyclone.flying");
            var vortex = flyer.GetSkills()[0].combatProfile.projectileVisual;
            flyer.RequestSkill(0);
            check(await waitFor(() => ActiveVisual(vortex) != null, 4), "Lốc Xoáy spawns its traveling prefab");
            var moving = ActiveVisual(vortex);
            var mesh = moving.GetComponent<MeshFilter>()?.sharedMesh;
            var renderer = moving.GetComponent<MeshRenderer>();
            check(mesh != null && mesh.bounds.size.y > .25f && mesh.bounds.size.z > .1f &&
                renderer != null && renderer.enabled && renderer.sharedMaterial.shader.isSupported,
                "Lốc Xoáy uses a supported mesh with height and depth");
            Vector3 launch = moving.position;
            check(await waitFor(() => moving.gameObject.activeInHierarchy && Vector3.Distance(launch, moving.position) > 2f, 3) &&
                moving.up.y > .99f && ActiveVisual(vortex) == moving,
                "The same upright tornado mesh moves more than 2m in flight");

            await CaptureEffectFrame(renderer.bounds.center, Mathf.Max(3f, renderer.bounds.extents.magnitude * 2.6f), "cyclone-in-flight.png");
        }

        static async Task CaptureEffectFrame(Vector3 focus, float distance, string fileName)
        {
            var view = new GameObject("QA effects camera").AddComponent<Camera>();
            var targetTexture = RenderTexture.GetTemporary(640, 420, 24);
            var frame = new Texture2D(640, 420, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                view.targetTexture = targetTexture;
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = new Color(.08f, .12f, .16f);
                view.transform.position = focus + new Vector3(.72f, .27f, -.66f).normalized * distance;
                view.transform.LookAt(focus);
                await Awaitable.EndOfFrameAsync();
                RenderTexture.active = targetTexture;
                frame.ReadPixels(new Rect(0, 0, 640, 420), 0, 0);
                frame.Apply();
                File.WriteAllBytes(Path.Combine(Out, fileName), frame.EncodeToPNG());
            }
            finally
            {
                view.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(targetTexture);
                Object.Destroy(view.gameObject);
                Object.Destroy(frame);
            }
        }

        static async Task VerifyDirector(CreatureFamilyDefinition bats, Action<bool, string> check,
            Func<Func<bool>, float, Task<bool>> waitFor, List<string> lines)
        {
            var fixture = new GameObject("QA summon director fixture");
            fixture.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Thắng/Player.prefab");
            var player = Object.Instantiate(source, fixture.transform);
            player.name = "QA summon player";
            player.tag = "Untagged";
            player.transform.position = new Vector3(-8, .2f, -6);
            foreach (var component in player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                bool playerComponent = component.gameObject == player && (component is BasicPlayerMovement
                    || component is PlayerVisualSwitcher || component is PetSummonDirector
                    || component is PetSummonHandSocket || component is PetSummonReleaseEffect);
                if (!playerComponent && !(component is AnimatorRootMotionRelay))
                    Object.DestroyImmediate(component);
            }
            var input = player.AddComponent<PetCommandInput>();
            var movement = player.GetComponent<BasicPlayerMovement>();
            var director = player.GetComponent<PetSummonDirector>();
            var effect = player.GetComponent<PetSummonReleaseEffect>();
            var settings = new SerializedObject(director);
            var aim = new GameObject("QA summon aim").AddComponent<Camera>();
            aim.transform.SetParent(fixture.transform, false);
            aim.enabled = false;
            aim.transform.SetPositionAndRotation(player.transform.position + Vector3.up, player.transform.rotation);
            settings.FindProperty("aimCamera").objectReferenceValue = aim;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var pets = new PetController[2];
            for (int i = 0; i < pets.Length; i++)
            {
                pets[i] = Object.Instantiate(bats.forms[i].petPrefab, fixture.transform).GetComponent<PetController>();
                pets[i].AssignOwner(player.transform);
            }
            input.petSlots = new PetController[6];
            Array.Copy(pets, input.petSlots, pets.Length);
            input.activePet = pets[0];
            fixture.SetActive(true);
            Physics.SyncTransforms();
            player.GetComponent<CharacterController>().Move(Vector3.down * .5f);
            check(await waitFor(() => movement.CanStartPetThrow, 3), "Authored player is grounded on the isolated floor");

            string failure = "";
            director.SequenceFinished += (_, success) => failure = success ? "" : director.LastFailureReason;
            var profile = effect.Profile != null ? effect.Profile
                : AssetDatabase.LoadAssetAtPath<PetVFXProfile>("Assets/Game/PetSummon/PetVFX_Default.asset");
            var baseline = Object.Instantiate(profile);
            baseline.spawnDuration = .8f;
            baseline.recallDuration = .65f;
            var speedFields = new[] { "summonBallSpeedMultiplier", "throwPlaybackSpeed", "recallPickupPlaybackSpeed" };
            var currentSpeeds = speedFields.Select(field => settings.FindProperty(field).floatValue).ToArray();

            async Task<(float spawn, float recall)> Measure(string label)
            {
                failure = "";
                float start = Time.time;
                check(director.RequestSummonSlot(0), label + " summon request accepted: " + director.LastFailureReason);
                check(await waitFor(() => !director.IsSequenceRunning, 8) && pets[0].IsSummoned
                    && await waitFor(() => pets[0].CanReceiveCommands && movement.CanStartPetThrow, 4),
                    label + " full summon through throw, bounce, reveal and greeting: " + failure);
                float spawn = Time.time - start;
                start = Time.time;
                check(director.RequestRecall(), label + " Recall request accepted");
                if (FollowUpRun && label == "Updated timing parameters")
                {
                    check(await waitFor(() =>
                    {
                        var recall = effect.ActiveVfx;
                        if (recall == null || recall.Beam == null || !recall.Beam.enabled) return false;
                        Vector3 main = recall.Beam.GetPosition(recall.Beam.positionCount - 1);
                        Vector3 ball = recall.Beam.GetPosition(0);
                        return recall.FunnelStrands.Any(strand => strand.enabled
                            && Vector3.Distance(strand.GetPosition(0), main) > .02f
                            && Vector3.Distance(strand.GetPosition(strand.positionCount - 1), ball) < .001f);
                    }, 4), "Live Recall branches from different body surfaces into the moving ball");
                }
                check(await waitFor(() => !director.IsSequenceRunning, 8) && !pets[0].IsSummoned,
                    label + " full Recall through pickup and VFX: " + failure);
                return (spawn, Time.time - start);
            }

            effect.ConfigureProfile(baseline);
            foreach (var field in speedFields) settings.FindProperty(field).floatValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var slower = await Measure("Previous timing parameters");
            effect.ConfigureProfile(profile);
            for (int i = 0; i < speedFields.Length; i++) settings.FindProperty(speedFields[i]).floatValue = currentSpeeds[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            var faster = await Measure("Updated timing parameters");
            lines.Add($"Director fixture (previous timing parameters → updated): summon {slower.spawn:F3}s → {faster.spawn:F3}s, "
                + $"Recall {slower.recall:F3}s → {faster.recall:F3}s");
            check(faster.spawn < slower.spawn && faster.recall < slower.recall, "Both full sequences run faster in the same fixture");

            check(director.RequestSummonSlot(0) && await waitFor(() => pets[0].CanReceiveCommands && !director.IsSequenceRunning, 8),
                "First Party member returns to battle");
            var stats = pets[0].GetComponent<PetHudRuntimeStats>();
            pets[0].GetComponent<CreatureCombatant>().ReceiveDamage(stats.MaxHealth * 100, CreatureDamageKind.Physical, null);
            check(pets[0].IsDefeatPresentation && input.activePet == pets[0] && !pets[1].IsSummoned,
                "Party keeps the defeated pet selected during the death presentation");
            check(await waitFor(() => !pets[0].IsSummoned && input.activePet == pets[1]
                && pets[1].CanReceiveCommands && !director.IsSequenceRunning, 10),
                "After Recall, Party selects and summons the next healthy pet through the existing director");
            check(stats.Health <= 0 && pets[1].GetComponent<CreatureAnimationDriver>().Cry.GetComponent<AudioSource>().clip
                != pets[0].GetComponent<CreatureAnimationDriver>().Cry.GetComponent<AudioSource>().clip,
                "The defeated pet stays at zero HP and the replacement has its own Form recording");
            Object.Destroy(baseline);
        }
    }
}

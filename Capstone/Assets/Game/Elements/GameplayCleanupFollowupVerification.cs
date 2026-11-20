#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.Elements
{
    // Kiem phan con thieu cua dot cleanup; chi chay tu menu, khong tu Play sau reload.
    public static class GameplayCleanupFollowupVerification
    {
        public const string Root = ElementGameplayVerification.Root + "followup-2026-11-17/";
        static readonly List<string> checks = new List<string>();
        static readonly List<Object> owned = new List<Object>();
        static void Write(string name, string content)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root + name, content);
            File.AppendAllText(Root + "created-paths.txt", Root + name + "\n");
        }
        static void Check(bool value, string label)
        {
            checks.Add((value ? "PASS " : "FAIL ") + label);
            Write("followup-contract-checks.txt", string.Join("\n", checks));
            if (!value) throw new InvalidOperationException(label);
        }
        static T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            owned.Add(asset);
            return asset;
        }
        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/11. Verify remaining authoring and save contracts")]
        public static void Contracts()
        {
            checks.Clear();
            try
            {
                AuthoringCache();
                SparseLegacySaves();
                ElementGameplayVerification.ContractsAtRoot(Root);
                Check(File.ReadAllText(Root + "element-contract-status.txt").StartsWith("COMPLETE"), "current shared elemental contracts pass");
                Write("followup-contract-status.txt", "COMPLETE " + checks.Count + " additional checks; " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                Write("followup-contract-status.txt", "FAILED\n" + ex);
                Debug.LogException(ex);
            }
            finally
            {
                foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
                owned.Clear();
            }
        }
        static void AuthoringCache()
        {
            var skill = Asset<PetSkillDefinition>();
            skill.skillId = "qa.cache";
            skill.element = PetElement.Fire;
            var catalog = Asset<PetSkillCatalog>();
            catalog.skills.Add(skill);
            int validErrors = catalog.ValidateCatalog().Count;
            skill.element = PetElement.Wind;
            int invalidErrors = catalog.ValidateCatalog().Count;
            Write("cache-observation.txt", "Valid Fire errors=" + validErrors + "; after referenced definition changed to legacy Wind errors=" + invalidErrors);
            Check(validErrors == 0, "valid authored skill accepted");
            Check(invalidErrors > 0, "explicit validation detects referenced definition element edits after cache was built");
            skill.element = PetElement.Fire;
            Check(catalog.ValidateCatalog().Count == 0, "corrected definition clears cached error");

            var set = Asset<PetSpeciesSkillSet>();
            set.speciesId = "qa.cache.species";
            set.catalog = catalog;
            set.learnset.Add(new PetSpeciesSkillEntry { skill = skill });
            set.defaultLoadout.Add(skill);
            Check(set.ValidateSet().Count == 0, "valid species learnset and defaults accepted");
            Check(set.TryGet("qa.cache", out _), "species lookup primed");
            set.learnset.Clear();
            Check(set.ValidateSet().Any(e => e.Contains("Default skill")), "validation rejects default removed from referenced learnset despite old lookup cache");
            set.learnset.Add(new PetSpeciesSkillEntry { skill = skill });
            Check(set.ValidateSet().Count == 0, "restored learnset validates without domain reload");
            skill.legacyIds = new[] { "qa.old" };
            catalog.ValidateCatalog(); set.ValidateSet();
            Check(catalog.TryGet("qa.old", out var alias) && alias == skill && set.TryGet("qa.old", out _), "explicit revalidation refreshes authoring aliases in both lookups");
        }
        static void SparseLegacySaves()
        {
            const string v1 = @"{""version"":1,""currency"":{""captured"":true,""gold"":7},""pets"":{""captured"":true,""activePetId"":""old-party"",""partyPetIds"":[""old-party""],""boxPetIds"":[""old-box""],""petStates"":[{""petId"":""old-party"",""definitionId"":""dragon"",""customization"":{""nickname"":""Old name"",""favorite"":true,""attack"":12,""defense"":8,""speed"":10,""secondaryElement"":4},""runtimeStats"":{""captured"":true,""level"":5,""health"":20,""maxHealth"":40,""equippedSkillIds"":[""bite"","""",""wing strike""]}},{""petId"":""old-box"",""definitionId"":""dragon""}]}}";
            var migrated = PlayerSaveMigration.Read(v1, out int source);
            Check(source == 1 && migrated.version == 14, "sparse v1 migrates to unchanged v14 format");
            Check(migrated.pets.partyPetIds.SequenceEqual(new[] { "old-party" }) && migrated.pets.boxSlots.Count == 1 && migrated.pets.boxSlots[0].petId == "old-box", "real v1 compact BOX representation becomes sparse slots without changing IDs");
            var pet = migrated.pets.petStates[0];
            Check(pet.customization.nickname == "Old name" && pet.customization.favorite && pet.customization.secondaryElement == PetElement.Wind, "legacy custom name favorite and raw secondary retained");
            Check(pet.customization.magicAttack == 12 && pet.customization.magicDefense == 8 && !pet.customization.hasGenetics && pet.runtimeStats.hasSkillLoadout, "sparse historical fields migrate without genetics reroll or empty loadout refill");
            Check(pet.runtimeStats.equippedSkillIds.SequenceEqual(new[] { "bite", "", "wing strike" }) && pet.runtimeStats.health == 20, "legacy skill IDs empty slot and injury preserved");
            string current = PlayerSaveMigration.Write(migrated);
            Check(PlayerSaveMigration.Write(PlayerSaveMigration.Read(current, out _)) == current, "sparse old save migration is idempotent");
            Write("sparse-v1-fixture.json", v1);
            Write("sparse-v1-migrated.json", current);
        }

        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/12. Verify projectile cancel form and UI bindings")]
        public static void Runtime() => EnemyAiVerification.BeginFollowupRun(Root);

        static void Types(GameObject actor, PetElement primary, PetElement secondary)
        {
            var metadata = actor.GetComponent<PetCollectionMetadata>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(PetCollectionMetadata).GetField("element", flags).SetValue(metadata, primary);
            typeof(PetCollectionMetadata).GetField("secondaryElement", flags).SetValue(metadata, secondary);
        }

        public static async Task RunRuntime(PetCommandInput input, Scene arena, Vector3 origin, Action<bool, string> check, Func<int, Task> gap)
        {
            await ElementGameplayVerification.RuntimeAtRoot(Root, input, arena, origin, check, gap);
            foreach (var root in arena.GetRootGameObjects()) if (root.GetComponent<CreatureCombatant>() != null) Object.Destroy(root);
            await gap(150);
            GameObject Spawn(string path, Vector3 position)
            {
                var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(actor, arena);
                var brain = actor.GetComponent<BasicCreatureEnemy>();
                if (brain != null) brain.enabled = false;
                return actor;
            }
            void Place(GameObject actor, Vector3 position)
            {
                var agent = actor.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);
                else actor.transform.position = position;
                Physics.SyncTransforms();
            }
            async Task Until(Func<bool> condition, float seconds, string label)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (!condition() && Time.realtimeSinceStartup < end) await gap(20);
                check(condition(), label);
            }
            var actor = Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon03.prefab", origin);
            var pet = actor.GetComponent<PetController>();
            pet.AssignOwner(input.transform);
            pet.roamAroundOwner = false;
            check(pet.TrySummonAt(origin, Vector3.forward), "edge fixture summon begins");
            await Until(() => pet.CanReceiveCommands, 6, "edge fixture summon confirmed");
            input.petSlots[0] = pet;
            input.SetActivePet(pet);
            var stats = actor.GetComponent<PetHudRuntimeStats>();
            stats.SetStatus(10000, 10000, 0, 0);
            var foe = Spawn("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab", origin + Vector3.forward * 10);
            var target = foe.GetComponent<CreatureCombatant>();
            var health = foe.GetComponent<DummyEnemy>();
            health.autoRevive = false;
            health.InitializeCaptureHealth(10000, 10000);
            Types(actor, PetElement.Fire, PetElement.Flying);
            Types(foe, PetElement.Nature, PetElement.Dark);
            var events = new List<CombatDamageResult>();
            void Record(CombatDamageResult result) { if (result.Hit.Source == actor) events.Add(result); }
            CombatDamageEvents.Applied += Record;
            try
            {
                async Task Prepare()
                {
                    await Until(() => !stats.GetSkills()[2].IsCoolingDown && (actor.GetComponent<PetCombatActionDriver>() == null || !actor.GetComponent<PetCombatActionDriver>().IsSkillBusy), 5, "projectile cooldown and action ready");
                    Place(actor, origin);
                    actor.transform.rotation = Quaternion.identity;
                    Place(foe, origin + Vector3.forward * 10);
                    await gap(40);
                }
                async Task Release()
                {
                    int before = actor.GetComponent<PetSkillPresentationPlayer>()?.ReleasedCount ?? 0;
                    stats.RequestSkill(2);
                    await Until(() => actor.GetComponent<PetSkillPresentationPlayer>().ReleasedCount > before, 5, "real animation releases projectile");
                    check(actor.GetComponent<PetSkillCombatExecutor>().ActiveProjectileCount > 0, "real projectile is in flight before edge mutation");
                }
                await Prepare();
                await Release();
                Types(actor, PetElement.Water, PetElement.Unknown);
                Types(foe, PetElement.Water, PetElement.Dragon);
                await Until(() => events.Any(e => e.Hit.SkillId == "projectile"), 3, "real flying projectile reaches target");
                var hit = events.First(e => e.Hit.SkillId == "projectile");
                check(hit.Hit.Elements.SourcePrimary == PetElement.Fire && hit.Hit.Elements.Attack == PetElement.Fire && Mathf.Abs(hit.HpLost - target.Mitigate(hit.Hit.RawDamage, hit.Hit.Kind) * .375f) < .02f, "actual flight retains source STAB and reads new defender types once");

                await Prepare();
                int count = events.Count;
                await Release();
                Place(foe, origin + Vector3.forward * 10 + Vector3.right * 6);
                await Until(() => actor.GetComponent<PetSkillCombatExecutor>().ActiveProjectileCount == 0, 4, "missed projectile expires");
                check(events.Count == count, "target crossing out of flight path takes no transferred damage");

                await Prepare();
                count = events.Count;
                await Release();
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(wall, arena);
                wall.transform.position = origin + Vector3.forward * 5 + Vector3.up * 2;
                wall.transform.localScale = new Vector3(10, 4, .5f);
                Physics.SyncTransforms();
                await Until(() => actor.GetComponent<PetSkillCombatExecutor>().ActiveProjectileCount == 0, 3, "flight collides with newly blocking wall");
                check(events.Count == count, "wall impact does not damage target behind it");
                Object.Destroy(wall);
                await gap(50);

                await Prepare();
                count = events.Count;
                await Release();
                target.ApplyCombatHit(new CombatHit(null, "qa-fatal", -1, 1000000, CreatureDamageKind.Physical, false, false, target.Center));
                await Until(() => actor.GetComponent<PetSkillCombatExecutor>().ActiveProjectileCount == 0, 4, "flight ends after target is downed");
                check(events.Count == count, "already downed target gets no projectile damage event");

                await Until(() => !stats.GetSkills()[0].IsCoolingDown && !actor.GetComponent<PetCombatActionDriver>().IsSkillBusy, 5, "bite ready for recall cancellation");
                int released = actor.GetComponent<PetSkillPresentationPlayer>().ReleasedCount;
                count = events.Count;
                stats.RequestSkill(0);
                check(actor.GetComponent<PetCombatActionDriver>().IsSkillBusy, "real bite is pending before recall");
                pet.Withdraw();
                await gap(550);
                check(actor.GetComponent<PetSkillPresentationPlayer>().ReleasedCount == released && events.Count == count && actor.GetComponent<PetSkillCombatExecutor>().ActiveProjectileCount == 0, "recall cancels pending release and all owned shots");

                check(pet.TrySummonAt(origin, Vector3.forward), "resummon after cancelled cast");
                await Until(() => pet.CanReceiveCommands, 6, "resummon is ready");
                input.SetActivePet(pet);
                stats.RequestSkill(1);
                var action = actor.GetComponent<PetCombatActionDriver>();
                check(action.IsSkillBusy, "buff windup starts before visual form change");
                var oldAnimator = actor.GetComponent<CreatureAnimationDriver>().BoundAnimator;
                var visual = actor.GetComponent<PetEvolutionVisualController>();
                check(visual.TryApply(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Dragons/Visual/Visual_Dragon02.prefab"), out string error), "real form visual replacement succeeds: " + error);
                await gap(250);
                check(!action.IsSkillBusy && actor.GetComponent<CreatureAnimationDriver>().BoundAnimator != oldAnimator && actor.GetComponent<CreatureCombatant>().OffenseBuffRemaining == 0, "form rebind cancels old animation token before buff release");

                foreach (int width in new[] { 1920, 1366 })
                {
                    await EnemyAiVerification.ResolutionForElements(width, width == 1920 ? 1080 : 768, "skill-hud");
                    var hud = Object.FindFirstObjectByType<GameplayHudController>();
                    check(hud != null && hud.PetSkillsAvailable, "HUB skill readiness bound to confirmed summoned pet " + width);
                    var menu = Object.FindFirstObjectByType<GameMenuController>();
                    menu.OpenPetsDirect();
                    await gap(200);
                    var doc = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(d => d.rootVisualElement?.Q("pets-panel") != null);
                    var tab = doc.rootVisualElement.Q<Button>("pets-tab-skills");
                    tab.Focus();
                    using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = tab; tab.SendEvent(submit); }
                    await gap(200);
                    Write("ui-skill-state-" + width + ".txt", "tab=" + doc.GetComponent<PetPartyPanelController>()?.CompanionTab + "; display=" + doc.rootVisualElement.Q("pets-tab-body-skills").resolvedStyle.display);
                    check(doc.rootVisualElement.Q("pets-tab-body-skills").resolvedStyle.display != DisplayStyle.None && ElementUi.All.Select(t => t.element).SequenceEqual(ElementCatalog.Entries.Select(t => t.Element)), "keyboard submit opens real skill tab and shared nine types " + width);
                    menu.HandleShortcut(KeyCode.Escape);
                    await gap(100);
                }

                await Until(() => !stats.GetSkills()[2].IsCoolingDown && !action.IsSkillBusy, 5, "ready for scene unload cancellation");
                stats.RequestSkill(2);
                check(action.IsSkillBusy, "cast pending before temporary scene unload");
                var temporary = SceneManager.CreateScene("QA cast unload (temporary)");
                SceneManager.MoveGameObjectToScene(actor, temporary);
                SceneManager.MoveGameObjectToScene(foe, temporary);
                count = events.Count;
                SceneManager.UnloadSceneAsync(temporary);
                await gap(650);
                check(actor == null && foe == null && events.Count == count, "scene unload destroys pending cast with no late damage callback");
                Write("flight-events.csv", "skill,cast,emission,raw,hpLost\n" + string.Join("\n", events.Select(e => $"{e.Hit.SkillId},{e.Hit.CastId},{e.Hit.EmissionIndex},{e.Hit.RawDamage},{e.HpLost}")));
            }
            finally { CombatDamageEvents.Applied -= Record; }
        }
    }
}
#endif

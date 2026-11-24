using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Capstone.Game.CreatureSystem.Editor {
    static class SkillStatusVerification {
        static readonly (string id, string name, float mp, float cd)[] balance = {
            ("blackhole.dark", "Hố Đen", 28, 13),
            ("spirit.dark", "Linh Hồn Ma Quái", 19, 15),
            ("orb.dark", "Quả Cầu Bóng Tối", 18, 6),
            ("flock.dark", "Đàn Dơi", 24, 12),
            ("stars.dragon", "Chùm Sao Rồng", 34, 15),
            ("breath.dragon", "Hơi Thở Rồng", 28, 10),
            ("laser.electric", "Chùm Tia Laser", 32, 14),
            ("thunder.electric", "Sấm Sét", 36, 17),
            ("spark.electric", "Tia Lửa", 11, 4),
            ("bolt.electric", "Tia Sét", 22, 9),
            ("orb.electric", "Điện Cầu", 29, 11),
            ("shield.fire", "Khiên Lửa", 30, 19),
            ("breath.fire", "Phun Lửa", 25, 9),
            ("projectile", "Quả Cầu Lửa", 28, 10),
            ("heatwave.fire", "Đốt Nóng", 19, 11),
            ("wing strike", "Chém Gió", 22, 10),
            ("cyclone.flying", "Lốc Xoáy", 27, 14),
            ("peck.flying", "Mổ", 20, 8),
            ("leafstorm.grass", "Bão Lá", 26, 14),
            ("orb.grass", "Bóng Năng Lượng", 18, 6),
            ("seed.volley.grass", "Đạn Hạt Liên Thanh", 29, 13),
            ("absorb.grass", "Hấp Thụ", 29, 23),
            ("poisonburst.grass", "Độc Nổ", 23, 14),
            ("protect.normal", "Bảo Vệ", 40, 36),
            ("curl.normal", "Cuộn Thủ", 21, 19),
            ("bubbleburst.water", "Bong Bóng Nổ", 27, 11),
            ("jet.water", "Siêu Súng Nước", 26, 10),
            ("orb.water", "Tia Bong Bóng", 19, 8),
            ("waterwhirl.water", "Tia Xoáy Nước", 29, 13),
            ("rain.water", "Tạo Mưa", 34, 32),
            ("roar.power", "Gầm Uy Lực", 20, 16),
        };

        [MenuItem("Tools/Capstone/Pet Skills/Verify status and balance assets")]
        public static void Verify() {
            var lines = new List<string>();
            int failures = 0;
            void Check(bool passed, string label) {
                if (!passed) failures++;
                lines.Add((passed ? "PASS " : "FAIL ") + label);
            }

            var catalog = Resources.Load<PetSkillCatalog>("PetSkills/PetSkillCatalog");
            Check(catalog != null, "Runtime catalog");
            if (catalog != null) {
                Check(catalog.skills.Count == 31 && catalog.skills.All(s => s != null) &&
                    catalog.skills.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 31,
                    "31 unique active skill IDs");
                Check(catalog.retiredSkills.Count == 9 && catalog.retiredSkills.All(s => s != null &&
                    !catalog.TryGet(s.Id, out _) && catalog.FindRetired(s.Id) == s), "9 legacy skills recognized only for migration");
                Check(catalog.buffAura != null && catalog.debuffAura != null && catalog.hpPotionAura != null &&
                    catalog.mpPotionAura != null && catalog.shockAura != null && catalog.burnAura != null, "Six linked status VFX prefabs");
                foreach (var (id, name, mp, cd) in balance) {
                    bool found = catalog.TryGet(id, out var skill);
                    var data = found ? skill.CreateHudData() : default;
                    Check(found && skill.displayName == name && data.manaCost == mp && data.cooldownSeconds == cd &&
                        skill.icon != null && skill.combat != null && skill.combat.authoredStatusRules && skill.presentation != null,
                        $"{id} | {name} | {mp} MP | {cd}s | sprite loads");
                }
                Check(catalog.TryGet("blackhole.dark", out var blackHole) && blackHole.combat.projectileVisual != null &&
                    blackHole.presentation.impact.effectPrefab != null && blackHole.combat.pattern == PetSkillPattern.ImpactArea &&
                    blackHole.combat.minimumHits == 6 && blackHole.combat.TravelRange == 16f,
                    "Hố Đen has flight/impact VFX, six pulses and 16m travel");
                Check(catalog.TryGet("spirit.dark", out var spirit) && spirit.combat.impactAtTargetFeet &&
                    spirit.combat.trackTargetUntilRelease && spirit.combat.statusCues.Length == 2,
                    "Linh Hồn Ma Quái tracks to release and spawns its VFX at the target's feet");
                var groundSkills = new[] { "spirit.dark", "flock.dark", "stars.dragon", "laser.electric",
                    "thunder.electric", "bubbleburst.water", "orb.water", "poisonburst.grass", "waterwhirl.water", "bolt.electric" };
                foreach (string id in groundSkills)
                    Check(catalog.TryGet(id, out var skill) && skill.combat.GroundsImpact &&
                        skill.presentation.impact.effectPrefab != null, id + " | VFX anchored under the target");
                foreach (string id in new[] { "breath.dragon", "breath.fire", "jet.water" })
                    Check(catalog.TryGet(id, out var skill) && skill.combat.releaseSocket == "Mouth" &&
                        skill.presentation.release.socket == "Mouth" && skill.presentation.release.followSocket,
                        id + " | channel leaves the mouth socket");
                Check(catalog.TryGet("shield.fire", out var fireShield) && fireShield.combat.fireContact &&
                    fireShield.presentation.release.fitActorBounds && fireShield.presentation.release.followSocket &&
                    fireShield.presentation.release.anchorAtActorFeet && fireShield.presentation.release.socket == "Ground" &&
                    fireShield.presentation.release.lifetimeSeconds == 10 &&
                    fireShield.presentation.release.effectPrefab != null &&
                    Quaternion.Angle(Quaternion.Euler(fireShield.presentation.release.localEulerAngles)
                        * fireShield.presentation.release.effectPrefab.transform.localRotation, Quaternion.identity) < .1f &&
                    fireShield.combat.statusCues.Any(c => c.kind == CombatAilment.DefenseUp && c.duration == 10),
                    "Khiên Lửa is upright, fitted and anchored at Ground for its active duration");
                Check(catalog.TryGet("cyclone.flying", out var cyclone) &&
                    cyclone.combat.pattern == PetSkillPattern.TravelingArea &&
                    cyclone.combat.projectileVisual != null &&
                    cyclone.combat.projectileVisual.GetComponent<MeshFilter>()?.sharedMesh is Mesh vortex &&
                    vortex.bounds.size.y > .25f && vortex.bounds.size.z > .1f &&
                    cyclone.combat.projectileVisual.GetComponent<MeshRenderer>()?.sharedMaterial?.shader?.isSupported == true &&
                    cyclone.presentation.impact.effectPrefab == null,
                    "Lốc Xoáy uses a 3D mesh in flight and has no stationary billboard impact");
                Check(catalog.TryGet("heatwave.fire", out var heatwave) && heatwave.combat.statusOnly &&
                    heatwave.combat.ObstacleRadius < heatwave.combat.projectileRadius &&
                    heatwave.combat.projectileVisual != null &&
                    heatwave.combat.projectileVisual.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 &&
                    heatwave.combat.statusCues.Any(c => c.kind == CombatAilment.Burn && c.chance == 1),
                    "Đốt Nóng uses a visual-only prefab, a narrow obstacle core and a wide Burn hitbox");
                Check(catalog.TryGet("leafstorm.grass", out var leaves) &&
                    leaves.combat.projectileVisual != null && leaves.presentation.impact.effectPrefab == null &&
                    leaves.combat.projectileVisual.GetComponentsInChildren<ParticleSystem>(true).All(system =>
                        system.main.simulationSpace == ParticleSystemSimulationSpace.Local),
                    "Bão Lá travels with local-space particles and has no stationary duplicate");
                Check(catalog.skills.Count(s => s?.combat?.authoredReleaseSound != null) == 29,
                    "29 Trung AudioClips loaded");
                Check(catalog.TryGet("roar.power", out var roar) && roar.combat.delivery == PetSkillDelivery.SelfBuff &&
                    roar.combat.statusOnly && roar.combat.projectileVisual == null &&
                    roar.combat.statusCues.Length == 1 && roar.combat.statusCues[0].kind == CombatAilment.PowerUp &&
                    Mathf.Approximately(roar.combat.statusCues[0].magnitude, .15f) && roar.combat.statusCues[0].duration == 8,
                    "Gầm Uy Lực is a single +15% PowerUp for 8s with no projectile");
            }

            var sprites = Resources.LoadAll<Sprite>("StatusIcons");
            var icons = new[] { "burn", "poison", "bleed", "stun", "root", "slow", "shock", "def_down", "spdef_down",
                "power_up", "defense_up", "shield", "invuln", "regen", "rain", "generic_buff" };
            foreach (string id in icons)
                Check(sprites.Any(sprite => sprite.name == "status_" + id), $"Runtime Sprite status_{id}");

            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(CreatureLibraryAuthoring.LibraryPath);
            var forms = library.families.SelectMany(f => f.forms).ToArray();
            var ids = new HashSet<string>(balance.Select(x => x.id), StringComparer.OrdinalIgnoreCase);
            Check(forms.Length == 33, "33 creature forms");
            foreach (var form in forms) {
                var profile = form.animationProfile;
                var set = form.skillSet;
                string name = profile != null ? profile.name : form.displayName;
                var animator = form.visualPrefab != null ? form.visualPrefab.GetComponentInChildren<Animator>(true) : null;
                var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
                var visualBinding = animator != null ? animator.GetComponent<CreatureVisualBinding>() : null;
                var owned = form.petPrefab != null ? form.petPrefab.GetComponent<PetHudRuntimeStats>() : null;
                var wild = form.wildPrefab != null ? form.wildPrefab.GetComponent<PetHudRuntimeStats>() : null;
                Check(set != null && owned?.SpeciesSkillSet == set && wild?.SpeciesSkillSet == set && visualBinding?.skillSet == set,
                    name + " | Owned, Wild and Visual use the same set");
                Check(set != null && set.enforceLearnset && set.initializeFreshWildLoadout &&
                    set.learnset.All(e => e?.skill != null && ids.Contains(e.skill.Id)) &&
                    set.defaultLoadout.All(s => s != null && ids.Contains(s.Id)) &&
                    profile != null && profile.skills.All(binding => binding != null && ids.Contains(binding.skillId)),
                    name + " | only active skills in learnset, defaults and animation mappings");
                var starter = set?.CreateFreshDefaultLoadout(1);
                var adult = set?.CreateFreshDefaultLoadout(36);
                Check(starter?.Count > 0 && starter[0].combatProfile?.statusOnly == false && adult?.Count == 4 &&
                    adult.All(skill => skill.unlocked && ids.Contains(skill.skillId) && skill.skillId != "roar.power"),
                    name + " | damaging starter and four valid level-36 slots");
                var roarEntry = set?.learnset.FirstOrDefault(e => e.skill != null && e.skill.Id == "roar.power");
                bool nonDefault = set != null && set.defaultLoadout.All(s => s != null && s.Id != "roar.power");
                var cast = profile?.actions.FirstOrDefault(action => action.action == CreatureAction.Cast);
                var states = controller != null ? controller.layers[0].stateMachine.states : Array.Empty<ChildAnimatorState>();
                bool hasSpell = cast != null && states.Any(entry => entry.state.name == cast.state.Replace("Base Layer.", "") && entry.state.motion != null);
                Check(roarEntry != null && roarEntry.learnAtLevel == 20 && nonDefault && hasSpell &&
                    profile.skills.Any(binding => binding.skillId == "roar.power" && binding.action == CreatureAction.Cast), name + " | Lv20 Cast Spell, manual equip");
                if (set == null || profile == null) continue;
                if (profile.definitionId == "Dragon Dusk" &&
                    set.TryGet("breath.dragon", out var breath))
                {
                    var state = states.FirstOrDefault(e => e.state.name == "Dragon Breath").state;
                    var clip = state?.motion as AnimationClip;
                    float duration = clip != null && state.speed > 0 ? clip.length / state.speed : 0;
                    Check(clip != null && clip.name.StartsWith("WingVolley", StringComparison.Ordinal) &&
                        breath.Presentation.release.followChannelAim && breath.Presentation.release.socket == "Mouth" &&
                        breath.skill.combat.releaseSocket == "Mouth" && breath.skill.combat.splashFraction > 0 &&
                        duration * (1 - breath.Presentation.releaseNormalized) >= breath.skill.combat.effectSeconds,
                        name + " | WingVolley covers the full mouth channel");
                }
                foreach (var entry in set.learnset) {
                    if (entry.skill == null || !ids.Contains(entry.skill.Id)) continue;
                    var action = profile.skills.FirstOrDefault(skill => skill.skillId == entry.skill.Id);
                    var binding = action != null ? profile.actions.FirstOrDefault(candidate => candidate.action == action.action) : null;
                    Check(binding != null && states.Any(state => state.state.name == binding.state.Replace("Base Layer.", "") && state.state.motion != null),
                        name + " | " + entry.skill.Id + " animation");
                }
            }

            if (catalog != null && forms.FirstOrDefault(f => f.skillSet?.speciesId == "wolf/03") is CreatureFormDefinition wolf)
                VerifyLegacySave(wolf, ids, Check);
            VerifySavedForm(library, Check);
            VerifySpecialZonesOnDisk(Check);

            lines.Insert(0, $"{lines.Count - failures}/{lines.Count} checks passed");
            string directory = Path.Combine(QaArtifactPaths.Root, "GameplayConsistency");
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "edit-mode.txt"), lines);
            if (failures != 0) Debug.LogError($"{failures} skill/status asset checks failed. {directory}");
            else Debug.Log($"Skill/status asset checks passed. {directory}");
        }

        static void VerifySpecialZonesOnDisk(Action<bool, string> check)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/DemoScene 1 - Creature QA.unity");
            try
            {
                var zones = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CreatureEncounterZone>(true))
                    .Where(zone => zone.zoneId is "capstone-special-dragon-fire" or "capstone-special-shell" or "capstone-special-sun-blossom")
                    .ToArray();
                check(zones.Length == 3, "Three special starter zones are present in the saved QA scene");
                foreach (var zone in zones)
                {
                    bool valid = zone.ValidateConfiguration(out string reason);
                    var entry = zone.spawnTable?.species.SingleOrDefault();
                    bool allDay = entry != null && (zone.ignoreSpeciesTimeWindows ||
                        Mathf.Approximately(entry.spawnFromHour, entry.spawnUntilHour));
                    check(zone.spawningEnabled && allDay && !zone.requireWaterNearby && valid && zone.EligibleSpeciesCount == 1,
                        $"{zone.zoneId} | active, all-day QA spawn | {reason}");
                    check(PrefabUtility.GetCorrespondingObjectFromSource(zone) is CreatureEncounterZone source &&
                        !source.ignoreSpeciesTimeWindows && (zone.zoneId != "capstone-special-shell" || source.requireWaterNearby),
                        zone.zoneId + " | shared zone prefab keeps its original eligibility rules");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void VerifySavedForm(CreatureLibrary library, Action<bool, string> check) {
            var family = library.families.Single(item => item.definitionId == "egg-shadow");
            var initial = family.forms.Single(form => form.formId == "01");
            var evolved = family.forms.Single(form => form.formId == "03");
            var prefabCatalog = AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>("Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset");
            check(prefabCatalog.TryGetSavedPrefab("egg-shadow", "01", 2, out var legacyPrefab) && legacyPrefab.gameObject == evolved.petPrefab,
                "Legacy first-form ID with committed stage 2 selects Form 03");

            var preview = EditorSceneManager.NewPreviewScene();
            try {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(initial.petPrefab, preview);
                var pet = actor.GetComponent<PetController>();
                var stats = actor.GetComponent<PetHudRuntimeStats>();
                var saved = new PetInstanceSaveData {
                    definitionId = "egg-shadow",
                    customization = new PetCustomizationSaveData { currentFormId = "03", evolutionStage = 2 },
                    runtimeStats = new PetRuntimeStatsSaveData {
                        captured = true, hasSkillLoadout = true, level = 36,
                        health = 80, maxHealth = 100, energy = 70, maxEnergy = 100,
                        equippedSkillIds = new List<string> { "protect.normal", "orb.dark", "", "curl.normal" }
                    }
                };
                var restore = typeof(PlayerSaveController).GetMethod("RestorePetState", BindingFlags.Static | BindingFlags.NonPublic);
                restore.Invoke(null, new object[] { pet, saved, false });
                check(stats.SpeciesSkillSet == evolved.skillSet && stats.Level == 36 && stats.Energy == 70,
                    "Live save restoration binds the saved Form before reading its stats");
                check(stats.CreateSaveData().equippedSkillIds.SequenceEqual(saved.runtimeStats.equippedSkillIds),
                    "Form-specific skills, slot order and empty slot survive restore on a base prefab");
            } finally {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void VerifyLegacySave(CreatureFormDefinition form, HashSet<string> active, Action<bool, string> check) {
            var preview = EditorSceneManager.NewPreviewScene();
            try {
                var pet = (GameObject)PrefabUtility.InstantiatePrefab(form.petPrefab, preview);
                var stats = pet.GetComponent<PetHudRuntimeStats>();
                var old = new PetRuntimeStatsSaveData {
                    captured = true, hasSkillLoadout = true, level = 36, health = 100, maxHealth = 100,
                    energy = 100, maxEnergy = 100,
                    equippedSkillIds = new List<string> { "bite.normal", "orb.dark", "", "blast" },
                    learnedSkillProgress = new List<PetSkillProgressSaveData> {
                        new() { skillId = "bite.normal", unlocked = true, usable = true },
                        new() { skillId = "blast", unlocked = true, usable = true }
                    }
                };
                bool accepted = stats.CanRestoreSaveData(old, out string error);
                check(accepted, "Legacy save is recognized " + error);
                if (!accepted) return;
                stats.RestoreFromSaveData(old);
                var saved = stats.CreateSaveData();
                var equipped = saved.equippedSkillIds;
                check(equipped.Count == 4 && active.Contains(equipped[0]) && equipped[1] == "orb.dark" &&
                    equipped[2] == "" && active.Contains(equipped[3]) && equipped[0] != equipped[3] &&
                    !equipped.Contains("roar.power"), "Legacy slots migrate without overwriting a valid slot, empty slot or manual-equip rule");
                check(saved.learnedSkillProgress.All(skill => active.Contains(skill.skillId)) &&
                    stats.GetLearnedSkills().Any(skill => skill.skillId == "roar.power"), "Retired IDs are removed and Lv36 learns eligible active skills");

                old.level = 1;
                old.equippedSkillIds = new List<string> { "orb.dark", "bite.normal", "", "blast" };
                stats.RestoreFromSaveData(old);
                equipped = stats.CreateSaveData().equippedSkillIds;
                check(equipped.SequenceEqual(new[] { "orb.dark", "", "", "" }), "Lv1 migration respects level and slot uniqueness");
            } finally {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [MenuItem("Tools/Capstone/Pet Skills/Verify skill panel unlocks")]
        public static void VerifySkillPanelUnlocks() {
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>(CreatureLibraryAuthoring.LibraryPath);
            var family = library.families.Single(value => value.definitionId == "egg-shadow");
            var evolved = family.forms.Single(value => value.formId == "03");
            var earlier = family.forms.Single(value => value.formId == "02");
            var preview = EditorSceneManager.NewPreviewScene();
            var lines = new List<string>();
            int failures = 0;
            void Check(bool passed, string name) {
                lines.Add((passed ? "PASS " : "FAIL ") + name);
                if (!passed) failures++;
            }

            try {
                var pet = (GameObject)PrefabUtility.InstantiatePrefab(evolved.petPrefab, preview);
                var stats = pet.GetComponent<PetHudRuntimeStats>();
                string[] expected = { "orb.dark", "spirit.dark", "flock.dark", "blackhole.dark", "curl.normal", "protect.normal", "roar.power" };
                var saved = stats.CreateSaveData();
                saved.level = 36;
                saved.energy = saved.maxEnergy = 100;
                saved.equippedSkillIds = new List<string> { "flock.dark", "orb.dark", "", "" };
                saved.learnedSkillProgress = expected.Select(id => new PetSkillProgressSaveData {
                    skillId = id, unlocked = id == "orb.dark", usable = id == "orb.dark"
                }).ToList();
                stats.RestoreFromSaveData(saved);
                var learned = stats.GetLearnedSkills();
                Check(learned.Select(value => value.skillId).ToHashSet().SetEquals(expected), "Lv36 panel receives all seven skills despite stale unlock flags");
                var equipped = stats.GetSkills();
                Check(equipped[0].skillId == "flock.dark" && equipped[0].unlocked && equipped[0].usable &&
                    equipped[1].skillId == "orb.dark" && string.IsNullOrEmpty(equipped[2].skillId) && string.IsNullOrEmpty(equipped[3].skillId),
                    "Equipped skills work and preserve both empty slots");
                int shield = learned.ToList().FindIndex(skill => skill.skillId == "curl.normal");
                Check(stats.TryEquipLearnedSkill(shield, 2) && stats.GetSkills()[2].skillId == "curl.normal", "An eligible recovered skill can be equipped by panel index");

                stats.SetIdentity(stats.DisplayName, 1);
                Check(stats.GetLearnedSkills().Select(skill => skill.skillId).SequenceEqual(new[] { "orb.dark" }) &&
                    !stats.GetSkills()[0].unlocked, "Lv1 gates both learned and equipped skills");
                stats.ConfigureSkillSet(earlier.skillSet, true);
                stats.SetIdentity(stats.DisplayName, 36);
                stats.ConfigureSkillSet(evolved.skillSet, true);
                Check(stats.GetLearnedSkills().Select(value => value.skillId).ToHashSet().SetEquals(expected), "Level and form changes refresh the eligible skill pool");
            } finally {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            lines.Insert(0, $"{lines.Count - failures}/{lines.Count} checks passed");
            string directory = Path.Combine(QaArtifactPaths.Root, "SkillPanelUnlocks");
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "edit-mode.txt"), lines);
            if (failures != 0) Debug.LogError($"{failures} skill panel checks failed. {directory}");
            else Debug.Log($"Skill panel unlock checks passed. {directory}");
        }
    }
}

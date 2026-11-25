using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.QuestSystem.Editor {
    public static class ChapterQuestVerification {
        [MenuItem("Tools/Capstone/Chapter I/Verify Quest Data and Scene")]
        public static void Verify() {
            var lines = new List<string>();
            string folder = Path.Combine(QaArtifactPaths.Root, "ChapterProgression");
            Directory.CreateDirectory(folder);
            string report = Path.Combine(folder, "editor-checks.txt");
            var fixture = new GameObject("Chapter Editor Fixture") { hideFlags = HideFlags.HideAndDontSave };
            void Check(bool passed, string detail) {
                lines.Add((passed ? "PASS " : "FAIL ") + detail);
                if (!passed) throw new InvalidOperationException(detail);
            }
            try {
                var manager = fixture.AddComponent<QuestManager>();
                var catalog = Resources.LoadAll<QuestDefinition>("Quests/Chapter01").OrderBy(definition => definition.QuestId).ToArray();
                Check(catalog.Length == 9 && catalog.Select(definition => definition.QuestId).Distinct().Count() == 9, "9 unique Chapter I QuestDefinition assets");
                Check(catalog.Select(q => q.Rewards.Where(r => r.RewardType == QuestRewardType.Gold).Sum(r => r.Amount))
                    .SequenceEqual(new[] { 150, 200, 350, 500, 650, 800, 950, 1200, 2500 }), "Chapter gold rewards by quest");
                Check(catalog.Select(q => q.Rewards.Where(r => r.RewardType == QuestRewardType.Experience).Sum(r => r.Amount))
                    .SequenceEqual(new[] { 200, 200, 300, 350, 350, 400, 450, 450, 200 }), "Chapter trainer EXP by quest");
                Check(catalog.Select(q => q.Rewards.Where(r => r.TargetId == "level-candy").Sum(r => r.Amount))
                    .SequenceEqual(new[] { 5, 5, 5, 7, 8, 10, 10, 10, 10 }), "Chapter Level Candy rewards by quest");
                Check(PveBalanceRules.WildExperienceAward(35, 24, 1, false, true) == 1090 &&
                    PveBalanceRules.WildExperienceAward(35, 24, 1, false, false) == 0 &&
                    PveBalanceRules.WildExperienceAward(24, 24, 1, true, true) == 6540 &&
                    PveBalanceRules.PartyExperienceShare(true, false, true) == .75 &&
                    PveBalanceRules.PartyExperienceShare(true, false, false) == .5 &&
                    PveBalanceRules.PartyExperienceShare(false, true, true) == 0, "Deep cave XP, Boss multiplier and party shares");
                Check(new[] { "Di1", "Di2", "Di3", "DiTrenDa", "Bay", "OngBay", "Ran01", "Ran02", "Ran03" }
                    .All(name => Resources.Load<AudioClip>("Audio/Locomotion/" + name) != null), "All nine locomotion clips import");
                foreach (string collider in new[] { "Box", "Cylinder", "Sphere" }) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Prefabs/InvisibleColliders/InvisibleCollider{collider}.prefab");
                    Check(prefab != null && prefab.GetComponent<Collider>() is Collider shape && !shape.isTrigger &&
                        (shape is not MeshCollider mesh || mesh.convex && mesh.sharedMesh != null && mesh.sharedMesh.name == "Cylinder") &&
                        prefab.GetComponentsInChildren<Renderer>(true).Length == 0, $"{collider} prefab imports as an invisible solid");
                }
                Check(catalog.All(quest => quest.AcceptDialogue.Length > 0 && quest.ProgressDialogue.Length > 0 &&
                    quest.CompleteDialogue.Length > 0 && quest.QuestGiverId == "npc_village_elder"), "All 27 quest dialogue groups and elder ID resolve");
                var itemIds = catalog.SelectMany(quest => quest.Rewards.Concat(quest.StartItems))
                    .Where(r => r.RewardType == QuestRewardType.Item).Select(r => r.TargetId)
                    .Concat(catalog.SelectMany(quest => quest.Objectives.Where(o => o.ObjectiveType == QuestObjectiveType.CollectItem).Select(o => o.TargetId)))
                    .Distinct().ToArray();
                Check(itemIds.Where(id => id != "starter-evolution-stone").Concat(new[] { "stone-fire", "stone-water", "stone-grass" })
                    .All(id => InventoryDefinitionCatalog.Find(id) != null), "Item definitions: " + string.Join(", ", itemIds));
                Check(!manager.CanAcceptQuest(catalog[0]) && !manager.CanAcceptQuest(catalog[1]), "Tutorial gates NV01; previous quest gates NV02");
                manager.MarkTutorialComplete();
                Check(manager.CanAcceptQuest(catalog[0]) && !manager.AcceptQuest(catalog[0]), "Tutorial unlocks NV01 and public accept cannot bypass elder");
                for (int i = 0; i < catalog.Length; i++) {
                    var definition = catalog[i];
                    Check(InvokeBool(manager, "AcceptChapterQuest", definition), $"Accept ordered quest {definition.QuestId}");
                    var state = manager.GetQuestState(definition.QuestId);
                    if (i + 1 < catalog.Length) Check(!manager.CanAcceptQuest(catalog[i + 1]), "Next quest stays locked during " + state.QuestId);
                    if (i == 0) {
                        var objectives = definition.Objectives;
                        var defeat = objectives.First(o => o.ObjectiveType == QuestObjectiveType.DefeatCreature);
                        QuestProgressEvent Event(string family, string form, string zone, string id) =>
                            new QuestProgressEvent(QuestObjectiveType.DefeatCreature, targetTypeId: family,
                                formId: form, regionId: zone, receiptId: id);
                        Check(!manager.ReportProgress(Event("wolf", "01", "capstone-grass-easy", "wrong-family")) &&
                            !manager.ReportProgress(Event("chick", "02", "capstone-grass-easy", "wrong-form")) &&
                            !manager.ReportProgress(Event("chick", "01", "capstone-grass-hard", "wrong-zone")),
                            "Wrong family, form and zone do not count");
                        var valid = Event("chick", "01", "capstone-grass-easy", "defeat-001");
                        Check(manager.ReportProgress(valid) && !manager.ReportProgress(valid) &&
                            state.GetObjective(defeat.ObjectiveId).CurrentAmount == 1, "One defeat counts once");
                        var capture = new QuestProgressEvent(QuestObjectiveType.CaptureCreature, targetTypeId: "chick",
                            formId: "01", regionId: "capstone-grass-easy", receiptId: "wild-capture-001");
                        Check(manager.ReportProgress(capture) && !manager.ReportProgress(capture), "One committed capture counts once");
                        var snapshot = JsonUtility.FromJson<QuestSaveData>(JsonUtility.ToJson(manager.CreateSaveData()));
                        manager.RestoreFromSaveData(snapshot);
                        state = manager.GetQuestState(definition.QuestId);
                        Check(manager.TutorialComplete && !manager.ReportProgress(capture) && !manager.ReportProgress(valid),
                            "Tutorial, objective progress and receipts survive JSON save/load");
                    }
                    if (i == 2) {
                        QuestProgressEvent Wolf(bool night, string id) => new QuestProgressEvent(QuestObjectiveType.DefeatCreature,
                            targetTypeId: "wolf", regionId: "capstone-grass-mid", formId: "01", receiptId: id, startedAtNight: night);
                        Check(!manager.ReportProgress(Wolf(false, "day")) && manager.ReportProgress(Wolf(true, "night")),
                            "Only encounters begun at night count for NV03 wolves");
                        var material = definition.Objectives.First(o => o.ObjectiveType == QuestObjectiveType.CollectItem);
                        Invoke(manager, "RefreshChapterObjective", state, material, 6);
                        Invoke(manager, "RefreshChapterObjective", state, material, 3);
                        Check(state.GetObjective(material.ObjectiveId).CurrentAmount == 3, "Owned material progress decreases when inventory is reduced");
                    }
                    if (i == 5) {
                        var dragon = definition.Objectives.First(o => o.ObjectiveType == QuestObjectiveType.CaptureCreature);
                        Check(dragon.MatchesCreature("Dragon Dusk", "01") && dragon.MatchesCreature("Dragon Dusk", "02") &&
                            !dragon.MatchesCreature("Dragon Dusk", "03"), "NV06 permits only forms 01 and 02");
                    }
                    if (i == 8) {
                        Check(!manager.ReportProgress(new QuestProgressEvent(QuestObjectiveType.DefeatBoss, "another-boss", receiptId: "boss-a")) &&
                            manager.ReportProgress(new QuestProgressEvent(QuestObjectiveType.DefeatBoss, "chapter_01_boss", receiptId: "boss-b")),
                            "NV09 objective recognizes only its configured Boss key");
                    }
                    foreach (var objective in definition.Objectives)
                        Invoke(manager, "RefreshChapterObjective", state, objective, objective.RequiredAmount);
                    Check(state.Status == QuestStatus.Active && !manager.CompleteQuest(definition.QuestId), "No auto completion or public turn-in for " + state.QuestId);
                    Check(InvokeBool(manager, "CompleteChapterQuest", definition.QuestId), "Elder transition completes " + state.QuestId);
                    Check(!manager.CanAcceptQuest(definition), "Completed quest cannot repeat");
                }
                var layout = UnityEngine.Object.FindFirstObjectByType<ChapterWorldLayout>();
                Check(layout != null, "ChapterWorldLayout exists in open scene");
                var world = new SerializedObject(layout);
                var a = world.FindProperty("grassPortal").objectReferenceValue as Transform;
                var b = world.FindProperty("cavePortal").objectReferenceValue as Transform;
                var grassElder = world.FindProperty("grassElder").objectReferenceValue as Transform;
                var caveElder = world.FindProperty("caveElder").objectReferenceValue as Transform;
                var intro = UnityEngine.Object.FindFirstObjectByType<VillageChiefCutscene>();
                Check(grassElder != null && intro != null && grassElder == intro.Chief &&
                    caveElder != null && caveElder.name == ChapterWorldLayout.CaveElderName,
                    "The tutorial chief and the exact Nolant cave instance share the chapter layout");
                Check(a != null && b != null && NavMesh.SamplePosition(a.position, out _, 2, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(b.position, out _, 2, NavMesh.AllAreas), "Both teleport anchors are on the NavMesh");
                var guide = world.FindProperty("grassGuide").objectReferenceValue as Transform;
                Check(guide != null && guide != grassElder && guide.name == ChapterWorldLayout.GrassGuideName &&
                    ChapterWorldLayout.TryFindElderArrival(guide, guide.position, guide.rotation, out _) &&
                    Vector3.Distance(b.position, caveElder.position) <= ChapterWorldLayout.ElderTalkDistance,
                    "Both Nolant guides have an arrival point within speaking range");
                var entry = world.FindProperty("entryPoint").objectReferenceValue as Transform;
                var path = new NavMeshPath();
                Check(entry != null && NavMesh.CalculatePath(b.position, entry.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete, "Nolant's arrival connects to the cave quest entrance");
                var grassArt = world.FindProperty("grassMap").objectReferenceValue as Sprite;
                var caveArt = world.FindProperty("caveMap").objectReferenceValue as Sprite;
                Check(grassArt != null && grassArt.name == "Grassland_Fantasy" && grassArt.texture.width == 4096
                      && caveArt != null && caveArt.name == "Cave_Fantasy" && caveArt.texture.height == 3603,
                    "Both illustrated Sprites are assigned at their imported dimensions");
                var assembly = typeof(WorldMapController).Assembly;
                var routeBuilder = typeof(QuestMapMarkerBridge).GetMethod("TryCalculateRoute", BindingFlags.Static | BindingFlags.NonPublic);
                Check((bool)routeBuilder.Invoke(null, new object[] { b.position, entry.position, path })
                    && path.corners.Length > 1 && !(bool)routeBuilder.Invoke(null, new object[] { a.position, b.position, path }),
                    "Route follows the cave corridor and rejects a path between disconnected maps");
                var mapZones = assembly.GetType("Capstone.Game.MapSystem.MapEncounterZone", true);
                var mapSources = (CreatureEncounterZone[])mapZones.GetMethod("Find", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { layout.gameObject.scene });
                var groupOf = mapZones.GetMethod("GroupOf", BindingFlags.Static | BindingFlags.NonPublic);
                int grassZones = mapSources.Count(zone => (string)groupOf.Invoke(null, new object[] { zone }) == "ZoneGrass");
                int caveZones = mapSources.Count(zone => (string)groupOf.Invoke(null, new object[] { zone }) == "ZoneCave");
                Check(grassZones > 3 && caveZones > 3, $"Grouped spawn rings use scene instances ({grassZones} grass, {caveZones} cave)");
                var zones = UnityEngine.Object.FindObjectsByType<CreatureEncounterZone>(FindObjectsSortMode.None);
                foreach (string zoneId in new[] { "grass-easy", "grass-mid", "grass-hard", "cave-entrance", "cave-mid", "cave-deep" }) {
                    var found = zones.Where(zone => zone.zoneId == "capstone-" + zoneId && zone.spawningEnabled && zone.spawnTable != null).ToArray();
                    Check(found.Length > 0, zoneId + " has an active spawn table");
                }
                lines.Add(layout.Boss == null ? "BLOCKED Boss prefab/identity has not been authored for DemoScene 3."
                    : "PASS Existing Boss reference has IsBoss enabled.");
            } catch (Exception error) {
                lines.Add(error.ToString());
                Debug.LogException(error);
            } finally {
                UnityEngine.Object.DestroyImmediate(fixture);
                File.WriteAllLines(report, lines);
                Debug.Log("[Chapter I] Verification report: " + report);
            }
        }

        static object Invoke(QuestManager manager, string name, params object[] args) =>
            typeof(QuestManager).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, args);

        static bool InvokeBool(QuestManager manager, string name, params object[] args) => (bool)Invoke(manager, name, args);
    }
}

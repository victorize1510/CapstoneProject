#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CodexSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.Elements
{
    // Chi chay tu menu ro rang; khong tu Play, khong ghi save that hay bake scene.
    public static class ElementGameplayVerification
    {
        public const string Root = QaArtifactPaths.Root + "gameplay-cleanup-elements-2026-11-17/";
        static string outputRoot;
        static readonly List<string> results = new List<string>();
        static void Write(string file, string content)
        {
            string root = outputRoot ?? Root;
            Directory.CreateDirectory(root);
            File.WriteAllText(root + file, content);
            File.AppendAllText(root + "created-paths.txt", root + file + "\n");
        }
        public static void ContractsAtRoot(string artifactRoot)
        {
            string full = Path.GetFullPath(artifactRoot);
            if (!full.StartsWith(Path.GetFullPath(QaArtifactPaths.Root), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("QA phai nam trong thu muc artifact tren E.");
            string previous = outputRoot;
            outputRoot = full.Replace('\\', '/').TrimEnd('/') + "/";
            try { Contracts(); }
            finally { outputRoot = previous; }
        }
        static void Check(bool ok, string label)
        {
            results.Add((ok ? "PASS " : "FAIL ") + label);
            if (!ok) throw new InvalidOperationException(label);
        }
        static bool Near(float a, float b) => Mathf.Abs(a - b) <= Mathf.Max(.002f, Mathf.Abs(b) * .0001f);

        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/5. Verify element contracts and save compatibility")]
        public static void Contracts()
        {
            results.Clear();
            try
            {
                var asset = Resources.Load<ElementMatchupTable>("Combat/ElementMatchups");
                Check(asset != null && asset.Lookup.IsValid && asset.rows.Count == 9, "one valid authored nine-row matchup table");
                var table = asset.Lookup;
                float[,] expected = {
                    {1,.5f,2,1,1,1,1,2,.5f}, {2,1,.5f,1,1,1,1,1,1}, {.5f,2,1,1,1,.5f,1,1,1},
                    {1,2,.5f,1,1,2,1,1,1}, {1,1,1,1,1,.5f,2,2,1}, {1,1,2,.5f,2,1,1,.5f,1},
                    {1,1,1,1,1,1,1,1,1}, {.5f,1,1,1,.5f,2,1,1,1}, {1,1,1,1,1,1,1,1,2}
                };
                for (int row = 0; row < 9; row++)
                    for (int col = 0; col < 9; col++)
                        Check(table.Against(ElementCatalog.Entries[row].Element, ElementCatalog.Entries[col].Element) == expected[row, col], "matrix " + row + "/" + col);
                Check(ElementDamageMath.Effectiveness(table, PetElement.Fire, PetElement.Nature, PetElement.Dark) == 4, "dual weakness x4");
                Check(ElementDamageMath.Effectiveness(table, PetElement.Fire, PetElement.Water, PetElement.Dragon) == .25f, "dual resistance x0.25");
                Check(ElementDamageMath.Effectiveness(table, PetElement.Fire, PetElement.Nature, PetElement.Water) == 1, "mixed dual x1");
                foreach (var first in ElementCatalog.Entries)
                    foreach (var second in ElementCatalog.Entries)
                        Check(ElementDamageMath.Effectiveness(table, PetElement.Fire, first.Element, second.Element) == ElementDamageMath.Effectiveness(table, PetElement.Fire, second.Element, first.Element), "dual order " + first.Element + "/" + second.Element);
                var fire = new CombatElementSnapshot(PetElement.Fire, PetElement.Fire, PetElement.Fire);
                Check(ElementDamageMath.Stab(fire) == 1.5f, "duplicate source STAB once");
                Check(ElementDamageMath.Stab(new CombatElementSnapshot(PetElement.Fire, PetElement.Water, PetElement.Fire)) == 1.5f, "secondary source STAB");
                Check(ElementDamageMath.Stab(new CombatElementSnapshot(PetElement.Fire, PetElement.Water, PetElement.Unknown)) == 1, "nonmatching source neutral");
                Check(ElementDamageMath.Multiplier(table, fire, PetElement.Nature, PetElement.Dark) == 6, "STAB dual total x6 without clamp");
                Check(ElementDamageMath.Effectiveness(table, PetElement.Fire, PetElement.Nature, PetElement.Nature) == 2, "duplicate target counted once");
                foreach (var missing in new[] { PetElement.Unknown, PetElement.Wind, PetElement.Earth, PetElement.Ice, PetElement.Light, (PetElement)999 })
                {
                    Check(table.Against(missing, PetElement.Fire) == 1 && table.Against(PetElement.Fire, missing) == 1, "legacy or invalid neutral " + missing);
                    Check(ElementDamageMath.Stab(new CombatElementSnapshot(missing, missing, missing)) == 1, "unclassified cannot STAB " + missing);
                    Check(ElementCatalog.Name(missing) == ElementCatalog.UnclassifiedName, "unclassified UI " + missing);
                }
                Check(ElementDamageMath.Multiplier(table, default, PetElement.Nature, PetElement.Dark) == 1, "legacy hit has no elemental rules");
                Check(ElementCatalog.IsValidCreatureTypes(PetElement.Fire, PetElement.Unknown), "new single-type authoring accepts empty secondary");
                Check(!ElementCatalog.IsValidCreatureTypes(PetElement.Unknown, PetElement.Fire) && !ElementCatalog.IsValidCreatureTypes(PetElement.Wind, PetElement.Unknown), "new authoring rejects missing or legacy primary");
                Check(!ElementCatalog.IsValidCreatureTypes(PetElement.Fire, PetElement.Ice), "new authoring rejects legacy secondary");
                Check(!new ElementMatchupLookup(new[] { new ElementMatchupRow { attack = PetElement.Fire, strong = new[] { PetElement.Nature, PetElement.Nature } } }).IsValid, "reject duplicate pair");
                Check(!new ElementMatchupLookup(new[] { new ElementMatchupRow { attack = PetElement.Fire, strong = new[] { PetElement.Nature }, weak = new[] { PetElement.Nature } } }).IsValid, "reject conflicting pair");
                Check(!new ElementMatchupLookup(new[] { new ElementMatchupRow { attack = PetElement.Wind } }).IsValid, "reject unsupported authoring");
                Check(!new ElementMatchupLookup(new[] { new ElementMatchupRow { attack = PetElement.Normal, strong = new[] { PetElement.Fire } } }).IsValid, "normal attack remains neutral");
                foreach (var entry in ElementCatalog.Entries)
                {
                    Check(CodexElementAdapter.ToPet(CodexElementAdapter.ToCodex(entry.Element)) == entry.Element, "explicit enum adapter " + entry.Element);
                    var token = ElementUi.Find(entry.Element);
                    Check(token != null && token.name == entry.Name && ElementUi.Art(entry.IconKey + "-glyph") != null, "UI label and original glyph " + entry.Element);
                }
                Check((int)PetElement.Wind == 4 && (int)PetElement.Dark == 9 && (int)PetElement.Dragon == 10 && (int)CodexType.Dark == 8 && (int)CodexType.Dragon == 11, "legacy enum integers stable");
                Check(SkillElementAssignments.Resolve("bite", PetElement.Nature) == PetElement.Normal && SkillElementAssignments.Resolve("blast", PetElement.Light) == PetElement.Normal && SkillElementAssignments.Resolve("wing strike", PetElement.Wind) == PetElement.Flying && SkillElementAssignments.Resolve("projectile", PetElement.Fire) == PetElement.Fire, "four approved skill IDs");
                Check(SkillElementAssignments.Resolve("unreviewed", PetElement.Wind) == PetElement.Wind, "other legacy content not guessed");
                SaveRoundTrips();
                AuditRuntimeBindings();
                var query = new PetCodexQuery { Search = "Secret", Type = CodexType.Fire };
                var hidden = new PetCodexEntryViewData { Id = "hidden", Number = 1, Name = "Secret", PrimaryType = CodexType.Fire, Discovery = CodexDiscovery.Unknown };
                var output = new List<PetCodexEntryViewData>();
                query.Apply(new[] { hidden }, output);
                Check(output.Count == 0, "undiscovered species does not leak through type or name search");
                // Can mau duong tinh: mot so ban Mono tra 0 cho counter nay.
                long controlBefore = GC.GetAllocatedBytesForCurrentThread();
                var control = new byte[8192];
                GC.KeepAlive(control);
                long controlBytes = GC.GetAllocatedBytesForCurrentThread() - controlBefore;
                bool allocationCounterValid = controlBytes >= 8192;
                var watch = new Stopwatch();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                watch.Start();
                float total = 0;
                for (int i = 0; i < 100000; i++) total += ElementDamageMath.Multiplier(table, fire, PetElement.Nature, PetElement.Dark);
                watch.Stop();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Check(total == 600000, "100000 cached lookups remain deterministic");
                Write("element-lookup-performance.txt", "100000 lookups; ns total=" + watch.ElapsedTicks * (1000000000d / Stopwatch.Frequency) + "; raw thread allocation counter=" + bytes + "; counter valid=" + allocationCounterValid + "; positive control=" + controlBytes + "; do not claim zero GC when counter is invalid");
                Write("element-contract-status.txt", "COMPLETE " + results.Count + " checks; " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex) { Write("element-contract-status.txt", "FAILED\n" + ex); UnityEngine.Debug.LogException(ex); }
            finally { Write("element-contract-checks.txt", string.Join("\n", results)); }
        }

        static void AuditRuntimeBindings()
        {
            var lines = new List<string>();
            var clips = new HashSet<AnimationClip>();
            var eventLines = new List<string>();
            string[] actors = {
                "Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon01.prefab",
                "Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon02.prefab",
                "Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon03.prefab",
                "Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab"
            };
            foreach (string path in actors)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = prefab.GetComponentInChildren<Animator>(true);
                Check(animator != null && animator.runtimeAnimatorController != null, "runtime animation controller still bound " + path);
                lines.Add(path + " => " + AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip == null || !clips.Add(clip)) continue;
                    var events = AnimationUtility.GetAnimationEvents(clip);
                    lines.Add("  " + AssetDatabase.GetAssetPath(clip) + " / " + clip.name + " / events=" + events.Length);
                    foreach (var item in events) eventLines.Add(clip.name + " / " + item.functionName + " / time=" + item.time);
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:PetSkillDefinition", new[] { "Assets/Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>(path);
                Check(skill.CreateHudData().element == SkillElementAssignments.Resolve(skill.Id, skill.element), "definition UI/combat element contract " + skill.Id);
                lines.Add("skill " + skill.Id + " / raw=" + skill.element + " / " + path);
            }
            lines.Add("unique clips=" + clips.Count + "; imported animation events=" + eventLines.Count);
            lines.AddRange(eventLines);
            Write("runtime-binding-audit.txt", string.Join("\n", lines));
        }

        static void SaveRoundTrips()
        {
            var data = new PlayerSaveData { quest = null, achievements = null, currency = new CurrencySaveData { captured = true, gold = 321 } };
            data.inventory.captured = true;
            data.inventory.items.Add(new InventoryItemSaveData { itemId = "fixture-ball", quantity = 5, stackable = true, maxStackSize = 99 });
            data.profile.captured = true;
            data.profile.favoriteCodexForms.Add("dragon:02");
            data.profile.discoveredCodexForms.Add("dragon:03");
            data.pets.captured = true;
            data.pets.activePetId = "fixture-pet";
            data.pets.partyPetIds.Add("fixture-pet");
            data.pets.petStates.Add(new PetInstanceSaveData {
                petId = "fixture-pet", definitionId = "dragon",
                customization = new PetCustomizationSaveData { nickname = "Tên giữ nguyên", favorite = true, currentFormId = "02", evolutionStage = 1, secondaryElement = PetElement.Wind, hasGenetics = true, nature = "Serious", individualValues = new PetStatValues { hp = 17, attack = 13 }, effortValues = new PetStatValues { speed = 19 }, captureBallId = "fixture-ball" },
                runtimeStats = new PetRuntimeStatsSaveData { captured = true, hasSkillLoadout = true, level = 18, health = 40, maxHealth = 80, energy = 0, maxEnergy = 0, equippedSkillIds = new List<string> { "bite", "blast", "projectile", "wing strike" } }
            });
            string before = PlayerSaveMigration.Write(data);
            for (int version = 1; version <= PlayerSaveData.CurrentVersion; version++)
            {
                var root = JObject.Parse(before); root["version"] = version;
                var restored = PlayerSaveMigration.Read(root.ToString(), out int sourceVersion);
                Check(sourceVersion == version && PlayerSaveMigration.Write(restored) == before, "save v" + version + " roundtrip preserves IDs genetics forms raw legacy roster and inventory");
                Check(PlayerSaveMigration.Write(PlayerSaveMigration.Read(PlayerSaveMigration.Write(restored), out _)) == before, "save migration idempotent v" + version);
            }
            Write("element-save-fixture.json", before);
            Check(PlayerSaveData.CurrentVersion == 14, "save version unchanged at 14");
        }

        [MenuItem("Tools/Capstone/Advanced/Gameplay Cleanup/6. Verify elemental receivers and skill pipeline")]
        public static void Runtime() => EnemyAiVerification.BeginElementRun(Root + "elements/");

        public static async Task RuntimeAtRoot(string artifactRoot, PetCommandInput input, Scene arena, Vector3 origin, Action<bool, string> check, Func<int, Task> gap)
        {
            string previous = outputRoot;
            outputRoot = artifactRoot;
            try { await RunRuntime(input, arena, origin, check, gap); }
            finally { outputRoot = previous; }
        }

        static void Types(GameObject actor, PetElement primary, PetElement secondary)
        {
            var metadata = actor.GetComponent<PetCollectionMetadata>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(PetCollectionMetadata).GetField("element", flags).SetValue(metadata, primary);
            typeof(PetCollectionMetadata).GetField("secondaryElement", flags).SetValue(metadata, secondary);
        }

        public static async Task RunRuntime(PetCommandInput input, Scene arena, Vector3 origin, Action<bool, string> check, Func<int, Task> gap)
        {
            GameObject Spawn(string path, Vector3 position)
            {
                var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(actor, arena);
                var brain = actor.GetComponent<BasicCreatureEnemy>(); if (brain != null) brain.enabled = false;
                return actor;
            }
            var owned = Spawn("Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon03.prefab", origin);
            var pet = owned.GetComponent<PetController>(); pet.AssignOwner(input.transform); pet.roamAroundOwner = false;
            check(pet.TrySummonAt(origin, Vector3.forward), "element fixture summon begins");
            float until = Time.realtimeSinceStartup + 6;
            while (!pet.CanReceiveCommands && Time.realtimeSinceStartup < until) await gap(50);
            check(pet.CanReceiveCommands, "element fixture real summon confirmed");
            input.petSlots[0] = pet; input.SetActivePet(pet); pet.enabled = false;
            var foe = Spawn("Assets/Game/CreatureSystem/Runtime/Bats/Enemy/Enemy_Bat01.prefab", origin + Vector3.forward * 2);
            var target = foe.GetComponent<CreatureCombatant>(); var health = foe.GetComponent<DummyEnemy>();
            health.autoRevive = false; health.InitializeCaptureHealth(10000, 10000);
            var source = owned.GetComponent<CreatureCombatant>(); var stats = owned.GetComponent<PetHudRuntimeStats>();
            stats.SetStatus(10000, 10000, 0, 0);
            Types(owned, PetElement.Fire, PetElement.Fire); Types(foe, PetElement.Nature, PetElement.Dark);
            var snapshot = source.SnapshotElements(PetElement.Fire);
            CombatHit Hit(CreatureDamageKind kind, bool dot = false, bool legacy = false, float raw = 10, int emission = 0) => legacy
                ? new CombatHit(owned, "legacy", 1, raw, kind, false, dot, target.Center)
                : new CombatHit(owned, "projectile", 2, raw, kind, true, dot, target.Center, emission, snapshot);
            foreach (var kind in new[] { CreatureDamageKind.Physical, CreatureDamageKind.Magical })
            {
                float expected = target.Mitigate(10, kind);
                var result = target.ApplyCombatHit(Hit(kind));
                check(Near(result.HpLost, expected * 6) && result.Hit.RawDamage == 10 && result.Hit.Critical, "enemy receiver mitigation then x6 once " + kind);
                check(Near(target.ApplyCombatHit(Hit(kind, legacy: true)).HpLost, expected), "legacy receiver neutral " + kind);
                check(Near(target.ApplyCombatHit(Hit(kind, dot: true)).HpLost, expected), "bleed bypasses STAB and matchup " + kind);
            }
            Types(owned, PetElement.Water, PetElement.Unknown);
            check(Near(target.ApplyCombatHit(Hit(CreatureDamageKind.Physical)).HpLost, target.Mitigate(10, CreatureDamageKind.Physical) * 6), "in-flight source snapshot survives source form type change");
            Types(foe, PetElement.Water, PetElement.Dragon);
            check(Near(target.ApplyCombatHit(Hit(CreatureDamageKind.Physical)).HpLost, target.Mitigate(10, CreatureDamageKind.Physical) * .375f), "defender types read at valid impact");
            Types(foe, PetElement.Nature, PetElement.Dark);
            float lost = 0;
            for (int i = 0; i < 5; i++) lost += target.ApplyCombatHit(Hit(CreatureDamageKind.Physical, raw: 20, emission: i)).HpLost;
            check(Near(lost, target.Mitigate(20, CreatureDamageKind.Physical) * 6 * 5), "five emission raw budget split keeps one multiplier per hit");
            Types(owned, PetElement.Nature, PetElement.Dark);
            var received = source.ApplyCombatHit(new CombatHit(foe, "orb.dark", 3, 10, CreatureDamageKind.Magical, false, false, source.Center, 0, snapshot));
            check(Near(received.HpLost, source.Mitigate(10, CreatureDamageKind.Magical) * 6), "owned pet uses same elemental receiver");

            var events = new List<CombatDamageResult>();
            void Record(CombatDamageResult result) { if (result.Hit.Source == owned) events.Add(result); }
            CombatDamageEvents.Applied += Record;
            try
            {
                pet.enabled = true; Types(owned, PetElement.Fire, PetElement.Flying); Types(foe, PetElement.Nature, PetElement.Dark);
                var action = owned.GetComponent<PetCombatActionDriver>(); var executor = owned.GetComponent<PetSkillCombatExecutor>();
                for (int slot = 0; slot < 4; slot++)
                {
                    string id = stats.GetSkills()[slot].skillId;
                    int started = action != null ? action.StartedCount : 0, hitCount = events.Count;
                    stats.RequestSkill(slot);
                    action = owned.GetComponent<PetCombatActionDriver>();
                    check(action != null, "runtime lazily creates shared action driver " + id);
                    until = Time.realtimeSinceStartup + 6;
                    while ((action.StartedCount == started || action.IsSkillBusy) && Time.realtimeSinceStartup < until) await gap(40);
                    await gap(400);
                    check(action.StartedCount == started + 1, "real equipped skill commits once " + id);
                    if (slot == 1) check(source.OffenseBuffRemaining > 0 && !events.Skip(hitCount).Any(e => e.Hit.SkillId == id) && events.Skip(hitCount).All(e => e.Hit.DamageOverTime), "buff produces no hit; pre-existing bite bleed continues separately");
                    else
                    {
                        until = Time.realtimeSinceStartup + 3;
                        while (!events.Skip(hitCount).Any(e => e.Hit.SkillId == id && !e.Hit.DamageOverTime) && Time.realtimeSinceStartup < until) await gap(50);
                        var actual = events.Skip(hitCount).Where(e => e.Hit.SkillId == id && !e.Hit.DamageOverTime).ToArray();
                        var expectedType = slot == 0 ? PetElement.Normal : slot == 2 ? PetElement.Fire : PetElement.Flying;
                        check(actual.Length > 0 && actual.All(e => e.Hit.Elements.Enabled && e.Hit.Elements.Attack == expectedType), "real skill emissions carry canonical element " + id);
                        check(actual.GroupBy(e => e.Hit.CastId + ":" + e.Hit.EmissionIndex).All(g => g.Count() == 1), "no duplicate skill damage " + id);
                        if (slot == 3) check(executor.LastVolleyCount >= 2 && actual.All(e => Near(e.Hit.RawDamage, executor.LastCastRawBudget / executor.LastVolleyCount)), "real wind volley keeps raw budget and per-cast snapshot");
                    }
                }
                check(stats.MaxEnergy == 0 && stats.Energy == 0 && stats.GetSkills().All(s => s.manaCost == 0), "MP retirement remains intact");
                Write("element-skill-impact-results.csv", "skill,cast,emission,element,sourcePrimary,sourceSecondary,kind,raw,hpLost,critical,dot\n" + string.Join("\n", events.Select(e => string.Join(",", e.Hit.SkillId,e.Hit.CastId,e.Hit.EmissionIndex,e.Hit.Elements.Attack,e.Hit.Elements.SourcePrimary,e.Hit.Elements.SourceSecondary,e.Hit.Kind,e.Hit.RawDamage.ToString(System.Globalization.CultureInfo.InvariantCulture),e.HpLost.ToString(System.Globalization.CultureInfo.InvariantCulture),e.Hit.Critical,e.Hit.DamageOverTime))));
                await UiBindings(check, gap);
            }
            finally { CombatDamageEvents.Applied -= Record; }
        }

        static async Task UiBindings(Action<bool, string> check, Func<int, Task> gap)
        {
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            foreach (int width in new[] { 1920, 1366 })
            {
                int height = width == 1920 ? 1080 : 768;
                menu.OpenPetsDirect(); await gap(220);
                await EnemyAiVerification.ResolutionForElements(width, height, "pet");
                var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                check(docs.Any(d => d.rootVisualElement?.Q("pets-panel") != null), "real Pet document present " + width);
                menu.OpenBoxDirect(); await gap(180);
                var box = docs.Select(d => d.rootVisualElement?.Q("box-filter-panel")).FirstOrDefault(r => r != null);
                check(box != null && box.Query<Button>(className: "box-filter-chip").ToList().Count == 10, "BOX retains all plus nine existing filter slots " + width);
                await EnemyAiVerification.ResolutionForElements(width, height, "box");
                menu.OpenCodexDirect(); await gap(180);
                var codex = docs.Select(d => d.rootVisualElement?.Q("codex-panel")).FirstOrDefault(r => r != null);
                check(codex != null && codex.Query<Button>(className: "codex-category").ToList().Count == 10, "Codex all plus nine catalog types " + width);
                await EnemyAiVerification.ResolutionForElements(width, height, "codex");
                menu.HandleShortcut(KeyCode.Escape); await gap(150);
                check(!GameplayPause.IsPaused, "Back restores gameplay gate " + width);
            }
        }
    }
}
#endif

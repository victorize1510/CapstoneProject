using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.QuestSystem;
using Capstone.Game.QuestSystem.Save;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.SaveSystem.Editor {
    // Batch-only: all objects are temporary and every save is under a unique temp directory.
    public static class SaveSystemValidation {
        [Serializable] sealed class TestResult { public string name; public bool passed; public string error; }
        [Serializable] sealed class Report {
            public string unityVersion;
            public string fixtures;
            public int passed;
            public int failed;
            public List<TestResult> tests = new List<TestResult>();
        }
        static readonly List<Object> Owned = new List<Object>();
        static Report report;
        static string caseRoot;

        public static void RunBatch() {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in a separate Unity batch process.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            report = new Report { unityVersion = Application.unityVersion,
                fixtures = Path.Combine(Path.GetTempPath(), "CapstoneSaveValidation", Guid.NewGuid().ToString("N")) };
            Test("Missing save does not write", Missing);
            Test("Full snapshot save-load-edit-save-load", FullRoundtrip);
            for (int i = 1; i <= 5; i++) { int version = i; Test("Migrate v" + version + " and preserve original archive", () => Migration(version)); }
            Test("Missing sections remain absent", MissingSections);
            Test("Legacy nested defaults and missing customization", NestedDefaults);
            Test("Corrupt primary recovers backup without poisoning it", BackupRecovery);
            Test("Both copies corrupt refuse overwrite", CorruptCopies);
            Test("Future primary blocks downgrade", FuturePrimary);
            Test("Future backup blocks overwrite", FutureBackup);
            Test("Duplicate JSON keys rejected", DuplicateJson);
            Test("Invalid roster references rejected", InvalidRoster);
            Test("Inventory repeated restore and stable rename", InventoryRoundtrip);
            Test("Inventory failed restore keeps original", InventoryFailure);
            Test("Legacy inventory alias resolves stable ID", InventoryAlias);
            Test("Unknown prototype item preserves metadata", PrototypeItem);
            Test("Two-slot skill loadout", () => Skills(2));
            Test("Four-slot skill loadout with gaps", () => Skills(4));
            Test("Explicit empty skill loadout stays empty", EmptySkills);
            Test("Skill legacy alias migrates to stable ID", SkillAlias);
            Test("Missing skill rejected without mutation", MissingSkill);
            Test("Quest claimed rewards survive repeated load", QuestRoundtrip);
            Test("Unknown quest rejected without clearing state", MissingQuest);
            Test("Controller save-load-edit-save-load", ControllerRoundtrip);
            Test("Controller corrupt load protects file from save", ControllerCorrupt);
            Test("Controller refuses save before loading existing slot", ControllerUnloaded);
            Test("Controller missing source protects all data", ControllerMissingSource);
            Test("Legacy quest file migrates once without deletion", LegacyQuest);
            Test("Party Box nickname favorite stats integrated roundtrip", RosterRoundtrip);
            Test("Missing pet catalog blocks load before applying inventory", MissingPet);
            Test("Missing pet customization survives disk roundtrip", MissingCustomization);
            string path = Argument("-saveValidationReport") ?? Path.Combine(report.fixtures, "report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("SAVE_VALIDATION: " + report.passed + " passed, " + report.failed + " failed. " + path);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }

        static void Test(string name, Action action) {
            caseRoot = Path.Combine(report.fixtures, report.tests.Count.ToString("D2"));
            Directory.CreateDirectory(caseRoot);
            var result = new TestResult { name = name };
            try { action(); result.passed = true; report.passed++; }
            catch (Exception e) { result.error = e.ToString(); report.failed++; Debug.LogError(name + ": " + e); }
            finally {
                for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) Object.DestroyImmediate(Owned[i]);
                Owned.Clear();
                report.tests.Add(result);
            }
        }

        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Equal<T>(T expected, T actual, string message) { Check(EqualityComparer<T>.Default.Equals(expected, actual), message + ": expected " + expected + ", got " + actual); }
        static void Throws(Action action) {
            try { action(); } catch (Exception) { return; }
            throw new InvalidOperationException("Expected rejection.");
        }
        static T Component<T>() where T : Component {
            var go = new GameObject(typeof(T).Name + "-test");
            go.SetActive(false);
            Owned.Add(go);
            return go.AddComponent<T>();
        }
        static T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); Owned.Add(asset); return asset; }
        static void Set(object target, string field, object value) {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null) throw new MissingFieldException(target.GetType().Name, field);
            info.SetValue(target, value);
        }
        static string Argument(string key) {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        static PlayerJsonFileSaveStore Store() => new PlayerJsonFileSaveStore(caseRoot);
        static PlayerSaveData Currency(int gold = 10) => new PlayerSaveData {
            quest = null, inventory = null, pets = null, profile = null, achievements = null,
            currency = new CurrencySaveData { captured = true, gold = gold }
        };
        static PlayerSaveData Read(PlayerJsonFileSaveStore store) {
            Check(store.TryLoad("main", out var data, out var error), error);
            return data;
        }
        static void Write(PlayerJsonFileSaveStore store, PlayerSaveData data) { Check(store.TrySave("main", data, out var error), error); }
        static PlayerSaveData Full() {
            var data = new PlayerSaveData {
                inventory = new InventorySaveData { captured = true, items = new List<InventoryItemSaveData> {
                    new InventoryItemSaveData { itemId = "herb", displayName = "Herb", quantity = 12, stackable = true, maxStackSize = 99, healAmount = 400 }
                } },
                currency = new CurrencySaveData { captured = true, gold = 345 },
                profile = new PlayerProfileSaveData { captured = true, playerId = "player-one", displayName = "Tester", playTimeSeconds = 123456, capturedSpeciesIds = new List<string> { "leaf" } },
                achievements = new AchievementSaveData { captured = true, unlockedAchievementIds = new List<string> { "first-catch" }, metrics = new List<AchievementMetricSaveData> { new AchievementMetricSaveData { metricId = "capture", progress = 4 } } },
                pets = new PetRosterSaveData { captured = true, activePetId = "p1", partyPetIds = new List<string> { "p1", "", "p2" }, boxPetIds = new List<string> { "p3" }, releasedPetIds = new List<string> { "released" }, releasedToday = 2, releaseCountDateUtc = "2026-01-01" }
            };
            foreach (string id in new[] { "p1", "p2", "p3" }) data.pets.petStates.Add(new PetInstanceSaveData {
                petId = id, definitionId = "leaf",
                customization = new PetCustomizationSaveData { nickname = id + "-nick", favorite = true, evolutionStage = 1, currentFormId = "form2", promptedEvolutionIds = new List<string> { "e1" }, resourceInvestments = new List<PetResourceInvestmentSaveData> { new PetResourceInvestmentSaveData { itemId = "berry", quantity = 10 } } },
                runtimeStats = new PetRuntimeStatsSaveData { captured = true, hasSkillLoadout = true, level = 12, health = 75, maxHealth = 100, equippedSkillSlotCount = 4, equippedSkillIds = new List<string> { "leaf", "", "gust" } }
            });
            data.quest.questStates.Add(new QuestRuntimeSaveData { questId = "q1", status = QuestStatus.Completed, rewardsClaimed = true });
            return data;
        }
        static void Missing() {
            var store = Store();
            Check(!store.TryLoad("main", out _, out var error) && string.IsNullOrEmpty(error), "Missing file should be non-error.");
            Check(!store.Exists("main"), "Must not create a save on read.");
        }
        static void FullRoundtrip() {
            var store = Store(); var data = Full();
            Write(store, data);
            Equal(JsonUtility.ToJson(data), JsonUtility.ToJson(Read(store)), "All sections preserved");
            data.currency.gold = 987; data.inventory.items[0].quantity = 8; data.pets.petStates[0].customization.nickname = "Renamed";
            Write(store, data);
            Equal(JsonUtility.ToJson(data), JsonUtility.ToJson(Read(store)), "Second save preserved");
            Check(!File.Exists(store.GetPath("main") + ".tmp"), "Temporary file left behind.");
        }
        static void Migration(int version) {
            var store = Store(); JObject root = JObject.Parse(JsonUtility.ToJson(Full())); root["version"] = version;
            foreach (JObject state in (JArray)root["pets"]["petStates"]) ((JObject)state["runtimeStats"]).Remove("hasSkillLoadout");
            string original = root.ToString(); File.WriteAllText(store.GetPath("main"), original);
            var data = Read(store); Equal(version, store.LoadedVersion, "Source version");
            Check(data.pets.petStates[0].runtimeStats.hasSkillLoadout, "Legacy loadout not recognized.");
            Write(store, data); Write(store, Read(store));
            Equal(original, File.ReadAllText(store.GetPath("main") + ".pre-v6.bak"), "Original migration archive");
            Equal(6, Read(store).version, "Migrated version");
        }
        static void MissingSections() {
            var data = PlayerSaveMigration.Read("{\"version\":1,\"currency\":{\"captured\":true,\"gold\":7}}", out _);
            Check(data.quest == null && data.inventory == null && data.profile == null && data.pets == null && data.achievements == null, "Absent sections instantiated.");
            Write(Store(), data); data = Read(Store());
            Check(data.quest == null && data.pets == null, "Null sections instantiated after roundtrip.");
        }
        static void NestedDefaults() {
            var data = PlayerSaveMigration.Read("{\"version\":1,\"pets\":{\"captured\":true,\"partyPetIds\":[\"p1\"],\"petStates\":[{\"petId\":\"p1\",\"runtimeStats\":{\"captured\":true,\"level\":2}}]}}", out _);
            Check(data.pets.petStates[0].customization == null, "Missing customization must not reset nickname.");
            Equal(4, data.pets.petStates[0].runtimeStats.equippedSkillSlotCount, "Default slots");
            Check(!data.pets.petStates[0].runtimeStats.hasSkillLoadout, "Missing loadout must preserve existing skills.");
        }
        static void BackupRecovery() {
            var store = Store(); Write(store, Currency(10)); Write(store, Currency(20));
            string path = store.GetPath("main"), backup = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, "{broken");
            Equal(10, Read(store).currency.gold, "Backup data"); Check(store.RecoveredFromBackup, "Recovery flag");
            Write(store, Currency(30)); Equal(backup, File.ReadAllText(path + ".bak"), "Good backup kept");
            Equal(30, Read(store).currency.gold, "Repaired primary");
        }
        static void CorruptCopies() {
            var store = Store(); string path = store.GetPath("main"); File.WriteAllText(path, "broken"); File.WriteAllText(path + ".bak", "broken backup");
            Check(!store.TryLoad("main", out _, out _), "Corrupt read accepted");
            Check(!store.TrySave("main", Currency(), out _), "Corrupt slot overwritten"); Equal("broken", File.ReadAllText(path), "Primary untouched");
        }
        static void FuturePrimary() {
            var store = Store(); Write(store, Currency()); Write(store, Currency());
            string path = store.GetPath("main"), future = "{\"version\":999,\"newFormat\":{}}"; File.WriteAllText(path, future);
            Check(!store.TryLoad("main", out _, out _), "Must not downgrade to old backup");
            Check(!store.TrySave("main", Currency(), out _), "Future overwritten"); Equal(future, File.ReadAllText(path), "Future bytes");
        }
        static void FutureBackup() {
            var store = Store(); Write(store, Currency()); File.WriteAllText(store.GetPath("main") + ".bak", "{\"version\":999}");
            Check(!store.TrySave("main", Currency(), out _), "Future backup overwritten");
        }
        static void DuplicateJson() { Throws(() => PlayerSaveMigration.Read("{\"version\":1,\"version\":6,\"currency\":{}}", out _)); }
        static void InvalidRoster() {
            var data = Full(); data.pets.boxPetIds.Add("p1"); Throws(() => PlayerSaveMigration.Validate(data));
            data = Full(); data.pets.activePetId = "p3"; Throws(() => PlayerSaveMigration.Validate(data));
            data = Full(); data.pets.partyPetIds.Add("unknown"); Throws(() => PlayerSaveMigration.Validate(data));
            data = Full(); data.pets.petStates.Add(data.pets.petStates[0]); Throws(() => PlayerSaveMigration.Validate(data));
        }
        static MonsterItemDefinition Item() {
            var item = MonsterItemDefinition.CreateRuntime("test-herb-" + Guid.NewGuid().ToString("N"), GameItemCategory.Material, "Description", "Effect");
            Owned.Add(item); return item;
        }
        static void InventoryRoundtrip() {
            var adapter = Component<MonsterInventoryAdapter>(); var item = Item();
            adapter.AddItem(item, 125); var data = adapter.CreateSaveData(); string id = data.items[0].itemId;
            item.name = "RenamedAsset";
            for (int i = 0; i < 3; i++) Check(adapter.RestoreFromSaveData(data, out var error), error);
            Equal(125, adapter.GetQuantity(item), "No duplication"); Equal(id, adapter.CreateSaveData().items[0].itemId, "Stable item ID");
        }
        static void InventoryFailure() {
            var adapter = Component<MonsterInventoryAdapter>(); var item = Item(); adapter.AddItem(item, 3);
            var data = adapter.CreateSaveData(); data.capacity = 1; data.items[0].quantity = 500;
            Check(!adapter.RestoreFromSaveData(data, out _), "Overflow must reject"); Equal(3, adapter.GetQuantity(item), "Original inventory untouched");
        }
        static void InventoryAlias() {
            var adapter = Component<MonsterInventoryAdapter>(); var item = Item(); adapter.AddItem(item, 5);
            var data = adapter.CreateSaveData(); string old = item.StableId; item.AssignStableId("stable-" + Guid.NewGuid().ToString("N")); item.name = "Renamed"; Set(item, "legacyIds", new[] { old });
            Check(adapter.RestoreFromSaveData(data, out var error), error); Equal(5, adapter.GetQuantity(item), "Alias quantity");
            Equal(item.StableId, adapter.CreateSaveData().items[0].itemId, "Alias upgraded");
        }
        static void PrototypeItem() {
            var adapter = Component<MonsterInventoryAdapter>(); var data = Full().inventory; data.items[0].itemId = "unknown-" + Guid.NewGuid().ToString("N");
            Check(adapter.RestoreFromSaveData(data, out var error), error);
            Equal(data.items[0].itemId, adapter.CreateSaveData().items[0].itemId, "Prototype ID");
            Equal(400, adapter.CreateSaveData().items[0].healAmount, "Prototype metadata");
        }
        static SkillHudData Skill(string id) => new SkillHudData { skillId = id, displayName = id, unlocked = true, usable = true, skillLevel = 1, requiredPetLevel = 1 };
        static PetHudRuntimeStats Stats() {
            var stats = Component<PetHudRuntimeStats>(); Set(stats, "usePrototypeSkillsWhenEmpty", false);
            stats.SetIdentity("Test", 12); stats.SetStatus(70, 100, 15, 20); stats.SetSkills(new[] { Skill("a"), Skill("b") }); return stats;
        }
        static void Skills(int slots) {
            var stats = Stats(); var data = stats.CreateSaveData(); data.equippedSkillSlotCount = slots;
            data.equippedSkillIds = slots == 2 ? new List<string> { "b", "a" } : new List<string> { "a", "", "b" };
            stats.RestoreFromSaveData(data); var after = stats.CreateSaveData();
            Equal(slots, after.equippedSkillSlotCount, "Slot count");
            Equal(string.Join(",", data.equippedSkillIds), string.Join(",", after.equippedSkillIds.Take(data.equippedSkillIds.Count)), "Slots not shifted");
            stats.RestoreFromSaveData(after); Equal(JsonUtility.ToJson(after), JsonUtility.ToJson(stats.CreateSaveData()), "Repeated skills restore");
        }
        static void EmptySkills() {
            var stats = Stats(); Set(stats, "usePrototypeSkillsWhenEmpty", true);
            var data = stats.CreateSaveData(); data.equippedSkillIds.Clear(); stats.RestoreFromSaveData(data);
            Check(stats.CreateSaveData().equippedSkillIds.All(string.IsNullOrEmpty), "Prototype filled empty loadout");
        }
        static void SkillAlias() {
            var stats = Stats(); var skill = Skill("stable-a"); skill.legacySkillIds = new[] { "old-a" }; stats.SetSkills(new[] { skill });
            var data = stats.CreateSaveData(); data.equippedSkillIds = new List<string> { "old-a" }; data.learnedSkillProgress.Clear();
            stats.RestoreFromSaveData(data); Equal("stable-a", stats.CreateSaveData().equippedSkillIds[0], "Skill alias");
        }
        static void MissingSkill() {
            var stats = Stats(); string before = JsonUtility.ToJson(stats.CreateSaveData()); var data = stats.CreateSaveData(); data.equippedSkillIds[0] = "unknown";
            Check(!stats.CanRestoreSaveData(data, out _), "Unknown skill accepted"); Throws(() => stats.RestoreFromSaveData(data));
            Equal(before, JsonUtility.ToJson(stats.CreateSaveData()), "Stats unchanged");
        }
        static QuestManager Quest() {
            var manager = Component<QuestManager>(); var definition = Asset<QuestDefinition>(); Set(definition, "questId", "q1");
            Check(manager.RegisterQuestDefinition(definition), "Register quest"); return manager;
        }
        static void QuestRoundtrip() {
            var manager = Quest(); var data = Full().quest; int rewards = 0; manager.QuestRewardsReady += (_, __) => rewards++;
            for (int i = 0; i < 3; i++) manager.RestoreFromSaveData(data);
            var after = manager.CreateSaveData(); Equal(1, after.questStates.Count, "No duplicate quest states");
            Check(after.questStates[0].rewardsClaimed, "Reward claim lost"); Equal(0, rewards, "Rewards replayed");
        }
        static void MissingQuest() {
            var manager = Quest(); manager.RestoreFromSaveData(Full().quest); string before = JsonUtility.ToJson(manager.CreateSaveData());
            var bad = Full().quest; bad.questStates[0].questId = "unknown";
            Check(!manager.CanRestoreSaveData(bad, out _), "Missing quest accepted"); Throws(() => manager.RestoreFromSaveData(bad));
            Equal(before, JsonUtility.ToJson(manager.CreateSaveData()), "Quest state cleared");
        }
        static PlayerSaveController Controller(PlayerCurrencyWallet wallet = null, QuestManager quest = null) {
            var controller = Component<PlayerSaveController>(); Set(controller, "autoFindReferences", false); Set(controller, "loadOnStart", false);
            Set(controller, "saveOnApplicationQuit", false); Set(controller, "currencyWallet", wallet); Set(controller, "questManager", quest);
            controller.SetSaveStore(Store()); return controller;
        }
        static void ControllerRoundtrip() {
            var wallet = Component<PlayerCurrencyWallet>(); var controller = Controller(wallet); wallet.AddGold(100);
            Check(controller.SaveNow(), controller.SaveBlockedReason); wallet.AddGold(50);
            Check(controller.LoadNow(), controller.SaveBlockedReason); Equal(100, wallet.Gold, "First load");
            wallet.AddGold(27); Check(controller.SaveNow(), controller.SaveBlockedReason); wallet.AddGold(99);
            Check(controller.LoadNow(), controller.SaveBlockedReason); Equal(127, wallet.Gold, "Second load");
        }
        static void ControllerCorrupt() {
            var controller = Controller(Component<PlayerCurrencyWallet>()); string path = Store().GetPath("main"); File.WriteAllText(path, "broken");
            Check(!controller.LoadNow() && controller.IsSaveBlocked, "Corrupt load not blocked"); Check(!controller.SaveNow(), "Save allowed after failed load");
            Equal("broken", File.ReadAllText(path), "Corrupt bytes overwritten");
        }
        static void ControllerUnloaded() {
            Write(Store(), Currency(55)); var controller = Controller(Component<PlayerCurrencyWallet>());
            Check(!controller.SaveNow(), "Unloaded overwrite allowed"); Equal(55, Read(Store()).currency.gold, "Unloaded save changed");
            Check(controller.LoadNow() && !controller.IsSaveBlocked, "Successful load must unblock");
        }
        static void ControllerMissingSource() {
            Write(Store(), Full()); var wallet = Component<PlayerCurrencyWallet>(); wallet.AddGold(7); var controller = Controller(wallet);
            Check(!controller.LoadNow(), "Missing providers accepted"); Equal(7, wallet.Gold, "Partial application");
            Check(!controller.SaveNow(), "Missing data overwritten"); Equal(3, Read(Store()).pets.petStates.Count, "Pet data lost");
        }
        static void LegacyQuest() {
            var legacy = new QuestJsonFileSaveStore(caseRoot); Check(legacy.TrySave("main", Full().quest, out var error), error);
            string path = legacy.GetPath("main"), original = File.ReadAllText(path);
            var controller = Controller(null, Quest()); Check(controller.LoadNow(), controller.SaveBlockedReason);
            Check(controller.LoadNow(), controller.SaveBlockedReason); Equal(original, File.ReadAllText(path), "Legacy file changed");
            Check(Read(Store()).quest.questStates[0].rewardsClaimed, "Claim lost during legacy migration");
        }

        static PetController Pet(string id) {
            var pet = Component<PetController>();
            var state = typeof(PetController).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
            state.SetValue(pet, Enum.Parse(state.FieldType, "Underground"));
            var metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();
            metadata.AssignPersistentId(id); metadata.AssignDefinitionId("test-definition");
            metadata.TrySetNickname(id, out _); metadata.ToggleFavorite(); metadata.SetStats(20, 30, 40);
            var stats = pet.gameObject.AddComponent<PetHudRuntimeStats>(); Set(stats, "usePrototypeSkillsWhenEmpty", false);
            stats.SetIdentity(id, 12); stats.SetStatus(70, 100, 15, 20); stats.SetSkills(new[] { Skill("a"), Skill("b") });
            return pet;
        }
        static PlayerSaveController RosterController(out PetCommandInput input, out PetBoxRuntimeProvider box) {
            input = Component<PetCommandInput>(); input.petSlots = new PetController[6];
            box = Component<PetBoxRuntimeProvider>(); Set(box, "autoFindPetCommandInput", false); box.Bind(input);
            var controller = Controller(); Set(controller, "petCommandInput", input); Set(controller, "petBoxProvider", box);
            return controller;
        }
        static void RosterRoundtrip() {
            var controller = RosterController(out var input, out var box);
            var first = Pet("first"); var second = Pet("second"); var third = Pet("third");
            input.petSlots[0] = first; input.petSlots[2] = second; input.SetActivePet(first);
            box.RestoreState(new[] { third }, 60);
            Check(controller.SaveNow(), controller.SaveBlockedReason);
            var metadata = first.GetComponent<PetCollectionMetadata>(); var stats = first.GetComponent<PetHudRuntimeStats>();
            metadata.TrySetNickname("changed", out _); metadata.ToggleFavorite(); stats.SetStatus(1, 100, 0, 20);
            for (int i = 0; i < 3; i++) Check(controller.LoadNow(), controller.SaveBlockedReason);
            Equal("first", metadata.Nickname, "Nickname restored"); Check(metadata.IsFavorite, "Favorite restored"); Equal(70f, stats.Health, "Health restored");
            Equal(first, input.petSlots[0], "Party first"); Equal(second, input.petSlots[2], "Party gap preserved"); Equal(1, box.StoredCount, "Box not duplicated"); Equal(third, box.StoredPets[0], "Box identity");
            metadata.TrySetNickname("new-name", out _); metadata.ToggleFavorite(); stats.SetIdentity("first", 13);
            Check(controller.SaveNow(), controller.SaveBlockedReason); Check(controller.LoadNow(), controller.SaveBlockedReason);
            Equal("new-name", metadata.Nickname, "Second nickname"); Check(!metadata.IsFavorite, "Second favorite"); Equal(13, stats.Level, "Second level");
        }
        static void MissingPet() {
            var controller = RosterController(out _, out _);
            var inventory = Component<MonsterInventoryAdapter>(); var item = Item(); inventory.AddItem(item, 7); Set(controller, "inventoryAdapter", inventory);
            var data = Currency(); data.currency = null; data.inventory = inventory.CreateSaveData(); data.inventory.items[0].quantity = 99;
            data.pets = new PetRosterSaveData { captured = true, partyPetIds = new List<string> { "missing" }, petStates = new List<PetInstanceSaveData> { new PetInstanceSaveData { petId = "missing", definitionId = "missing-prefab" } } };
            Write(Store(), data); Check(!controller.LoadNow() && controller.IsSaveBlocked, "Missing pet load accepted");
            Equal(7, inventory.GetQuantity(item), "Inventory changed before preflight finished"); Check(!controller.SaveNow(), "Missing pet discarded on save");
        }
        static void MissingCustomization() {
            var data = Full(); data.pets.petStates[0].customization = null; data.pets.petStates[0].runtimeStats = null;
            Write(Store(), data); var restored = Read(Store()).pets.petStates[0];
            Check(restored.customization == null && restored.runtimeStats == null, "Absent nested data materialized");
        }
    }
}

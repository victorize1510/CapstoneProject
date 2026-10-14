using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.Inventory.Editor {
    /// <summary>Recovery integration fixtures use inactive objects and a separate temporary save.</summary>
    public static class RecoverySamplesValidation {
        static readonly List<string> results = new List<string>();
        static void Check(bool condition, string label) {
            if (!condition) throw new InvalidOperationException(label);
            results.Add("PASS " + label);
        }
        static void Set(object owner, string field, object value) => owner.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

        sealed class Fixture : IDisposable {
            readonly GameObject root;
            public readonly MonsterInventoryAdapter bag;
            public readonly PetController pet;
            public readonly PetHudRuntimeStats stats;
            public readonly PetHealService service;
            public readonly PlayerSaveController save;
            public Fixture(MonsterItemDefinition item, float hp, float mp, float maxMp = 50) {
                root = new GameObject("Recovery validation fixture");
                root.SetActive(false);
                bag = root.AddComponent<MonsterInventoryAdapter>();
                save = root.AddComponent<PlayerSaveController>();
                Set(save, "autoFindReferences", false);
                Set(save, "inventoryAdapter", bag);
                Set(save, "saveOnApplicationQuit", false);
                save.SetSaveStore(new PlayerJsonFileSaveStore(Path.Combine(Path.GetTempPath(),
                    "CapstoneRecoveryValidation", Guid.NewGuid().ToString("N"))));
                pet = root.AddComponent<PetController>();
                stats = root.AddComponent<PetHudRuntimeStats>();
                stats.SetStatus(hp, 200, mp, maxMp);
                service = root.AddComponent<PetHealService>();
                service.Bind(bag);
                Set(service, "saveController", save);
                Check(bag.TryAddItemAtomic(item, 3), item.Name + " fixture delivery");
            }
            public void Dispose() => Object.DestroyImmediate(root);
        }

        [MenuItem("Game Tools/GameToolThang/Inventory/Validate Recovery Samples")]
        public static void Run() {
            if (Application.isPlaying) return;
            results.Clear();
            try {
                RecoverySamplesSetup.EnsureDefinitions();
                var catalog = InventoryRecoveryCatalog.Load();
                Check(catalog != null, "recovery catalog exists");
                foreach (var spec in new[] { ("healing-apple", 30, 0), ("Potion", 100, 0), ("mana-potion", 0, 30) }) {
                    var item = catalog.items.Single(x => x != null && x.StableId == spec.Item1);
                    Check(item.Icon != null && item.HealAmount == spec.Item2 && item.EnergyRestoreAmount == spec.Item3,
                        item.Name + " icon and approved effect");
                    using (var f = new Fixture(item, 40, 5)) {
                        Check(f.service.TryHeal(f.pet, item, out _), item.Name + " non-clamped use succeeds");
                        Check(f.stats.Health == 40 + spec.Item2 && f.stats.Energy == 5 + spec.Item3,
                            item.Name + " restores configured amount exactly");
                    }
                    using (var f = new Fixture(item, 185, 40)) {
                        Check(f.bag.GetItems().Single().IsRecoveryItem, item.Name + " recovery/quick-slot eligibility");
                        Check(f.service.TryHeal(f.pet, item, out var message), item.Name + " real use: " + message);
                        Check(f.bag.GetQuantity(item) == 2, item.Name + " consumes exactly one");
                        Check(f.stats.Health == (spec.Item2 > 0 ? 200 : 185)
                            && f.stats.Energy == (spec.Item3 > 0 ? 50 : 40), item.Name + " clamps resource and preserves other resource");
                        Check(!f.service.TryHeal(f.pet, item, out _), item.Name + " repeat click rejected");
                        Check(f.bag.GetQuantity(item) == 2, item.Name + " repeat does not consume");
                        var snapshot = f.bag.CreateSaveData();
                        f.bag.SetInventory(null);
                        Check(f.bag.RestoreFromSaveData(snapshot, out _) && f.bag.GetItems().Single().EnergyRestoreAmount == spec.Item3,
                            item.Name + " effect survives inventory reload without new save fields");
                    }
                    using (var f = new Fixture(item, 200, 50)) {
                        Check(!f.service.CreatePreview(f.pet, item).CanHeal && !f.service.TryHeal(f.pet, item, out _), item.Name + " full resource disabled");
                        Check(f.bag.GetQuantity(item) == 3, item.Name + " full resource preserves quantity");
                    }
                    using (var f = new Fixture(item, 40, 5)) {
                        Set(f.save, "saveBlockedReason", "isolated fixture save failure");
                        Check(!f.service.TryHeal(f.pet, item, out _), item.Name + " failed save rejected");
                        Check(f.stats.Health == 40 && f.stats.Energy == 5 && f.bag.GetQuantity(item) == 3,
                            item.Name + " failed save rolls back stats and item");
                    }
                }
                var mana = catalog.items.Single(x => x != null && x.StableId == "mana-potion");
                using (var f = new Fixture(mana, 100, 0, 0)) {
                    Check(!f.service.TryHeal(f.pet, mana, out var reason) && reason.Contains("chưa sử dụng MP"), "pet without MP gets explicit explanation");
                    Check(f.stats.Health == 100 && f.bag.GetQuantity(mana) == 3, "no-MP pet keeps health and potion");
                }
            } catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            Directory.CreateDirectory("../artifacts/panel-consistency");
            File.WriteAllLines("../artifacts/panel-consistency/recovery-validation.txt", results);
            Debug.Log("Recovery sample validation: " + results.Count + " results.");
        }
        [InitializeOnLoadMethod] static void Initialize() {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }
        static void Poll() {
            const string request = "Temp/ValidateRecoverySamples.request";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            try { File.Delete(request); }
            catch (IOException) { return; } // The request writer may still hold the file this frame.
            Run();
        }
    }
}

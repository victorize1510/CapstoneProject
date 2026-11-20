using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.SaveSystem;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.Inventory {
    [DisallowMultipleComponent]
    public sealed class InventoryDebugSeeder : MonoBehaviour {
        [SerializeField] MonsterInventoryAdapter adapter = null;
        [SerializeField] bool seedOnStart = true;
        [SerializeField] bool seedOnce = true;
        [SerializeField] bool includeDefaultItemsWithCustom;
        [SerializeField] List<SeedItem> customSeedItems = new List<SeedItem>();

        bool seeded;
        Coroutine pendingSeed;

        void Start() {
            if (seedOnStart) Seed();
        }

        [ContextMenu("Seed Debug Inventory")]
        public void Seed() {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isPlaying || !isActiveAndEnabled) {
                Debug.LogWarning("Inventory Debug Seeder: hãy dùng lệnh trong Play Mode trên component đang bật.", this);
                return;
            }
            if (pendingSeed != null || (seedOnce && seeded)) {
                Debug.Log("Inventory Debug Seeder: lệnh đang chờ hoặc Seed Once đã khóa. Dùng Reset Seed Lock để cấp bù lại.", this);
                return;
            }

            ResolveAdapter();
            if (adapter == null || !adapter.isActiveAndEnabled) {
                Debug.LogWarning("Inventory Debug Seeder: chưa gán Monster Inventory Adapter đang hoạt động của túi.", this);
                return;
            }

            var saves = FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var save = Array.Find(saves, candidate => candidate.isActiveAndEnabled && candidate.UsesInventory(adapter));
            if (save == null) {
                save = Array.Find(saves, candidate => candidate.isActiveAndEnabled && candidate.gameObject.scene == adapter.gameObject.scene);
            }
            bool isCreatureQa = gameObject.scene.name == DemoSceneOneSaveIsolation.CreatureQaSceneName;
            if (isCreatureQa && (save == null || save.gameObject.scene != gameObject.scene
                || save.SlotId != DemoSceneOneSaveIsolation.CreatureQaSlot || !save.UsesInventory(adapter))) {
                Debug.LogWarning("Inventory Debug Seeder: scene QA cần slot creature-qa-items-test và cùng Adapter với Player Save Controller.", this);
                return;
            }
            if (save != null && !save.IsSaveBlocked && !save.HasCompletedStartup) pendingSeed = StartCoroutine(SeedAfterLoad(save));
            else SeedLoadedInventory(save);
#else
            Debug.Log("InventoryDebugSeeder is disabled outside Editor and Development Build.");
#endif
        }

        [ContextMenu("Reset Seed Lock")]
        public void ResetSeedLock() {
            seeded = false;
        }

        void OnDisable() {
            if (pendingSeed == null) return;
            StopCoroutine(pendingSeed);
            pendingSeed = null;
        }

        void ResolveAdapter() {
            if (adapter != null) return;
            adapter = GetComponent<MonsterInventoryAdapter>();
            if (adapter == null) adapter = GetComponentInParent<MonsterInventoryAdapter>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IEnumerator SeedAfterLoad(PlayerSaveController save) {
            Debug.Log("Inventory Debug Seeder: đang chờ Player Save Controller load xong.", this);
            float deadline = Time.realtimeSinceStartup + 10f;
            yield return null;
            while (save != null && save.isActiveAndEnabled && !save.HasCompletedStartup
                && !save.IsSaveBlocked && Time.realtimeSinceStartup < deadline) yield return null;
            pendingSeed = null;
            if (save == null || !save.isActiveAndEnabled) {
                Debug.LogWarning("Inventory Debug Seeder: Player Save Controller đã bị tắt khi chờ load.", this);
                yield break;
            }
            SeedLoadedInventory(save);
        }

        void SeedLoadedInventory(PlayerSaveController save) {
            if (save != null && (save.IsSaveBlocked || !save.HasCompletedStartup || save.IsApplyingSave
                || (save.HasSave && !save.IsCurrentSlotLoaded))) {
                Debug.LogWarning($"Inventory Debug Seeder: slot '{save.SlotId}' chưa sẵn sàng. {save.SaveBlockedReason}", this);
                return;
            }
            if (adapter == null || !adapter.isActiveAndEnabled || (save != null && !save.UsesInventory(adapter))) {
                Debug.LogWarning("Inventory Debug Seeder: Adapter không còn hoạt động hoặc khác Adapter đang được lưu.", this);
                return;
            }

            if (customSeedItems.Count > 0 && !TopUpCustomItems()) return;
            if (customSeedItems.Count == 0 || includeDefaultItemsWithCustom) SeedDefaultItems();
            seeded = true;
        }

        bool TopUpCustomItems() {
            var snapshot = adapter.CaptureSlots();
            var report = new List<string>();
            bool changed = false;
            foreach (var entry in customSeedItems) {
                string id = entry?.ItemDefinition != null ? entry.ItemDefinition.StableId : null;
                var definition = InventoryDefinitionCatalog.Find(id);
                if (definition == null || entry.Quantity <= 0) {
                    adapter.RestoreCaptureSlots(snapshot);
                    Debug.LogWarning($"Inventory Debug Seeder: '{id ?? "(chưa gán)"}' thiếu trong Item Catalog hoặc số lượng không hợp lệ.", this);
                    return false;
                }

                int owned = adapter.GetQuantity(definition);
                int needed = Mathf.Max(0, entry.Quantity - owned);
                if (needed > 0 && !adapter.TryAddItemAtomic(definition, needed, notify: false)) {
                    adapter.RestoreCaptureSlots(snapshot);
                    Debug.LogWarning($"Inventory Debug Seeder: túi từ chối '{id}' ×{needed} (đang có {owned}, stack tối đa {definition.MaxStackSize}). Kiểm tra quy tắc nhận item và sức chứa của Adapter.", this);
                    return false;
                }
                changed |= needed > 0;
                report.Add($"{definition.StableId} ×{adapter.GetQuantity(definition)} (+{needed})");
            }
            if (changed) adapter.NotifyAtomicAddition();
            Debug.Log("Inventory Debug Seeder: " + string.Join(", ", report), this);
            return true;
        }

        void SeedDefaultItems() {
            AddRuntimeItem("Verdant Capture Ball", GameItemCategory.CaptureBall, 12, "A standard capture tool wrapped in moss-green lacquer.", "Can be thrown at weakened wild monsters.");
            AddRuntimeItem("Oak Great Ball", GameItemCategory.CaptureBall, 4, "A better capture ball reinforced with bronze rings.", "Higher capture chance than a basic ball.");
            AddRuntimeItem("Potion", GameItemCategory.Medicine, 12, "A basic restorative carried by traveling beast tamers.", "Restores 400 pet HP.", healingAmount: 400);
            AddRuntimeItem("Honeyed Berry", GameItemCategory.Food, 9, "Sweet field food that many young monsters like.", "Improves friendship slightly.");
            AddRuntimeItem("Leaf Berry", GameItemCategory.Food, 25, "A temporary growth berry used by the pet level-up prototype.", "Grants pet EXP through PetLevelUpService.");
            AddRuntimeItem("Ironwood Bark", GameItemCategory.Material, 18, "Hard bark collected from ancient forest roots.", "Used for crafting pet gear.", true, 99, false, false);
            AddRuntimeItem("Tamer Gloves", GameItemCategory.Equipment, 1, "Leather gloves made for handling capture gear.", "Equipment prototype; stats are not connected yet.", false, 1, false, false);
            AddRuntimeItem("Guild Token", GameItemCategory.KeyItem, 1, "A token proving the owner belongs to the local tamer guild.", "Key item. Cannot be consumed.", false, 1, false, false);
        }

        void AddRuntimeItem(
            string itemName,
            GameItemCategory category,
            int quantity,
            string description,
            string effect,
            bool stackable = true,
            int maxStackSize = 99,
            bool usableFromInventory = true,
            bool consumable = true,
            int healingAmount = 0) {
            var definition = adapter.FindItemBase(itemName) ?? MonsterItemDefinition.CreateRuntime(itemName, category, description,
                effect, null, stackable, maxStackSize, usableFromInventory, consumable, healingAmount);
            var result = adapter.AddItem(definition, Mathf.Max(0, quantity - adapter.GetQuantity(definition)));
            if (result is Fail) Debug.LogWarning($"Failed to seed inventory item: {itemName}");
        }
#endif

        [Serializable]
        public sealed class SeedItem {
            public MonsterItemDefinition ItemDefinition;
            public int Quantity = 1;
        }
    }
}

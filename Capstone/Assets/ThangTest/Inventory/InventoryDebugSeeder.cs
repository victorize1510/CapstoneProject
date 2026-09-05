using System;
using System.Collections.Generic;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.Inventory {
    [DisallowMultipleComponent]
    public sealed class InventoryDebugSeeder : MonoBehaviour {
        [SerializeField] MonsterInventoryAdapter adapter = null;
        [SerializeField] bool seedOnStart = true;
        [SerializeField] bool seedOnce = true;
        [SerializeField] List<SeedItem> customSeedItems = new List<SeedItem>();

        bool seeded;

        void Start() {
            if (seedOnStart) Seed();
        }

        [ContextMenu("Seed Debug Inventory")]
        public void Seed() {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (seedOnce && seeded) return;

            ResolveAdapter();
            if (adapter == null) {
                Debug.LogWarning("InventoryDebugSeeder needs a MonsterInventoryAdapter.");
                return;
            }

            var usedCustomItems = false;
            foreach (var seedItem in customSeedItems) {
                if (seedItem.ItemDefinition == null || seedItem.Quantity <= 0) continue;
                adapter.AddItem(seedItem.ItemDefinition, seedItem.Quantity);
                usedCustomItems = true;
            }

            if (!usedCustomItems) SeedDefaultItems();
            seeded = true;
#else
            Debug.Log("InventoryDebugSeeder is disabled outside Editor and Development Build.");
#endif
        }

        [ContextMenu("Reset Seed Lock")]
        public void ResetSeedLock() {
            seeded = false;
        }

        void ResolveAdapter() {
            if (adapter != null) return;
            adapter = GetComponent<MonsterInventoryAdapter>();
            if (adapter == null) adapter = GetComponentInParent<MonsterInventoryAdapter>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            var definition = MonsterItemDefinition.CreateRuntime(itemName, category, description, effect, null, stackable, maxStackSize, usableFromInventory, consumable, healingAmount);
            var result = adapter.AddItem(definition, quantity);
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

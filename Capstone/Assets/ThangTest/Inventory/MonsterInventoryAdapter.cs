using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.SaveSystem;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace Capstone.Game.Inventory {
    [DisallowMultipleComponent]
    public sealed class MonsterInventoryAdapter : MonoBehaviour {
        const int DefaultBagSize = 40;

        [SerializeField] ListBag inventory = CreateDefaultBag();
        ListBag subscribedInventory;
        int changeBatchDepth;
        bool hasPendingChange;

        public ListBag Inventory => inventory;
        public int Capacity {
            get {
                EnsureInventory();
                return inventory.Slots.Count;
            }
        }
        public int OccupiedSlotCount {
            get {
                EnsureInventory();
                return inventory.Slots.Count(slot => slot != null && slot.Item != null);
            }
        }

        public event Action InventoryChanged;
        public event Action<IReadOnlyList<InventoryItemSnapshot>> ItemsChanged;

        void OnEnable() {
            EnsureInventory();
            Subscribe(inventory);
        }

        void OnDisable() {
            Unsubscribe(inventory);
        }

        public void SetInventory(ListBag newInventory) {
            if (ReferenceEquals(inventory, newInventory)) return;

            var shouldResubscribe = subscribedInventory != null;
            Unsubscribe(subscribedInventory);
            inventory = newInventory ?? CreateDefaultBag();
            if (shouldResubscribe) Subscribe(inventory);
            PublishChanged();
        }

        public IReadOnlyList<InventoryItemSnapshot> GetItems() {
            return GetItems(GameItemCategory.All);
        }

        public IReadOnlyList<InventoryItemSnapshot> GetItems(GameItemCategory category) {
            EnsureInventory();

            return inventory.Items
                .Where(item => item != null && item.Base != null)
                .GroupBy(item => item.Base)
                .Select(group => new InventoryItemSnapshot(
                    group.Key,
                    group.First(),
                    group.Sum(item => Mathf.Max(0, item.StackSize))))
                .Where(item => item.Quantity > 0)
                .Where(item => category == GameItemCategory.All || item.Category == category)
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Name)
                .ToList();
        }

        public int GetQuantity(ItemBase itemBase) {
            if (itemBase == null) return 0;
            EnsureInventory();

            return inventory.Items
                .Where(item => item != null && item.Base == itemBase)
                .Sum(item => Mathf.Max(0, item.StackSize));
        }

        public ItemBase FindItemBase(string itemId) {
            if (string.IsNullOrWhiteSpace(itemId)) return null;
            string expected = itemId.Trim();

            foreach (InventoryItemSnapshot item in GetItems()) {
                if (item?.ItemBase == null) continue;
                if (string.Equals(item.ItemId, expected, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Name, expected, StringComparison.OrdinalIgnoreCase)) {
                    return item.ItemBase;
                }
            }

            foreach (ItemBase itemBase in Resources.FindObjectsOfTypeAll<ItemBase>()) {
                if (itemBase == null) continue;
                if ((itemBase is MonsterItemDefinition gameDefinition && gameDefinition.MatchesId(expected))
                    || string.Equals(itemBase.name, expected, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemBase.Name, expected, StringComparison.OrdinalIgnoreCase)) {
                    return itemBase;
                }
            }

            return null;
        }

        public Result AddItem(ItemBase itemBase, int quantity = 1) {
            if (itemBase == null) return Fail.NullRef;
            if (quantity <= 0) return Result.Success;

            EnsureInventory();
            if (!CanFit(itemBase, quantity)) return Fail.ItemCannotFit;
            BeginChangeBatch();

            try {
                var remaining = quantity;
                if (itemBase.Stackable) {
                    remaining = FillExistingStacks(itemBase, remaining);
                }

                while (remaining > 0) {
                    var stackSize = itemBase.Stackable ? Mathf.Min(GetSafeMaxStack(itemBase), remaining) : 1;
                    var result = inventory.Add(CreateStack(itemBase, stackSize));
                    if (result is Fail) return result;

                    remaining -= stackSize;
                }

                return Result.Success;
            } finally {
                EndChangeBatch();
            }
        }

        public Result AddItem(Item item) {
            if (item == null || item.Base == null) return Fail.NullRef;

            if (item.Stackable) {
                return AddItem(item.Base, item.StackSize);
            }

            EnsureInventory();
            BeginChangeBatch();
            try {
                return inventory.Add(item.Clone());
            } finally {
                EndChangeBatch();
            }
        }

        public Result RemoveItem(ItemBase itemBase, int quantity = 1) {
            if (itemBase == null) return Fail.NullRef;
            if (quantity <= 0) return Result.Success;

            EnsureInventory();
            if (GetQuantity(itemBase) < quantity) return Result.Fail;

            BeginChangeBatch();

            try {
                var remaining = quantity;
                var slots = inventory.Slots
                    .Where(slot => slot != null && slot.Item != null && slot.Item.Base == itemBase)
                    .ToList();

                foreach (var slot in slots) {
                    if (remaining <= 0) break;

                    var item = slot.Item;
                    var amount = Mathf.Min(remaining, Mathf.Max(1, item.StackSize));
                    if (amount >= item.StackSize) {
                        remaining -= item.StackSize;
                        var result = inventory.Remove(item);
                        if (result is Fail) return result;
                        continue;
                    }

                    item.StackSize -= amount;
                    remaining -= amount;
                    inventory.NotifyChanged(slot);
                }

                return remaining == 0 ? Result.Success : Result.Fail;
            } finally {
                EndChangeBatch();
            }
        }

        public InventorySaveData CreateSaveData() {
            EnsureInventory();

            var saveData = new InventorySaveData {
                captured = true,
                capacity = Capacity
            };

            foreach (var item in GetItems()) {
                saveData.items.Add(new InventoryItemSaveData {
                    itemId = item.ItemId,
                    displayName = item.Name,
                    category = item.Category,
                    rarity = item.Rarity,
                    description = item.Description,
                    effect = item.Effect,
                    source = item.Source,
                    flavorText = item.FlavorText,
                    healAmount = item.HealAmount,
                    quantity = item.Quantity,
                    stackable = item.Stackable,
                    maxStackSize = Mathf.Max(1, item.MaxStackSize),
                    usableFromInventory = item.UsableFromInventory,
                    consumable = item.Consumable
                });
            }

            return saveData;
        }

        public bool RestoreFromSaveData(InventorySaveData saveData, out string error) {
            error = string.Empty;
            if (saveData == null || !saveData.captured) return false;

            if (saveData.capacity < 1 || saveData.items == null) { error = "Invalid inventory save."; return false; }
            var knownDefinitions = new Dictionary<string, ItemBase>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in GetItems()) {
                if (knownDefinitions.TryGetValue(item.ItemId, out var other) && other != item.ItemBase) {
                    error = "Duplicate item definition ID: " + item.ItemId; return false;
                }
                knownDefinitions[item.ItemId] = item.ItemBase;
            }
            foreach (MonsterItemDefinition definition in Resources.FindObjectsOfTypeAll<MonsterItemDefinition>()) {
                if (definition == null || string.IsNullOrWhiteSpace(definition.name)) continue;
                if (!knownDefinitions.ContainsKey(definition.StableId)) knownDefinitions.Add(definition.StableId, definition);
            }

            var restoredBag = new ListBag {
                Name = "Monster Inventory",
                Size = saveData.capacity
            };
            var savedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try {
                foreach (var savedItem in saveData.items) {
                    if (savedItem == null || string.IsNullOrWhiteSpace(savedItem.itemId) || savedItem.quantity < 0 || !savedIds.Add(savedItem.itemId)) {
                        error = "Invalid or duplicate inventory item ID."; return false;
                    }
                    var definition = ResolveSavedDefinition(savedItem, knownDefinitions);
                    int remaining = savedItem.quantity;
                    while (remaining > 0) {
                        int stackSize = definition.Stackable ? Math.Min(GetSafeMaxStack(definition), remaining) : 1;
                        if (restoredBag.Add(CreateStack(definition, stackSize)) is Fail) {
                            error = $"Could not restore inventory item '{savedItem.itemId}' ({savedItem.quantity}); current inventory was kept.";
                            return false;
                        }
                        remaining -= stackSize;
                    }
                }
                SetInventory(restoredBag);
                return true;
            } catch (Exception exception) {
                error = exception.Message;
                return false;
            }
        }

        public bool HasItem(ItemBase itemBase, int quantity = 1) {
            return GetQuantity(itemBase) >= Mathf.Max(1, quantity);
        }

        static ListBag CreateDefaultBag() {
            return new ListBag {
                Name = "Monster Inventory",
                Size = DefaultBagSize
            };
        }

        void EnsureInventory() {
            inventory ??= CreateDefaultBag();
        }

        int FillExistingStacks(ItemBase itemBase, int quantity) {
            var remaining = quantity;
            var slots = inventory.Slots
                .Where(slot => slot != null && slot.Item != null && slot.Item.Base == itemBase)
                .ToList();

            foreach (var slot in slots) {
                if (remaining <= 0) break;

                var item = slot.Item;
                var freeSpace = GetSafeMaxStack(itemBase) - item.StackSize;
                if (freeSpace <= 0) continue;

                var incoming = CreateStack(itemBase, Mathf.Min(freeSpace, remaining));
                var before = item.StackSize;
                var result = inventory.TransferAll(incoming, slot, item);
                if (result is Fail) continue;

                remaining -= Mathf.Max(0, item.StackSize - before);
            }

            return remaining;
        }

        static Item CreateStack(ItemBase itemBase, int quantity) {
            var item = itemBase.CreateItem();
            item.StackSize = itemBase.Stackable ? Mathf.Clamp(quantity, 1, GetSafeMaxStack(itemBase)) : 1;
            return item;
        }

        static ItemBase ResolveSavedDefinition(
            InventoryItemSaveData savedItem,
            IReadOnlyDictionary<string, ItemBase> knownDefinitions) {
            if (!string.IsNullOrWhiteSpace(savedItem.itemId)
                && knownDefinitions.TryGetValue(savedItem.itemId, out var existingDefinition)) {
                return existingDefinition;
            }

            ItemBase match = null;
            foreach (var candidate in knownDefinitions.Values) {
                if (!(candidate is MonsterItemDefinition definition) || !definition.MatchesId(savedItem.itemId)) continue;
                if (match != null && match != candidate) throw new InvalidOperationException("Ambiguous item alias: " + savedItem.itemId);
                match = candidate;
            }
            if (match != null) return match;

            var displayName = string.IsNullOrWhiteSpace(savedItem.displayName)
                ? savedItem.itemId
                : savedItem.displayName;
            var runtimeDefinition = MonsterItemDefinition.CreateRuntime(
                displayName,
                savedItem.category,
                savedItem.description,
                savedItem.effect,
                null,
                savedItem.stackable,
                Mathf.Max(1, savedItem.maxStackSize),
                savedItem.usableFromInventory,
                savedItem.consumable,
                savedItem.healAmount,
                savedItem.rarity,
                savedItem.source,
                savedItem.flavorText);

            if (!string.IsNullOrWhiteSpace(savedItem.itemId)) {
                runtimeDefinition.name = savedItem.itemId;
                runtimeDefinition.AssignStableId(savedItem.itemId);
            }

            return runtimeDefinition;
        }

        static int GetSafeMaxStack(ItemBase itemBase) {
            return Mathf.Max(1, itemBase.MaxStackSize);
        }

        bool CanFit(ItemBase itemBase, int quantity) {
            int emptySlots = inventory.Slots.Count(slot => slot != null && slot.Item == null);
            if (!itemBase.Stackable) return emptySlots >= quantity;

            int maxStack = GetSafeMaxStack(itemBase);
            int availableInStacks = inventory.Slots
                .Where(slot => slot != null && slot.Item != null && slot.Item.Base == itemBase)
                .Sum(slot => Mathf.Max(0, maxStack - slot.Item.StackSize));

            long totalCapacity = (long)availableInStacks + (long)emptySlots * maxStack;
            return totalCapacity >= quantity;
        }

        void Subscribe(ListBag bag) {
            if (bag == null || ReferenceEquals(subscribedInventory, bag)) return;

            bag.OnCollectionChanged += PublishChanged;
            bag.OnCollectionReset += PublishChanged;
            subscribedInventory = bag;
        }

        void Unsubscribe(ListBag bag) {
            if (bag == null || !ReferenceEquals(subscribedInventory, bag)) return;

            bag.OnCollectionChanged -= PublishChanged;
            bag.OnCollectionReset -= PublishChanged;
            subscribedInventory = null;
        }

        void BeginChangeBatch() {
            changeBatchDepth++;
        }

        void EndChangeBatch() {
            changeBatchDepth = Mathf.Max(0, changeBatchDepth - 1);
            if (changeBatchDepth > 0 || !hasPendingChange) return;

            hasPendingChange = false;
            PublishChangedNow();
        }

        void PublishChanged() {
            if (changeBatchDepth > 0) {
                hasPendingChange = true;
                return;
            }

            PublishChangedNow();
        }

        void PublishChangedNow() {
            var items = GetItems();
            InventoryChanged?.Invoke();
            ItemsChanged?.Invoke(items);
        }
    }
}

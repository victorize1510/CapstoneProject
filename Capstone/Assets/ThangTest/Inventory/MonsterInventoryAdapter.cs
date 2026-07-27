using System;
using System.Collections.Generic;
using System.Linq;
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

        public Result AddItem(ItemBase itemBase, int quantity = 1) {
            if (itemBase == null) return Fail.NullRef;
            if (quantity <= 0) return Result.Success;

            EnsureInventory();
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

        static int GetSafeMaxStack(ItemBase itemBase) {
            return Mathf.Max(1, itemBase.MaxStackSize);
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

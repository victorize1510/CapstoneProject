using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {

    [Serializable]
    public class ListBag : ListBag<ListSlot>, ISerializationCallbackReceiver {
        [NonSerialized] public static readonly int DefaultSize = 10;
        [SerializeReference] List<ListSlot> slots = CreateSlots(DefaultSize);
        public override List<ListSlot> Slots { get => slots; set => slots = value; }

        public int Size { get => Slots.Count; set => slots = CreateSlots(value); }
        public static List<ListSlot> CreateSlots(int size) => Enumerable.Range(0, size).Select(i => new ListSlot { Index = i }).ToList();
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() {
            // Resizing the list in the inspector does instantiate all ita items due to virtualization
            // This ensures that list slots are never null
            for (var i = 0; i < Slots.Count; i++) {
                if (Slots[i] == null) { Slots[i] = new ListSlot { Index = i }; }
            }
        }
    }


    [Serializable]
    public class ListBag<TSlot> : Bag where TSlot : Slot, new() {

        /// <summary>
        /// Event trigerred when an item has been added, removed or changed.
        /// </summary>
        public event Action<TSlot> OnItemChanged;
        // public int SubscriberCount => ItemChanged?.GetInvocationList().Length ?? 0;

        /// <summary>
        /// Event trigerred when collection has been changed.
        /// </summary>
        public event Action OnCollectionChanged;

        /// <summary>
        /// Event trigerred when the collection has changed substantially. Typically requires a full redraw.
        /// </summary>
        public event Action OnCollectionReset;



        public virtual List<TSlot> Slots { get; set; }

        public bool Full => Slots.Count(s => s.Empty()) == 0;
        public override IEnumerable<Item> Items => Slots.Where(s => s.Full()).Select(s => s.Item);
        public override Slot GetItemPosition(Item item) => Slots.Find(s => s.Item == item);
        // public override string ToString() => $"{(string.IsNullOrWhiteSpace(Name) ? "<no name>" : Name)} ({GetType().Name})";



        public void NotifyChanged(TSlot slot) {
            // Debug.Log($"slot changed: {slot}");
            OnItemChanged?.Invoke(slot);
            NotifyChanged();
        }

        public void NotifyChanged() {
            OnCollectionChanged?.Invoke();
        }

        public void NotifyReset() {
            OnCollectionReset?.Invoke();
        }

        /// <summary>
        /// Clears the bag.
        /// </summary>
        public override void Clear(bool notify = true) {
            Slots.ForEach(s => s.Clear());
            if (notify) NotifyReset();
        }

        /// <summary>
        /// Checks whether the bag can accept the item and has available capacity.
        /// </summary>
        /// <param name="item">The item to check</param>
        /// <returns>ItemNotAccepted if bag does not accept the item. ItemCannotFit if the bag doesn't have enough capacity. Success if the item can be added.</returns>
        public override Result CanAdd(Item item) {
            if (!Accepts(item)) return Fail.ItemNotAccepted;
            if (Full) return Fail.ItemCannotFit;
            return Result.Success;
        }

        /// <summary>
        /// Adds an item to the bag (without notifying subscribers)
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <returns>Item index in the list if it was added; -1 otherwise.</returns>
        int AddInternal(Item item) {
            var index = Slots.FindIndex(SlotExt.Empty);
            if (index == -1) return index;
            var slot = Slots[index];
            slot.Item = item;
            return index;
        }

        /// <summary>
        /// Adds an item to the bag
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <returns>ItemNotAccepted if bag does not accept the item. ItemCannotFit if the bag doesn't have enough capacity. PlaceItemSuccess if the item was added.</returns>
        public override Result Add(Item item) {
            if (item == null) return Fail.NullRef;
            if (!Accepts(item)) return Fail.ItemNotAccepted;
            var index = AddInternal(item);
            if (index == -1) return Fail.ItemCannotFit;
            NotifyChanged(Slots[index]);
            return new PlaceItemSuccess(item, null);
        }

        /// <summary>
        /// Adds an item to the specified slot. Can replace an existing item or move a stack to target slot.
        /// </summary>
        /// <param name="slot">Target slot</param>
        /// <param name="item">Item to add</param>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result AddAt(Slot slot, Item item) {
            if (item == null) return Fail.NullRef;
            if (!Accepts(item)) return Fail.ItemNotAccepted;
            if (!slot.Accepts(item)) return Fail.ItemNotAccepted;
            if (AllowStack(item) && slot.Item != null && item.CanStackOn(slot.Item)) return TransferAll(item, slot, slot.Item);
            return ReplaceAt(slot, item);
        }

        /// <summary>
        /// Adds a collection of items to the bag
        /// </summary>
        /// <param name="items">Items to add</param>
        /// <returns>Success if all items where added; Fail otherwise</returns>
        public override Result AddRange(IEnumerable<Item> items) {
            bool success = true;
            foreach (var item in items) { if (AddInternal(item) == -1) success = false; }
            NotifyReset();
            return success == true ? Result.Success : Result.Fail;
        }

        /// <summary>
        /// Removes the item from the bag
        /// </summary>
        /// <param name="item">Item to remove</param>
        /// <returns>PickItemSuccess or Fail</returns>
        public override Result Remove(Item item) {
            if (GetItemPosition(item) is not TSlot slot) { Debug.LogWarning($"Could not find item {item} in {this}!"); return Result.Fail; }
            if (slot.Empty()) { Debug.LogWarning($"Slot is empty! This check should be performed in a Behavior, Manipulator or Store."); return Result.Fail; }
            slot.Item = null;
            NotifyChanged(slot);
            return new PickItemSuccess(item);
        }

        // TODO: add coments
        public override Result ReplaceAt(Slot slot, Item item) {
            if (!Accepts(item) || !slot.Accepts(item)) return Fail.ItemNotAccepted;
            var replaced = slot.Item;
            slot.Item = item;
            NotifyChanged((TSlot)slot);
            return new PlaceItemSuccess(item, replaced);
        }

        // TODO: add comments
        public override Result SwapItems(Slot slot1, Slot slot2) {
            var fromItem = slot1.Item;
            var toItem = slot2.Item;
            if (fromItem == toItem) return Result.Success;
            if (AllowStack(toItem) && toItem != null && fromItem.CanStackOn(toItem)) {
                var (newFrom, _) = fromItem.TransferAll(toItem);
                slot1.Item = newFrom;
            } else (slot1.Item, slot2.Item) = (slot2.Item, slot1.Item);

            NotifyChanged((TSlot)slot1);
            NotifyChanged((TSlot)slot2);
            return new PlaceItemSuccess(fromItem, null);
        }

        /// <summary>
        /// Transfers the whole stack from source item to target slot (up to max stack) 
        /// </summary>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result TransferAll(Item fromItem, Slot toSlot, Item _) {
            if (!Accepts(fromItem)) return Fail.ItemNotAccepted;
            if (!AllowStack(toSlot.Item)) return Fail.StackingNotAllowed;
            var (newFromItem, newToitem) = fromItem.TransferAll(toSlot.Item);
            toSlot.Item = newToitem;
            NotifyChanged((TSlot)toSlot);
            return new PlaceItemSuccess(newToitem, newFromItem);
        }

        /// <summary>
        /// Transfers one from a source item to target slot
        /// </summary>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result TransferOne(Item fromItem, Slot toSlot, Item _) {
            if (!Accepts(fromItem) || !toSlot.Accepts(fromItem)) return Fail.ItemNotAccepted;
            if (!AllowStack(toSlot.Item)) return Fail.StackingNotAllowed;
            var (newFromItem, newToItem) = fromItem.TransferOne(toSlot.Item);
            toSlot.Item = newToItem;
            NotifyChanged((TSlot)toSlot);
            return new PlaceItemSuccess(newToItem, newFromItem);
        }

        /// <summary>
        /// Splits a stack of items in half
        /// </summary>
        /// <param name="item">The item to split</param>
        /// <returns>PickItemSuccess or Fail</returns>
        public override Result SplitHalf(Item item) {
            if (GetItemPosition(item) is not TSlot slot) return Fail.WrongSlotType;
            if (!AllowStack(item)) return Fail.StackingNotAllowed;
            var (newFromItem, newToItem) = slot.Item.SplitHalf();
            slot.Item = newFromItem;
            NotifyChanged((TSlot)slot);
            return new PickItemSuccess(newToItem);
        }




    }
}
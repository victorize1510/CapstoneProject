using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public abstract class Bag {
        public readonly string Id = IdExt.ShortId();
        public string Name;
        public virtual IEnumerable<Item> Items { get => Enumerable.Empty<Item>(); }

        public virtual Result CanAdd(Item item) => Result.Success;
        public virtual Result CanRemove(Item item) => Result.Success;
        public virtual Result Add(Item item) => Result.Fail;
        public virtual Result AddAt(Slot slot, Item item) => Result.Fail;
        public virtual Result AddRange(IEnumerable<Item> items) => Result.Fail;
        public virtual Result Remove(Item item) => Result.Fail;
        public virtual Result ReplaceAt(Slot slot, Item item) => Result.Fail;
        public virtual Result TransferAll(Item fromItem, Slot toSlot, Item toItem) => Result.Fail;
        public virtual Result TransferOne(Item fromItem, Slot toSlot, Item toItem) => Result.Fail;
        public virtual Result SplitHalf(Item item) => Result.Fail;
        public virtual Result MoveItem(Item item, Bag targetBag) => BagExt.MoveItem(this, item, targetBag);
        public virtual Result SwapItems(Slot slot1, Slot slot2) => Result.Fail;

        public virtual void Clear(bool notify = true) { }
        public virtual bool Accepts(Item item) => true;
        public virtual bool AllowStack(Item item) => true;
        public virtual bool Contains(Item item) => false;
        public virtual int GetTotalQuantity(Item item) => 0;
        public virtual Slot GetItemPosition(Item item) => null;

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"<no name>" : Name;
        public override string ToString() => $"{DisplayName} ({Id}, {GetType().Name})";
    }

    public static class BagExt {
        public static Result MoveItem(IItemContext context, Bag toBag) => MoveItem(context.Bag, context.Item, toBag);
        public static Result MoveItem(Bag fromBag, Item item, Bag toBag) {
            Debug.Log($"shoould move {item} from {fromBag} to {toBag}");
            if (toBag == null) { Debug.LogWarning("target bag is null"); return Result.Fail; }
            if (!toBag.Accepts(item)) return new ItemNotAccepted();

            Result result = toBag.CanAdd(item);
            if (result is Fail) { Debug.LogWarning($"can't add {item} to {toBag}"); return result; }

            result = fromBag.CanRemove(item);
            if (result is Fail) { Debug.LogWarning($"Can't remove {item} from {fromBag}"); return result; }

            result = fromBag.Remove(item);
            if (result is Fail) { Debug.LogWarning($"tried removing but failed {fromBag}, {item}"); return result; }

            result = toBag.Add(item);
            if (result is Fail) { Debug.LogWarning($"tried adding but failed {toBag}, {item}"); return result; }

            return new PlaceItemSuccess(item, null);
        }

        public static Result MoveAllItems(Bag fromBag, Bag toBag) {
            // Debug.Log($"should move all items from {fromBag} to {toBag}");
            if (fromBag.Items.Count() == 0) return new SourceBagEmpty();

            List<Item> remaining = new();
            foreach (var i in fromBag.Items) {
                var result = toBag.Add(i);
                if (result is Fail) remaining.Add(i);
            }

            fromBag.Clear(false);
            fromBag.AddRange(remaining);

            // TODO: Add a "typed" fail event
            if (remaining.Count > 0) return Result.Fail;
            return new PlaceItemSuccess(null, null);
        }

        public static Result SwapItems(IItemContext from, IItemContext to) {
            // Swap rules
            // Fail:
            // - either slots/bags do not accept the other item
            // - item can't be removed from it's bag

            // Debug.Log($"should swap {from.Item} and {to.Item}");
            if (to.Bag == null) { Debug.LogWarning("target bag is null"); return Result.Fail; }
            if (from.Bag == null) { Debug.LogWarning("source bag is null"); return Result.Fail; }
            if (!to.Bag.Accepts(from.Item) || !to.Slot.Accepts(from.Item)) return new ItemNotAccepted();
            if (!from.Bag.Accepts(to.Item) || !from.Slot.Accepts(to.Item)) return new ItemNotAccepted();

            // Do nothing is it's the same item
            // TODO: Replace with some sort of Cancel event?
            if (from.Bag == to.Bag) return from.Bag.SwapItems(from.Slot, to.Slot);

            Result result = from.Bag.CanRemove(from.Item);
            if (result is Fail) { Debug.LogWarning($"Can't remove {from.Item} from {from.Bag}"); return result; }

            result = to.Bag.CanRemove(to.Item);
            if (result is Fail) { Debug.LogWarning($"Can't remove {to.Item} from {to.Bag}"); return result; }

            result = to.Bag.AddAt(to.Slot, from.Item);
            if (result is Fail) { Debug.LogWarning($"tried adding but failed, Bag: {to.Bag}, Item: {from.Item}"); return result; }

            // if result is success should 
            if (result is PlaceItemSuccess r1) {
                from.Bag.ReplaceAt(from.Slot, r1.Replaced);
            }

            return new PlaceItemSuccess(from.Item, null);
        }
    }

}
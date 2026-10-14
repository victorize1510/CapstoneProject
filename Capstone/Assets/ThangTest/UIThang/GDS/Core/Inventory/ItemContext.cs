using UnityEngine;

namespace GDS.Core {

    public interface IItemContext {
        Bag Bag { get; }
        Slot Slot { get; }
        Item Item { get; }
        Rect WorldBound { get; }
        // public (Bag bag, Slot slot, Item item, Rect rect) Deconstruct() => (Bag, Slot, Item, WorldBound);
        public void Deconstruct(out Bag bag, out Slot slot, out Item item) => (bag, slot, item) = (Bag, Slot, Item);
        public void Deconstruct(out Bag bag, out Slot slot) => (bag, slot) = (Bag, Slot);
    }

    public class ItemContext : IItemContext {
        public Bag Bag { get; set; }
        public Slot Slot { get; set; }
        public Item Item { get; set; }
        public Rect WorldBound { get; set; }
        public bool Empty => Item == null;
        public void Copy(IItemContext c) => (Bag, Slot, Item, WorldBound) = (c.Bag, c.Slot, c.Item, c.WorldBound);
        public void Clear() => (Bag, Slot, Item, WorldBound) = (null, null, null, Rect.zero);
    }
}
using UnityEngine;

namespace GDS.Core {
    public class GhostContext : IItemContext, IObservable<IItemContext> {
        public Bag Bag { get; set; }
        public Slot Slot { get; set; }
        public Item Item { get; set; }
        public Rect WorldBound { get; set; }

        public IItemContext Value => this;
        public event System.Action<IItemContext> OnChange;
        public void Notify() { OnChange?.Invoke(this); }
        public bool Empty => Item == null;

        public void SetValue(IItemContext value) => SetValue(value, value?.Item);
        public void SetValue(IItemContext value, Item item) {
            if (value == null) { Reset(); return; }
            Bag = value.Bag;
            Slot = value.Slot;
            Item = item;
            WorldBound = value.WorldBound;
            Notify();
        }

        public void Reset() {
            Clear();
            Notify();
        }

        void Clear() {
            Bag = null;
            Slot = null;
            Item = null;
        }

        public override string ToString() {
            if (Empty) return "<empty>";
            return $"Item: {Item}\nBag: {Bag}\nSlot: {Slot}\nRect: {WorldBound}";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public class SetBag : ListBag<SetSlot>, ISerializationCallbackReceiver {
        [NonSerialized] public static readonly int DefaultSize = 5;
        [SerializeReference] List<SetSlot> slots = CreateSlots(DefaultSize);

        public override List<SetSlot> Slots => slots;

        public int Size { get => Slots.Count; set => slots = CreateSlots(value); }
        public SetSlot FindSlot(string key) => Slots.Find(s => s.Key == key);
        static protected List<SetSlot> CreateSlots(int size) => Enumerable.Range(0, size).Select(CreateSlot).ToList();
        static protected string Key(int i) => "slot-" + i;
        static protected SetSlot CreateSlot(int i) => new(Key(i));
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() {
            // Resizing the list in the inspector does instantiate all ita items due to virtualization
            // This ensures that list slots are never null
            for (var i = 0; i < Slots.Count; i++) {
                if (Slots[i] == null) { Slots[i] = CreateSlot(i); }
            }
        }
    }
}
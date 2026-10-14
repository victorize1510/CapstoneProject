using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Examples {
    public class CustomRightClickEvent : ItemCommand {
        public CustomRightClickEvent(Bag bag, Slot slot, Item item, EventModifiers mods) : base(bag, slot, item, mods) { }
    }

    public class CustomDoubleClickEvent : ItemCommand {
        public CustomDoubleClickEvent(Bag bag, Slot slot, Item item, EventModifiers mods) : base(bag, slot, item, mods) { }
    }
}

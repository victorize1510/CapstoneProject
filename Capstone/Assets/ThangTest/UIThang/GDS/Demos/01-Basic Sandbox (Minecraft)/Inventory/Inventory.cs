using System;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Demos.Basic {

    [Serializable]
    public class Inventory : ListBag {
        public Inventory() {
            Name = "Inventory";
            Size = 40;
        }

        public override Result MoveItem(Item item, Bag targetBag) {
            Result result = base.MoveItem(item, targetBag);
            if (targetBag is Shop) return result.MapTo(new SellItemSuccess(item));
            return result;
        }
    }

}
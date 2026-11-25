using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Demos.Basic {
    [Serializable]
    public class Shop : DenseListBag {
        public List<Basic_ItemBase> Catalog = new();

        Inventory inventory;
        Observable<int> playerGold;

        public void Init(Inventory inventory, Observable<int> playerGold) {
            this.inventory = inventory;
            this.playerGold = playerGold;
            Refresh();
        }

        public void Refresh() {
            Clear();
            AddRange(Catalog.Select(b => b.CreateItem()).OrderBy(_ => UnityEngine.Random.Range(0, 100)));
        }

        public override bool AllowStack(Item _) => false;

        public override Result Add(Item item) {
            var result = base.Add(item);
            if (result is Success) playerGold.SetValue(playerGold.Value + item.Cost());
            return result.MapTo(new SellItemSuccess(item));
            // return result;
        }

        public override Result AddAt(Slot slot, Item item) {
            return Add(item);
        }

        public override Result CanRemove(Item item) {
            var itemCost = item.Cost();
            if (playerGold.Value < itemCost) { Debug.Log("Not enough gold".Red()); }
            return playerGold.Value < itemCost ? Result.Fail : Result.Success;
        }

        public override Result Remove(Item item) {
            var result = CanRemove(item);
            if (result is Success) result = base.Remove(item);
            if (result is Success) playerGold.SetValue(playerGold.Value - item.Cost());
            return result.MapTo(new BuyItemSuccess(item));
            // return result;
        }

        public override Result MoveItem(Item item, Bag targetBag) {
            if (targetBag != inventory) return Result.Fail;
            var result = BagExt.MoveItem(this, item, targetBag);
            return result.MapTo(new BuyItemSuccess(item));
        }
    }

}
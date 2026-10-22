using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {

    [Serializable]
    public class BagCollection<T> : Bag where T : Bag {
        public BagCollection(params T[] bags) { Bags = bags.ToList(); }
        public Observable<int> CurrentIndex = new(0);
        public T Current => Bags.ElementAtOrDefault(CurrentIndex.Value);
        public List<T> Bags;

        public void SetCurrentIndex(int i) => CurrentIndex.SetValue(i);
        public override Result Add(Item item) => Current.Add(item);
        public override Result CanAdd(Item item) => Current.CanAdd(item);
    }

}

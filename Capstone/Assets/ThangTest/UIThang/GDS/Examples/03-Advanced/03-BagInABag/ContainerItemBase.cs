using GDS.Core;
using UnityEngine;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/ContainerItemBase")]
    public class ContainerItemBase : ItemBase {
        public int Capacity;
        public override Item CreateItem() => new ContainerItem() { Base = this, Name = Name, Capacity = new ListBag() { Size = Capacity } };
    }

    [System.Serializable]
    public class ContainerItem : Item {
        public ListBag Capacity;
    }

}
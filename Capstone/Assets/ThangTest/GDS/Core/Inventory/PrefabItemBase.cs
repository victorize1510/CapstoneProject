using UnityEngine;

namespace GDS.Core
{

    [CreateAssetMenu(menuName = "SO/Core/PrefabItemBase", order = 1)]
    public class PrefabItemBase : ItemBase, IHasPrefab
    {
        public GameObject prefab;
        public GameObject Prefab => prefab;

        override public Item CreateItem() => new Item() { Base = this, Name = Name, StackSize = MaxStackSize };
    }
}
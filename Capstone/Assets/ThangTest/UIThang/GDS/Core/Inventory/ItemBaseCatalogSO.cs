using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GDS.Core {
    [CreateAssetMenu(menuName = "SO/Core/ItemBaseCatalog", order = 100)]
    public class ItemBaseCatalogSO : ScriptableObject {
        [SerializeField] List<ItemBase> items;
        public List<ItemBase> Items => items.Where(x => x != null).ToList();
        public int Count => Items.Count;
        public ItemBase GetRandom() => Count == 0 ? null : Items[Random.Range(0, Items.Count)];
        public Item CreateRandomItem() {
            var itemBase = GetRandom();
            if (itemBase == null) return null;
            var item = itemBase.CreateItem();
            if (item.Stackable) item.StackSize = Random.Range(1, itemBase.MaxStackSize + 1);
            return item;
        }
    }

}
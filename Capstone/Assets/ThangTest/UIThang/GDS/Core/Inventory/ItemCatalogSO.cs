using System.Collections.Generic;
using UnityEngine;

namespace GDS.Core {
    [CreateAssetMenu(menuName = "SO/Core/ItemCatalog", order = 100)]
    public class ItemCatalogSO : ScriptableObject {
        [SerializeReference]
        public List<Item> Items;
    }

}
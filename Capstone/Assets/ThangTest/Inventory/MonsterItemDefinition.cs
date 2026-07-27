using GDS.Core;
using UnityEngine;

namespace Capstone.Game.Inventory {
    [CreateAssetMenu(menuName = "Game/Inventory/Item Definition", fileName = "NewGameItem")]
    public class MonsterItemDefinition : ItemBase {
        [SerializeField] GameItemCategory category = GameItemCategory.Material;
        [SerializeField, TextArea(2, 5)] string description = string.Empty;
        [SerializeField, TextArea(1, 3)] string effect = string.Empty;
        [SerializeField] bool usableFromInventory = true;
        [SerializeField] bool consumable = true;

        public GameItemCategory Category => category;
        public string Description => description;
        public string Effect => effect;
        public bool UsableFromInventory => usableFromInventory;
        public bool Consumable => consumable;

        public override Item CreateItem() {
            return new MonsterInventoryItem {
                Base = this,
                Name = Name,
                StackSize = 1
            };
        }

        public static MonsterItemDefinition CreateRuntime(
            string itemName,
            GameItemCategory itemCategory,
            string itemDescription,
            string itemEffect,
            Sprite itemIcon = null,
            bool itemStackable = true,
            int itemMaxStackSize = 99,
            bool itemUsableFromInventory = true,
            bool itemConsumable = true) {
            var item = CreateInstance<MonsterItemDefinition>();
            item.name = itemName;
            item.Name = itemName;
            item.Icon = itemIcon;
            item.Stackable = itemStackable;
            item.MaxStackSize = Mathf.Max(1, itemMaxStackSize);
            item.category = itemCategory;
            item.description = itemDescription;
            item.effect = itemEffect;
            item.usableFromInventory = itemUsableFromInventory;
            item.consumable = itemConsumable;
            return item;
        }
    }

    [System.Serializable]
    public class MonsterInventoryItem : Item {
        public override Item Clone() {
            return new MonsterInventoryItem {
                Id = Id,
                Base = Base,
                Name = Name,
                StackSize = StackSize
            };
        }
    }
}

using GDS.Core;
using UnityEngine;

namespace Capstone.Game.Inventory {
    public enum InventoryItemRarity {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    [CreateAssetMenu(menuName = "Game/Inventory/Item Definition", fileName = "NewGameItem")]
    public class MonsterItemDefinition : ItemBase {
        [SerializeField] string stableId = string.Empty;
        [SerializeField] string[] legacyIds = System.Array.Empty<string>();
        [SerializeField] GameItemCategory category = GameItemCategory.Material;
        [SerializeField] InventoryItemRarity rarity = InventoryItemRarity.Common;
        [SerializeField, TextArea(2, 5)] string description = string.Empty;
        [SerializeField, TextArea(1, 3)] string effect = string.Empty;
        [SerializeField] string source = string.Empty;
        [SerializeField, TextArea(1, 3)] string flavorText = string.Empty;
        [SerializeField, Min(0)] int healAmount;
        [SerializeField] bool usableFromInventory = true;
        [SerializeField] bool consumable = true;

        public GameItemCategory Category => category;
        public string StableId => string.IsNullOrWhiteSpace(stableId) ? name : stableId.Trim();
        public void AssignStableId(string value) { stableId = value?.Trim() ?? string.Empty; }
        public bool MatchesId(string value) {
            if (string.Equals(StableId, value, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(name, value, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (legacyIds != null) foreach (string alias in legacyIds)
                if (string.Equals(alias, value, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        public InventoryItemRarity Rarity => rarity;
        public string Description => description;
        public string Effect => effect;
        public string Source => source;
        public string FlavorText => flavorText;
        public int HealAmount => Mathf.Max(0, healAmount);
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
            bool itemConsumable = true,
            int itemHealAmount = 0,
            InventoryItemRarity itemRarity = InventoryItemRarity.Common,
            string itemSource = "",
            string itemFlavorText = "") {
            var item = CreateInstance<MonsterItemDefinition>();
            item.name = itemName;
            item.stableId = itemName;
            item.Name = itemName;
            item.Icon = itemIcon;
            item.Stackable = itemStackable;
            item.MaxStackSize = Mathf.Max(1, itemMaxStackSize);
            item.category = itemCategory;
            item.rarity = itemRarity;
            item.description = itemDescription;
            item.effect = itemEffect;
            item.source = itemSource ?? string.Empty;
            item.flavorText = itemFlavorText ?? string.Empty;
            item.healAmount = Mathf.Max(0, itemHealAmount);
            item.usableFromInventory = itemUsableFromInventory;
            item.consumable = itemConsumable;
            return item;
        }

        void OnValidate() {
            healAmount = Mathf.Max(0, healAmount);
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

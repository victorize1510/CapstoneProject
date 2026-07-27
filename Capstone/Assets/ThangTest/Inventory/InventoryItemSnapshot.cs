using GDS.Core;
using UnityEngine;

namespace Capstone.Game.Inventory {
    public sealed class InventoryItemSnapshot {
        public ItemBase ItemBase { get; }
        public Item RepresentativeItem { get; }
        public string Name { get; }
        public string Description { get; }
        public string Effect { get; }
        public string ItemId { get; }
        public GameItemCategory Category { get; }
        public Sprite Icon { get; }
        public int Quantity { get; }
        public bool Stackable { get; }
        public int MaxStackSize { get; }
        public bool UsableFromInventory { get; }
        public bool Consumable { get; }

        public InventoryItemSnapshot(ItemBase itemBase, Item representativeItem, int quantity) {
            ItemBase = itemBase;
            RepresentativeItem = representativeItem;
            Quantity = quantity;

            var gameItem = itemBase as MonsterItemDefinition;
            Name = !string.IsNullOrWhiteSpace(representativeItem?.Name)
                ? representativeItem.Name
                : itemBase != null && !string.IsNullOrWhiteSpace(itemBase.Name)
                    ? itemBase.Name
                    : itemBase != null
                        ? itemBase.name
                        : string.Empty;
            ItemId = ResolveItemId(itemBase, representativeItem, Name);
            Description = gameItem != null ? gameItem.Description : string.Empty;
            Effect = gameItem != null ? gameItem.Effect : string.Empty;
            Category = gameItem != null ? gameItem.Category : GameItemCategory.Material;
            Icon = itemBase != null ? itemBase.Icon : null;
            Stackable = itemBase != null && itemBase.Stackable;
            MaxStackSize = itemBase != null ? itemBase.MaxStackSize : 1;
            UsableFromInventory = gameItem != null && gameItem.UsableFromInventory;
            Consumable = gameItem == null || gameItem.Consumable;
        }

        static string ResolveItemId(ItemBase itemBase, Item representativeItem, string fallbackName) {
            if (itemBase != null && !string.IsNullOrWhiteSpace(itemBase.name)) return itemBase.name;
            if (representativeItem != null && !string.IsNullOrWhiteSpace(representativeItem.Id)) return representativeItem.Id;
            return fallbackName ?? string.Empty;
        }
    }
}

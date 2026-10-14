using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.ShopSystem {
    // Display grouping only; does not change Item category, identity or save format.
    public enum ShopCategory { Automatic = -1, Consumables, Capture, Materials, Special, Evolution }
    public enum ShopSort { Default, Name, PriceAscending, PriceDescending, Rarity }

    [Serializable]
    public sealed class ShopOffer {
        public string Id;
        public MonsterItemDefinition Item;
        public ShopCategory Category = ShopCategory.Automatic;
        [Tooltip("-1: chưa cấu hình giá; 0: miễn phí.")] public int GoldPrice = -1;
        [Tooltip("-1: không giới hạn; số khác là tồn kho cho phiên chơi này.")] public int Stock = -1;
        [Min(1)] public int MaxPerPurchase = 99;
        public bool Unlocked = true;
        public string LockedReason;
    }

    [CreateAssetMenu(menuName = "Game/Shop/Catalog", fileName = "ShopCatalog")]
    public sealed class ShopCatalog : ScriptableObject {
        [SerializeField] List<ShopOffer> offers = new List<ShopOffer>();
        public IReadOnlyList<ShopOffer> Offers => offers;
    }

    public static class ShopPresentation {
        public static readonly string[] CategoryNames = {
            "Vật phẩm tiêu hao", "Vật phẩm thu phục", "Nguyên liệu", "Đặc biệt", "Đá tiến hóa"
        };
        public static ShopCategory Category(ShopOffer offer) {
            if (offer == null || offer.Item == null) return ShopCategory.Automatic;
            if (offer.Category != ShopCategory.Automatic) return offer.Category;
            switch (offer.Item.Category) {
                case GameItemCategory.Medicine: case GameItemCategory.Food: return ShopCategory.Consumables;
                case GameItemCategory.CaptureBall: return ShopCategory.Capture;
                case GameItemCategory.Material: return ShopCategory.Materials;
                default: return ShopCategory.Automatic; // No implicit sale of quest/key/equipment items.
            }
        }
        public static string Price(int gold) => gold >= 0 ? gold.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " vàng" : "Chưa có giá";
    }
}

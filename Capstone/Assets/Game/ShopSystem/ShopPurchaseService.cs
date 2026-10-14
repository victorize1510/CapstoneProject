using System;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;

namespace Capstone.Game.ShopSystem {
    public sealed class ShopPurchaseService {
        readonly MonsterInventoryAdapter inventory;
        readonly PlayerCurrencyWallet wallet;
        readonly ActionRepeatGate gate;
        bool buying;
        public ShopPurchaseService(MonsterInventoryAdapter inventory, PlayerCurrencyWallet wallet, Func<double> clock = null) {
            this.inventory = inventory; this.wallet = wallet;
            gate = clock == null ? new ActionRepeatGate() : new ActionRepeatGate(clock);
        }

        public int Maximum(ShopOffer offer, out string reason) {
            reason = string.Empty;
            if (inventory == null || wallet == null) { reason = "Chưa kết nối túi đồ hoặc ví vàng."; return 0; }
            if (offer?.Item == null) { reason = "Vật phẩm không còn được bán."; return 0; }
            if (!offer.Unlocked) { reason = string.IsNullOrWhiteSpace(offer.LockedReason) ? "Vật phẩm đang bị khóa." : offer.LockedReason; return 0; }
            if (offer.GoldPrice < 0) { reason = "Vật phẩm chưa được cấu hình giá mua."; return 0; }
            if (offer.Stock < -1 || offer.MaxPerPurchase < 1) { reason = "Cấu hình số lượng không hợp lệ."; return 0; }
            if (offer.Stock == 0) { reason = "Vật phẩm đã hết hàng."; return 0; }
            int affordable = offer.GoldPrice == 0 ? int.MaxValue : wallet.Gold / offer.GoldPrice;
            int maximum = Math.Min(affordable, offer.MaxPerPurchase);
            if (offer.Stock >= 0) maximum = Math.Min(maximum, offer.Stock);
            maximum = Math.Min(maximum, int.MaxValue - Owned(offer));
            if (maximum == 0) reason = affordable == 0 ? "Không đủ vàng." : "Số lượng đang có đã đạt giới hạn.";
            return maximum;
        }

        GDS.Core.ItemBase InventoryDefinition(ShopOffer offer) => inventory.FindItemBase(offer.Item.StableId) ?? offer.Item;
        public int Owned(ShopOffer offer) => inventory != null && offer?.Item != null ? inventory.GetQuantity(InventoryDefinition(offer)) : 0;

        public bool TryBuy(ShopSource source, string offerId, int quantity, int expectedPrice, out string message) {
            message = string.Empty;
            if (buying || !gate.TryAccept("purchase")) return false;
            buying = true;
            try {
                var offer = source != null ? source.Find(offerId) : null;
                int maximum = Maximum(offer, out message);
                if (maximum == 0) return false;
                if (offer.GoldPrice != expectedPrice) { message = "Giá đã thay đổi. Hãy kiểm tra lại trước khi mua."; return false; }
                if (quantity < 1 || quantity > maximum) { message = "Số lượng mua vượt giới hạn hiện tại."; return false; }
                long total = (long)quantity * offer.GoldPrice;
                if (total > int.MaxValue) { message = "Tổng giá vượt giới hạn thanh toán."; return false; }
                bool received = wallet.TrySpendGold((int)total, () => {
                    if (!inventory.TryAddItemAtomic(InventoryDefinition(offer), quantity, false)) return false;
                    if (offer.Stock >= 0) offer.Stock -= quantity;
                    return true;
                });
                if (!received) { message = "Không thể nhận vật phẩm. Vàng của bạn được giữ nguyên."; return false; }
                // Every observer now sees both committed balances and stock. Existing
                // PlayerSaveController subscribes to these events and schedules its save.
                inventory.NotifyAtomicAddition(); source.NotifyChanged();
                message = "Đã mua " + quantity + " × " + offer.Item.Name + ".";
                return true;
            } finally { buying = false; }
        }
    }
}

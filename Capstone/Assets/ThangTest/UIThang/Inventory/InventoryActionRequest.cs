namespace Capstone.Game.Inventory {
    public sealed class InventoryActionRequest {
        public InventoryActionType ActionType { get; }
        public InventoryItemSnapshot Item { get; }
        public string ItemId { get; }
        public string TargetMonsterId { get; }
        public bool IsCompleted { get; private set; }
        public bool Success { get; private set; }
        public bool RemoveItemOnSuccess { get; private set; }
        public int RemoveQuantity { get; private set; }
        public int Quantity => RemoveQuantity;
        public string Message { get; private set; }

        public InventoryActionRequest(
            InventoryActionType actionType,
            InventoryItemSnapshot item,
            bool removeItemOnSuccess,
            int removeQuantity,
            string targetMonsterId = null) {
            ActionType = actionType;
            Item = item;
            ItemId = item != null ? item.ItemId : string.Empty;
            TargetMonsterId = targetMonsterId ?? string.Empty;
            RemoveItemOnSuccess = removeItemOnSuccess;
            RemoveQuantity = removeQuantity < 0 ? 0 : removeQuantity;
        }

        public void Complete(bool success, string message = null) {
            Complete(success, RemoveItemOnSuccess, RemoveQuantity, message);
        }

        public void Complete(bool success, bool removeItemOnSuccess, int removeQuantity = 1, string message = null) {
            if (IsCompleted) return;

            IsCompleted = true;
            Success = success;
            RemoveItemOnSuccess = removeItemOnSuccess;
            RemoveQuantity = removeQuantity < 0 ? 0 : removeQuantity;
            Message = message;
        }
    }
}

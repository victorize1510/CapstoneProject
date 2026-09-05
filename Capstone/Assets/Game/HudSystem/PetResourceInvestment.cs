using System;
using GDS.Core;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [Serializable]
    public sealed class PetResourceInvestment {
        [SerializeField] ItemBase itemBase = null;
        [SerializeField] string itemId = string.Empty;
        [SerializeField] string displayName = string.Empty;
        [SerializeField, Min(0)] int quantity;
        [SerializeField] bool refundable = true;

        public ItemBase ItemBase => itemBase;
        public string ItemId => itemId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public int Quantity => Mathf.Max(0, quantity);
        public bool Refundable => refundable;

        public PetResourceInvestment(ItemBase source, string id, string name, int amount, bool canRefund) {
            itemBase = source;
            itemId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            quantity = Mathf.Max(0, amount);
            refundable = canRefund;
        }

        public bool Matches(string id) {
            return !string.IsNullOrWhiteSpace(id)
                && string.Equals(ItemId, id.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public void Add(ItemBase source, string name, int amount, bool canRefund) {
            if (itemBase == null && source != null) itemBase = source;
            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(name)) displayName = name.Trim();
            quantity = Mathf.Max(0, quantity + Mathf.Max(0, amount));
            refundable &= canRefund;
        }
    }
}

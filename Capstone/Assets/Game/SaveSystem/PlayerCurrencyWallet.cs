using System;
using UnityEngine;

namespace Capstone.Game.SaveSystem {
    [DisallowMultipleComponent]
    public sealed class PlayerCurrencyWallet : MonoBehaviour {
        [SerializeField, Min(0)] int gold;
        bool receivingPurchase;

        public int Gold => Mathf.Max(0, gold);

        public event Action<int> GoldChanged;

        public void AddGold(int amount) {
            if (amount <= 0) return;

            gold = (int)Math.Min(int.MaxValue, (long)Gold + amount);
            GoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(int amount) {
            if (receivingPurchase) return false;
            if (amount <= 0) return true;
            if (Gold < amount) return false;

            gold -= amount;
            GoldChanged?.Invoke(Gold);
            return true;
        }

        // Reserve without notifying observers; receive must either commit the whole
        // delivery or leave inventory unchanged. Both events then see final balances.
        public bool TrySpendGold(int amount, Func<bool> receive) {
            if (receivingPurchase || amount < 0 || receive == null || Gold < amount) return false;
            receivingPurchase = true;
            gold -= amount;
            bool accepted = false;
            try { accepted = receive(); }
            finally {
                if (!accepted) gold += amount;
                receivingPurchase = false;
            }
            if (accepted) GoldChanged?.Invoke(Gold);
            return accepted;
        }

        public CurrencySaveData CreateSaveData() {
            return new CurrencySaveData {
                captured = true,
                gold = Gold
            };
        }

        public void RestoreFromSaveData(CurrencySaveData saveData) {
            if (saveData == null || !saveData.captured) return;

            gold = Mathf.Max(0, saveData.gold);
            GoldChanged?.Invoke(Gold);
        }

        void OnValidate() {
            gold = Mathf.Max(0, gold);
        }
    }
}

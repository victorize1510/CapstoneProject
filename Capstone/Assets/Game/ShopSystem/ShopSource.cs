using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.ShopSystem {
    /// <summary>One runtime inventory per merchant. Catalog assets are never mutated.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopSource : MonoBehaviour {
        [SerializeField] ShopCatalog catalog;
        readonly List<ShopOffer> offers = new List<ShopOffer>();
        public IReadOnlyList<ShopOffer> Offers => offers;
        public event Action Changed;

        void Awake() => Configure(catalog != null ? catalog : Resources.Load<ShopCatalog>("Shop/DefaultShopCatalog"));
        public void Configure(ShopCatalog value) {
            catalog = value; offers.Clear();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (value != null) foreach (var source in value.Offers) {
                if (source == null) continue;
                string id = string.IsNullOrWhiteSpace(source.Id) ? source.Item?.StableId : source.Id;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || source.Item == null
                    || (int)ShopPresentation.Category(source) < 0 || (int)ShopPresentation.Category(source) > 4) {
                    Debug.LogError("Shop catalog has an invalid or duplicate offer: " + id, value); continue;
                }
                offers.Add(new ShopOffer { Id = id, Item = source.Item, Category = source.Category,
                    GoldPrice = source.GoldPrice, Stock = source.Stock, MaxPerPurchase = source.MaxPerPurchase,
                    Unlocked = source.Unlocked, LockedReason = source.LockedReason });
            }
            Changed?.Invoke();
        }
        public ShopOffer Find(string id) => offers.Find(offer => offer.Id == id);
        public void NotifyChanged() => Changed?.Invoke();
    }
}

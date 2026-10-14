using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.ShopSystem {
    internal sealed class ShopProductCardView : IDisposable {
        public readonly TemplateContainer Root;
        readonly Button button;
        readonly Label name, price, status, placeholder;
        readonly VisualElement icon;
        readonly Action click;
        ShopOffer offer;
        public ShopProductCardView(VisualTreeAsset template, Action<ShopOffer> select) {
            Root = template.CloneTree(); button = Root.Q<Button>("shop-card");
            name = Root.Q<Label>("card-name"); price = Root.Q<Label>("card-price"); status = Root.Q<Label>("card-status");
            icon = Root.Q("card-icon"); placeholder = Root.Q<Label>("card-placeholder");
            click = () => { if (offer != null) select(offer); }; button.clicked += click;
        }
        public void Render(ShopOffer value, string selectedId) {
            offer = value;
            Root.style.display = value == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (value == null) return;
            name.text = value.Item.Name; price.text = ShopPresentation.Price(value.GoldPrice);
            status.text = !value.Unlocked ? "Bị khóa" : value.Stock == 0 ? "Hết hàng" : value.Stock > 0 ? "Còn " + value.Stock : "";
            status.style.display = status.text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            icon.style.backgroundImage = value.Item.Icon != null ? new StyleBackground(value.Item.Icon) : new StyleBackground(StyleKeyword.None);
            placeholder.style.display = value.Item.Icon == null ? DisplayStyle.Flex : DisplayStyle.None;
            button.EnableInClassList("is-selected", value.Id == selectedId);
            button.EnableInClassList("is-unavailable", !value.Unlocked || value.Stock == 0 || value.GoldPrice < 0);
            button.tooltip = value.Item.Name + " • " + price.text;
        }
        public void Dispose() { button.clicked -= click; Root.RemoveFromHierarchy(); }
    }
}

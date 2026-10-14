using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.ShopSystem {
    [DisallowMultipleComponent]
    public sealed class ShopScreenController : MonoBehaviour {
        readonly List<Action> unbind = new List<Action>();
        readonly List<ShopProductCardView> cards = new List<ShopProductCardView>();
        readonly List<ShopOffer> filtered = new List<ShopOffer>();
        readonly List<Button> categories = new List<Button>();
        readonly Dictionary<string, string> searchNames = new Dictionary<string, string>();
        VisualElement root, grid, detail, purchase;
        ScrollView productScroll;
        Label empty, detailEmpty, gold, feedback, itemName, owned, badge, rarity, description, effect, stock, total, reasonLabel;
        Image portrait;
        TextField search;
        DropdownField sort;
        IntegerField quantity;
        Button minus, plus, max, buy;
        ShopSource source, defaultSource;
        MonsterInventoryAdapter inventory;
        PlayerCurrencyWallet wallet;
        ShopPurchaseService service;
        VisualTreeAsset cardTemplate;
        ShopCategory category;
        ShopOffer selected;
        int selectedPrice;
        bool open, subscribed, buying;
        public bool IsOpen => open;
        public event Action BackRequested;
        static readonly List<string> SortNames = new List<string> {
            "Mặc định", "Tên A–Z", "Giá tăng dần", "Giá giảm dần", "Độ hiếm"
        };

        public void Bind(UIDocument document, MonsterInventoryAdapter bag, PlayerCurrencyWallet currency) => Bind(document?.rootVisualElement, bag, currency);
        public void Bind(VisualElement documentRoot, MonsterInventoryAdapter bag, PlayerCurrencyWallet currency) {
            PanelFocusUtility.Close(root);
            Unsubscribe(); Unbind(); inventory = bag; wallet = currency;
            service = new ShopPurchaseService(bag, currency);
            root = documentRoot?.Q("shop-panel");
            if (root == null) return;
            cardTemplate = Resources.Load<VisualTreeAsset>("UI/ShopProductCard");
            grid = root.Q("shop-grid"); detail = root.Q("shop-detail"); purchase = root.Q("shop-purchase");
            productScroll = root.Q<ScrollView>(className: "shop-grid-scroll");
            empty = root.Q<Label>("shop-empty"); detailEmpty = root.Q<Label>("shop-detail-empty");
            gold = root.Q<Label>("shop-gold"); feedback = root.Q<Label>("shop-feedback");
            portrait = root.Q<Image>("shop-portrait"); itemName = root.Q<Label>("shop-item-name"); owned = root.Q<Label>("shop-owned");
            badge = root.Q<Label>("shop-item-category"); rarity = root.Q<Label>("shop-rarity");
            description = root.Q<Label>("shop-description"); effect = root.Q<Label>("shop-effect"); stock = root.Q<Label>("shop-stock");
            total = root.Q<Label>("shop-total"); search = root.Q<TextField>("shop-search"); sort = root.Q<DropdownField>("shop-sort");
            reasonLabel = root.Q<Label>("shop-reason");
            quantity = root.Q<IntegerField>("shop-quantity"); minus = root.Q<Button>("shop-minus"); plus = root.Q<Button>("shop-plus");
            max = root.Q<Button>("shop-max"); buy = root.Q<Button>("shop-buy");
            sort.choices = SortNames; sort.SetValueWithoutNotify(SortNames[0]); VerticalDropdownPopup.Bind(sort);
            categories.AddRange(root.Query<Button>(className: "shop-category").ToList());
            for (int i = 0; i < categories.Count; i++) {
                int index = i; categories[i].Q<Label>(className: "shop-category-name").text = ShopPresentation.CategoryNames[i];
                Click(categories[i], () => { category = (ShopCategory)index; selected = null; search.SetValueWithoutNotify(""); ApplyQuery(); });
            }
            Click(root.Q<Button>("shop-back"), () => BackRequested?.Invoke());
            Click(root.Q<Button>("shop-clear"), () => { search.value = ""; search.Focus(); });
            Click(minus, () => SetQuantity(quantity.value - 1));
            Click(plus, () => SetQuantity(quantity.value + 1));
            Click(max, () => SetQuantity(service.Maximum(selected, out _)));
            // The service owns the purchase throttle for mouse and keyboard together.
            buy.clicked += Buy; unbind.Add(() => buy.clicked -= Buy);
            Change(search, _ => ApplyQuery()); Change(sort, _ => ApplyQuery());
            EventCallback<ChangeEvent<int>> changeQuantity = _ => SetQuantity(quantity.value);
            quantity.RegisterValueChangedCallback(changeQuantity); unbind.Add(() => quantity.UnregisterValueChangedCallback(changeQuantity));
            productScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(LayoutGrid);
            unbind.Add(() => productScroll.contentViewport.UnregisterCallback<GeometryChangedEvent>(LayoutGrid));
            if (open) { Show(root, true); PanelFocusUtility.Open(root); Subscribe(); Refresh(); } else Show(root, false);
        }
        public void SetSource(ShopSource value) {
            Unsubscribe(); source = value;
            if (open) { Subscribe(); Refresh(); }
        }
        public void SetPlayerSources(MonsterInventoryAdapter bag, PlayerCurrencyWallet currency) {
            if (ReferenceEquals(inventory, bag) && ReferenceEquals(wallet, currency)) return;
            Unsubscribe(); inventory = bag; wallet = currency; service = new ShopPurchaseService(bag, currency);
            if (open) { Subscribe(); Refresh(); }
        }
        public void Open(ShopSource value = null) {
            if (root == null || service == null) { Debug.LogError("ShopScreenController must be bound before opening.", this); return; }
            if (value == null) {
                if (defaultSource == null) { defaultSource = GetComponent<ShopSource>(); if (defaultSource == null) defaultSource = gameObject.AddComponent<ShopSource>(); }
                value = defaultSource;
            }
            SetSource(value); open = true; Subscribe();
            category = ShopCategory.Consumables; selected = null; feedback.text = "";
            search.SetValueWithoutNotify(""); sort.SetValueWithoutNotify(SortNames[0]);
            for (int i = 0; i < 5; i++) {
                if (source != null && HasCategory((ShopCategory)i)) { category = (ShopCategory)i; break; }
            }
            Show(root, true); PanelFocusUtility.Open(root); Refresh();
        }
        bool HasCategory(ShopCategory value) { foreach (var offer in source.Offers) if (ShopPresentation.Category(offer) == value) return true; return false; }
        public void Close() { open = false; Unsubscribe(); Show(root, false); PanelFocusUtility.Close(root); }
        void OnDisable() => Close();
        void OnDestroy() { Close(); Unbind(); }
        void Subscribe() {
            if (subscribed || !open) return;
            if (source != null) source.Changed += OnChanged;
            if (inventory != null) inventory.InventoryChanged += OnChanged;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
            subscribed = true;
        }
        void Unsubscribe() {
            if (!subscribed) return;
            if (source != null) source.Changed -= OnChanged;
            if (inventory != null) inventory.InventoryChanged -= OnChanged;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            subscribed = false;
        }
        void OnChanged() { if (open && !buying) Refresh(); }
        void OnGoldChanged(int _) => OnChanged();
        void Unbind() {
            foreach (var action in unbind) action(); unbind.Clear();
            VerticalDropdownPopup.Unbind(sort);
            foreach (var card in cards) card.Dispose(); cards.Clear(); categories.Clear(); searchNames.Clear();
        }
        void Click(Button button, Action action) {
            Action guarded = PlayerActionGate.Guard(this, "shop:" + button.name, action);
            button.clicked += guarded; unbind.Add(() => button.clicked -= guarded);
        }
        void Change(BaseField<string> field, EventCallback<ChangeEvent<string>> callback) {
            field.RegisterValueChangedCallback(callback); unbind.Add(() => field.UnregisterValueChangedCallback(callback));
        }
        public void Refresh() {
            if (root == null) return;
            gold.text = wallet != null ? ShopPresentation.Price(wallet.Gold) : "Chưa có ví vàng";
            searchNames.Clear();
            for (int i = 0; i < categories.Count; i++) {
                int count = 0;
                if (source != null) foreach (var offer in source.Offers) if (ShopPresentation.Category(offer) == (ShopCategory)i) count++;
                categories[i].Q<Label>(className: "shop-category-count").text = count.ToString();
            }
            ApplyQuery();
        }
        void ApplyQuery() {
            if (root == null) return;
            filtered.Clear(); string key = VietnameseUI.SearchKey(search.value);
            if (source != null) foreach (var offer in source.Offers) {
                if (ShopPresentation.Category(offer) != category) continue;
                if (!searchNames.TryGetValue(offer.Id, out string name)) { name = VietnameseUI.SearchKey(offer.Item.Name); searchNames[offer.Id] = name; }
                if (key.Length == 0 || name.Contains(key)) filtered.Add(offer);
            }
            var mode = (ShopSort)Mathf.Max(0, sort.index);
            if (mode != ShopSort.Default) filtered.Sort((a, b) => Compare(a, b, mode));
            if (selected == null || !filtered.Contains(selected)) { selected = filtered.Count > 0 ? filtered[0] : null; quantity.SetValueWithoutNotify(1); }
            for (int i = 0; i < categories.Count; i++) categories[i].EnableInClassList("is-selected", (int)category == i);
            if (cardTemplate == null) { empty.text = "Không tìm thấy mẫu ô vật phẩm."; Show(empty, true); return; }
            while (cards.Count < filtered.Count) { var card = new ShopProductCardView(cardTemplate, Select); cards.Add(card); grid.Add(card.Root); }
            for (int i = 0; i < cards.Count; i++) cards[i].Render(i < filtered.Count ? filtered[i] : null, selected?.Id);
            SizeCards(productScroll.contentViewport.contentRect.width);
            Show(empty, filtered.Count == 0);
            empty.text = key.Length > 0 ? "Không tìm thấy vật phẩm phù hợp trong danh mục này." : "Hiện chưa có vật phẩm.";
            RenderDetail();
        }
        int Compare(ShopOffer a, ShopOffer b, ShopSort sort) {
            int result;
            switch (sort) {
                case ShopSort.Name: result = string.Compare(searchNames[a.Id], searchNames[b.Id], StringComparison.Ordinal); break;
                case ShopSort.PriceAscending: result = (a.GoldPrice < 0 ? int.MaxValue : a.GoldPrice).CompareTo(b.GoldPrice < 0 ? int.MaxValue : b.GoldPrice); break;
                case ShopSort.PriceDescending: result = b.GoldPrice.CompareTo(a.GoldPrice); break;
                default: result = b.Item.Rarity.CompareTo(a.Item.Rarity); break;
            }
            return result != 0 ? result : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        }
        void Select(ShopOffer offer) {
            if (!PlayerActionGate.TryAccept(this, "shop-select")) return;
            selected = offer; quantity.SetValueWithoutNotify(1); feedback.text = ""; ApplyQuery();
        }
        void RenderDetail() {
            bool hasItem = selected?.Item != null; Show(detail, hasItem); Show(purchase, hasItem); Show(detailEmpty, !hasItem);
            if (!hasItem) { detailEmpty.text = "Chọn vật phẩm để xem thông tin."; return; }
            var item = selected.Item;
            portrait.sprite = item.Icon; Show(portrait.parent, item.Icon != null);
            if (item.Icon != null) {
                // Keep existing low resolution sprites at their native design size.
                portrait.style.width = Mathf.Min(220, item.Icon.rect.width);
                portrait.style.height = Mathf.Min(220, item.Icon.rect.height);
            }
            itemName.text = item.Name; owned.text = "Đang có: " + service.Owned(selected);
            badge.text = ShopPresentation.CategoryNames[(int)category]; rarity.text = VietnameseUI.Rarity(item.Rarity.ToString());
            description.text = VietnameseUI.ItemText(item.Description); Show(description, !string.IsNullOrWhiteSpace(item.Description));
            effect.text = VietnameseUI.ItemText(item.Effect); Show(effect.parent, !string.IsNullOrWhiteSpace(item.Effect));
            stock.text = selected.Stock >= 0 ? "Còn trong cửa hàng: " + selected.Stock : ""; Show(stock, selected.Stock >= 0);
            selectedPrice = selected.GoldPrice; SetQuantity(quantity.value);
        }
        void SetQuantity(int requested) {
            if (selected == null) return;
            int limit = service.Maximum(selected, out string reason);
            int value = Mathf.Clamp(requested, 1, Math.Max(1, limit)); quantity.SetValueWithoutNotify(value);
            quantity.SetEnabled(limit > 0); minus.SetEnabled(limit > 0 && value > 1); plus.SetEnabled(limit > value); max.SetEnabled(limit > value);
            total.text = selected.GoldPrice < 0 ? "—" : ((long)value * selected.GoldPrice).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " vàng";
            reasonLabel.text = reason;
            buy.SetEnabled(limit > 0 && !buying); buy.tooltip = reason.Length > 0 ? reason : "Mua và đưa vật phẩm vào Túi đồ.";
        }
        void Buy() {
            if (selected == null || buying) return;
            buying = true; buy.SetEnabled(false);
            try {
                bool success = service.TryBuy(source, selected.Id, quantity.value, selectedPrice, out string message);
                if (message.Length > 0) { feedback.text = message; feedback.EnableInClassList("is-error", !success); }
            } finally { buying = false; Refresh(); }
        }
        void LayoutGrid(GeometryChangedEvent evt) {
            SizeCards(productScroll.contentViewport.contentRect.width);
        }
        void SizeCards(float available) {
            if (!float.IsFinite(available) || available <= 0) return;
            grid.style.width = available;
            // Yoga rounds layout to physical pixels. Reserve a pixel per cell so
            // the fifth column cannot wrap because of cumulative rounding.
            float width = Mathf.Max(1, Mathf.Floor(available / 5f) - 1);
            foreach (var card in cards) card.Root.style.width = width;
        }
        static void Show(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
    }
}

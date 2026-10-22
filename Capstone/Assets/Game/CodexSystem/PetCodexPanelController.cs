using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.CodexSystem {
    [DisallowMultipleComponent]
    public sealed class PetCodexPanelController : MonoBehaviour {
        [SerializeField] MonoBehaviour providerSource;
        readonly PetCodexQuery query = new PetCodexQuery();
        readonly List<PetCodexEntryViewData> filtered = new List<PetCodexEntryViewData>();
        readonly List<Action> unbind = new List<Action>();
        readonly List<Button> cards = new List<Button>();
        readonly List<Button> categories = new List<Button>();
        readonly List<Button> pageButtons = new List<Button>();
        readonly List<Button> sorts = new List<Button>();
        sealed class CardView {
            public Label Number, Type;
            public VisualElement Favorite, Unknown, Portrait;
        }
        sealed class EvolutionView {
            public VisualElement Root, Icon;
            public Label Unknown, Name, Requirement;
        }
        readonly List<CardView> cardViews = new List<CardView>();
        readonly List<EvolutionView> evolutionViews = new List<EvolutionView>();
        readonly Dictionary<string, Label> detailLabels = new Dictionary<string, Label>();
        readonly List<Label> badges = new List<Label>();
        readonly List<int> paginationPages = new List<int>(5);
        readonly List<int> paginationSlots = new List<int>(7);
        VisualElement detailPortrait, evolution;
        Label evolutionUnknown;
        bool detailsDirty = true, changingFavorite;
        static readonly CodexType?[] Types = { null, CodexType.Fire, CodexType.Water, CodexType.Nature,
            CodexType.Electric, CodexType.Ice, CodexType.Earth, CodexType.Wind, CodexType.Dark, CodexType.Light, CodexType.Mythic, CodexType.Dragon };
        static readonly string[] SortNames = { "Theo số thứ tự", "Theo hệ", "Theo khu vực", "Độ hiếm", "Theo tên" };
        static readonly string[] DiscoveryNames = { "Mọi trạng thái", "Chưa khám phá", "Đã gặp", "Đã ghi nhận", "Đã thu phục" };
        IPetCodexProvider provider;
        VisualElement root, grid, details, filterPanel;
        TextField search;
        Label empty, detailEmpty, progressNumber;
        ProgressBar progressBar;
        DropdownField region, rarity, discovery, extraSort;
        Button favorite;
        int page;
        string selectedId;
        bool open;
        bool subscribed;
        public event Action BackRequested;
        public bool IsOpen => open;
        public int VisibleCount => Mathf.Min(PetCodexQuery.PageSize, Mathf.Max(0, filtered.Count - page * PetCodexQuery.PageSize));
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)PetCodexQuery.PageSize));

        public void Bind(UIDocument document) => Bind(document?.rootVisualElement);
        public void Bind(VisualElement documentRoot) {
            UnbindView();
            root = documentRoot?.Q("codex-panel");
            if (root == null) return;
            grid = root.Q("codex-grid"); details = root.Q("codex-details"); filterPanel = root.Q("codex-filters");
            search = root.Q<TextField>("codex-search"); empty = root.Q<Label>("codex-empty");
            detailEmpty = root.Q<Label>("codex-detail-empty"); progressNumber = root.Q<Label>("codex-progress-number");
            progressBar = root.Q<ProgressBar>("codex-progress-bar");
            favorite = root.Q<Button>("codex-favorite");
            region = root.Q<DropdownField>("codex-region"); rarity = root.Q<DropdownField>("codex-rarity");
            discovery = root.Q<DropdownField>("codex-discovery"); extraSort = root.Q<DropdownField>("codex-extra-sort");
            cards.AddRange(grid.Query<Button>(className: "codex-card").ToList());
            categories.AddRange(root.Query<Button>(className: "codex-category").ToList());
            pageButtons.AddRange(root.Query<Button>(className: "codex-page-button").ToList());
            sorts.AddRange(root.Query<Button>(className: "codex-sort").ToList());
            foreach (var card in cards) cardViews.Add(new CardView {
                Number = card.Q<Label>("number"), Type = card.Q<Label>("type"), Favorite = card.Q("favorite"),
                Unknown = card.Q("unknown"), Portrait = card.Q("portrait")
            });
            badges.AddRange(root.Query<Label>(className: "codex-type-badge").ToList());
            root.Query<Label>().ForEach(label => { if (label.name.StartsWith("codex-")) detailLabels[label.name] = label; });
            detailPortrait = root.Q("codex-detail-portrait"); evolution = root.Q("codex-evolution");
            evolutionUnknown = new Label(VietnameseUI.Unknown); evolution.Add(evolutionUnknown);
            for (int i = 0; i < cards.Count; i++) { Button card = cards[i]; Click(card, () => Select(card.userData as string)); }
            for (int i = 0; i < categories.Count; i++) {
                int index = i;
                categories[i].Q<Label>("category-name").text = Types[i].HasValue ? PetCodexQuery.TypeName(Types[i].Value) : "Tất cả";
                Click(categories[i], () => { query.Type = Types[index]; ApplyQuery(); });
            }
            for (int i = 0; i < sorts.Count; i++) { int index = i; Click(sorts[i], () => { query.Sort = (CodexSort)index; ApplyQuery(); }); }
            for (int i = 0; i < pageButtons.Count; i++) { Button button = pageButtons[i]; Click(button, () => { if (button.userData is int target) { page = target; RenderPage(); } }); }
            Click(root.Q<Button>("codex-back"), () => BackRequested?.Invoke());
            Click(root.Q<Button>("codex-clear"), () => { search.value = string.Empty; search.Focus(); });
            Click(root.Q<Button>("codex-filter-toggle"), () => { bool visible = filterPanel.resolvedStyle.display == DisplayStyle.None; Show(filterPanel, visible); });
            Click(root.Q<Button>("codex-reset"), ResetFilters);
            Click(favorite, ToggleFavorite);
            Change(search, evt => { query.Search = evt.newValue; ApplyQuery(); });
            region.choices = new List<string> { "Mọi khu vực" }; region.SetValueWithoutNotify(region.choices[0]);
            rarity.choices = new List<string> { "Mọi độ hiếm", "Thường", "Ít gặp", "Hiếm", "Sử thi", "Huyền thoại" }; rarity.SetValueWithoutNotify(rarity.choices[0]);
            discovery.choices = new List<string>(DiscoveryNames); discovery.SetValueWithoutNotify(DiscoveryNames[0]);
            extraSort.choices = new List<string>(SortNames); extraSort.SetValueWithoutNotify(SortNames[0]);
            // Binding must preserve the visible controls and the query as one state.
            search.SetValueWithoutNotify(query.Search);
            rarity.SetValueWithoutNotify(rarity.choices[query.Rarity.HasValue ? (int)query.Rarity.Value : 0]);
            discovery.SetValueWithoutNotify(DiscoveryNames[query.Discovery.HasValue ? (int)query.Discovery.Value + 1 : 0]);
            extraSort.SetValueWithoutNotify(SortNames[(int)query.Sort]);
            Change(region, evt => { query.Region = region.index > 0 ? evt.newValue : null; ApplyQuery(); });
            Change(rarity, _ => { query.Rarity = rarity.index > 0 ? (PetRarity?)rarity.index : null; ApplyQuery(); });
            Change(discovery, _ => { query.Discovery = discovery.index > 0 ? (CodexDiscovery?)(discovery.index - 1) : null; ApplyQuery(); });
            Change(extraSort, _ => { query.Sort = (CodexSort)Mathf.Max(0, extraSort.index); ApplyQuery(); });
            SetProvider(provider ?? providerSource as IPetCodexProvider);
            if (!open) Refresh();
        }

        public void SetProvider(IPetCodexProvider value) {
            Unsubscribe(); provider = value;
            if (open && isActiveAndEnabled) Subscribe();
            if (open) Refresh();
        }
        public void Open() { open = true; Subscribe(); Show(root, true); Refresh(); root?.Q<Button>("codex-back")?.Focus(); }
        public void Close() { open = false; Unsubscribe(); Show(root, false); }
        public bool TryCloseFilters() {
            if (filterPanel == null || filterPanel.resolvedStyle.display == DisplayStyle.None) return false;
            Show(filterPanel, false); root.Q<Button>("codex-filter-toggle")?.Focus(); return true;
        }
        void OnEnable() { if (open) Subscribe(); }
        void OnDisable() { Unsubscribe(); Close(); }
        void OnDestroy() { Unsubscribe(); UnbindView(); }
        void Subscribe() { if (provider == null || subscribed) return; provider.Changed += OnProviderChanged; subscribed = true; }
        void Unsubscribe() { if (provider != null && subscribed) provider.Changed -= OnProviderChanged; subscribed = false; }
        void OnProviderChanged() { detailsDirty = true; if (open && !changingFavorite) Refresh(); }
        void UnbindView() {
            foreach (var callback in unbind) callback();
            unbind.Clear(); cards.Clear(); categories.Clear(); pageButtons.Clear(); sorts.Clear();
            cardViews.Clear(); badges.Clear(); detailLabels.Clear(); evolutionViews.Clear();
            evolution?.Clear(); evolution = null; detailsDirty = true;
        }
        void Click(Button button, Action action) {
            if (button == null) return;
            Action guarded = PlayerActionGate.Guard(this, "codex:" + (button.name.Length > 0 ? button.name : unbind.Count.ToString()), action);
            button.clicked += guarded; unbind.Add(() => button.clicked -= guarded);
        }
        void Change(BaseField<string> field, EventCallback<ChangeEvent<string>> callback) {
            field.RegisterValueChangedCallback(callback); unbind.Add(() => field.UnregisterValueChangedCallback(callback));
        }

        public void Refresh() {
            if (root == null) return;
            detailsDirty = true;
            var entries = provider?.Entries;
            bool available = entries != null && entries.Count > 0;
            search.SetEnabled(available); filterPanel.SetEnabled(available);
            root.Q<Button>("codex-filter-toggle").SetEnabled(available);
            root.Q<Button>("codex-clear").SetEnabled(available);
            string unavailable = "Sẽ khả dụng khi có thông tin cẩm nang.";
            search.tooltip = available ? "Tìm tên hoặc số thứ tự; có thể nhập không dấu." : unavailable;
            root.Q<Button>("codex-filter-toggle").tooltip = available ? "Lọc khu vực, độ hiếm và trạng thái." : unavailable;
            int known = 0;
            var regions = new List<string> { "Mọi khu vực" };
            if (entries != null) foreach (var entry in entries) {
                if (entry == null || !entry.Known) continue;
                known++;
                if (!string.IsNullOrWhiteSpace(entry.Region) && !regions.Contains(entry.Region)) regions.Add(entry.Region);
            }
            region.choices = regions;
            if (query.Region != null && !regions.Contains(query.Region)) query.Region = null;
            region.SetValueWithoutNotify(query.Region ?? regions[0]);
            progressNumber.text = available ? $"{known} / {entries.Count}" : "—";
            progressBar.value = available ? 100f * known / entries.Count : 0;
            progressBar.title = available ? Mathf.RoundToInt(progressBar.value) + "% • Đã khám phá" : "Chưa có thông tin";
            for (int i = 0; i < categories.Count; i++) {
                int count = 0;
                if (entries != null) foreach (var e in entries) if (e != null && (!Types[i].HasValue || (e.Known && (e.PrimaryType == Types[i] || e.SecondaryType == Types[i])))) count++;
                categories[i].Q<Label>("category-count").text = available ? count.ToString() : "—";
                categories[i].SetEnabled(available); categories[i].tooltip = available ? "Lọc danh sách sinh vật." : unavailable;
            }
            foreach (var sort in sorts) sort.SetEnabled(available);
            query.Apply(entries, filtered);
            page = Mathf.Clamp(page, 0, PageCount - 1);
            ValidateSelection(); RenderPage();
        }

        void ApplyQuery() {
            page = 0; query.Apply(provider?.Entries, filtered); ValidateSelection(); RenderPage();
        }
        void ResetFilters() {
            query.Search = string.Empty; query.Type = null; query.Region = null; query.Rarity = null; query.Discovery = null; query.Sort = CodexSort.Number;
            search.SetValueWithoutNotify(string.Empty); region.SetValueWithoutNotify(region.choices[0]);
            rarity.SetValueWithoutNotify(rarity.choices[0]); discovery.SetValueWithoutNotify(discovery.choices[0]);
            extraSort.SetValueWithoutNotify(SortNames[0]); ApplyQuery();
        }
        void ValidateSelection() {
            if (selectedId != null && !filtered.Exists(e => e.Id == selectedId)) { selectedId = null; detailsDirty = true; }
        }
        void RenderPage() {
            if (root == null) return;
            Show(empty, filtered.Count == 0); Show(grid, filtered.Count > 0);
            empty.text = provider?.Entries == null || provider.Entries.Count == 0
                ? "Cẩm nang chưa có thông tin sinh vật." : "Không tìm thấy sinh vật phù hợp. Hãy thử bộ lọc khác.";
            for (int i = 0; i < cards.Count; i++) {
                int index = page * PetCodexQuery.PageSize + i;
                var card = cards[i]; bool visible = index < filtered.Count;
                Show(card.parent, visible); if (!visible) { card.userData = null; continue; }
                var entry = filtered[index]; card.userData = entry.Id;
                card.EnableInClassList("is-selected", entry.Id == selectedId);
                var view = cardViews[i];
                view.Number.text = entry.Number.ToString("D3");
                view.Type.text = entry.Known ? PetCodexQuery.TypeName(entry.PrimaryType) : string.Empty;
                Show(view.Favorite, entry.Known && entry.Favorite);
                Show(view.Unknown, !entry.Known || entry.Icon == null);
                view.Portrait.style.backgroundImage = entry.Icon != null ? new StyleBackground(entry.Icon) : StyleKeyword.None;
                view.Portrait.style.unityBackgroundImageTintColor = entry.Known ? Color.white : new Color(0, 0, 0, .22f);
                card.tooltip = entry.Known ? entry.Name : "Chưa khám phá";
            }
            for (int i = 0; i < categories.Count; i++) categories[i].EnableInClassList("is-selected", Types[i] == query.Type);
            for (int i = 0; i < sorts.Count; i++) sorts[i].EnableInClassList("is-selected", i == (int)query.Sort);
            extraSort.SetValueWithoutNotify(SortNames[(int)query.Sort]);
            RenderPagination(); RenderDetails();
        }
        void RenderPagination() {
            int count = PageCount;
            var pages = paginationPages; pages.Clear(); pages.Add(0);
            for (int i = Mathf.Max(1, page - 1); i <= Mathf.Min(count - 2, page + 1); i++) pages.Add(i);
            if (count > 1) pages.Add(count - 1);
            var slots = paginationSlots; slots.Clear();
            for (int i = 0; i < pages.Count; i++) { if (i > 0 && pages[i] - pages[i - 1] > 1) slots.Add(-1); slots.Add(pages[i]); }
            for (int i = 0; i < pageButtons.Count; i++) {
                var button = pageButtons[i]; int target;
                if (i == 0) { target = page - 1; button.text = "‹"; }
                else if (i == pageButtons.Count - 1) { target = page + 1; button.text = "›"; }
                else { int slot = i - 1; Show(button.parent, slot < slots.Count); if (slot >= slots.Count) continue; target = slots[slot]; button.text = target >= 0 ? (target + 1).ToString() : "…"; }
                button.userData = target; button.SetEnabled(target >= 0 && target < count && filtered.Count > 0);
                button.EnableInClassList("is-selected", target == page);
                button.tooltip = target >= 0 ? "Trang " + (target + 1) : string.Empty;
            }
        }
        void Select(string id) { selectedId = id; detailsDirty = true; RenderPage(); }
        void ToggleFavorite() {
            var entry = filtered.Find(e => e.Id == selectedId);
            if (entry == null || !entry.Known || provider?.CanSetFavorite != true) return;
            bool changed;
            changingFavorite = true;
            try { changed = provider.TrySetFavorite(entry.Id, !entry.Favorite); }
            finally { changingFavorite = false; }
            if (changed) Refresh();
        }
        void RenderDetails() {
            if (!detailsDirty) return;
            detailsDirty = false;
            var entry = filtered.Find(e => e.Id == selectedId);
            Show(details, entry != null); Show(detailEmpty, entry == null);
            if (entry == null) return;
            LabelText("codex-number", "#" + entry.Number.ToString("D3"));
            LabelText("codex-name", entry.Known ? entry.Name : "Chưa khám phá");
            LabelText("codex-description", entry.Known ? Value(entry.Description) : "Khám phá sinh vật này để mở thông tin cẩm nang.");
            Sprite sprite = entry.Portrait != null ? entry.Portrait : entry.Icon;
            detailPortrait.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
            detailPortrait.style.unityBackgroundImageTintColor = entry.Known ? Color.white : new Color(0, 0, 0, .22f);
            for (int i = 0; i < badges.Count; i++) { var type = i == 0 ? entry.PrimaryType : entry.SecondaryType; badges[i].text = PetCodexQuery.TypeName(type); Show(badges[i].parent, entry.Known && type != CodexType.Unknown); }
            LabelText("codex-basic", entry.Known ? $"Chiều cao: {Value(entry.Height)}\nCân nặng: {Value(entry.Weight)}\nKhu vực sinh sống: {Value(entry.Habitat)}\nĐặc tính: {Value(entry.Trait)}\nThức ăn ưa thích: {Value(entry.FavoriteFood)}\nĐộ hiếm: {VietnameseUI.Rarity(entry.Rarity)}" : VietnameseUI.Unknown);
            favorite.text = entry.Known && entry.Favorite ? "★  Đã yêu thích" : "Đánh dấu yêu thích";
            favorite.SetEnabled(entry.Known && provider?.CanSetFavorite == true);
            favorite.tooltip = provider?.CanSetFavorite == true ? "Đánh dấu loài trong cẩm nang." : "Đánh dấu sẽ khả dụng khi dữ liệu cẩm nang được kết nối.";
            foreach (var view in evolutionViews) Show(view.Root, false);
            bool hasEvolution = entry.Known && entry.Evolution != null && entry.Evolution.Count > 0;
            Show(evolutionUnknown, !hasEvolution);
            if (!hasEvolution) return;
            int used = 0;
            foreach (var node in entry.Evolution) {
                if (node == null) continue;
                if (used == evolutionViews.Count) {
                    var view = new EvolutionView { Root = new VisualElement(), Icon = new VisualElement(),
                        Unknown = new Label("?"), Name = new Label(), Requirement = new Label() };
                    view.Root.AddToClassList("codex-evolution-node"); view.Icon.AddToClassList("codex-evolution-icon");
                    view.Icon.Add(view.Unknown); view.Root.Add(view.Icon); view.Root.Add(view.Name); view.Root.Add(view.Requirement);
                    evolution.Add(view.Root); evolutionViews.Add(view);
                }
                var item = evolutionViews[used++]; Show(item.Root, true);
                item.Icon.style.backgroundImage = node.Known && node.Icon != null ? new StyleBackground(node.Icon) : StyleKeyword.None;
                Show(item.Unknown, !node.Known || node.Icon == null);
                item.Name.text = node.Known ? Value(node.Name) : "?";
                item.Requirement.text = node.Known ? Value(node.Requirement) : VietnameseUI.Unknown;
            }
        }
        void LabelText(string name, string text) { if (detailLabels.TryGetValue(name, out var label)) label.text = text; }
        static string Value(string value) => string.IsNullOrWhiteSpace(value) ? VietnameseUI.Unknown : value;
        static void Show(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
    }
}

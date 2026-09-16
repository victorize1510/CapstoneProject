using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    public enum PetBoxReturnTarget {
        Menu,
        Pets,
        Gameplay
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetBoxPanelController : MonoBehaviour {
        const int PartySize = 6;
        const int SlotsPerPage = 30;
        const int StorageColumns = 6;
        const float StorageCardOuterWidth = 148f;
        const float StorageCardOuterHeight = 112f;
        const float StorageGridPreferredWidth = StorageColumns * StorageCardOuterWidth;
        const float DoubleClickInterval = 0.45f;

        static readonly string[] SortChoices = {
            "Vị trí Box",
            "Mới nhận",
            "Tên A → Z",
            "Tên Z → A",
            "Level cao → thấp",
            "Level thấp → cao",
            "Rarity cao → thấp",
            "Rarity thấp → cao"
        };

        [SerializeField] UIDocument document = null;
        [SerializeField] PetBoxRuntimeProvider provider = null;
        [SerializeField] PetCaptureCoordinator captureCoordinator = null;
        [SerializeField] bool autoFindOrCreateProvider = true;

        readonly List<Button> partyCards = new List<Button>(6);
        readonly List<Button> storageCards = new List<Button>();
        readonly Dictionary<Button, Action> buttonCallbacks = new Dictionary<Button, Action>();
        readonly Dictionary<PetElement, Button> elementButtons = new Dictionary<PetElement, Button>();
        readonly HashSet<PetElement> selectedElements = new HashSet<PetElement>();
        readonly List<BoxViewEntry> visibleStorage = new List<BoxViewEntry>();
        readonly List<Button> sortOptionButtons = new List<Button>();

        MonsterInventoryController inventoryController;
        GameMenuController gameMenuController;
        VisualElement root;
        VisualElement panel;
        VisualElement partyRow;
        VisualElement storageGrid;
        ScrollView storageScroll;
        VisualElement filterPanel;
        VisualElement sortPopup;
        ScrollView sortScroll;
        VisualElement purchasePopup;
        VisualElement selectedPortrait;
        VisualElement selectedHealthFill;
        VisualElement radarHost;
        Label partyCountLabel;
        Label capacityLabel;
        Label pageTitle;
        Label pageCount;
        Label emptyLabel;
        Label selectedPortraitFallback;
        Label selectedFavorite;
        Label selectedName;
        Label selectedLocation;
        Label selectedLevel;
        Label selectedElement;
        Label selectedGender;
        Label selectedRarity;
        Label selectedHealthValue;
        Label selectedPersonality;
        Label selectedNatureUp;
        Label selectedNatureDown;
        Label feedback;
        Label footerSummary;
        Label sourceLabel;
        Label purchaseTitle;
        Label purchaseCurrent;
        Label purchaseExpand;
        Label purchasePrice;
        Label purchaseAfter;
        Button replaceButton;
        Button addButton;
        Button moveButton;
        Button detailsButton;
        Button allFilterButton;
        Button purchaseConfirmButton;
        Button previousPageButton;
        Button nextPageButton;
        Button sortButton;
        TextField searchField;

        PetBoxReturnTarget returnTarget = PetBoxReturnTarget.Menu;
        SelectionSource selectionSource;
        PetController selectedPet;
        int targetPartySlot = -1;
        SelectionSource pendingSwapSource;
        int pendingSwapSourceIndex = -1;
        SelectionSource lastClickSource;
        int lastClickIndex = -1;
        float lastClickTime = float.NegativeInfinity;
        int currentPage;
        int sortIndex;
        bool controlsRegistered;
        bool subscribed;
        VisualElement registeredStorageViewport;
        RadarChartElement radarChart;
        Label[] radarLabels;

        public event Action<PetController> DetailsRequested;

        enum SelectionSource {
            None,
            Party,
            Storage
        }

        struct BoxViewEntry {
            public int providerIndex;
            public PetSnapshot snapshot;
        }

        struct PetSnapshot {
            public PetController pet;
            public string displayName;
            public string species;
            public string gender;
            public PetElement element;
            public PetRarity rarity;
            public int level;
            public long obtainedOrder;
            public bool favorite;
            public Sprite icon;
            public float health;
            public float maxHealth;
            public int attack;
            public int defense;
            public int speed;
            public int magicAttack;
            public int magicDefense;
            public string personality;
        }

        sealed class RadarChartElement : VisualElement {
            const int AxisCount = 6;
            readonly float[] values = new float[AxisCount];

            public RadarChartElement() {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetValues(float hp, float attack, float defense, float speed, float magicDefense, float magicAttack) {
                float maximum = Mathf.Max(1f, hp, attack, defense, speed, magicDefense, magicAttack);
                values[0] = Normalize(hp, maximum);
                values[1] = Normalize(attack, maximum);
                values[2] = Normalize(defense, maximum);
                values[3] = Normalize(speed, maximum);
                values[4] = Normalize(magicDefense, maximum);
                values[5] = Normalize(magicAttack, maximum);
                MarkDirtyRepaint();
            }

            static float Normalize(float value, float maximum) {
                if (value <= 0f) return 0f;
                return Mathf.Lerp(0.14f, 1f, Mathf.Clamp01(value / maximum));
            }

            void Draw(MeshGenerationContext context) {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f) return;

                Vector2 center = rect.center;
                float radius = Mathf.Max(4f, Mathf.Min(rect.width, rect.height) * 0.39f);
                Painter2D painter = context.painter2D;

                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.45f, 0.72f, 0.80f, 0.52f);
                for (int ring = 1; ring <= 3; ring++) {
                    DrawPolygon(painter, center, radius * ring / 3f, null);
                }

                painter.strokeColor = new Color(0.49f, 0.72f, 0.78f, 0.48f);
                for (int i = 0; i < AxisCount; i++) {
                    painter.BeginPath();
                    painter.MoveTo(center);
                    painter.LineTo(Point(center, radius, i));
                    painter.Stroke();
                }

                painter.fillColor = new Color(0.22f, 0.62f, 0.96f, 0.34f);
                painter.strokeColor = new Color(0.16f, 0.48f, 0.91f, 0.95f);
                painter.lineWidth = 2f;
                DrawPolygon(painter, center, radius, values);
            }

            static void DrawPolygon(Painter2D painter, Vector2 center, float radius, float[] scales) {
                painter.BeginPath();
                for (int i = 0; i < AxisCount; i++) {
                    float scale = scales != null ? scales[i] : 1f;
                    Vector2 point = Point(center, radius * scale, i);
                    if (i == 0) painter.MoveTo(point);
                    else painter.LineTo(point);
                }
                painter.ClosePath();
                if (scales != null) painter.Fill();
                painter.Stroke();
            }

            static Vector2 Point(Vector2 center, float radius, int index) {
                float angle = -Mathf.PI * 0.5f + index * Mathf.PI * 2f / AxisCount;
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            RegisterControls();
            SubscribeProvider();
            Refresh();
        }

        void OnDisable() {
            ClearSwapSelection();
            UnsubscribeProvider();
            UnregisterControls();
            UnregisterStorageLayoutCallback();
        }

        public void Bind(UIDocument targetDocument, MonsterInventoryController owner) {
            document = targetDocument != null ? targetDocument : document;
            inventoryController = owner != null ? owner : inventoryController;
            ResolveReferences();
            CacheElements();
            RegisterControls();
            SubscribeProvider();
            Refresh();
        }

        public void OpenFrom(PetBoxReturnTarget target) {
            ClearSwapSelection();
            returnTarget = target;
            if (sourceLabel != null) sourceLabel.text = target == PetBoxReturnTarget.Pets ? "Nguồn mở: TỪ PETS" : "Nguồn mở: TỪ MENU";
            Refresh();
        }

        public void CloseTransientUi() {
            HidePurchasePopup();
            ClearSwapSelection();
            if (filterPanel != null) filterPanel.style.display = DisplayStyle.None;
            if (sortPopup != null) sortPopup.style.display = DisplayStyle.None;
        }

        public bool TryCancelInteraction() {
            if (purchasePopup != null && purchasePopup.resolvedStyle.display != DisplayStyle.None) {
                HidePurchasePopup();
                return true;
            }
            if (pendingSwapSource != SelectionSource.None) {
                ClearSwapSelection();
                SetFeedback("Đã hủy thay thế.");
                return true;
            }
            if (filterPanel != null && filterPanel.resolvedStyle.display != DisplayStyle.None) {
                filterPanel.style.display = DisplayStyle.None;
                return true;
            }
            if (sortPopup != null && sortPopup.resolvedStyle.display != DisplayStyle.None) {
                sortPopup.style.display = DisplayStyle.None;
                return true;
            }
            return false;
        }

        public void Refresh() {
            ResolveReferences();
            if (panel == null) CacheElements();
            if (panel == null || provider == null) return;

            EnsureValidSelection();
            RebuildParty();
            RebuildStorage();
            RefreshSelectedDetails();
            RefreshActions();
            RefreshFilterButtons();

            if (partyCountLabel != null) partyCountLabel.text = $"ĐỘI HÌNH ({provider.PartyCount} / 6)";
            if (capacityLabel != null) capacityLabel.text = $"{provider.StoredCount} / {provider.Capacity}";
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            inventoryController = inventoryController != null ? inventoryController : GetComponent<MonsterInventoryController>();
            if (provider == null) provider = GetComponent<PetBoxRuntimeProvider>();
            if (provider == null && autoFindOrCreateProvider) provider = FindFirstObjectByType<PetBoxRuntimeProvider>();
            if (provider == null && autoFindOrCreateProvider && Application.isPlaying) provider = gameObject.AddComponent<PetBoxRuntimeProvider>();
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;
            VisualElement docRoot = document.rootVisualElement;
            root = docRoot.Q<VisualElement>("monster-inventory-root");
            panel = docRoot.Q<VisualElement>("box-panel");
            partyRow = docRoot.Q<VisualElement>("box-party-row");
            storageGrid = docRoot.Q<VisualElement>("box-storage-grid");
            storageScroll = docRoot.Q<ScrollView>("box-storage-scroll");
            ConfigureStorageScroll();
            filterPanel = docRoot.Q<VisualElement>("box-filter-panel");
            sortPopup = docRoot.Q<VisualElement>("box-sort-popup");
            sortScroll = docRoot.Q<ScrollView>("box-sort-scroll");
            ConfigureSortScroll();
            purchasePopup = docRoot.Q<VisualElement>("box-purchase-popup");
            selectedPortrait = docRoot.Q<VisualElement>("box-selected-portrait");
            selectedHealthFill = docRoot.Q<VisualElement>("box-selected-health-fill");
            radarHost = docRoot.Q<VisualElement>("box-radar-host");
            partyCountLabel = docRoot.Q<Label>("box-party-count");
            capacityLabel = docRoot.Q<Label>("box-capacity-label");
            pageTitle = docRoot.Q<Label>("box-page-title");
            pageCount = docRoot.Q<Label>("box-page-count");
            emptyLabel = docRoot.Q<Label>("box-empty-label");
            selectedPortraitFallback = docRoot.Q<Label>("box-selected-portrait-fallback");
            selectedFavorite = docRoot.Q<Label>("box-selected-favorite");
            selectedName = docRoot.Q<Label>("box-selected-name");
            selectedLocation = docRoot.Q<Label>("box-selected-location");
            selectedLevel = docRoot.Q<Label>("box-selected-level");
            selectedElement = docRoot.Q<Label>("box-selected-element");
            selectedGender = docRoot.Q<Label>("box-selected-gender");
            selectedRarity = docRoot.Q<Label>("box-selected-rarity");
            selectedHealthValue = docRoot.Q<Label>("box-selected-health-value");
            selectedPersonality = docRoot.Q<Label>("box-selected-personality");
            selectedNatureUp = docRoot.Q<Label>("box-selected-nature-up");
            selectedNatureDown = docRoot.Q<Label>("box-selected-nature-down");
            feedback = docRoot.Q<Label>("box-feedback");
            footerSummary = docRoot.Q<Label>("box-footer-summary");
            sourceLabel = docRoot.Q<Label>("box-source-label");
            purchaseTitle = docRoot.Q<Label>("box-purchase-title");
            purchaseCurrent = docRoot.Q<Label>("box-purchase-current");
            purchaseExpand = docRoot.Q<Label>("box-purchase-expand");
            purchasePrice = docRoot.Q<Label>("box-purchase-price");
            purchaseAfter = docRoot.Q<Label>("box-purchase-after");
            purchaseConfirmButton = docRoot.Q<Button>("box-purchase-confirm");
            previousPageButton = docRoot.Q<Button>("box-page-previous");
            nextPageButton = docRoot.Q<Button>("box-page-next");
            replaceButton = docRoot.Q<Button>("box-replace-button");
            addButton = docRoot.Q<Button>("box-add-button");
            moveButton = docRoot.Q<Button>("box-move-button");
            detailsButton = docRoot.Q<Button>("box-details-button");
            allFilterButton = docRoot.Q<Button>("box-filter-all");
            searchField = docRoot.Q<TextField>("box-search-field");
            sortButton = docRoot.Q<Button>("box-sort-button");
            EnsureRadarChart(docRoot);
            RegisterStorageLayoutCallback();
        }

        void EnsureRadarChart(VisualElement docRoot) {
            if (radarHost != null && (radarChart == null || radarChart.parent != radarHost)) {
                radarChart?.RemoveFromHierarchy();
                radarChart = new RadarChartElement { name = "box-radar-chart" };
                radarChart.AddToClassList("box-radar-chart");
                radarHost.Add(radarChart);
            }
            radarLabels = new[] {
                docRoot.Q<Label>(className: "box-radar-hp"),
                docRoot.Q<Label>(className: "box-radar-atk"),
                docRoot.Q<Label>(className: "box-radar-def"),
                docRoot.Q<Label>(className: "box-radar-spd"),
                docRoot.Q<Label>(className: "box-radar-spdef"),
                docRoot.Q<Label>(className: "box-radar-spatk")
            };
        }

        void RegisterStorageLayoutCallback() {
            VisualElement viewport = storageScroll?.contentViewport;
            if (registeredStorageViewport == viewport) return;
            UnregisterStorageLayoutCallback();
            registeredStorageViewport = viewport;
            registeredStorageViewport?.RegisterCallback<GeometryChangedEvent>(HandleStorageViewportGeometryChanged);
            ApplyStorageGridWidth(viewport != null ? viewport.resolvedStyle.width : 0f);
        }

        void ConfigureStorageScroll() {
            if (storageScroll == null) return;

            storageScroll.mode = ScrollViewMode.Vertical;
            storageScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            storageScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        }

        void ConfigureSortScroll() {
            if (sortScroll == null) return;

            sortScroll.mode = ScrollViewMode.Vertical;
            sortScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sortScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        }

        void UnregisterStorageLayoutCallback() {
            registeredStorageViewport?.UnregisterCallback<GeometryChangedEvent>(HandleStorageViewportGeometryChanged);
            registeredStorageViewport = null;
        }

        void HandleStorageViewportGeometryChanged(GeometryChangedEvent evt) {
            ApplyStorageGridWidth(evt.newRect.width);
        }

        void ApplyStorageGridWidth(float viewportWidth) {
            if (storageGrid == null) return;

            bool hasViewportWidth = !float.IsNaN(viewportWidth)
                && !float.IsInfinity(viewportWidth)
                && viewportWidth > 1f;
            float resolvedWidth = hasViewportWidth
                ? Mathf.Min(StorageGridPreferredWidth, Mathf.Floor(viewportWidth))
                : StorageGridPreferredWidth;
            int columnCount = Mathf.Max(1, Mathf.FloorToInt(resolvedWidth / StorageCardOuterWidth));
            int rowCount = Mathf.CeilToInt(SlotsPerPage / (float)columnCount);
            float resolvedHeight = rowCount * StorageCardOuterHeight;

            storageGrid.style.alignSelf = Align.Center;
            storageGrid.style.width = resolvedWidth;
            storageGrid.style.minWidth = resolvedWidth;
            storageGrid.style.height = resolvedHeight;
            storageGrid.style.minHeight = resolvedHeight;
            if (storageScroll != null) {
                storageScroll.contentContainer.style.alignItems = Align.Center;
                storageScroll.contentContainer.style.width = Length.Percent(100f);
                storageScroll.contentContainer.style.minWidth = Length.Percent(100f);
            }
        }

        void RegisterControls() {
            if (controlsRegistered || root == null) return;

            RegisterButton(document.rootVisualElement.Q<Button>("box-back-button"), GoBack);
            RegisterButton(document.rootVisualElement.Q<Button>("box-expand-button"), () => ShowPurchasePopup(false));
            RegisterButton(document.rootVisualElement.Q<Button>("box-filter-toggle"), ToggleFilterPanel);
            RegisterButton(sortButton, ToggleSortPopup);
            RegisterButton(previousPageButton, () => ChangePage(-1));
            RegisterButton(nextPageButton, () => ChangePage(1));
            RegisterButton(document.rootVisualElement.Q<Button>("box-purchase-cancel"), HidePurchasePopup);
            RegisterButton(purchaseConfirmButton, RequestPurchase);
            RegisterButton(replaceButton, ReplaceSelectedPet);
            RegisterButton(addButton, AddSelectedPetToParty);
            RegisterButton(moveButton, MoveSelectedPetToBox);
            RegisterButton(detailsButton, RequestDetails);
            RegisterButton(allFilterButton, SelectAllElements);
            RegisterElementButton("box-filter-nature", PetElement.Nature);
            RegisterElementButton("box-filter-fire", PetElement.Fire);
            RegisterElementButton("box-filter-water", PetElement.Water);
            RegisterElementButton("box-filter-wind", PetElement.Wind);
            RegisterElementButton("box-filter-earth", PetElement.Earth);
            RegisterElementButton("box-filter-electric", PetElement.Electric);
            RegisterElementButton("box-filter-ice", PetElement.Ice);
            RegisterElementButton("box-filter-light", PetElement.Light);
            RegisterElementButton("box-filter-dark", PetElement.Dark);

            BuildSortOptions();
            searchField?.RegisterValueChangedCallback(HandleSearchChanged);
            controlsRegistered = true;
        }

        void UnregisterControls() {
            foreach (KeyValuePair<Button, Action> pair in buttonCallbacks) pair.Key.clicked -= pair.Value;
            buttonCallbacks.Clear();
            sortOptionButtons.Clear();
            searchField?.UnregisterValueChangedCallback(HandleSearchChanged);
            controlsRegistered = false;
        }

        void SelectSortOption(int index) {
            sortIndex = Mathf.Clamp(index, 0, SortChoices.Length - 1);
            if (sortButton != null) sortButton.text = $"{SortChoices[sortIndex]}  ▾";
            for (int i = 0; i < sortOptionButtons.Count; i++) {
                sortOptionButtons[i].EnableInClassList("is-selected", i == sortIndex);
            }
            if (sortPopup != null) sortPopup.style.display = DisplayStyle.None;
            currentPage = 0;
            ClearSwapSelection();
            RebuildStorage();
            RefreshActions();
        }

        void BuildSortOptions() {
            if (sortScroll == null) return;

            sortScroll.contentContainer.Clear();
            sortOptionButtons.Clear();
            for (int i = 0; i < SortChoices.Length; i++) {
                int optionIndex = i;
                var option = new Button { text = SortChoices[i] };
                option.AddToClassList("box-sort-option");
                option.EnableInClassList("is-selected", i == sortIndex);
                RegisterButton(option, () => SelectSortOption(optionIndex));
                sortOptionButtons.Add(option);
                sortScroll.Add(option);
            }
            if (sortButton != null) sortButton.text = $"{SortChoices[sortIndex]}  ▾";
        }

        void HandleSearchChanged(ChangeEvent<string> _) {
            RebuildStorage();
        }

        void RegisterButton(Button button, Action callback) {
            if (button == null || callback == null || buttonCallbacks.ContainsKey(button)) return;
            button.clicked += callback;
            buttonCallbacks.Add(button, callback);
        }

        void RegisterElementButton(string name, PetElement element) {
            Button button = document?.rootVisualElement?.Q<Button>(name);
            if (button == null) return;
            elementButtons[element] = button;
            RegisterButton(button, () => ToggleElement(element));
        }

        void SubscribeProvider() {
            if (provider == null || subscribed) return;
            provider.Changed += HandleProviderChanged;
            provider.StorageFull += HandleStorageFull;
            subscribed = true;
        }

        void UnsubscribeProvider() {
            if (provider != null && subscribed) {
                provider.Changed -= HandleProviderChanged;
                provider.StorageFull -= HandleStorageFull;
            }
            subscribed = false;
        }

        void HandleProviderChanged() {
            Refresh();
        }

        void HandleStorageFull(PetController _) {
            if (inventoryController == null) inventoryController = GetComponent<MonsterInventoryController>();
            inventoryController?.OpenPetBoxPanel(false);
            ShowPurchasePopup(true);
        }

        void RebuildParty() {
            if (partyRow == null) return;
            partyRow.Clear();
            partyCards.Clear();

            for (int i = 0; i < PartySize; i++) {
                int slotIndex = i;
                PetSnapshot snapshot = BuildSnapshot(provider.GetPartyPet(i));
                var card = new Button {
                    name = $"box-party-slot-{i + 1}",
                    text = string.Empty,
                    userData = i,
                    tooltip = snapshot.pet != null ? snapshot.displayName : $"Slot Party {i + 1}"
                };
                card.AddToClassList("box-party-card");
                card.EnableInClassList("is-empty", snapshot.pet == null);
                card.EnableInClassList("is-selected", selectionSource == SelectionSource.Party && selectedPet == snapshot.pet && snapshot.pet != null);
                card.EnableInClassList("is-swap-source", pendingSwapSource == SelectionSource.Party && pendingSwapSourceIndex == i);
                card.RegisterCallback<ClickEvent>(
                    _ => HandleBoxCardClick(SelectionSource.Party, slotIndex),
                    TrickleDown.TrickleDown);

                card.Add(CreatePortrait(snapshot, "box-party-portrait"));
                card.Add(CreateLabel((i + 1).ToString(), "box-slot-number"));
                if (i == 0) card.Add(CreateLabel("LEAD", "box-lead-badge"));
                if (snapshot.pet != null && snapshot.favorite) card.Add(CreateLabel("\u2605", "box-favorite-badge"));
                if (snapshot.pet != null) card.Add(CreateLabel($"Lv. {Mathf.Max(1, snapshot.level)}", "box-party-level"));
                partyCards.Add(card);
                partyRow.Add(card);
            }
        }

        void RebuildStorage() {
            if (storageGrid == null || provider == null) return;
            ApplyStorageGridWidth(storageScroll?.contentViewport.resolvedStyle.width ?? 0f);
            storageGrid.Clear();
            storageCards.Clear();
            visibleStorage.Clear();

            List<BoxViewEntry> orderedSlots = BuildOrderedStorage();
            visibleStorage.AddRange(orderedSlots);
            int pageCountValue = Mathf.Max(1, Mathf.CeilToInt(provider.Capacity / (float)SlotsPerPage));
            currentPage = Mathf.Clamp(currentPage, 0, pageCountValue - 1);
            int start = currentPage * SlotsPerPage;
            string query = searchField != null ? searchField.value?.Trim() : string.Empty;
            int occupiedOnPage = 0;
            var pageEntries = new List<BoxViewEntry>(SlotsPerPage);
            for (int cellIndex = 0; cellIndex < SlotsPerPage; cellIndex++) {
                int viewIndex = start + cellIndex;
                BoxViewEntry local = viewIndex < visibleStorage.Count
                    ? visibleStorage[viewIndex]
                    : new BoxViewEntry { providerIndex = -1 };
                pageEntries.Add(local);
                bool locked = local.providerIndex < 0;
                bool empty = local.snapshot.pet == null;
                bool matches = empty || (MatchesSearch(local.snapshot, query) && MatchesElement(local.snapshot.element));
                bool selected = selectionSource == SelectionSource.Storage
                    && local.providerIndex >= 0 && selectedPet == local.snapshot.pet && local.snapshot.pet != null;
                if (!empty) occupiedOnPage++;
                var card = new Button {
                    text = string.Empty,
                    userData = local.providerIndex,
                    tooltip = !empty
                        ? local.snapshot.displayName
                        : locked ? "Slot chưa mở" : $"Box slot {local.providerIndex + 1}"
                };
                card.AddToClassList("box-storage-card");
                card.EnableInClassList("is-empty", empty && !locked);
                card.EnableInClassList("is-locked", locked);
                card.EnableInClassList("is-filtered-out", !matches && !selected);
                card.EnableInClassList("is-selected", selected);
                card.EnableInClassList("is-swap-source", pendingSwapSource == SelectionSource.Storage && pendingSwapSourceIndex == local.providerIndex);
                if (!locked) {
                    int targetIndex = local.providerIndex;
                    card.RegisterCallback<ClickEvent>(
                        _ => HandleBoxCardClick(SelectionSource.Storage, targetIndex),
                        TrickleDown.TrickleDown);
                }
                if (!empty) {
                    card.Add(CreatePortrait(local.snapshot, "box-storage-portrait"));
                    card.Add(CreateStorageCardInfo(local.snapshot));
                }
                if (local.snapshot.favorite) card.Add(CreateLabel("\u2605", "box-storage-favorite"));
                storageCards.Add(card);
                storageGrid.Add(card);
            }

            visibleStorage.Clear();
            visibleStorage.AddRange(pageEntries);
            if (emptyLabel != null) emptyLabel.style.display = DisplayStyle.None;
            if (pageTitle != null) pageTitle.text = $"BOX {currentPage + 1:00}";
            if (pageCount != null) pageCount.text = $"{occupiedOnPage} PET  •  TRANG {currentPage + 1} / {pageCountValue}";
            if (footerSummary != null) {
                footerSummary.text = $"Box {currentPage + 1:00} — {provider.StoredCount} / {provider.Capacity} pet • Trang {currentPage + 1} / {pageCountValue}";
            }
            if (previousPageButton != null) previousPageButton.SetEnabled(currentPage > 0);
            if (nextPageButton != null) nextPageButton.SetEnabled(currentPage + 1 < pageCountValue);
            ScrollSelectedStorageCardIntoView();
        }

        void ScrollSelectedStorageCardIntoView() {
            if (storageScroll == null || selectionSource != SelectionSource.Storage || selectedPet == null) return;
            int cardIndex = visibleStorage.FindIndex(entry => entry.snapshot.pet == selectedPet);
            if (cardIndex < 0 || cardIndex >= storageCards.Count) return;
            Button selectedCard = storageCards[cardIndex];
            storageScroll.schedule.Execute(() => {
                if (selectedCard.panel != null) storageScroll.ScrollTo(selectedCard);
            });
        }

        List<BoxViewEntry> BuildOrderedStorage() {
            var entries = new List<BoxViewEntry>(provider != null ? provider.Capacity : 0);
            if (provider == null) return entries;
            for (int i = 0; i < provider.Capacity; i++) {
                entries.Add(new BoxViewEntry { providerIndex = i, snapshot = BuildSnapshot(provider.GetStoredPet(i)) });
            }
            SortStorage(entries);
            return entries;
        }

        void SortStorage(List<BoxViewEntry> entries) {
            int index = sortIndex;
            if (index <= 0) return;
            var occupied = entries.Where(entry => entry.snapshot.pet != null).ToList();
            var empty = entries.Where(entry => entry.snapshot.pet == null).ToList();
            Comparison<BoxViewEntry> comparison;
            switch (index) {
                case 2: comparison = (a, b) => string.Compare(a.snapshot.displayName, b.snapshot.displayName, StringComparison.CurrentCultureIgnoreCase); break;
                case 3: comparison = (a, b) => string.Compare(b.snapshot.displayName, a.snapshot.displayName, StringComparison.CurrentCultureIgnoreCase); break;
                case 4: comparison = (a, b) => b.snapshot.level.CompareTo(a.snapshot.level); break;
                case 5: comparison = (a, b) => a.snapshot.level.CompareTo(b.snapshot.level); break;
                case 6: comparison = (a, b) => b.snapshot.rarity.CompareTo(a.snapshot.rarity); break;
                case 7: comparison = (a, b) => a.snapshot.rarity.CompareTo(b.snapshot.rarity); break;
                default: comparison = (a, b) => b.snapshot.obtainedOrder.CompareTo(a.snapshot.obtainedOrder); break;
            }
            occupied.Sort(comparison);
            entries.Clear();
            entries.AddRange(occupied);
            entries.AddRange(empty);
        }

        void SelectPartySlot(int slotIndex) {
            PetController pet = provider?.GetPartyPet(slotIndex);
            if (pet == null) {
                SetFeedback("Slot Party đang trống. Chọn hoặc double-click pet nguồn trước.");
                return;
            }
            selectionSource = SelectionSource.Party;
            selectedPet = pet;
            targetPartySlot = slotIndex;
            Refresh();
        }

        void SelectStoragePet(int providerIndex) {
            PetController pet = provider?.GetStoredPet(providerIndex);
            if (pet == null) {
                SetFeedback("Slot Box đang trống. Chọn hoặc double-click pet nguồn trước.");
                return;
            }
            selectionSource = SelectionSource.Storage;
            selectedPet = pet;
            targetPartySlot = -1;
            SetFeedback(string.Empty);
            Refresh();
        }

        void ReplaceSelectedPet() {
            if (provider == null || selectionSource == SelectionSource.None || selectedPet == null) {
                SetFeedback("Chọn một pet trước.");
                return;
            }
            int index = selectionSource == SelectionSource.Party
                ? IndexOfPartyPet(selectedPet)
                : IndexOfStoredPet(selectedPet);
            if (index < 0) {
                SetFeedback("Pet không còn ở vị trí đã chọn.");
                return;
            }
            pendingSwapSource = selectionSource;
            pendingSwapSourceIndex = index;
            UpdateSwapSelectionVisual();
            SetFeedback($"Đã chọn {FormatSource(selectionSource, index)}. Click ô đích để thay thế.");
        }

        void AddSelectedPetToParty() {
            if (provider == null || selectionSource != SelectionSource.Storage || selectedPet == null) return;
            int boxIndex = IndexOfStoredPet(selectedPet);
            if (provider.TryMoveBoxToNextEmptyPartySlot(boxIndex, out int slot, out string error)) {
                selectedPet = provider.GetPartyPet(slot);
                selectionSource = SelectionSource.Party;
                targetPartySlot = slot;
                SetFeedback($"Đã đưa pet vào slot {slot + 1}.");
            }
            else SetFeedback(error);
            Refresh();
        }

        void MoveSelectedPetToBox() {
            if (provider == null || selectionSource != SelectionSource.Party || selectedPet == null) return;
            int partyIndex = IndexOfPartyPet(selectedPet);
            if (provider.TryMovePartyToBox(partyIndex, out string error)) {
                selectionSource = SelectionSource.Storage;
                targetPartySlot = -1;
                int boxIndex = IndexOfStoredPet(selectedPet);
                FocusStoredPet(selectedPet);
                SetFeedback(boxIndex >= 0
                    ? $"Đã chuyển {BuildSnapshot(selectedPet).displayName} vào Box {boxIndex / SlotsPerPage + 1:00}, slot {boxIndex + 1:00}."
                    : "Đã chuyển pet vào Box.");
            }
            else SetFeedback(error);
            Refresh();
        }

        void FocusStoredPet(PetController pet) {
            if (provider == null || pet == null) return;
            List<BoxViewEntry> entries = BuildOrderedStorage();
            int viewIndex = entries.FindIndex(entry => entry.snapshot.pet == pet);
            if (viewIndex >= 0) currentPage = viewIndex / SlotsPerPage;
        }

        void RequestDetails() {
            if (selectedPet == null) return;
            DetailsRequested?.Invoke(selectedPet);
            inventoryController?.OpenPetDetailsPanel(selectedPet);
        }

        void RefreshSelectedDetails() {
            PetSnapshot snapshot = BuildSnapshot(selectedPet);
            SetSelectedPortrait(snapshot);
            if (selectedName != null) selectedName.text = snapshot.pet != null ? snapshot.displayName : "Chọn một pet";
            if (selectedLocation != null) {
                int index = snapshot.pet == null
                    ? -1
                    : selectionSource == SelectionSource.Party ? IndexOfPartyPet(snapshot.pet) : IndexOfStoredPet(snapshot.pet);
                selectedLocation.text = index < 0
                    ? "Chọn pet trong Party hoặc Box"
                    : selectionSource == SelectionSource.Party
                        ? $"Trong Party (Slot {index + 1})"
                        : $"Trong Box {index / SlotsPerPage + 1:00} (Ô {index % SlotsPerPage + 1})";
            }
            bool hasSelection = snapshot.pet != null;
            if (selectedLevel != null) selectedLevel.text = hasSelection ? $"Lv. {Mathf.Max(1, snapshot.level)}" : "—";
            if (selectedElement != null) selectedElement.text = hasSelection ? FormatElement(snapshot.element) : "—";
            if (selectedGender != null) selectedGender.text = hasSelection ? FormatGender(snapshot.gender) : "—";
            if (selectedRarity != null) selectedRarity.text = hasSelection ? FormatRarity(snapshot.rarity) : "—";
            if (selectedHealthValue != null) {
                selectedHealthValue.text = hasSelection && snapshot.maxHealth > 0f
                    ? $"{Mathf.RoundToInt(snapshot.health):N0} / {Mathf.RoundToInt(snapshot.maxHealth):N0}"
                    : "— / —";
            }
            SetFill(selectedHealthFill, hasSelection && snapshot.maxHealth > 0f
                ? snapshot.health / snapshot.maxHealth
                : 0f);
            if (selectedPersonality != null) {
                selectedPersonality.text = hasSelection && !string.IsNullOrWhiteSpace(snapshot.personality)
                    ? snapshot.personality.Trim()
                    : "Trung tính";
            }

            ResolveNatureEffect(snapshot.personality, out string increasedStat, out string decreasedStat);
            if (selectedNatureUp != null) {
                selectedNatureUp.text = hasSelection
                    ? increasedStat != null ? $"Tăng {increasedStat}" : "Không tăng chỉ số"
                    : "Chỉ số tăng: —";
            }
            if (selectedNatureDown != null) {
                selectedNatureDown.text = hasSelection
                    ? decreasedStat != null ? $"Giảm {decreasedStat}" : "Không giảm chỉ số"
                    : "Chỉ số giảm: —";
            }

            radarChart?.SetValues(
                hasSelection ? snapshot.maxHealth : 0f,
                hasSelection ? snapshot.attack : 0f,
                hasSelection ? snapshot.defense : 0f,
                hasSelection ? snapshot.speed : 0f,
                hasSelection ? snapshot.magicDefense : 0f,
                hasSelection ? snapshot.magicAttack : 0f);
            RefreshRadarNatureColors(increasedStat, decreasedStat);
            SetVisible(selectedFavorite, hasSelection && snapshot.favorite);
        }

        void RefreshActions() {
            bool hasSelection = selectedPet != null;
            bool fromStorage = selectionSource == SelectionSource.Storage;
            bool fromParty = selectionSource == SelectionSource.Party;
            bool canAdd = hasSelection && fromStorage && provider != null && provider.PartyCount < PartySize;
            bool canMove = hasSelection && fromParty && provider != null && !provider.IsFull;
            SetVisible(addButton, canAdd);
            SetVisible(moveButton, canMove);
            SetVisible(replaceButton, hasSelection);
            SetVisible(detailsButton, hasSelection);
            addButton?.SetEnabled(canAdd);
            moveButton?.SetEnabled(canMove);
            replaceButton?.SetEnabled(hasSelection);
            detailsButton?.SetEnabled(hasSelection);
        }

        void HandleBoxCardClick(SelectionSource source, int index) {
            if (provider == null || source == SelectionSource.None || index < 0) return;

            if (pendingSwapSource != SelectionSource.None) {
                TryCompleteSwap(source, index);
                return;
            }

            float clickTime = Time.unscaledTime;
            bool isDoubleClick = source == lastClickSource
                && index == lastClickIndex
                && clickTime - lastClickTime <= DoubleClickInterval;
            lastClickSource = source;
            lastClickIndex = index;
            lastClickTime = clickTime;

            if (source == SelectionSource.Party) SelectPartySlot(index);
            else SelectStoragePet(index);

            if (!isDoubleClick) return;
            ClearLastClick();

            PetController pet = source == SelectionSource.Party
                ? provider.GetPartyPet(index)
                : provider.GetStoredPet(index);
            if (pet == null) {
                SetFeedback("Hãy double-click một ô đang có pet.");
                return;
            }

            pendingSwapSource = source;
            pendingSwapSourceIndex = index;
            UpdateSwapSelectionVisual();
            SetFeedback($"Đã chọn {FormatSource(source, index)}. Click ô đích để đổi.");
        }

        void TryCompleteSwap(SelectionSource target, int targetIndex) {
            SelectionSource source = pendingSwapSource;
            int sourceIndex = pendingSwapSourceIndex;
            string sourceDescription = FormatSource(source, sourceIndex);
            string targetDescription = FormatSource(target, targetIndex);
            ClearSwapSelection();

            if (source == target && sourceIndex == targetIndex) {
                SetFeedback("Đã hủy đổi vị trí.");
                if (target == SelectionSource.Party) SelectPartySlot(targetIndex);
                else SelectStoragePet(targetIndex);
                return;
            }

            bool succeeded = false;
            string error = string.Empty;
            if (source == SelectionSource.Party && target == SelectionSource.Party) {
                succeeded = provider.TrySwapPartySlots(sourceIndex, targetIndex, out error);
            }
            else if (source == SelectionSource.Storage && target == SelectionSource.Party) {
                succeeded = provider.TrySwapPartyWithBox(targetIndex, sourceIndex, out error);
            }
            else if (source == SelectionSource.Party && target == SelectionSource.Storage) {
                succeeded = provider.TrySwapPartyWithBox(sourceIndex, targetIndex, out error);
            }
            else if (source == SelectionSource.Storage && target == SelectionSource.Storage) {
                if (sortIndex > 0) {
                    succeeded = false;
                    error = "Hãy chọn “Vị trí Box” trước khi đổi hai slot Box.";
                }
                else succeeded = provider.TrySwapStoredPets(sourceIndex, targetIndex, out error);
            }

            if (!succeeded) {
                SetFeedback(string.IsNullOrWhiteSpace(error) ? "Không thể đổi hai pet này." : error);
                Refresh();
                return;
            }

            if (target == SelectionSource.Party) {
                selectedPet = provider.GetPartyPet(targetIndex);
                selectionSource = SelectionSource.Party;
                targetPartySlot = targetIndex;
            }
            else {
                selectedPet = provider.GetStoredPet(targetIndex);
                selectionSource = SelectionSource.Storage;
                targetPartySlot = -1;
            }

            SetFeedback($"Đã đổi {sourceDescription} với {targetDescription}.");
            Refresh();
        }

        void UpdateSwapSelectionVisual() {
            for (int i = 0; i < partyCards.Count; i++) {
                partyCards[i].EnableInClassList(
                    "is-swap-source",
                    pendingSwapSource == SelectionSource.Party && pendingSwapSourceIndex == i);
            }
            for (int i = 0; i < storageCards.Count && i < visibleStorage.Count; i++) {
                storageCards[i].EnableInClassList(
                    "is-swap-source",
                    pendingSwapSource == SelectionSource.Storage
                    && pendingSwapSourceIndex == visibleStorage[i].providerIndex);
            }
        }

        void ClearSwapSelection() {
            pendingSwapSource = SelectionSource.None;
            pendingSwapSourceIndex = -1;
            ClearLastClick();
            UpdateSwapSelectionVisual();
        }

        void ClearLastClick() {
            lastClickSource = SelectionSource.None;
            lastClickIndex = -1;
            lastClickTime = float.NegativeInfinity;
        }

        static string FormatSource(SelectionSource source, int index) {
            return source == SelectionSource.Party
                ? $"Party {index + 1}"
                : $"Box {index / SlotsPerPage + 1:00}, slot {index + 1:00}";
        }

        void ChangePage(int direction) {
            int pageCountValue = provider == null
                ? 1
                : Mathf.Max(1, Mathf.CeilToInt(provider.Capacity / (float)SlotsPerPage));
            currentPage = Mathf.Clamp(currentPage + direction, 0, pageCountValue - 1);
            RebuildStorage();
        }

        void ToggleFilterPanel() {
            if (filterPanel == null) return;
            if (sortPopup != null) sortPopup.style.display = DisplayStyle.None;
            filterPanel.style.display = filterPanel.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ToggleSortPopup() {
            if (sortPopup == null) return;
            if (filterPanel != null) filterPanel.style.display = DisplayStyle.None;
            sortPopup.style.display = sortPopup.resolvedStyle.display == DisplayStyle.None
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        void ToggleElement(PetElement element) {
            if (!selectedElements.Add(element)) selectedElements.Remove(element);
            if (selectedElements.Count == 0) selectedElements.Clear();
            RefreshFilterButtons();
            RebuildStorage();
        }

        void SelectAllElements() {
            selectedElements.Clear();
            RefreshFilterButtons();
            RebuildStorage();
        }

        void RefreshFilterButtons() {
            allFilterButton?.EnableInClassList("is-selected", selectedElements.Count == 0);
            foreach (KeyValuePair<PetElement, Button> pair in elementButtons) {
                pair.Value.EnableInClassList("is-selected", selectedElements.Contains(pair.Key));
            }
        }

        bool MatchesElement(PetElement element) {
            return selectedElements.Count == 0 || selectedElements.Contains(element);
        }

        static bool MatchesSearch(PetSnapshot snapshot, string query) {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return snapshot.displayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
                || (!string.IsNullOrWhiteSpace(snapshot.species) && snapshot.species.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        void ShowPurchasePopup(bool isFull) {
            if (provider == null || purchasePopup == null) return;
            bool retryPendingCapture = provider.PendingCapturedPet != null
                && provider.StoredCount < provider.Capacity;
            if (purchaseTitle != null) {
                purchaseTitle.text = retryPendingCapture
                    ? "HOÀN TẤT BẮT PET"
                    : isFull ? "BOX ĐÃ ĐẦY" : "MUA THÊM SLOT";
            }
            if (purchaseCurrent != null) purchaseCurrent.text = $"Hiện tại: {provider.StoredCount} / {provider.Capacity}";
            if (purchaseExpand != null) {
                purchaseExpand.text = retryPendingCapture
                    ? "Pet đang chờ được lưu vào Box"
                    : $"Mở rộng: +{provider.ExpansionSize} slot";
            }
            if (purchasePrice != null) {
                purchasePrice.text = retryPendingCapture
                    ? "Không tốn thêm Gold"
                    : $"Giá: {provider.ExpansionGoldCost:N0} Gold";
            }
            if (purchaseAfter != null) {
                purchaseAfter.text = retryPendingCapture
                    ? "Nhấn THỬ LẠI để hoàn tất"
                    : $"Sau khi mua: {provider.Capacity + provider.ExpansionSize}";
            }
            if (purchaseConfirmButton != null) {
                purchaseConfirmButton.text = retryPendingCapture ? "THỬ LẠI" : "MUA THÊM";
            }
            purchasePopup.style.display = DisplayStyle.Flex;
        }

        void HidePurchasePopup() {
            if (purchasePopup != null) purchasePopup.style.display = DisplayStyle.None;
        }

        void RequestPurchase() {
            if (provider == null) return;

            if (provider.PendingCapturedPet != null && provider.StoredCount < provider.Capacity) {
                if (TryCommitPendingCapture(out string retryError)) {
                    SetFeedback("Đã đưa pet vừa bắt vào Box.");
                }
                else {
                    SetFeedback($"Pet vẫn đang chờ: {retryError}");
                }
                Refresh();
                HidePurchasePopup();
                return;
            }

            if (provider.TryPurchaseCapacity(out string error)) {
                PetController pendingCapture = provider.PendingCapturedPet;
                if (pendingCapture != null) {
                    if (TryCommitPendingCapture(out string captureError)) {
                        SetFeedback($"Đã mở thêm {provider.ExpansionSize} slot và đưa pet vừa bắt vào Box.");
                    }
                    else {
                        SetFeedback($"Đã mở thêm {provider.ExpansionSize} slot, nhưng pet vẫn đang chờ: {captureError}.");
                    }
                }
                else {
                    SetFeedback($"Đã mở thêm {provider.ExpansionSize} slot Box.");
                }
                Refresh();
            }
            else {
                SetFeedback(error);
            }
            HidePurchasePopup();
        }

        bool TryCommitPendingCapture(out string error) {
            captureCoordinator = captureCoordinator != null
                ? captureCoordinator
                : FindFirstObjectByType<PetCaptureCoordinator>(FindObjectsInactive.Include);
            if (captureCoordinator == null) {
                error = "không tìm thấy bộ xử lý bắt pet";
                return false;
            }

            return captureCoordinator.TryCommitPendingCapture(out error);
        }

        void GoBack() {
            HidePurchasePopup();
            if (returnTarget == PetBoxReturnTarget.Pets && inventoryController != null) {
                inventoryController.OpenPetPartyPanel();
                return;
            }

            inventoryController?.Close();
            if (returnTarget == PetBoxReturnTarget.Gameplay) return;
            gameMenuController = gameMenuController != null ? gameMenuController : FindFirstObjectByType<GameMenuController>();
            gameMenuController?.OpenMenu();
        }

        void EnsureValidSelection() {
            if (selectedPet != null) {
                int partyIndex = IndexOfPartyPet(selectedPet);
                int boxIndex = IndexOfStoredPet(selectedPet);
                if (partyIndex >= 0) selectionSource = SelectionSource.Party;
                else if (boxIndex >= 0) selectionSource = SelectionSource.Storage;
                else {
                    selectedPet = null;
                    selectionSource = SelectionSource.None;
                }
            }

            if (selectedPet == null) {
                for (int i = 0; i < 6; i++) {
                    PetController pet = provider.GetPartyPet(i);
                    if (pet == null) continue;
                    selectedPet = pet;
                    selectionSource = SelectionSource.Party;
                    targetPartySlot = i;
                    break;
                }
            }
        }

        int IndexOfPartyPet(PetController pet) {
            if (provider == null || pet == null) return -1;
            for (int i = 0; i < 6; i++) if (provider.GetPartyPet(i) == pet) return i;
            return -1;
        }

        int IndexOfStoredPet(PetController pet) {
            if (provider == null || pet == null) return -1;
            for (int i = 0; i < provider.StoredPets.Count; i++) if (provider.StoredPets[i] == pet) return i;
            return -1;
        }

        PetSnapshot BuildSnapshot(PetController pet) {
            if (pet == null) return default;
            IPetHudDataSource data = FindDataSource(pet);
            PetCollectionMetadata metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            string baseName = CleanName(data?.DisplayName, pet.name);
            return new PetSnapshot {
                pet = pet,
                displayName = metadata != null ? metadata.ResolveDisplayName(baseName) : baseName,
                species = metadata?.Species,
                gender = metadata?.Gender,
                element = metadata != null ? metadata.Element : PetElement.Unknown,
                rarity = metadata != null ? metadata.Rarity : PetRarity.Unknown,
                level = data != null ? data.Level : 0,
                obtainedOrder = metadata != null ? metadata.ObtainedOrder : 0,
                favorite = metadata != null && metadata.IsFavorite,
                icon = data?.Icon,
                health = data != null ? Mathf.Max(0f, data.Health) : 0f,
                maxHealth = data != null ? Mathf.Max(0f, data.MaxHealth) : 0f,
                attack = metadata != null ? metadata.Attack : 0,
                defense = metadata != null ? metadata.Defense : 0,
                speed = metadata != null ? metadata.Speed : 0,
                magicAttack = metadata != null ? metadata.MagicAttack : 0,
                magicDefense = metadata != null ? metadata.MagicDefense : 0,
                personality = metadata?.Personality
            };
        }

        static IPetHudDataSource FindDataSource(PetController pet) {
            if (pet == null) return null;
            foreach (MonoBehaviour behaviour in pet.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour is IPetHudDataSource source) return source;
            }
            return null;
        }

        void SetSelectedPortrait(PetSnapshot snapshot) {
            if (selectedPortrait != null) {
                if (snapshot.icon != null) selectedPortrait.style.backgroundImage = new StyleBackground(snapshot.icon);
                else selectedPortrait.style.backgroundImage = StyleKeyword.None;
            }
            if (selectedPortraitFallback != null) {
                selectedPortraitFallback.text = snapshot.pet != null ? FirstLetter(snapshot.displayName) : "?";
                SetVisible(selectedPortraitFallback, snapshot.icon == null);
            }
        }

        static string FormatElement(PetElement value) {
            switch (value) {
                case PetElement.Nature: return "Thảo";
                case PetElement.Fire: return "Lửa";
                case PetElement.Water: return "Nước";
                case PetElement.Wind: return "Gió";
                case PetElement.Earth: return "Đất";
                case PetElement.Electric: return "Điện";
                case PetElement.Ice: return "Băng";
                case PetElement.Light: return "Ánh sáng";
                case PetElement.Dark: return "Bóng tối";
                default: return "Chưa rõ";
            }
        }

        static string FormatRarity(PetRarity value) {
            switch (value) {
                case PetRarity.Common: return "★ Thường";
                case PetRarity.Uncommon: return "★★ Ít gặp";
                case PetRarity.Rare: return "★★★ Hiếm";
                case PetRarity.Epic: return "★★★★ Sử thi";
                case PetRarity.Legendary: return "★★★★★ Huyền thoại";
                default: return "Chưa rõ";
            }
        }

        void RefreshRadarNatureColors(string increasedStat, string decreasedStat) {
            if (radarLabels == null) return;
            string[] statNames = { "HP", "Attack", "Defense", "Speed", "Sp. Def", "Sp. Atk" };
            for (int i = 0; i < radarLabels.Length && i < statNames.Length; i++) {
                Label label = radarLabels[i];
                if (label == null) continue;
                label.EnableInClassList("is-up", StatMatches(statNames[i], increasedStat));
                label.EnableInClassList("is-down", StatMatches(statNames[i], decreasedStat));
            }
        }

        static bool StatMatches(string left, string right) {
            return !string.IsNullOrWhiteSpace(right)
                && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        static void ResolveNatureEffect(string personality, out string increasedStat, out string decreasedStat) {
            increasedStat = null;
            decreasedStat = null;
            if (string.IsNullOrWhiteSpace(personality)) return;

            switch (personality.Trim().ToLowerInvariant()) {
                case "lonely": case "cô độc": increasedStat = "Attack"; decreasedStat = "Defense"; break;
                case "brave": case "dũng cảm": increasedStat = "Attack"; decreasedStat = "Speed"; break;
                case "adamant": case "kiên định": increasedStat = "Attack"; decreasedStat = "Sp. Atk"; break;
                case "naughty": case "nghịch ngợm": increasedStat = "Attack"; decreasedStat = "Sp. Def"; break;
                case "bold": case "táo bạo": increasedStat = "Defense"; decreasedStat = "Attack"; break;
                case "relaxed": case "thư giãn": increasedStat = "Defense"; decreasedStat = "Speed"; break;
                case "impish": case "tinh quái": increasedStat = "Defense"; decreasedStat = "Sp. Atk"; break;
                case "lax": case "lỏng lẻo": increasedStat = "Defense"; decreasedStat = "Sp. Def"; break;
                case "timid": case "rụt rè": increasedStat = "Speed"; decreasedStat = "Attack"; break;
                case "hasty": case "vội vàng": increasedStat = "Speed"; decreasedStat = "Defense"; break;
                case "jolly": case "vui vẻ": increasedStat = "Speed"; decreasedStat = "Sp. Atk"; break;
                case "naive": case "ngây thơ": increasedStat = "Speed"; decreasedStat = "Sp. Def"; break;
                case "modest": case "khiêm tốn": increasedStat = "Sp. Atk"; decreasedStat = "Attack"; break;
                case "mild": case "ôn hòa": increasedStat = "Sp. Atk"; decreasedStat = "Defense"; break;
                case "quiet": case "trầm lặng": increasedStat = "Sp. Atk"; decreasedStat = "Speed"; break;
                case "rash": case "hấp tấp": increasedStat = "Sp. Atk"; decreasedStat = "Sp. Def"; break;
                case "calm": case "điềm tĩnh": increasedStat = "Sp. Def"; decreasedStat = "Attack"; break;
                case "gentle": case "dịu dàng": increasedStat = "Sp. Def"; decreasedStat = "Defense"; break;
                case "sassy": case "táo tợn": increasedStat = "Sp. Def"; decreasedStat = "Speed"; break;
                case "careful": case "cẩn thận": increasedStat = "Sp. Def"; decreasedStat = "Sp. Atk"; break;
            }
        }

        static string FormatGender(string value) {
            if (string.IsNullOrWhiteSpace(value)) return "Chưa rõ";
            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "f" || normalized.Contains("female") || normalized.Contains("cái")) return "♀ Cái";
            if (normalized == "m" || normalized.Contains("male") || normalized.Contains("đực")) return "♂ Đực";
            return value.Trim();
        }

        static VisualElement CreatePortrait(PetSnapshot snapshot, string className) {
            var portrait = new VisualElement();
            portrait.AddToClassList(className);
            if (snapshot.icon != null) portrait.style.backgroundImage = new StyleBackground(snapshot.icon);
            else portrait.Add(CreateLabel(FirstLetter(snapshot.displayName), "box-portrait-fallback"));
            return portrait;
        }

        static VisualElement CreateStorageCardInfo(PetSnapshot snapshot) {
            var info = new VisualElement();
            info.AddToClassList("box-storage-info");
            var name = CreateLabel(snapshot.displayName, "box-storage-name");
            name.tooltip = snapshot.displayName;
            info.Add(name);
            info.Add(CreateLabel($"Lv. {Mathf.Max(1, snapshot.level)}", "box-storage-meta"));
            return info;
        }

        static Label CreateLabel(string text, string className) {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        static void SetVisible(VisualElement element, bool visible) {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetFill(VisualElement element, float normalized) {
            if (element != null) element.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
        }

        void SetFeedback(string message) {
            if (feedback == null) return;
            feedback.text = message ?? string.Empty;
            feedback.style.display = string.IsNullOrWhiteSpace(feedback.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        static string CleanName(string preferred, string fallback) {
            string value = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
            return string.IsNullOrWhiteSpace(value) ? "Pet" : value.Replace("(Clone)", string.Empty).Trim();
        }

        static string FirstLetter(string value) {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().Substring(0, 1).ToUpperInvariant();
        }

    }
}

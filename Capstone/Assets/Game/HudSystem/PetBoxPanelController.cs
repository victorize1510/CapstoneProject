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
        Pets
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetBoxPanelController : MonoBehaviour {
        const float DoubleClickInterval = 0.45f;

        static readonly string[] SortChoices = {
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

        MonsterInventoryController inventoryController;
        GameMenuController gameMenuController;
        VisualElement root;
        VisualElement panel;
        VisualElement partyRow;
        VisualElement storageGrid;
        VisualElement storageScroll;
        VisualElement filterPanel;
        VisualElement purchasePopup;
        VisualElement selectedPortrait;
        Label selectedPortraitFallback;
        Label partyCountLabel;
        Label capacityLabel;
        Label emptyLabel;
        Label selectedName;
        Label selectedMeta;
        Label selectedHealth;
        Label selectedExperience;
        VisualElement healthFill;
        VisualElement experienceFill;
        Label statHealth;
        Label statAttack;
        Label statDefense;
        Label statSpeed;
        VisualElement selectedSkills;
        Label feedback;
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
        TextField searchField;
        DropdownField sortField;

        PetBoxReturnTarget returnTarget = PetBoxReturnTarget.Menu;
        SelectionSource selectionSource;
        PetController selectedPet;
        int targetPartySlot = -1;
        SelectionSource pendingSwapSource;
        int pendingSwapSourceIndex = -1;
        SelectionSource lastClickSource;
        int lastClickIndex = -1;
        float lastClickTime = float.NegativeInfinity;
        bool controlsRegistered;
        bool subscribed;

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
            public float health;
            public float maxHealth;
            public int experience;
            public int maxExperience;
            public int attack;
            public int defense;
            public int speed;
            public long obtainedOrder;
            public bool favorite;
            public Sprite icon;
            public IReadOnlyList<SkillHudData> skills;
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

            if (partyCountLabel != null) partyCountLabel.text = $"TEAM ({provider.PartyCount} / 6)";
            if (capacityLabel != null) capacityLabel.text = $"SLOT BOX: {provider.StoredCount} / {provider.Capacity}";
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
            storageScroll = docRoot.Q<VisualElement>("box-storage-scroll");
            filterPanel = docRoot.Q<VisualElement>("box-filter-panel");
            purchasePopup = docRoot.Q<VisualElement>("box-purchase-popup");
            selectedPortrait = docRoot.Q<VisualElement>("box-selected-portrait");
            selectedPortraitFallback = docRoot.Q<Label>("box-selected-portrait-fallback");
            partyCountLabel = docRoot.Q<Label>("box-party-count");
            capacityLabel = docRoot.Q<Label>("box-capacity-label");
            emptyLabel = docRoot.Q<Label>("box-empty-label");
            selectedName = docRoot.Q<Label>("box-selected-name");
            selectedMeta = docRoot.Q<Label>("box-selected-meta");
            selectedHealth = docRoot.Q<Label>("box-selected-health");
            selectedExperience = docRoot.Q<Label>("box-selected-experience");
            healthFill = docRoot.Q<VisualElement>("box-health-fill");
            experienceFill = docRoot.Q<VisualElement>("box-experience-fill");
            statHealth = docRoot.Q<Label>("box-stat-health");
            statAttack = docRoot.Q<Label>("box-stat-attack");
            statDefense = docRoot.Q<Label>("box-stat-defense");
            statSpeed = docRoot.Q<Label>("box-stat-speed");
            selectedSkills = docRoot.Q<VisualElement>("box-selected-skills");
            feedback = docRoot.Q<Label>("box-feedback");
            sourceLabel = docRoot.Q<Label>("box-source-label");
            purchaseTitle = docRoot.Q<Label>("box-purchase-title");
            purchaseCurrent = docRoot.Q<Label>("box-purchase-current");
            purchaseExpand = docRoot.Q<Label>("box-purchase-expand");
            purchasePrice = docRoot.Q<Label>("box-purchase-price");
            purchaseAfter = docRoot.Q<Label>("box-purchase-after");
            purchaseConfirmButton = docRoot.Q<Button>("box-purchase-confirm");
            replaceButton = docRoot.Q<Button>("box-replace-button");
            addButton = docRoot.Q<Button>("box-add-button");
            moveButton = docRoot.Q<Button>("box-move-button");
            detailsButton = docRoot.Q<Button>("box-details-button");
            allFilterButton = docRoot.Q<Button>("box-filter-all");
            searchField = docRoot.Q<TextField>("box-search-field");
            sortField = docRoot.Q<DropdownField>("box-sort-field");
        }

        void RegisterControls() {
            if (controlsRegistered || root == null) return;

            RegisterButton(document.rootVisualElement.Q<Button>("box-back-button"), GoBack);
            RegisterButton(document.rootVisualElement.Q<Button>("box-expand-button"), () => ShowPurchasePopup(false));
            RegisterButton(document.rootVisualElement.Q<Button>("box-filter-toggle"), ToggleFilterPanel);
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

            if (sortField != null) {
                sortField.choices = SortChoices.ToList();
                sortField.index = 0;
                sortField.RegisterValueChangedCallback(HandleSortChanged);
            }
            searchField?.RegisterValueChangedCallback(HandleSearchChanged);
            controlsRegistered = true;
        }

        void UnregisterControls() {
            foreach (KeyValuePair<Button, Action> pair in buttonCallbacks) pair.Key.clicked -= pair.Value;
            buttonCallbacks.Clear();
            sortField?.UnregisterValueChangedCallback(HandleSortChanged);
            searchField?.UnregisterValueChangedCallback(HandleSearchChanged);
            controlsRegistered = false;
        }

        void HandleSortChanged(ChangeEvent<string> _) {
            RebuildStorage();
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

            for (int i = 0; i < 6; i++) {
                int slotIndex = i;
                PetSnapshot snapshot = BuildSnapshot(provider.GetPartyPet(i));
                var card = new Button { name = $"box-party-slot-{i + 1}", text = string.Empty, userData = i };
                card.AddToClassList("box-party-card");
                card.EnableInClassList("is-empty", snapshot.pet == null);
                card.EnableInClassList("is-selected", selectionSource == SelectionSource.Party && selectedPet == snapshot.pet && snapshot.pet != null);
                card.EnableInClassList("is-target", targetPartySlot == i);
                card.EnableInClassList("is-swap-source", pendingSwapSource == SelectionSource.Party && pendingSwapSourceIndex == i);
                card.RegisterCallback<ClickEvent>(
                    _ => HandleBoxCardClick(SelectionSource.Party, slotIndex),
                    TrickleDown.TrickleDown);

                var top = new VisualElement();
                top.AddToClassList("box-card-top");
                top.Add(CreateLabel((i + 1).ToString(), "box-slot-number"));
                top.Add(CreateLabel(i == 0 ? "LEAD" : string.Empty, "box-lead-badge"));
                top.Add(CreateLabel(snapshot.pet != null && snapshot.favorite ? "\u2605" : string.Empty, "box-favorite-badge"));
                card.Add(top);
                card.Add(CreatePortrait(snapshot, "box-party-portrait"));
                card.Add(CreateLabel(snapshot.pet != null ? snapshot.displayName : "Slot trống", "box-card-name"));
                card.Add(CreateLabel(
                    snapshot.pet != null ? $"Lv.{snapshot.level}  {Safe(snapshot.gender)}  {FormatElement(snapshot.element)}" : "+",
                    "box-card-meta"));
                card.Add(CreateLabel(snapshot.pet != null ? FormatRarity(snapshot.rarity) : string.Empty, "box-card-rarity"));
                card.Add(CreateSmallBar("HP", Percent(snapshot.health, snapshot.maxHealth), "box-hp-fill"));
                card.Add(CreateSmallBar("EXP", Percent(snapshot.experience, snapshot.maxExperience), "box-exp-fill"));
                partyCards.Add(card);
                partyRow.Add(card);
            }
        }

        void RebuildStorage() {
            if (storageGrid == null || provider == null) return;
            storageGrid.Clear();
            storageCards.Clear();
            visibleStorage.Clear();

            string query = searchField != null ? searchField.value?.Trim() : string.Empty;
            for (int i = 0; i < provider.StoredPets.Count; i++) {
                PetSnapshot snapshot = BuildSnapshot(provider.StoredPets[i]);
                if (snapshot.pet == null || !MatchesSearch(snapshot, query) || !MatchesElement(snapshot.element)) continue;
                visibleStorage.Add(new BoxViewEntry { providerIndex = i, snapshot = snapshot });
            }

            SortVisibleStorage();
            foreach (BoxViewEntry entry in visibleStorage) {
                BoxViewEntry local = entry;
                var card = new Button { text = string.Empty, userData = local.providerIndex };
                card.AddToClassList("box-storage-card");
                card.EnableInClassList("is-selected", selectionSource == SelectionSource.Storage && selectedPet == local.snapshot.pet);
                card.EnableInClassList("is-swap-source", pendingSwapSource == SelectionSource.Storage && pendingSwapSourceIndex == local.providerIndex);
                card.RegisterCallback<ClickEvent>(
                    _ => HandleBoxCardClick(SelectionSource.Storage, local.providerIndex),
                    TrickleDown.TrickleDown);
                card.Add(CreatePortrait(local.snapshot, "box-storage-portrait"));
                card.Add(CreateLabel(local.snapshot.displayName, "box-storage-name"));
                card.Add(CreateLabel(
                    $"Lv.{local.snapshot.level}  {Safe(local.snapshot.gender)}  {FormatElement(local.snapshot.element)}  {FormatRarity(local.snapshot.rarity)}",
                    "box-storage-meta"));
                card.Add(CreateSmallBar(string.Empty, Percent(local.snapshot.health, local.snapshot.maxHealth), "box-hp-fill"));
                card.Add(CreateLabel(local.snapshot.favorite ? "\u2605" : string.Empty, "box-storage-favorite"));
                storageCards.Add(card);
                storageGrid.Add(card);
            }

            if (emptyLabel != null) emptyLabel.style.display = visibleStorage.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SortVisibleStorage() {
            int index = sortField != null ? sortField.index : 0;
            Comparison<BoxViewEntry> comparison;
            switch (index) {
                case 1: comparison = (a, b) => string.Compare(a.snapshot.displayName, b.snapshot.displayName, StringComparison.CurrentCultureIgnoreCase); break;
                case 2: comparison = (a, b) => string.Compare(b.snapshot.displayName, a.snapshot.displayName, StringComparison.CurrentCultureIgnoreCase); break;
                case 3: comparison = (a, b) => b.snapshot.level.CompareTo(a.snapshot.level); break;
                case 4: comparison = (a, b) => a.snapshot.level.CompareTo(b.snapshot.level); break;
                case 5: comparison = (a, b) => b.snapshot.rarity.CompareTo(a.snapshot.rarity); break;
                case 6: comparison = (a, b) => a.snapshot.rarity.CompareTo(b.snapshot.rarity); break;
                default: comparison = (a, b) => b.snapshot.obtainedOrder.CompareTo(a.snapshot.obtainedOrder); break;
            }
            visibleStorage.Sort(comparison);
        }

        void SelectPartySlot(int slotIndex) {
            PetController pet = provider?.GetPartyPet(slotIndex);
            if (selectionSource == SelectionSource.Storage && selectedPet != null) {
                targetPartySlot = slotIndex;
                SetFeedback($"Đã chọn slot Party {slotIndex + 1}. Bấm THAY THẾ để xác nhận.");
            }
            else {
                selectionSource = pet != null ? SelectionSource.Party : SelectionSource.None;
                selectedPet = pet;
                targetPartySlot = slotIndex;
            }
            Refresh();
        }

        void SelectStoragePet(int providerIndex) {
            PetController pet = provider?.GetStoredPet(providerIndex);
            if (pet == null) return;
            selectionSource = SelectionSource.Storage;
            selectedPet = pet;
            SetFeedback(targetPartySlot >= 0 ? $"Chọn THAY THẾ để đưa pet vào slot {targetPartySlot + 1}." : "Chọn slot Party để thay, hoặc bấm ĐƯA VÀO ĐỘI.");
            Refresh();
        }

        void ReplaceSelectedPet() {
            if (provider == null || selectionSource != SelectionSource.Storage || selectedPet == null) {
                SetFeedback("Chọn một pet trong Box trước.");
                return;
            }
            if (targetPartySlot < 0) {
                SetFeedback("Chọn slot Party muốn thay.");
                return;
            }

            int boxIndex = IndexOfStoredPet(selectedPet);
            if (provider.TryMoveBoxToParty(boxIndex, targetPartySlot, out string error)) {
                selectedPet = provider.GetPartyPet(targetPartySlot);
                selectionSource = SelectionSource.Party;
                SetFeedback($"Đã cập nhật slot Party {targetPartySlot + 1}.");
            }
            else SetFeedback(error);
            Refresh();
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
                SetFeedback("Đã chuyển pet vào Box.");
            }
            else SetFeedback(error);
            Refresh();
        }

        void RequestDetails() {
            if (selectedPet == null) return;
            DetailsRequested?.Invoke(selectedPet);
            inventoryController?.OpenPetDetailsPanel(selectedPet);
        }

        void RefreshSelectedDetails() {
            PetSnapshot snapshot = BuildSnapshot(selectedPet);
            SetPortrait(selectedPortrait, selectedPortraitFallback, snapshot.icon, snapshot.displayName);
            if (selectedName != null) selectedName.text = snapshot.pet != null ? snapshot.displayName : "Chọn một pet";
            if (selectedMeta != null) {
                selectedMeta.text = snapshot.pet != null
                    ? $"{Safe(snapshot.species)}  •  Lv.{snapshot.level}  •  {Safe(snapshot.gender)}  •  {FormatElement(snapshot.element)}  •  {FormatRarity(snapshot.rarity)}"
                    : "Chọn pet trong Party hoặc Box để xem thông tin.";
            }
            if (selectedHealth != null) selectedHealth.text = FormatPair(snapshot.health, snapshot.maxHealth);
            if (selectedExperience != null) selectedExperience.text = FormatPair(snapshot.experience, snapshot.maxExperience);
            SetFill(healthFill, Percent(snapshot.health, snapshot.maxHealth));
            SetFill(experienceFill, Percent(snapshot.experience, snapshot.maxExperience));
            SetLabel(statHealth, snapshot.maxHealth > 0f ? Mathf.RoundToInt(snapshot.maxHealth).ToString() : "-");
            SetLabel(statAttack, snapshot.attack > 0 ? snapshot.attack.ToString() : "-");
            SetLabel(statDefense, snapshot.defense > 0 ? snapshot.defense.ToString() : "-");
            SetLabel(statSpeed, snapshot.speed > 0 ? snapshot.speed.ToString() : "-");
            RebuildSelectedSkills(snapshot.skills);
        }

        void RebuildSelectedSkills(IReadOnlyList<SkillHudData> skills) {
            if (selectedSkills == null) return;
            selectedSkills.Clear();
            int visibleSkillCount = Mathf.Clamp(skills?.Count ?? 0, 0, 4);
            for (int i = 0; i < visibleSkillCount; i++) {
                bool hasSkill = skills != null && i < skills.Count && skills[i].unlocked;
                SkillHudData skill = hasSkill ? skills[i] : default;
                var card = new VisualElement();
                card.AddToClassList("box-skill-chip");
                var icon = new VisualElement();
                icon.AddToClassList("box-skill-icon");
                if (hasSkill && skill.icon != null) icon.style.backgroundImage = new StyleBackground(skill.icon);
                card.Add(icon);
                card.Add(CreateLabel(hasSkill ? skill.displayName : "-", "box-skill-name"));
                card.Add(CreateLabel(hasSkill ? FormatSkillMeta(skill) : string.Empty, "box-skill-meta"));
                selectedSkills.Add(card);
            }
        }

        void RefreshActions() {
            bool hasSelection = selectedPet != null;
            bool fromStorage = selectionSource == SelectionSource.Storage;
            bool fromParty = selectionSource == SelectionSource.Party;
            SetVisible(replaceButton, fromStorage);
            SetVisible(addButton, fromStorage && provider != null && provider.PartyCount < 6);
            SetVisible(moveButton, fromParty);
            SetVisible(detailsButton, hasSelection);
            if (replaceButton != null) replaceButton.SetEnabled(fromStorage && targetPartySlot >= 0);
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
                succeeded = provider.TryMoveBoxToParty(sourceIndex, targetIndex, out error);
            }
            else if (source == SelectionSource.Party && target == SelectionSource.Storage) {
                succeeded = provider.TryMoveBoxToParty(targetIndex, sourceIndex, out error);
                target = SelectionSource.Party;
                targetIndex = sourceIndex;
            }
            else if (source == SelectionSource.Storage && target == SelectionSource.Storage) {
                succeeded = provider.TrySwapStoredPets(sourceIndex, targetIndex, out error);
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
            return source == SelectionSource.Party ? $"Party {index + 1}" : $"Box {index + 1}";
        }

        void ToggleFilterPanel() {
            if (filterPanel == null) return;
            filterPanel.style.display = filterPanel.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
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
                health = data != null ? data.Health : 0f,
                maxHealth = data != null ? data.MaxHealth : 0f,
                experience = metadata != null ? metadata.Experience : 0,
                maxExperience = metadata != null ? metadata.ExperienceToNextLevel : 0,
                attack = metadata != null ? metadata.Attack : 0,
                defense = metadata != null ? metadata.Defense : 0,
                speed = metadata != null ? metadata.Speed : 0,
                obtainedOrder = metadata != null ? metadata.ObtainedOrder : 0,
                favorite = metadata != null && metadata.IsFavorite,
                icon = data?.Icon,
                skills = data?.GetSkills()
            };
        }

        static IPetHudDataSource FindDataSource(PetController pet) {
            if (pet == null) return null;
            foreach (MonoBehaviour behaviour in pet.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour is IPetHudDataSource source) return source;
            }
            return null;
        }

        static VisualElement CreatePortrait(PetSnapshot snapshot, string className) {
            var portrait = new VisualElement();
            portrait.AddToClassList(className);
            if (snapshot.icon != null) portrait.style.backgroundImage = new StyleBackground(snapshot.icon);
            else portrait.Add(CreateLabel(FirstLetter(snapshot.displayName), "box-portrait-fallback"));
            return portrait;
        }

        static VisualElement CreateSmallBar(string label, float percent, string fillClass) {
            var row = new VisualElement();
            row.AddToClassList("box-small-bar-row");
            if (!string.IsNullOrEmpty(label)) row.Add(CreateLabel(label, "box-small-bar-label"));
            var track = new VisualElement();
            track.AddToClassList("box-small-bar-track");
            var fill = new VisualElement();
            fill.AddToClassList("box-small-bar-fill");
            fill.AddToClassList(fillClass);
            fill.style.width = Length.Percent(Mathf.Clamp01(percent) * 100f);
            track.Add(fill);
            row.Add(track);
            return row;
        }

        static Label CreateLabel(string text, string className) {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        static void SetPortrait(VisualElement portrait, Label fallback, Sprite sprite, string name) {
            if (portrait == null) return;
            if (sprite != null) portrait.style.backgroundImage = new StyleBackground(sprite);
            else portrait.style.backgroundImage = StyleKeyword.None;
            if (fallback != null) {
                fallback.text = FirstLetter(name);
                fallback.style.display = sprite == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        static void SetFill(VisualElement fill, float percent) {
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(percent) * 100f);
        }

        static void SetVisible(VisualElement element, bool visible) {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetLabel(Label label, string value) {
            if (label != null) label.text = value;
        }

        void SetFeedback(string message) {
            if (feedback != null) feedback.text = message ?? string.Empty;
        }

        static float Percent(float current, float maximum) {
            return maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        }

        static string FormatPair(float current, float maximum) {
            return maximum > 0f ? $"{Mathf.RoundToInt(current):N0} / {Mathf.RoundToInt(maximum):N0}" : "- / -";
        }

        static string CleanName(string preferred, string fallback) {
            string value = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
            return string.IsNullOrWhiteSpace(value) ? "Pet" : value.Replace("(Clone)", string.Empty).Trim();
        }

        static string FirstLetter(string value) {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().Substring(0, 1).ToUpperInvariant();
        }

        static string Safe(string value) {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        static string FormatElement(PetElement element) {
            return element == PetElement.Unknown ? "Hệ -" : element.ToString();
        }

        static string FormatRarity(PetRarity rarity) {
            return rarity == PetRarity.Unknown ? "Rarity -" : rarity.ToString();
        }

        static string FormatSkillMeta(SkillHudData skill) {
            if (skill.cooldownSeconds > 0f) return $"Lv.{Mathf.Max(1, skill.skillLevel)}  •  CD {skill.cooldownSeconds:0.#}s";
            return $"Lv.{Mathf.Max(1, skill.skillLevel)}";
        }
    }
}

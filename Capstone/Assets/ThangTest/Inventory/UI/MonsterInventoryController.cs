using System.Collections.Generic;
using System.Linq;
using System;
using GDS.Core;
using GDS.Core.Events;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory {
    [RequireComponent(typeof(UIDocument))]
    public class MonsterInventoryController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] MonsterInventoryAdapter adapter = null;
        [SerializeField] PlayerCurrencyWallet currencyWallet = null;
        [SerializeField] bool autoInstallInputController = true;
        [SerializeField] bool allowPlaceholderActions;
        [SerializeField] bool autoInstallPickupActionFeedback = true;
        [SerializeField] bool autoInstallQuestPanelController = true;
        [SerializeField] bool autoInstallPetPartyPanelController = true;
        [SerializeField] bool autoInstallPetBoxPanelController = true;
        [SerializeField] bool autoInstallPetSkillsPanelController = true;
        [SerializeField] bool autoInstallPetLevelUpPanelController = true;
        [SerializeField] bool autoInstallPetHealPanelController = true;
        [SerializeField] bool autoInstallPetEvolutionPanelController = true;
        [SerializeField] bool autoInstallPetReleasePanelController = true;

        readonly List<InventoryItemSnapshot> filteredItems = new List<InventoryItemSnapshot>();
        readonly List<Button> categoryButtons = new List<Button>();
        readonly List<Button> itemCards = new List<Button>();

        VisualElement root;
        VisualElement questInventoryShell;
        VisualElement questPanel;
        VisualElement inventoryPanel;
        VisualElement petsPanel;
        VisualElement boxPanel;
        VisualElement placeholderPanel;
        Label placeholderTitle;
        Label placeholderBody;
        VisualElement categoryPanel;
        VisualElement toolbarSidebarSpacer;
        ScrollView itemScroll;
        VisualElement itemGrid;
        Label noItemsLabel;
        Label capacityLabel;
        Label detailName;
        Label detailRarity;
        Label detailCategory;
        Label detailQuantity;
        Label detailDescription;
        Label detailEffect;
        Label detailSource;
        Label detailFlavor;
        Label detailIconPlaceholder;
        Label goldLabel;
        VisualElement detailIcon;
        Button useButton;
        Button giveButton;
        Button equipButton;
        Button dropButton;
        Button cancelButton;
        Button filterButton;
        TextField searchField;
        DropdownField sortField;
        ItemQuantityPopup quantityPopup;
        PetPartyPanelController petPartyPanelController;
        PetBoxPanelController petBoxPanelController;
        PetSkillsPanelController petSkillsPanelController;
        PetLevelUpPanelController petLevelUpPanelController;
        PetHealPanelController petHealPanelController;
        PetEvolutionPanelController petEvolutionPanelController;
        PetReleasePanelController petReleasePanelController;

        GameItemCategory currentCategory = GameItemCategory.All;
        MenuSection activeSection = MenuSection.Bag;
        InventoryItemSnapshot selectedItem;
        InventoryActionType equipSlotActionType = InventoryActionType.EquipItem;
        bool controlsRegistered;
        VisualElement controlsRoot;
        string selectedTargetMonsterId = string.Empty;
        string searchQuery = string.Empty;
        int selectedItemIndex = -1;
        bool categoriesVisible = true;
        InventorySortMode sortMode;
        PlayerCurrencyWallet subscribedCurrencyWallet;

        const string QuestPanelControllerTypeName = "Capstone.Game.QuestSystem.UI.QuestPanelController, Assembly-CSharp";

        public event Action<bool> VisibilityChanged;
        public event Action<InventoryActionRequest> UseItem;
        public event Action<InventoryActionRequest> GiveItem;
        public event Action<InventoryActionRequest> EquipItem;
        public event Action<InventoryActionRequest> AssignQuickSlot;
        public event Action<InventoryActionRequest> DropItem;
        public event Action<InventoryActionRequest> ActionCompleted;
        public event Action<string, int, InventoryActionRequest> RequestDropItem;
        public event Action<string, string, int, InventoryActionRequest> RequestGiveItem;
        public bool IsOpen => root != null && root.style.display.value != DisplayStyle.None;
        public InventoryItemSnapshot SelectedItem => selectedItem;
        public string SelectedTargetMonsterId => selectedTargetMonsterId;

        void OnEnable() {
            ResolveReferences();
            EnsureInputController();
            EnsurePickupActionFeedback();
            CacheElements();
            EnsureQuestPanelController();
            EnsurePetPartyPanelController();
            EnsurePetBoxPanelController();
            EnsurePetSkillsPanelController();
            EnsurePetLevelUpPanelController();
            EnsurePetHealPanelController();
            EnsurePetEvolutionPanelController();
            EnsurePetReleasePanelController();
            RegisterCategoryButtons();
            RegisterActionButtons();
            SetupItemGrid();
            SubscribeToAdapter();
            SubscribeToCurrencyWallet();
            SelectCategory(GameItemCategory.All);
        }

        void OnDisable() {
            if (adapter != null) adapter.ItemsChanged -= HandleInventoryChanged;
            UnsubscribeFromCurrencyWallet();
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            adapter = adapter != null ? adapter : GetComponent<MonsterInventoryAdapter>();
            if (adapter == null) adapter = GetComponentInParent<MonsterInventoryAdapter>();
            if (adapter == null) adapter = FindFirstObjectByType<MonsterInventoryAdapter>();
            if (currencyWallet == null) currencyWallet = FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
        }

        void EnsureInputController() {
            if (!Application.isPlaying || !autoInstallInputController) return;

            var inputController = GetComponent<InventoryInputController>();
            if (inputController == null) inputController = gameObject.AddComponent<InventoryInputController>();
            inputController.Bind(this);
        }

        void EnsurePickupActionFeedback() {
            if (!Application.isPlaying || !autoInstallPickupActionFeedback) return;

            var feedback = GetComponent<InventoryActionPickupFeedback>();
            if (feedback == null) feedback = gameObject.AddComponent<InventoryActionPickupFeedback>();
            feedback.Bind(this);
        }

        void EnsureQuestPanelController() {
            if (!Application.isPlaying || !autoInstallQuestPanelController || document == null) return;

            var questPanelType = Type.GetType(QuestPanelControllerTypeName);
            if (questPanelType == null || !typeof(MonoBehaviour).IsAssignableFrom(questPanelType)) return;
            if (GetComponent(questPanelType) == null) gameObject.AddComponent(questPanelType);
        }

        void EnsurePetPartyPanelController() {
            petPartyPanelController = GetComponent<PetPartyPanelController>();
            if (!Application.isPlaying || !autoInstallPetPartyPanelController || document == null) return;

            if (petPartyPanelController == null) petPartyPanelController = gameObject.AddComponent<PetPartyPanelController>();
            petPartyPanelController.Bind(document);
        }

        void EnsurePetBoxPanelController() {
            petBoxPanelController = GetComponent<PetBoxPanelController>();
            if (!Application.isPlaying || !autoInstallPetBoxPanelController || document == null) return;

            if (petBoxPanelController == null) petBoxPanelController = gameObject.AddComponent<PetBoxPanelController>();
            petBoxPanelController.Bind(document, this);
        }

        void EnsurePetSkillsPanelController() {
            petSkillsPanelController = GetComponent<PetSkillsPanelController>();
            if (!Application.isPlaying || !autoInstallPetSkillsPanelController || document == null) return;

            if (petSkillsPanelController == null) petSkillsPanelController = gameObject.AddComponent<PetSkillsPanelController>();
            petSkillsPanelController.Bind(document);
        }

        void EnsurePetLevelUpPanelController() {
            petLevelUpPanelController = GetComponent<PetLevelUpPanelController>();
            if (!Application.isPlaying || !autoInstallPetLevelUpPanelController || document == null) return;

            if (petLevelUpPanelController == null) petLevelUpPanelController = gameObject.AddComponent<PetLevelUpPanelController>();
            petLevelUpPanelController.Bind(document, adapter);
        }

        void EnsurePetHealPanelController() {
            petHealPanelController = GetComponent<PetHealPanelController>();
            if (!Application.isPlaying || !autoInstallPetHealPanelController || document == null) return;

            if (petHealPanelController == null) petHealPanelController = gameObject.AddComponent<PetHealPanelController>();
            petHealPanelController.Bind(document, adapter);
        }

        void EnsurePetEvolutionPanelController() {
            petEvolutionPanelController = GetComponent<PetEvolutionPanelController>();
            if (!Application.isPlaying || !autoInstallPetEvolutionPanelController || document == null) return;

            if (petEvolutionPanelController == null) petEvolutionPanelController = gameObject.AddComponent<PetEvolutionPanelController>();
            petEvolutionPanelController.Bind(document, adapter);
        }

        void EnsurePetReleasePanelController() {
            petReleasePanelController = GetComponent<PetReleasePanelController>();
            if (!Application.isPlaying || !autoInstallPetReleasePanelController || document == null) return;

            if (petReleasePanelController == null) petReleasePanelController = gameObject.AddComponent<PetReleasePanelController>();
            petReleasePanelController.Bind(document, adapter);
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) {
                Debug.LogWarning("MonsterInventoryController needs a UIDocument.");
                return;
            }

            var rootElement = document.rootVisualElement;
            root = rootElement.Q<VisualElement>("monster-inventory-root");
            questInventoryShell = rootElement.Q<VisualElement>("quest-inventory-shell");
            questPanel = rootElement.Q<VisualElement>("quest-panel");
            inventoryPanel = rootElement.Q<VisualElement>("inventory-panel");
            petsPanel = rootElement.Q<VisualElement>("pets-panel");
            boxPanel = rootElement.Q<VisualElement>("box-panel");
            placeholderPanel = rootElement.Q<VisualElement>("menu-placeholder-panel");
            placeholderTitle = rootElement.Q<Label>("menu-placeholder-title");
            placeholderBody = rootElement.Q<Label>("menu-placeholder-body");
            categoryPanel = rootElement.Q<VisualElement>("category-panel");
            toolbarSidebarSpacer = rootElement.Q<VisualElement>(className: "inventory-sidebar-spacer");
            itemScroll = rootElement.Q<ScrollView>("item-scroll");
            itemGrid = rootElement.Q<VisualElement>("item-grid");
            noItemsLabel = rootElement.Q<Label>("no-items-label");
            capacityLabel = rootElement.Q<Label>("capacity-label");
            detailIcon = rootElement.Q<VisualElement>("detail-icon");
            detailIconPlaceholder = rootElement.Q<Label>("detail-icon-placeholder");
            detailName = rootElement.Q<Label>("detail-name");
            detailRarity = rootElement.Q<Label>("detail-rarity");
            detailCategory = rootElement.Q<Label>("detail-category");
            detailQuantity = rootElement.Q<Label>("detail-quantity");
            detailDescription = rootElement.Q<Label>("detail-description");
            detailEffect = rootElement.Q<Label>("detail-effect");
            detailSource = rootElement.Q<Label>("detail-source");
            detailFlavor = rootElement.Q<Label>("detail-flavor");
            goldLabel = rootElement.Q<Label>("inventory-gold-label");
            useButton = rootElement.Q<Button>("use-button");
            giveButton = rootElement.Q<Button>("give-button");
            equipButton = rootElement.Q<Button>("equip-button");
            dropButton = rootElement.Q<Button>("drop-button");
            cancelButton = rootElement.Q<Button>("cancel-button");
            filterButton = rootElement.Q<Button>("inventory-filter-button");
            filterButton?.EnableInClassList("is-selected", categoriesVisible);
            searchField = rootElement.Q<TextField>("inventory-search-field");
            sortField = rootElement.Q<DropdownField>("inventory-sort-field");
            if (root != null) quantityPopup = new ItemQuantityPopup(root);
        }

        void RegisterCategoryButtons() {
            if (document == null || document.rootVisualElement == null) return;
            if (controlsRegistered && controlsRoot == document.rootVisualElement) return;
            if (controlsRoot != document.rootVisualElement) controlsRegistered = false;

            categoryButtons.Clear();
            RegisterCategoryButton("category-all", GameItemCategory.All);
            RegisterCategoryButton("category-capture-ball", GameItemCategory.CaptureBall);
            RegisterCategoryButton("category-medicine", GameItemCategory.Medicine);
            RegisterCategoryButton("category-food", GameItemCategory.Food);
            RegisterCategoryButton("category-material", GameItemCategory.Material);
            RegisterCategoryButton("category-equipment", GameItemCategory.Equipment);
            RegisterCategoryButton("category-key-item", GameItemCategory.KeyItem);
            RegisterCategoryButton("category-quest-item", GameItemCategory.QuestItem);
        }

        void RegisterCategoryButton(string buttonName, GameItemCategory category) {
            var button = document.rootVisualElement.Q<Button>(buttonName);
            if (button == null) return;

            categoryButtons.Add(button);
            button.clicked += () => SelectCategory(category);
        }

        void RegisterActionButtons() {
            if (document == null || document.rootVisualElement == null) return;
            if (controlsRegistered && controlsRoot == document.rootVisualElement) return;

            if (useButton != null) useButton.clicked += () => RequestAction(InventoryActionType.UseItem);
            if (giveButton != null) giveButton.clicked += () => RequestAction(InventoryActionType.GiveItem);
            if (equipButton != null) equipButton.clicked += () => RequestAction(equipSlotActionType);
            if (dropButton != null) dropButton.clicked += () => RequestAction(InventoryActionType.DropItem);
            if (cancelButton != null) cancelButton.clicked += Close;
            if (filterButton != null) filterButton.clicked += ToggleCategoryPanel;
            if (searchField != null) {
                searchField.textEdition.placeholder = "Search items...";
                searchField.RegisterValueChangedCallback(evt => {
                    searchQuery = evt.newValue ?? string.Empty;
                    RefreshFromAdapter();
                });
            }
            if (sortField != null) {
                sortField.choices = SortChoices.ToList();
                sortField.index = 0;
                sortField.RegisterValueChangedCallback(evt => {
                    int index = SortChoices.IndexOf(evt.newValue);
                    sortMode = index >= 0 ? (InventorySortMode)index : InventorySortMode.Type;
                    RefreshFromAdapter();
                });
            }

            controlsRegistered = true;
            controlsRoot = document.rootVisualElement;
        }

        void SetupItemGrid() {
            if (itemGrid == null) return;
            itemGrid.Clear();
            itemCards.Clear();
        }

        void SubscribeToAdapter() {
            if (adapter == null) {
                Debug.LogWarning("MonsterInventoryController could not find a MonsterInventoryAdapter.");
                return;
            }

            adapter.ItemsChanged -= HandleInventoryChanged;
            adapter.ItemsChanged += HandleInventoryChanged;
        }

        void RebuildItemGrid() {
            if (itemGrid == null) return;

            itemGrid.Clear();
            itemCards.Clear();
            for (int index = 0; index < filteredItems.Count; index++) {
                int itemIndex = index;
                InventoryItemSnapshot item = filteredItems[index];
                var card = new Button { name = $"inventory-item-{index}" };
                card.AddToClassList("inventory-item-card");
                card.tooltip = item.Name;

                var icon = new VisualElement();
                icon.AddToClassList("inventory-item-icon");
                icon.pickingMode = PickingMode.Ignore;
                SetIcon(icon, item.Icon, SmallIconColor);

                var placeholder = new Label(GetItemPlaceholder(item));
                placeholder.AddToClassList("inventory-item-placeholder");
                placeholder.pickingMode = PickingMode.Ignore;
                placeholder.style.display = item.Icon == null ? DisplayStyle.Flex : DisplayStyle.None;
                icon.Add(placeholder);

                var nameLabel = new Label(item.Name);
                nameLabel.AddToClassList("inventory-item-name");
                nameLabel.pickingMode = PickingMode.Ignore;

                var quantityLabel = new Label(item.Quantity.ToString());
                quantityLabel.AddToClassList("inventory-item-quantity");
                quantityLabel.pickingMode = PickingMode.Ignore;

                card.Add(icon);
                card.Add(nameLabel);
                card.Add(quantityLabel);
                card.clicked += () => SelectItem(itemIndex, true);
                itemGrid.Add(card);
                itemCards.Add(card);
            }

            RefreshItemCardSelection();
        }

        void SelectItem(int index, bool scrollIntoView) {
            if (index < 0 || index >= filteredItems.Count) {
                selectedItemIndex = -1;
                selectedItem = null;
            }
            else {
                selectedItemIndex = index;
                selectedItem = filteredItems[index];
            }

            RefreshItemCardSelection();
            RefreshDetails();
            if (scrollIntoView && selectedItemIndex >= 0 && selectedItemIndex < itemCards.Count) {
                itemScroll?.ScrollTo(itemCards[selectedItemIndex]);
            }
        }

        void RefreshItemCardSelection() {
            for (int index = 0; index < itemCards.Count; index++) {
                itemCards[index].EnableInClassList("is-selected", index == selectedItemIndex);
            }
        }

        void HandleInventoryChanged(IReadOnlyList<InventoryItemSnapshot> _) {
            RefreshFromAdapter();
        }

        void SelectCategory(GameItemCategory category) {
            currentCategory = category;
            UpdateCategoryButtons();
            RefreshFromAdapter();
        }

        public void Open() {
            OpenInventoryPanel();
        }

        public void OpenInventoryPanel() {
            OpenContentSection(MenuSection.Bag);
        }

        public void OpenQuestJournalPanel() {
            OpenContentSection(MenuSection.Journal);
        }

        public void OpenPetPartyPanel() {
            OpenContentSection(MenuSection.Party);
        }

        public void OpenPetBoxPanel(bool returnToPets = false) {
            if (petBoxPanelController == null) EnsurePetBoxPanelController();
            petBoxPanelController?.OpenFrom(returnToPets ? PetBoxReturnTarget.Pets : PetBoxReturnTarget.Menu);
            OpenContentSection(MenuSection.Box);
        }

        public void OpenPetSkillsPanel() {
            if (activeSection != MenuSection.Party) OpenContentSection(MenuSection.Party);
            petPartyPanelController?.CloseRenamePopup();
            petLevelUpPanelController?.Close();
            petHealPanelController?.Close();
            petEvolutionPanelController?.Close();
            petReleasePanelController?.Close();
            petPartyPanelController?.CloseDetails();
            if (petSkillsPanelController == null) EnsurePetSkillsPanelController();
            petSkillsPanelController?.Open();
        }

        public void OpenPetLevelUpPanel(PetController pet = null) {
            if (activeSection != MenuSection.Party) OpenContentSection(MenuSection.Party);
            petPartyPanelController?.CloseRenamePopup();
            petSkillsPanelController?.Close();
            petHealPanelController?.Close();
            petEvolutionPanelController?.Close();
            petReleasePanelController?.Close();
            petPartyPanelController?.CloseDetails();
            if (petLevelUpPanelController == null) EnsurePetLevelUpPanelController();
            petLevelUpPanelController?.Open(pet);
        }

        public void OpenPetHealPanel(PetController pet = null) {
            if (activeSection != MenuSection.Party) OpenContentSection(MenuSection.Party);
            petPartyPanelController?.CloseRenamePopup();
            petLevelUpPanelController?.Close();
            petEvolutionPanelController?.Close();
            petReleasePanelController?.Close();
            petSkillsPanelController?.Close();
            petPartyPanelController?.CloseDetails();
            if (petHealPanelController == null) EnsurePetHealPanelController();
            petHealPanelController?.Open(pet);
        }

        public void OpenPetDetailsPanel(PetController pet = null) {
            petPartyPanelController?.CloseRenamePopup();
            petSkillsPanelController?.Close();
            petLevelUpPanelController?.Close();
            petHealPanelController?.Close();
            petEvolutionPanelController?.Close();
            petReleasePanelController?.Close();
            if (petPartyPanelController == null) EnsurePetPartyPanelController();
            petPartyPanelController?.OpenDetails(pet);
        }

        public void OpenPetEvolutionPanel(PetController pet = null) {
            if (activeSection != MenuSection.Party) OpenContentSection(MenuSection.Party);
            petPartyPanelController?.CloseRenamePopup();
            petSkillsPanelController?.Close();
            petLevelUpPanelController?.Close();
            petHealPanelController?.Close();
            petReleasePanelController?.Close();
            petPartyPanelController?.CloseDetails();
            if (petEvolutionPanelController == null) EnsurePetEvolutionPanelController();
            petEvolutionPanelController?.Open(pet);
        }

        public void OpenPetReleasePanel(PetController pet = null) {
            if (activeSection != MenuSection.Party) OpenContentSection(MenuSection.Party);
            petPartyPanelController?.CloseRenamePopup();
            petSkillsPanelController?.Close();
            petLevelUpPanelController?.Close();
            petHealPanelController?.Close();
            petEvolutionPanelController?.Close();
            petPartyPanelController?.CloseDetails();
            if (petReleasePanelController == null) EnsurePetReleasePanelController();
            petReleasePanelController?.Open(pet);
        }

        void OpenContentSection(MenuSection section) {
            if (root == null) CacheElements();
            if (root == null) return;

            root.style.display = DisplayStyle.Flex;
            SetActiveSection(section);
            RefreshFromAdapter();
            VisibilityChanged?.Invoke(true);
        }

        public void Close() {
            if (root == null) CacheElements();
            if (root == null) return;

            quantityPopup?.Hide();
            petBoxPanelController?.CloseTransientUi();
            petSkillsPanelController?.Close();
            petLevelUpPanelController?.Close();
            petHealPanelController?.Close();
            petEvolutionPanelController?.Close();
            petReleasePanelController?.Close();
            petPartyPanelController?.CloseRenamePopup();
            petPartyPanelController?.CloseDetails();
            root.style.display = DisplayStyle.None;
            VisibilityChanged?.Invoke(false);
        }

        public bool TryCloseTopmostPanel() {
            if (petPartyPanelController != null && petPartyPanelController.IsRenameOpen) {
                petPartyPanelController.CloseRenamePopup();
                return true;
            }
            if (petReleasePanelController != null && petReleasePanelController.IsOpen) {
                petReleasePanelController.Close();
                return true;
            }
            if (petEvolutionPanelController != null && petEvolutionPanelController.IsOpen) {
                petEvolutionPanelController.Close();
                return true;
            }
            if (petHealPanelController != null && petHealPanelController.IsOpen) {
                petHealPanelController.Close();
                return true;
            }
            if (petLevelUpPanelController != null && petLevelUpPanelController.IsOpen) {
                petLevelUpPanelController.Close();
                return true;
            }
            if (petSkillsPanelController != null && petSkillsPanelController.IsOpen) {
                petSkillsPanelController.Close();
                return true;
            }
            if (petPartyPanelController != null && petPartyPanelController.IsDetailsOpen) {
                petPartyPanelController.CloseDetails();
                return true;
            }

            return false;
        }

        public void Toggle() {
            if (IsOpen) Close();
            else Open();
        }

        void ShowPlaceholderSection(MenuSection section) {
            if (root == null) CacheElements();
            if (root == null) return;

            root.style.display = DisplayStyle.Flex;
            SetActiveSection(section);
            VisibilityChanged?.Invoke(true);
        }

        void SetActiveSection(MenuSection section) {
            petPartyPanelController?.CloseRenamePopup();
            petPartyPanelController?.CloseDetails();
            activeSection = section;
            bool showInventoryContent = section == MenuSection.Journal || section == MenuSection.Bag;
            bool showPetParty = section == MenuSection.Party;
            bool showPetBox = section == MenuSection.Box;

            if (!showPetBox) petBoxPanelController?.CloseTransientUi();
            if (!showPetParty) petSkillsPanelController?.Close();
            if (!showPetParty) petLevelUpPanelController?.Close();
            if (!showPetParty) petHealPanelController?.Close();
            if (!showPetParty) petEvolutionPanelController?.Close();
            if (!showPetParty) petReleasePanelController?.Close();

            SetVisible(questInventoryShell, showInventoryContent);
            SetVisible(petsPanel, showPetParty);
            SetVisible(boxPanel, showPetBox);
            SetVisible(placeholderPanel, !showInventoryContent && !showPetParty && !showPetBox);

            if (questInventoryShell != null) {
                questInventoryShell.EnableInClassList("journal-mode", section == MenuSection.Journal);
                questInventoryShell.EnableInClassList("bag-mode", section == MenuSection.Bag);
            }

            SetVisible(questPanel, section == MenuSection.Journal);
            SetVisible(inventoryPanel, section == MenuSection.Bag);

            if (showPetParty) {
                if (petPartyPanelController == null) EnsurePetPartyPanelController();
                petPartyPanelController?.Refresh();
            }

            if (showPetBox) {
                if (petBoxPanelController == null) EnsurePetBoxPanelController();
                petBoxPanelController?.Refresh();
            }

            if (!showInventoryContent && !showPetParty && !showPetBox) {
                string title = GetSectionTitle(section);
                if (placeholderTitle != null) placeholderTitle.text = title;
                if (placeholderBody != null) {
                    placeholderBody.text = title + " panel is a placeholder. Connect the real gameplay backend later.";
                }
            }
        }

        public void SelectNextCategory() {
            SelectCategory(GetShiftedCategory(1));
        }

        public void SelectPreviousCategory() {
            SelectCategory(GetShiftedCategory(-1));
        }

        public void SelectNextItem() {
            ShiftSelectedItem(1);
        }

        public void SelectPreviousItem() {
            ShiftSelectedItem(-1);
        }

        public void ConfirmSelected() {
            if (selectedItem == null) return;
            if (TryGetDefaultAction(selectedItem, out var actionType)) RequestAction(actionType);
        }

        public void SetSelectedTargetMonster(string targetMonsterId) {
            selectedTargetMonsterId = targetMonsterId ?? string.Empty;
            RefreshActionButtons();
        }

        public void ClearSelectedTargetMonster() {
            SetSelectedTargetMonster(string.Empty);
        }

        void RefreshFromAdapter() {
            ItemBase previousItemBase = selectedItem?.ItemBase;
            int previousIndex = selectedItemIndex;

            filteredItems.Clear();
            if (adapter != null) {
                IEnumerable<InventoryItemSnapshot> items = adapter.GetItems(currentCategory);
                if (!string.IsNullOrWhiteSpace(searchQuery)) {
                    string expected = searchQuery.Trim();
                    items = items.Where(item => MatchesSearch(item, expected));
                }
                filteredItems.AddRange(SortItems(items));
            }

            string capacityText = adapter != null ? $"{adapter.OccupiedSlotCount} / {adapter.Capacity}" : "0 / 0";
            if (capacityLabel != null) capacityLabel.text = capacityText;
            SubscribeToCurrencyWallet();
            RefreshGold();

            var hasItems = filteredItems.Count > 0;
            int nextSelectedIndex = hasItems ? GetNextSelectionIndex(previousItemBase, previousIndex) : -1;
            if (noItemsLabel != null) noItemsLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;

            selectedItemIndex = nextSelectedIndex;
            selectedItem = selectedItemIndex >= 0 ? filteredItems[selectedItemIndex] : null;
            RebuildItemGrid();
            RefreshDetails();
        }

        int GetNextSelectionIndex(ItemBase previousItemBase, int previousIndex) {
            if (filteredItems.Count == 0) return -1;

            if (previousItemBase != null) {
                int sameItemIndex = filteredItems.FindIndex(item => item.ItemBase == previousItemBase);
                if (sameItemIndex >= 0) return sameItemIndex;
            }

            return Mathf.Clamp(previousIndex, 0, filteredItems.Count - 1);
        }

        IEnumerable<InventoryItemSnapshot> SortItems(IEnumerable<InventoryItemSnapshot> items) {
            if (items == null) return Enumerable.Empty<InventoryItemSnapshot>();

            switch (sortMode) {
                case InventorySortMode.NameAscending:
                    return items.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
                case InventorySortMode.NameDescending:
                    return items.OrderByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
                case InventorySortMode.QuantityDescending:
                    return items.OrderByDescending(item => item.Quantity).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
                case InventorySortMode.RarityDescending:
                    return items.OrderByDescending(item => item.Rarity).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
                default:
                    return items.OrderBy(item => item.Category).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
            }
        }

        static bool MatchesSearch(InventoryItemSnapshot item, string expected) {
            if (item == null || string.IsNullOrWhiteSpace(expected)) return true;
            return ContainsIgnoreCase(item.Name, expected)
                || ContainsIgnoreCase(item.ItemId, expected)
                || ContainsIgnoreCase(FormatCategory(item.Category), expected);
        }

        static bool ContainsIgnoreCase(string value, string expected) {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(expected, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        static string GetItemPlaceholder(InventoryItemSnapshot item) {
            if (item == null) return "ITEM";
            switch (item.Category) {
                case GameItemCategory.CaptureBall: return "CAP";
                case GameItemCategory.Medicine: return "HP";
                case GameItemCategory.Food: return "FOOD";
                case GameItemCategory.Material: return "MAT";
                case GameItemCategory.Equipment: return "GEAR";
                case GameItemCategory.KeyItem: return "KEY";
                case GameItemCategory.QuestItem: return "QUEST";
                default: return "ITEM";
            }
        }

        void ToggleCategoryPanel() {
            categoriesVisible = !categoriesVisible;
            SetVisible(categoryPanel, categoriesVisible);
            SetVisible(toolbarSidebarSpacer, categoriesVisible);
            filterButton?.EnableInClassList("is-selected", categoriesVisible);
        }

        void SubscribeToCurrencyWallet() {
            ResolveReferences();
            if (ReferenceEquals(subscribedCurrencyWallet, currencyWallet)) return;

            UnsubscribeFromCurrencyWallet();
            subscribedCurrencyWallet = currencyWallet;
            if (subscribedCurrencyWallet != null) subscribedCurrencyWallet.GoldChanged += HandleGoldChanged;
        }

        void UnsubscribeFromCurrencyWallet() {
            if (subscribedCurrencyWallet != null) subscribedCurrencyWallet.GoldChanged -= HandleGoldChanged;
            subscribedCurrencyWallet = null;
        }

        void HandleGoldChanged(int value) {
            if (goldLabel != null) goldLabel.text = Mathf.Max(0, value).ToString("N0");
        }

        void RefreshGold() {
            if (goldLabel != null) goldLabel.text = currencyWallet != null ? currencyWallet.Gold.ToString("N0") : "0";
        }

        void RefreshDetails() {
            var hasItem = selectedItem != null;
            RefreshActionButtons();

            if (!hasItem) {
                SetIcon(detailIcon, null, DetailPlaceholderColor);
                if (detailIconPlaceholder != null) {
                    detailIconPlaceholder.text = "ITEM";
                    detailIconPlaceholder.style.display = DisplayStyle.Flex;
                }
                if (detailName != null) detailName.text = "Chọn một item";
                if (detailRarity != null) detailRarity.text = "COMMON";
                if (detailCategory != null) detailCategory.text = "Category: -";
                if (detailQuantity != null) detailQuantity.text = "Owned: - / -";
                if (detailDescription != null) detailDescription.text = "Chọn một item để xem thông tin.";
                if (detailEffect != null) detailEffect.text = string.Empty;
                if (detailSource != null) detailSource.text = "-";
                if (detailFlavor != null) detailFlavor.text = string.Empty;
                return;
            }

            SetIcon(detailIcon, selectedItem.Icon, DetailPlaceholderColor);
            if (detailIconPlaceholder != null) {
                detailIconPlaceholder.text = GetItemPlaceholder(selectedItem);
                detailIconPlaceholder.style.display = selectedItem.Icon == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (detailName != null) detailName.text = selectedItem.Name;
            if (detailRarity != null) detailRarity.text = FormatRarity(selectedItem.Rarity).ToUpperInvariant();
            if (detailCategory != null) detailCategory.text = $"Category: {FormatCategory(selectedItem.Category)}";
            if (detailQuantity != null) {
                int limit = selectedItem.Stackable ? Mathf.Max(1, selectedItem.MaxStackSize) : selectedItem.Quantity;
                detailQuantity.text = $"Owned: {selectedItem.Quantity} / {limit}";
            }
            if (detailDescription != null) detailDescription.text = string.IsNullOrWhiteSpace(selectedItem.Description) ? "Chưa có mô tả." : selectedItem.Description;
            if (detailEffect != null) detailEffect.text = string.IsNullOrWhiteSpace(selectedItem.Effect) ? string.Empty : selectedItem.Effect;
            if (detailSource != null) detailSource.text = string.IsNullOrWhiteSpace(selectedItem.Source) ? "-" : selectedItem.Source;
            if (detailFlavor != null) detailFlavor.text = string.IsNullOrWhiteSpace(selectedItem.FlavorText) ? string.Empty : $"\"{selectedItem.FlavorText}\"";
        }

        void ShiftSelectedItem(int direction) {
            if (filteredItems.Count == 0) return;

            var currentIndex = selectedItemIndex;
            if (currentIndex < 0) currentIndex = 0;

            var nextIndex = Mathf.Clamp(currentIndex + direction, 0, filteredItems.Count - 1);
            SelectItem(nextIndex, true);
        }

        GameItemCategory GetShiftedCategory(int direction) {
            int currentIndex = CategoryOrder.IndexOf(currentCategory);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = (currentIndex + direction + CategoryOrder.Count) % CategoryOrder.Count;
            return CategoryOrder[nextIndex];
        }

        void RefreshActionButtons() {
            ResetActionButton(useButton, "USE");
            ResetActionButton(giveButton, "GIVE");
            ResetActionButton(dropButton, "DROP");
            ResetActionButton(equipButton, "ASSIGN");

            if (selectedItem == null) return;

            if (IsSelectedPetEvolutionItem(selectedItem)) {
                ConfigureActionButton(useButton, InventoryActionType.UseItem, "EVOLVE");
                if (!IsProtectedDropItem(selectedItem)) {
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "DROP");
                }
                return;
            }

            switch (selectedItem.Category) {
                case GameItemCategory.Medicine:
                case GameItemCategory.Food:
                    ConfigureActionButton(useButton, InventoryActionType.UseItem, "USE");
                    ConfigureActionButton(giveButton, InventoryActionType.GiveItem, "GIVE");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "DROP");
                    break;
                case GameItemCategory.CaptureBall:
                    equipSlotActionType = InventoryActionType.AssignQuickSlot;
                    ConfigureActionButton(equipButton, InventoryActionType.AssignQuickSlot, "ASSIGN");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "DROP");
                    break;
                case GameItemCategory.Material:
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "DROP");
                    break;
                case GameItemCategory.Equipment:
                    equipSlotActionType = InventoryActionType.EquipItem;
                    ConfigureActionButton(equipButton, InventoryActionType.EquipItem, "EQUIP");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "DROP");
                    break;
                case GameItemCategory.KeyItem:
                    if (SupportsUse(selectedItem)) ConfigureActionButton(useButton, InventoryActionType.UseItem, "USE");
                    break;
                case GameItemCategory.QuestItem:
                    break;
            }
        }

        static void ResetActionButton(Button button, string label) {
            if (button == null) return;
            SetButtonVisible(button, true);
            button.text = label;
            button.SetEnabled(false);
        }

        void ConfigureActionButton(Button button, InventoryActionType actionType, string label) {
            if (button == null) return;

            SetButtonVisible(button, true);
            button.text = label;
            button.SetEnabled(IsActionAvailable(actionType, selectedItem));
        }

        static void SetButtonVisible(Button button, bool visible) {
            if (button == null) return;

            button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        bool IsActionAvailable(InventoryActionType actionType, InventoryItemSnapshot item) {
            if (item == null || !IsActionAllowed(actionType, item)) return false;
            if (actionType == InventoryActionType.UseItem && IsSelectedPetEvolutionItem(item)) return true;
            return allowPlaceholderActions || HasGameplayHandler(actionType);
        }

        bool IsActionAllowed(InventoryActionType actionType, InventoryItemSnapshot item) {
            if (item == null) return false;
            if (actionType == InventoryActionType.UseItem && IsSelectedPetEvolutionItem(item)) return true;

            switch (actionType) {
                case InventoryActionType.UseItem:
                    return item.Category == GameItemCategory.Medicine
                        || item.Category == GameItemCategory.Food
                        || (item.Category == GameItemCategory.KeyItem && SupportsUse(item));
                case InventoryActionType.GiveItem:
                    return HasSelectedTargetMonster()
                        && (item.Category == GameItemCategory.Medicine || item.Category == GameItemCategory.Food);
                case InventoryActionType.EquipItem:
                    return item.Category == GameItemCategory.Equipment;
                case InventoryActionType.AssignQuickSlot:
                    return item.Category == GameItemCategory.CaptureBall;
                case InventoryActionType.DropItem:
                    return !IsProtectedDropItem(item);
                default:
                    return false;
            }
        }

        bool IsSelectedPetEvolutionItem(InventoryItemSnapshot item) {
            return petEvolutionPanelController != null
                && petEvolutionPanelController.CanUseForSelectedPet(item);
        }

        bool TryGetDefaultAction(InventoryItemSnapshot item, out InventoryActionType actionType) {
            actionType = InventoryActionType.UseItem;
            if (item == null) return false;

            switch (item.Category) {
                case GameItemCategory.Medicine:
                case GameItemCategory.Food:
                    actionType = InventoryActionType.UseItem;
                    return true;
                case GameItemCategory.CaptureBall:
                    actionType = InventoryActionType.AssignQuickSlot;
                    return true;
                case GameItemCategory.Material:
                    actionType = InventoryActionType.DropItem;
                    return true;
                case GameItemCategory.Equipment:
                    actionType = InventoryActionType.EquipItem;
                    return true;
                case GameItemCategory.KeyItem:
                    if (!SupportsUse(item)) return false;
                    actionType = InventoryActionType.UseItem;
                    return true;
                case GameItemCategory.QuestItem:
                    return false;
                default:
                    return false;
            }
        }

        static bool SupportsUse(InventoryItemSnapshot item) {
            return item != null && item.UsableFromInventory;
        }

        bool HasSelectedTargetMonster() {
            return !string.IsNullOrWhiteSpace(selectedTargetMonsterId);
        }

        static bool IsProtectedDropItem(InventoryItemSnapshot item) {
            if (item == null) return true;

            return item.Category == GameItemCategory.KeyItem || item.Category == GameItemCategory.QuestItem;
        }

        void RequestAction(InventoryActionType actionType) {
            if (selectedItem == null || !IsActionAvailable(actionType, selectedItem)) return;

            if (NeedsQuantityPopup(actionType)) {
                OpenQuantityPopup(actionType);
                return;
            }

            RequestAction(actionType, 1);
        }

        void OpenQuantityPopup(InventoryActionType actionType) {
            if (selectedItem == null) return;

            if (actionType == InventoryActionType.GiveItem && !HasSelectedTargetMonster()) {
                Debug.LogWarning("Give item needs a selected target monster.");
                return;
            }

            if (quantityPopup == null) {
                if (root == null) CacheElements();
                if (root == null) return;
                quantityPopup = new ItemQuantityPopup(root);
            }

            quantityPopup.Show(GetQuantityPopupTitle(actionType), selectedItem, quantity => RequestAction(actionType, quantity));
        }

        void RequestAction(InventoryActionType actionType, int quantity) {
            if (selectedItem == null || !IsActionAvailable(actionType, selectedItem)) return;
            if (actionType == InventoryActionType.UseItem
                && petEvolutionPanelController != null
                && petEvolutionPanelController.TryOpenFromInventory(selectedItem)) return;

            bool removeByDefault = ShouldRemoveItemByDefault(actionType, selectedItem);
            int safeQuantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, selectedItem.Quantity));
            int removeQuantity = removeByDefault ? safeQuantity : 0;
            var targetMonsterId = actionType == InventoryActionType.GiveItem ? selectedTargetMonsterId : null;
            var request = new InventoryActionRequest(actionType, selectedItem, removeByDefault, removeQuantity, targetMonsterId);
            bool hasHandler = DispatchActionRequest(request);

            if (!hasHandler) {
                Debug.LogWarning($"Inventory action placeholder: {FormatAction(actionType)} has no gameplay handler for {selectedItem.Name}.");
                request.Complete(false, false, 0, "No gameplay handler registered.");
            }

            if (!request.IsCompleted) {
                Debug.LogWarning($"Inventory action did not report a result: {FormatAction(actionType)} / {selectedItem.Name}. Item quantity was not changed.");
                return;
            }

            if (!request.Success) {
                if (!string.IsNullOrWhiteSpace(request.Message)) Debug.Log(request.Message);
                return;
            }

            if (request.RemoveItemOnSuccess && request.RemoveQuantity > 0) {
                RemoveItemAfterSuccessfulAction(request);
            } else {
                RefreshFromAdapter();
            }

            ActionCompleted?.Invoke(request);
        }

        static bool NeedsQuantityPopup(InventoryActionType actionType) {
            return actionType == InventoryActionType.DropItem || actionType == InventoryActionType.GiveItem;
        }

        static string GetQuantityPopupTitle(InventoryActionType actionType) {
            switch (actionType) {
                case InventoryActionType.GiveItem: return "Give Item";
                case InventoryActionType.DropItem: return "Drop Item";
                default: return FormatAction(actionType);
            }
        }

        bool DispatchActionRequest(InventoryActionRequest request) {
            switch (request.ActionType) {
                case InventoryActionType.UseItem:
                    if (UseItem == null) return false;
                    UseItem.Invoke(request);
                    return true;
                case InventoryActionType.GiveItem:
                    if (RequestGiveItem != null) {
                        RequestGiveItem.Invoke(request.TargetMonsterId, request.ItemId, request.RemoveQuantity, request);
                        return true;
                    }

                    if (GiveItem == null) return false;
                    GiveItem.Invoke(request);
                    return true;
                case InventoryActionType.EquipItem:
                    if (EquipItem == null) return false;
                    EquipItem.Invoke(request);
                    return true;
                case InventoryActionType.AssignQuickSlot:
                    if (AssignQuickSlot == null) return false;
                    AssignQuickSlot.Invoke(request);
                    return true;
                case InventoryActionType.DropItem:
                    if (RequestDropItem != null) {
                        RequestDropItem.Invoke(request.ItemId, request.RemoveQuantity, request);
                        return true;
                    }

                    if (DropItem != null) DropItem.Invoke(request);
                    else request.Complete(true, true, request.RemoveQuantity, "Item đã được bỏ khỏi Bag.");
                    return true;
                default:
                    return false;
            }
        }

        bool HasGameplayHandler(InventoryActionType actionType) {
            switch (actionType) {
                case InventoryActionType.UseItem: return UseItem != null;
                case InventoryActionType.GiveItem: return RequestGiveItem != null || GiveItem != null;
                case InventoryActionType.EquipItem: return EquipItem != null;
                case InventoryActionType.AssignQuickSlot: return AssignQuickSlot != null;
                case InventoryActionType.DropItem: return true;
                default: return false;
            }
        }

        static bool ShouldRemoveItemByDefault(InventoryActionType actionType, InventoryItemSnapshot item) {
            if (item == null) return false;

            switch (actionType) {
                case InventoryActionType.UseItem:
                    return item.Consumable;
                case InventoryActionType.GiveItem:
                case InventoryActionType.DropItem:
                    return true;
                default:
                    return false;
            }
        }

        void RemoveItemAfterSuccessfulAction(InventoryActionRequest request) {
            if (adapter == null || request.Item == null || request.Item.ItemBase == null) {
                Debug.LogWarning($"Inventory action succeeded but item could not be removed: {FormatAction(request.ActionType)}.");
                RefreshFromAdapter();
                return;
            }

            var result = adapter.RemoveItem(request.Item.ItemBase, request.RemoveQuantity);
            if (result is Fail) {
                Debug.LogWarning($"Inventory action succeeded but RemoveItem failed: {request.Item.Name} x{request.RemoveQuantity}.");
            }

            RefreshFromAdapter();
        }

        static string FormatAction(InventoryActionType actionType) {
            switch (actionType) {
                case InventoryActionType.UseItem: return "UseItem";
                case InventoryActionType.GiveItem: return "GiveItem";
                case InventoryActionType.EquipItem: return "EquipItem";
                case InventoryActionType.AssignQuickSlot: return "AssignQuickSlot";
                case InventoryActionType.DropItem: return "DropItem";
                default: return actionType.ToString();
            }
        }

        void UpdateCategoryButtons() {
            foreach (var button in categoryButtons) {
                button.RemoveFromClassList("is-selected");
            }

            var selectedButton = GetCategoryButton(currentCategory);
            if (selectedButton != null) selectedButton.AddToClassList("is-selected");
        }

        static void SetPanelFocus(VisualElement panel, bool focused) {
            if (panel == null) return;

            panel.EnableInClassList("is-active-panel", focused);
            panel.EnableInClassList("is-passive-panel", !focused);
        }

        static void SetVisible(VisualElement element, bool visible) {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string GetSectionTitle(MenuSection section) {
            switch (section) {
                case MenuSection.Party: return "Party";
                case MenuSection.Map: return "Map";
                case MenuSection.Settings: return "Settings";
                case MenuSection.Journal: return "Quest Journal";
                case MenuSection.Box: return "Box";
                default: return "Bag";
            }
        }

        Button GetCategoryButton(GameItemCategory category) {
            if (document == null || document.rootVisualElement == null) return null;

            switch (category) {
                case GameItemCategory.CaptureBall: return document.rootVisualElement.Q<Button>("category-capture-ball");
                case GameItemCategory.Medicine: return document.rootVisualElement.Q<Button>("category-medicine");
                case GameItemCategory.Food: return document.rootVisualElement.Q<Button>("category-food");
                case GameItemCategory.Material: return document.rootVisualElement.Q<Button>("category-material");
                case GameItemCategory.Equipment: return document.rootVisualElement.Q<Button>("category-equipment");
                case GameItemCategory.KeyItem: return document.rootVisualElement.Q<Button>("category-key-item");
                case GameItemCategory.QuestItem: return document.rootVisualElement.Q<Button>("category-quest-item");
                default: return document.rootVisualElement.Q<Button>("category-all");
            }
        }

        static void SetIcon(VisualElement target, Sprite sprite, Color placeholderColor) {
            if (target == null) return;

            target.style.backgroundColor = placeholderColor;
            if (sprite != null) {
                target.style.backgroundImage = new StyleBackground(sprite);
            } else {
                target.style.backgroundImage = StyleKeyword.None;
            }
        }

        static string FormatCategory(GameItemCategory category) {
            switch (category) {
                case GameItemCategory.CaptureBall: return "Capture Tools";
                case GameItemCategory.Medicine: return "Recovery";
                case GameItemCategory.Food: return "Food";
                case GameItemCategory.Material: return "Materials";
                case GameItemCategory.Equipment: return "Gear";
                case GameItemCategory.KeyItem: return "Key Items";
                case GameItemCategory.QuestItem: return "Quest Items";
                default: return "All";
            }
        }

        static string FormatRarity(InventoryItemRarity rarity) {
            switch (rarity) {
                case InventoryItemRarity.Uncommon: return "Uncommon";
                case InventoryItemRarity.Rare: return "Rare";
                case InventoryItemRarity.Epic: return "Epic";
                case InventoryItemRarity.Legendary: return "Legendary";
                default: return "Common";
            }
        }

        static readonly Color SmallIconColor = new Color(0.62f, 0.55f, 0.39f);
        static readonly Color DetailPlaceholderColor = new Color(0.43f, 0.49f, 0.31f);
        static readonly List<string> SortChoices = new List<string> {
            "Sort: Type",
            "Sort: Name A-Z",
            "Sort: Name Z-A",
            "Sort: Quantity",
            "Sort: Rarity"
        };
        static readonly List<GameItemCategory> CategoryOrder = new List<GameItemCategory> {
            GameItemCategory.All,
            GameItemCategory.CaptureBall,
            GameItemCategory.Medicine,
            GameItemCategory.Food,
            GameItemCategory.Material,
            GameItemCategory.KeyItem,
            GameItemCategory.QuestItem,
            GameItemCategory.Equipment
        };

        enum InventorySortMode {
            Type,
            NameAscending,
            NameDescending,
            QuantityDescending,
            RarityDescending
        }

        enum MenuSection {
            Journal,
            Bag,
            Party,
            Box,
            Map,
            Settings
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System;
using GDS.Core;
using GDS.Core.Events;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.UI;
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
        [SerializeField] VisualTreeAsset petEvolutionScreen = null;
        [SerializeField] bool autoInstallPetReleasePanelController = true;

        readonly List<InventoryItemSnapshot> filteredItems = new List<InventoryItemSnapshot>();
        readonly List<Button> categoryButtons = new List<Button>();
        readonly List<Button> itemCards = new List<Button>();
        readonly Dictionary<Button, Action> controlCallbacks = new Dictionary<Button, Action>();

        VisualElement root;
        VisualElement questInventoryShell;
        VisualElement questPanel;
        VisualElement inventoryPanel;
        VisualElement petsPanel;
        VisualElement boxPanel;
        VisualElement codexPanel;
        Capstone.Game.CodexSystem.PetCodexPanelController codexController;
        public bool IsCodexOpen => IsOpen && activeSection == MenuSection.Codex;
        public event Action CodexBackRequested;
        VisualElement placeholderPanel;
        Label placeholderTitle;
        Label placeholderBody;
        ScrollView itemScroll;
        VisualElement itemGrid;
        Label noItemsLabel;
        Label detailEmpty;
        ScrollView detailPanel;
        VisualElement inventoryActions;
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
        TextField searchField;
        DropdownField sortField;
        DropdownField filterField;
        Button viewButton;
        bool listView;
        int filterIndex;
        ItemQuantityPopup quantityPopup;
        PetPartyPanelController petPartyPanelController;
        PetBoxPanelController petBoxPanelController;
        PetSkillsPanelController petSkillsPanelController;
        PetLevelUpPanelController petLevelUpPanelController;
        PetHealPanelController petHealPanelController;
        PetEvolutionPanelController petEvolutionPanelController;
        VisualElement bagEvolutionOverlay;
        Label bagEvolutionText;
        Label bagEvolutionFeedback;
        Button bagEvolutionConfirm;
        Button bagEvolutionCancel;
        PetController bagEvolutionPet;
        string bagEvolutionItemId;
        bool bagEvolutionComplete;
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
        public bool IsEvolutionScreenOpen => petEvolutionPanelController?.IsOpen == true;
        public bool IsPetsSectionOpen => IsOpen && activeSection == MenuSection.Party;
        public bool IsBoxSectionOpen => IsOpen && activeSection == MenuSection.Box;
        public InventoryItemSnapshot SelectedItem => selectedItem;
        public string SelectedTargetMonsterId => selectedTargetMonsterId;

        void OnEnable() {
            UnregisterControls();
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
            codexController = GetComponent<Capstone.Game.CodexSystem.PetCodexPanelController>();
            if (codexController == null) codexController = gameObject.AddComponent<Capstone.Game.CodexSystem.PetCodexPanelController>();
            codexController.Bind(document);
            codexController.BackRequested -= HandleCodexBack;
            codexController.BackRequested += HandleCodexBack;
            RegisterCategoryButtons();
            RegisterActionButtons();
            SetupItemGrid();
            SubscribeToAdapter();
            SubscribeToCurrencyWallet();
            SelectCategory(GameItemCategory.All);
        }

        void OnDisable() {
            if (codexController != null) codexController.BackRequested -= HandleCodexBack;
            Close();
            UnregisterControls();
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
            petEvolutionPanelController.Bind(document, adapter, petEvolutionScreen);
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
            // The UXML's edit-time Bag preview must not choose a screen at runtime.
            root?.RemoveFromClassList("ui-builder-preview");
            questInventoryShell = rootElement.Q<VisualElement>("quest-inventory-shell");
            questPanel = rootElement.Q<VisualElement>("quest-panel");
            inventoryPanel = rootElement.Q<VisualElement>("inventory-panel");
            petsPanel = rootElement.Q<VisualElement>("pets-panel");
            boxPanel = rootElement.Q<VisualElement>("box-panel");
            codexPanel = rootElement.Q<VisualElement>("codex-panel");
            placeholderPanel = rootElement.Q<VisualElement>("menu-placeholder-panel");
            placeholderTitle = rootElement.Q<Label>("menu-placeholder-title");
            placeholderBody = rootElement.Q<Label>("menu-placeholder-body");
            itemScroll = rootElement.Q<ScrollView>("item-scroll");
            itemGrid = rootElement.Q<VisualElement>("item-grid");
            noItemsLabel = rootElement.Q<Label>("no-items-label");
            detailEmpty = rootElement.Q<Label>("inventory-detail-empty");
            detailPanel = rootElement.Q<ScrollView>("detail-panel");
            inventoryActions = rootElement.Q<VisualElement>("inventory-actions");
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
            bagEvolutionOverlay = rootElement.Q<VisualElement>("bag-evolution-confirm-overlay");
            bagEvolutionText = rootElement.Q<Label>("bag-evolution-confirm-text");
            bagEvolutionFeedback = rootElement.Q<Label>("bag-evolution-confirm-feedback");
            bagEvolutionConfirm = rootElement.Q<Button>("bag-evolution-confirm");
            bagEvolutionCancel = rootElement.Q<Button>("bag-evolution-cancel");
            useButton = rootElement.Q<Button>("use-button");
            giveButton = rootElement.Q<Button>("give-button");
            equipButton = rootElement.Q<Button>("equip-button");
            dropButton = rootElement.Q<Button>("drop-button");
            cancelButton = rootElement.Q<Button>("cancel-button");
            searchField = rootElement.Q<TextField>("inventory-search-field");
            sortField = rootElement.Q<DropdownField>("inventory-sort-field");
            filterField = rootElement.Q<DropdownField>("inventory-filter-field");
            viewButton = rootElement.Q<Button>("inventory-view-button");
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
            RegisterControl(button, () => SelectCategory(category));
        }

        void RegisterActionButtons() {
            RegisterControl(document.rootVisualElement.Q<Button>("inventory-search-field-clear"), () => { searchField.value = string.Empty; searchField.Focus(); });
            if (document == null || document.rootVisualElement == null) return;
            if (controlsRegistered && controlsRoot == document.rootVisualElement) return;

            RegisterControl(useButton, () => RequestAction(InventoryActionType.UseItem));
            RegisterControl(giveButton, () => RequestAction(InventoryActionType.GiveItem));
            RegisterControl(equipButton, () => RequestAction(equipSlotActionType));
            RegisterControl(dropButton, () => RequestAction(InventoryActionType.DropItem));
            RegisterControl(cancelButton, RequestClose);
            RegisterControl(bagEvolutionCancel, Capstone.Game.UISystem.PlayerActionGate.Guard(this, "bag-evolution-cancel", CloseBagEvolutionConfirmation));
            RegisterControl(bagEvolutionConfirm, ConfirmBagEvolution);
            if (searchField != null) {
                searchField.textEdition.placeholder = "Tìm vật phẩm...";
                searchField.SetValueWithoutNotify(searchQuery);
                searchField.RegisterValueChangedCallback(HandleSearchChanged);
            }
            if (sortField != null) {
                sortField.choices = SortChoices.ToList();
                sortField.SetValueWithoutNotify(SortChoices[Mathf.Clamp((int)sortMode, 0, SortChoices.Count - 1)]);
                VerticalDropdownPopup.Bind(sortField);
                sortField.RegisterValueChangedCallback(HandleSortChanged);
            }

            if (filterField != null) {
                filterField.choices = new List<string> { "Lọc: không lọc", "Dùng được", "Rare trở lên", "Có thể bỏ" };
                filterField.SetValueWithoutNotify(filterField.choices[Mathf.Clamp(filterIndex, 0, 3)]);
                VerticalDropdownPopup.Bind(filterField);
                filterField.RegisterValueChangedCallback(HandleFilterChanged);
            }
            RegisterControl(viewButton, Capstone.Game.UISystem.PlayerActionGate.Guard(this, "view", () => {
                listView = !listView;
                itemGrid?.EnableInClassList("is-list", listView);
                viewButton.text = listView ? "▦" : "☷";
                viewButton.tooltip = listView ? "Chuyển sang lưới" : "Chuyển sang danh sách";
                LayoutItemCards();
            }));
            itemGrid?.EnableInClassList("is-list", listView);
            itemGrid?.RegisterCallback<GeometryChangedEvent>(HandleGridGeometryChanged);

            controlsRegistered = true;
            controlsRoot = document.rootVisualElement;
        }

        void RegisterControl(Button button, Action callback) {
            if (button == null || controlCallbacks.ContainsKey(button)) return;
            controlCallbacks.Add(button, callback);
            button.clicked += callback;
        }

        void UnregisterControls() {
            foreach (var pair in controlCallbacks) pair.Key.clicked -= pair.Value;
            controlCallbacks.Clear();
            searchField?.UnregisterValueChangedCallback(HandleSearchChanged);
            sortField?.UnregisterValueChangedCallback(HandleSortChanged);
            filterField?.UnregisterValueChangedCallback(HandleFilterChanged);
            VerticalDropdownPopup.Unbind(sortField);
            VerticalDropdownPopup.Unbind(filterField);
            itemGrid?.UnregisterCallback<GeometryChangedEvent>(HandleGridGeometryChanged);
            controlsRegistered = false;
            controlsRoot = null;
        }

        void HandleSearchChanged(ChangeEvent<string> evt) {
            searchQuery = evt.newValue ?? string.Empty;
            RefreshFromAdapter();
        }
        void HandleSortChanged(ChangeEvent<string> evt) {
            int index = SortChoices.IndexOf(evt.newValue);
            sortMode = index >= 0 ? (InventorySortMode)index : InventorySortMode.Type;
            RefreshFromAdapter();
        }
        void HandleFilterChanged(ChangeEvent<string> evt) {
            filterIndex = Mathf.Max(0, filterField.choices.IndexOf(evt.newValue));
            RefreshFromAdapter();
        }
        void HandleGridGeometryChanged(GeometryChangedEvent _) => LayoutItemCards();

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

            // Reuse cards across search, sorting, category and quantity changes.
            // A callback captures its stable pool index, never an old snapshot.
            for (int index = 0; index < filteredItems.Count; index++) {
                int itemIndex = index;
                InventoryItemSnapshot item = filteredItems[index];
                if (index < itemCards.Count) {
                    Button existing = itemCards[index];
                    existing.style.display = DisplayStyle.Flex;
                    BindItemCard(existing, item);
                    continue;
                }
                var card = new Button { name = $"inventory-item-{index}" };
                card.AddToClassList("inventory-item-card");
                card.tooltip = item.Name;

                var icon = new VisualElement { name = "card-icon" };
                icon.AddToClassList("inventory-item-icon");
                icon.pickingMode = PickingMode.Ignore;
                SetIcon(icon, item.Icon, Color.clear);

                var placeholder = new Label(GetItemPlaceholder(item)) { name = "card-placeholder" };
                placeholder.AddToClassList("inventory-item-placeholder");
                placeholder.pickingMode = PickingMode.Ignore;
                placeholder.style.display = item.Icon == null ? DisplayStyle.Flex : DisplayStyle.None;
                icon.Add(placeholder);

                var nameLabel = new Label(item.Name) { name = "card-name" };
                nameLabel.AddToClassList("inventory-item-name");
                nameLabel.pickingMode = PickingMode.Ignore;

                var quantityLabel = new Label(item.Quantity.ToString()) { name = "card-quantity" };
                quantityLabel.AddToClassList("inventory-item-quantity");
                quantityLabel.pickingMode = PickingMode.Ignore;

                card.Add(icon);
                card.Add(nameLabel);
                card.Add(quantityLabel);
                card.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "item-select:" + itemIndex, () => SelectItem(itemIndex, true));
                itemGrid.Add(card);
                itemCards.Add(card);
            }

            for (int index = filteredItems.Count; index < itemCards.Count; index++) itemCards[index].style.display = DisplayStyle.None;
            LayoutItemCards();

            RefreshItemCardSelection();
        }

        static void BindItemCard(Button card, InventoryItemSnapshot item) {
            card.tooltip = item.Name;
            SetIcon(card.Q<VisualElement>("card-icon"), item.Icon, Color.clear);
            Label placeholder = card.Q<Label>("card-placeholder");
            placeholder.text = GetItemPlaceholder(item);
            placeholder.style.display = item.Icon == null ? DisplayStyle.Flex : DisplayStyle.None;
            card.Q<Label>("card-name").text = item.Name;
            card.Q<Label>("card-quantity").text = item.Quantity.ToString();
        }

        void LayoutItemCards() {
            LayoutItemGrid(itemGrid, listView);
        }

        // The Editor preview uses this same sizing rule as the live Bag.
        public static void LayoutItemGrid(VisualElement grid, bool listView) {
            if (grid == null || !float.IsFinite(grid.resolvedStyle.width) || grid.resolvedStyle.width <= 0) return;
            float available = grid.resolvedStyle.width;
            int columns = Mathf.Clamp(Mathf.FloorToInt(available / 142f), 5, 7);
            float width = Mathf.Max(1f, available / columns - 10f);
            foreach (VisualElement card in grid.Children()) {
                card.style.width = listView ? available - 10f : width;
                card.style.height = listView ? 76f : width;
            }
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
            // Open/SetActiveSection refreshes from the adapter before displaying the bag.
            if (IsOpen && activeSection == MenuSection.Bag) RefreshFromAdapter();
        }

        void SelectCategory(GameItemCategory category) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "category:" + category)) return;
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
            OpenPetBoxPanel(returnToPets ? PetBoxReturnTarget.Pets : PetBoxReturnTarget.Menu);
        }

        public void OpenPetBoxPanel(PetBoxReturnTarget returnTarget) {
            if (petBoxPanelController == null) EnsurePetBoxPanelController();
            petBoxPanelController?.OpenFrom(returnTarget);
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
            OpenPetEvolutionPanel(pet, false);
        }

        public void OpenAutomaticPetEvolutionPrompt(PetController pet) {
            if (root == null) CacheElements();
            if (root == null) return;
            Close();
            if (petEvolutionPanelController == null) EnsurePetEvolutionPanelController();
            petEvolutionPanelController?.Open(pet, true);
            VisibilityChanged?.Invoke(IsEvolutionScreenOpen);
        }

        public void DismissEvolutionScreen() {
            petEvolutionPanelController?.DismissPanel();
        }

        public void EvolutionScreenClosed() {
            VisibilityChanged?.Invoke(IsOpen);
        }

        public void OpenBagEvolutionConfirmation(PetController pet, string itemId) {
            if (pet == null || string.IsNullOrWhiteSpace(itemId)) return;
            OpenInventoryPanel();
            if (bagEvolutionOverlay == null) return;
            bagEvolutionPet = pet;
            bagEvolutionItemId = itemId;
            bagEvolutionComplete = false;
            PetEvolutionPreview preview = GetComponent<PetEvolutionService>()?.CreatePreview(
                pet, PetEvolutionMethod.Item, itemId);
            if (bagEvolutionText != null) bagEvolutionText.text = preview?.IsConfigured == true
                ? $"Dùng 1 {itemId} để tiến hóa {preview.CurrentName} thành {preview.TargetName}?"
                : "thú này không thể dùng vật phẩm tiến hóa.";
            if (bagEvolutionFeedback != null) bagEvolutionFeedback.text = preview?.CanEvolve == true
                ? string.Empty : preview?.DisabledReason ?? string.Empty;
            if (bagEvolutionConfirm != null) {
                bagEvolutionConfirm.text = "DÙNG ĐÁ";
                bagEvolutionConfirm.SetEnabled(preview?.CanEvolve == true);
            }
            bagEvolutionOverlay.style.display = DisplayStyle.Flex;
            bagEvolutionOverlay.BringToFront();
        }

        void ConfirmBagEvolution() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "bag-evolution-confirm")) return;
            bagEvolutionConfirm?.SetEnabled(false);
            try { ConfirmBagEvolutionCore(); }
            finally { if (bagEvolutionComplete) bagEvolutionConfirm?.SetEnabled(true); }
        }

        void ConfirmBagEvolutionCore() {
            if (bagEvolutionComplete) { CloseBagEvolutionConfirmation(); return; }
            PetEvolutionService service = GetComponent<PetEvolutionService>();
            if (service == null || bagEvolutionPet == null) return;
            bool success = service.TryEvolve(bagEvolutionPet, out string message,
                PetEvolutionMethod.Item, bagEvolutionItemId);
            if (bagEvolutionFeedback != null) bagEvolutionFeedback.text = message;
            if (!success) {
                bagEvolutionConfirm?.SetEnabled(false);
                return;
            }
            bagEvolutionComplete = true;
            if (bagEvolutionText != null) bagEvolutionText.text = "Tiến hóa hoàn tất";
            if (bagEvolutionConfirm != null) bagEvolutionConfirm.text = "TIẾP TỤC";
            FindFirstObjectByType<PetCommandHudProvider>()?.NotifyHudDataChanged();
            RefreshFromAdapter();
        }

        void CloseBagEvolutionConfirmation() {
            if (bagEvolutionOverlay != null) bagEvolutionOverlay.style.display = DisplayStyle.None;
            bagEvolutionPet = null;
            bagEvolutionItemId = null;
            bagEvolutionComplete = false;
        }

        void OpenPetEvolutionPanel(PetController pet, bool automaticPrompt) {
            OpenContentSection(MenuSection.Party);
            petPartyPanelController?.CloseRenamePopup();
            petSkillsPanelController?.Close();
            petLevelUpPanelController?.Close();
            petHealPanelController?.Close();
            petReleasePanelController?.Close();
            petPartyPanelController?.CloseDetails();
            if (petEvolutionPanelController == null) EnsurePetEvolutionPanelController();
            petEvolutionPanelController?.Open(pet, automaticPrompt);
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

        public void OpenCodexPanel() => OpenContentSection(MenuSection.Codex);
        void HandleCodexBack() { Close(); CodexBackRequested?.Invoke(); }

        void OpenContentSection(MenuSection section) {
            if (root == null) CacheElements();
            if (root == null) return;

            root.style.display = DisplayStyle.Flex;
            SetActiveSection(section);
            if (section == MenuSection.Bag) RefreshFromAdapter();
            VisibilityChanged?.Invoke(true);
        }

        public void RequestClose() {
            if (Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "close")) Close();
        }

        public void Close() {
            if (root == null) CacheElements();
            if (root == null) return;

            quantityPopup?.Hide();
            CloseBagEvolutionConfirmation();
            petBoxPanelController?.CloseTransientUi();
            petSkillsPanelController?.Close();
            petLevelUpPanelController?.Close();
            petHealPanelController?.Close();
            petEvolutionPanelController?.Close();
            petReleasePanelController?.Close();
            petPartyPanelController?.CloseRenamePopup();
            petPartyPanelController?.CloseDetails();
            petPartyPanelController?.CancelEvolutionItemSelection();
            codexController?.Close();
            root.style.display = DisplayStyle.None;
            VisibilityChanged?.Invoke(false);
        }

        public bool TryCloseTopmostPanel() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "close-top")) return true; // Already handled: do not close the underlying panel.
            if (bagEvolutionOverlay != null
                && bagEvolutionOverlay.style.display.value != DisplayStyle.None) {
                CloseBagEvolutionConfirmation();
                return true;
            }
            if (petPartyPanelController != null && petPartyPanelController.HasEvolutionItemSelection) {
                petPartyPanelController.CancelEvolutionItemSelection();
                return true;
            }
            if (activeSection == MenuSection.Box
                && petBoxPanelController != null
                && petBoxPanelController.TryCancelInteraction()) {
                return true;
            }
            if (petPartyPanelController != null && petPartyPanelController.IsRenameOpen) {
                petPartyPanelController.CloseRenamePopup();
                return true;
            }
            if (petReleasePanelController != null && petReleasePanelController.IsOpen) {
                petReleasePanelController.Close();
                return true;
            }
            if (petEvolutionPanelController != null && petEvolutionPanelController.IsOpen) {
                petEvolutionPanelController.DismissPanel();
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
            if (section != MenuSection.Party) petPartyPanelController?.CancelEvolutionItemSelection();
            activeSection = section;
            bool showInventoryContent = section == MenuSection.Journal || section == MenuSection.Bag;
            bool showPetParty = section == MenuSection.Party;
            bool showPetBox = section == MenuSection.Box;
            bool showCodex = section == MenuSection.Codex;
            if (showCodex) codexController?.Open(); else codexController?.Close();
            SetVisible(codexPanel, showCodex);

            if (!showPetBox) petBoxPanelController?.CloseTransientUi();
            if (!showPetParty) petSkillsPanelController?.Close();
            if (!showPetParty) petLevelUpPanelController?.Close();
            if (!showPetParty) petHealPanelController?.Close();
            if (!showPetParty) petEvolutionPanelController?.Close();
            if (!showPetParty) petReleasePanelController?.Close();

            SetVisible(questInventoryShell, showInventoryContent);
            SetVisible(petsPanel, showPetParty);
            SetVisible(boxPanel, showPetBox);
            SetVisible(placeholderPanel, !showInventoryContent && !showPetParty && !showPetBox && !showCodex);

            if (questInventoryShell != null) {
                questInventoryShell.EnableInClassList("journal-mode", section == MenuSection.Journal);
                questInventoryShell.EnableInClassList("bag-mode", section == MenuSection.Bag);
            }

            SetVisible(questPanel, section == MenuSection.Journal);
            if (section == MenuSection.Journal) GetComponent<QuestPanelController>()?.RefreshOnOpen();
            SetVisible(inventoryPanel, section == MenuSection.Bag);

            if (showPetParty) {
                if (petPartyPanelController == null) EnsurePetPartyPanelController();
                petPartyPanelController?.Refresh();
            }

            if (showPetBox) {
                if (petBoxPanelController == null) EnsurePetBoxPanelController();
                petBoxPanelController?.Refresh();
            }

            if (!showInventoryContent && !showPetParty && !showPetBox && !showCodex) {
                string title = GetSectionTitle(section);
                if (placeholderTitle != null) placeholderTitle.text = title;
                if (placeholderBody != null) {
                    placeholderBody.text = "Tính năng này chưa sẵn sàng.";
                }
            }
        }

        public void SelectNextCategory() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "next-category")) return ;
            SelectCategory(GetShiftedCategory(1));
        }

        public void SelectPreviousCategory() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "previous-category")) return ;
            SelectCategory(GetShiftedCategory(-1));
        }

        public void SelectNextItem() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "next-item")) return ;
            ShiftSelectedItem(1);
        }

        public void SelectPreviousItem() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "previous-item")) return ;
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
                IReadOnlyList<InventoryItemSnapshot> allItems = adapter.GetItems();
                foreach (GameItemCategory category in CategoryOrder) {
                    Label count = GetCategoryButton(category)?.Q<Label>(className: "category-count");
                    // Counts describe owned kinds of items, matching the aggregated grid.
                    if (count != null) count.text = (category == GameItemCategory.All ? allItems.Count
                        : allItems.Count(item => item.Category == category)).ToString();
                }
                IEnumerable<InventoryItemSnapshot> items = allItems.Where(item => currentCategory == GameItemCategory.All || item.Category == currentCategory);
                items = items.Where(MatchesFilter);
                if (!string.IsNullOrWhiteSpace(searchQuery)) {
                    string expected = searchQuery.Trim();
                    items = items.Where(item => MatchesSearch(item, expected));
                }
                filteredItems.AddRange(SortItems(items));
            }

            SubscribeToCurrencyWallet();
            RefreshGold();

            var hasItems = filteredItems.Count > 0;
            int nextSelectedIndex = hasItems ? GetNextSelectionIndex(previousItemBase, previousIndex) : -1;
            if (noItemsLabel != null) noItemsLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
            if (noItemsLabel != null) noItemsLabel.text = !string.IsNullOrWhiteSpace(searchQuery) || filterIndex != 0
                ? "Không có vật phẩm phù hợp. Thử đổi từ khóa hoặc bộ lọc."
                : "Danh mục này chưa có vật phẩm.";

            selectedItemIndex = nextSelectedIndex;
            selectedItem = selectedItemIndex >= 0 ? filteredItems[selectedItemIndex] : null;
            RebuildItemGrid();
            RefreshDetails();
        }

        int GetNextSelectionIndex(ItemBase previousItemBase, int previousIndex) {
            if (filteredItems.Count == 0 || previousIndex < 0) return -1;

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

        bool MatchesFilter(InventoryItemSnapshot item) {
            switch (filterIndex) {
                case 1: return IsActionAvailable(InventoryActionType.UseItem, item);
                case 2: return item.Rarity >= InventoryItemRarity.Rare;
                case 3: return !IsProtectedDropItem(item);
                default: return true;
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
            SetVisible(detailEmpty, !hasItem);
            SetVisible(detailPanel, hasItem);
            SetVisible(inventoryActions, hasItem);
            if (!hasItem) return;
            SetVisible(detailRarity, hasItem);
            SetVisible(detailCategory, hasItem);
            SetVisible(detailQuantity, hasItem);
            SetVisible(detailEffect, hasItem && !string.IsNullOrWhiteSpace(selectedItem.Effect));
            SetVisible(detailFlavor, hasItem && !string.IsNullOrWhiteSpace(selectedItem.FlavorText));
            SetVisible(document?.rootVisualElement?.Q<VisualElement>("detail-source-block"),
                hasItem && !string.IsNullOrWhiteSpace(selectedItem.Source));

            SetIcon(detailIcon, selectedItem.Icon, Color.clear);
            if (detailIconPlaceholder != null) {
                detailIconPlaceholder.text = GetItemPlaceholder(selectedItem);
                detailIconPlaceholder.style.display = selectedItem.Icon == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (detailName != null) detailName.text = selectedItem.Name;
            if (detailRarity != null) detailRarity.text = FormatRarity(selectedItem.Rarity).ToUpperInvariant();
            if (detailCategory != null) detailCategory.text = FormatCategory(selectedItem.Category);
            if (detailQuantity != null) {
                detailQuantity.text = $"Số lượng sở hữu: {selectedItem.Quantity}";
            }
            if (detailDescription != null) detailDescription.text = string.IsNullOrWhiteSpace(selectedItem.Description) ? "Chưa có mô tả." : selectedItem.Description;
            if (detailEffect != null) detailEffect.text = string.IsNullOrWhiteSpace(selectedItem.Effect) ? string.Empty : selectedItem.Effect;
            if (detailSource != null) detailSource.text = selectedItem.Source;
            if (detailFlavor != null) detailFlavor.text = string.IsNullOrWhiteSpace(selectedItem.FlavorText) ? string.Empty : $"\"{selectedItem.FlavorText}\"";
        }

        void ShiftSelectedItem(int direction) {
            if (filteredItems.Count == 0) return;

            var currentIndex = selectedItemIndex;
            if (currentIndex < 0) { SelectItem(0, true); return; }

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
            ResetActionButton(useButton, "SỬ DỤNG");
            ResetActionButton(giveButton, "CHO THÚ");
            ResetActionButton(dropButton, "BỎ");
            ResetActionButton(equipButton, "GÁN");

            if (selectedItem == null) return;

            if (IsSelectedPetEvolutionItem(selectedItem)) {
                ConfigureActionButton(useButton, InventoryActionType.UseItem, "TIẾN HÓA");
                if (!IsProtectedDropItem(selectedItem)) {
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "BỎ");
                }
                return;
            }

            switch (selectedItem.Category) {
                case GameItemCategory.Medicine:
                    ConfigureActionButton(useButton, InventoryActionType.UseItem, "SỬ DỤNG");
                    if (SupportsQuickSlot(selectedItem)) ConfigureActionButton(equipButton, InventoryActionType.AssignQuickSlot, "GÁN PHÍM TẮT");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "BỎ");
                    break;
                case GameItemCategory.Food:
                    ConfigureActionButton(useButton, InventoryActionType.UseItem, "SỬ DỤNG");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "BỎ");
                    break;
                case GameItemCategory.CaptureBall:
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "BỎ");
                    break;
                case GameItemCategory.Material:
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "BỎ");
                    break;
                case GameItemCategory.Equipment:
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "BỎ");
                    break;
                case GameItemCategory.KeyItem:
                    if (SupportsUse(selectedItem)) ConfigureActionButton(useButton, InventoryActionType.UseItem, "SỬ DỤNG");
                    break;
                case GameItemCategory.QuestItem:
                    break;
            }
        }

        static void ResetActionButton(Button button, string label) {
            if (button == null) return;
            SetButtonVisible(button, false);
            button.text = label;
            button.SetEnabled(false);
        }

        void ConfigureActionButton(Button button, InventoryActionType actionType, string label) {
            if (button == null) return;

            bool available = IsActionAvailable(actionType, selectedItem);
            SetButtonVisible(button, true);
            button.text = label;
            button.SetEnabled(available);
            button.tooltip = available ? label : GetUnavailableActionReason(actionType, selectedItem);
        }

        string GetUnavailableActionReason(InventoryActionType actionType, InventoryItemSnapshot item) {
            if (item == null) return "Chọn vật phẩm để thực hiện thao tác.";
            if (item.Quantity <= 0) return "Bạn không còn vật phẩm này.";
            if (actionType == InventoryActionType.UseItem && !SupportsUse(item))
                return "Vật phẩm này không thể sử dụng trực tiếp từ túi.";
            if (actionType == InventoryActionType.GiveItem && !HasSelectedTargetMonster())
                return "Chọn thú đồng hành trước khi dùng vật phẩm.";
            if (actionType == InventoryActionType.DropItem && IsProtectedDropItem(item))
                return "Không thể bỏ vật phẩm quan trọng hoặc vật phẩm nhiệm vụ.";
            return "Thao tác này hiện chưa khả dụng.";
        }

        static void SetButtonVisible(Button button, bool visible) {
            if (button == null) return;

            button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        bool IsActionAvailable(InventoryActionType actionType, InventoryItemSnapshot item) {
            if (item == null || item.Quantity <= 0 || !IsActionAllowed(actionType, item)) return false;
            if (actionType == InventoryActionType.UseItem && IsSelectedPetEvolutionItem(item)) return true;
            if (actionType == InventoryActionType.UseItem && IsHealingItem(item)) return true;
            return allowPlaceholderActions || HasGameplayHandler(actionType);
        }

        bool IsActionAllowed(InventoryActionType actionType, InventoryItemSnapshot item) {
            if (item == null) return false;
            if (actionType == InventoryActionType.UseItem && IsSelectedPetEvolutionItem(item)) return true;

            switch (actionType) {
                case InventoryActionType.UseItem:
                    return ((item.Category == GameItemCategory.Medicine || item.Category == GameItemCategory.Food) && SupportsUse(item))
                        || (item.Category == GameItemCategory.KeyItem && SupportsUse(item));
                case InventoryActionType.GiveItem:
                    return HasSelectedTargetMonster()
                        && (item.Category == GameItemCategory.Medicine || item.Category == GameItemCategory.Food);
                case InventoryActionType.EquipItem:
                    return item.Category == GameItemCategory.Equipment;
                case InventoryActionType.AssignQuickSlot:
                    return SupportsQuickSlot(item);
                case InventoryActionType.DropItem:
                    return !IsProtectedDropItem(item);
                default:
                    return false;
            }
        }

        bool IsSelectedPetEvolutionItem(InventoryItemSnapshot item) {
            return petEvolutionPanelController != null
                && petEvolutionPanelController.IsEvolutionItem(item);
        }

        bool IsHealingItem(InventoryItemSnapshot item) => petHealPanelController != null
            && item != null && item.Category == GameItemCategory.Medicine && item.HealAmount > 0
            && item.UsableFromInventory && item.Consumable;

        static bool SupportsQuickSlot(InventoryItemSnapshot item) => item != null
            && item.Category == GameItemCategory.Medicine && item.HealAmount > 0
            && item.UsableFromInventory && item.Consumable;

        bool TryGetDefaultAction(InventoryItemSnapshot item, out InventoryActionType actionType) {
            actionType = InventoryActionType.UseItem;
            if (item == null) return false;

            switch (item.Category) {
                case GameItemCategory.Medicine:
                case GameItemCategory.Food:
                    actionType = InventoryActionType.UseItem;
                    return true;
                case GameItemCategory.CaptureBall:
                    return false;
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
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "request-action:" + actionType)) return;
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
            if (!Capstone.Game.UISystem.PlayerActionGate.TryBegin(this, "item-action:" + actionType, out var operation)) return;
            useButton?.SetEnabled(false); giveButton?.SetEnabled(false);
            equipButton?.SetEnabled(false); dropButton?.SetEnabled(false);
            try { using (operation) RequestActionCore(actionType, quantity); }
            finally { RefreshActionButtons(); }
        }

        void RequestActionCore(InventoryActionType actionType, int quantity) {
            if (selectedItem == null || !IsActionAvailable(actionType, selectedItem)) return;
            if (actionType == InventoryActionType.UseItem
                && petEvolutionPanelController != null
                && petEvolutionPanelController.TryOpenFromInventory(selectedItem)) return;
            if (actionType == InventoryActionType.UseItem && IsHealingItem(selectedItem)
                && petHealPanelController.TryOpenFromInventory(selectedItem)) return;

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
                case InventoryActionType.GiveItem: return "CHO VẬT PHẨM";
                case InventoryActionType.DropItem: return "XÁC NHẬN BỎ VẬT PHẨM";
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
                    else request.Complete(true, true, request.RemoveQuantity, "Item đã được bỏ khỏi túi đồ.");
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
                case MenuSection.Party: return Capstone.Game.UISystem.VietnameseUI.Screen("Party");
                case MenuSection.Map: return Capstone.Game.UISystem.VietnameseUI.Screen("Map");
                case MenuSection.Settings: return Capstone.Game.UISystem.VietnameseUI.Screen("Settings");
                case MenuSection.Journal: return Capstone.Game.UISystem.VietnameseUI.Screen("Nhiệm vụ Journal");
                case MenuSection.Box: return Capstone.Game.UISystem.VietnameseUI.Screen("Box");
                default: return Capstone.Game.UISystem.VietnameseUI.Screen("Bag");
            }
        }

        Button GetCategoryButton(GameItemCategory category) {
            if (document == null || document.rootVisualElement == null) return null;

            switch (category) {
                case GameItemCategory.All: return document.rootVisualElement.Q<Button>("category-all");
                case GameItemCategory.CaptureBall: return document.rootVisualElement.Q<Button>("category-capture-ball");
                case GameItemCategory.Medicine: return document.rootVisualElement.Q<Button>("category-medicine");
                case GameItemCategory.Food: return document.rootVisualElement.Q<Button>("category-food");
                case GameItemCategory.Material: return document.rootVisualElement.Q<Button>("category-material");
                case GameItemCategory.Equipment: return document.rootVisualElement.Q<Button>("category-equipment");
                case GameItemCategory.KeyItem: return document.rootVisualElement.Q<Button>("category-key-item");
                case GameItemCategory.QuestItem: return document.rootVisualElement.Q<Button>("category-quest-item");
                default: return null;
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
                case GameItemCategory.All: return "Tất cả";
                case GameItemCategory.CaptureBall: return "Dụng cụ bắt";
                case GameItemCategory.Medicine: return "Hồi phục";
                case GameItemCategory.Food: return "Thức ăn";
                case GameItemCategory.Material: return "Nguyên liệu";
                case GameItemCategory.Equipment: return "Trang bị";
                case GameItemCategory.KeyItem: return "Vật phẩm chính";
                case GameItemCategory.QuestItem: return "Vật phẩm nhiệm vụ";
                default: return "Không xác định";
            }
        }

        static string FormatRarity(InventoryItemRarity rarity) => Capstone.Game.UISystem.VietnameseUI.Rarity(rarity.ToString());

        static readonly List<string> SortChoices = new List<string> {
            "Sắp xếp: Loại",
            "Sắp xếp: Tên A-Z",
            "Sắp xếp: Tên Z-A",
            "Sắp xếp: Số lượng",
            "Sắp xếp: Độ hiếm"
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
            Codex,
            Journal,
            Bag,
            Party,
            Box,
            Map,
            Settings
        }
    }

    /// <summary>
    /// Forces UI Toolkit dropdown popups to scroll vertically. GenericDropdownMenu
    /// otherwise enables both axes and shows a horizontal bar for long labels.
    /// </summary>
    public static class VerticalDropdownPopup {
        const float PopupMinWidth = 220f;
        const float PopupMaxHeight = 250f;

        public static void Bind(DropdownField field) {
            field?.RegisterCallback<PointerDownEvent>(ScheduleConfiguration, TrickleDown.TrickleDown);
        }

        public static void Unbind(DropdownField field) {
            field?.UnregisterCallback<PointerDownEvent>(ScheduleConfiguration, TrickleDown.TrickleDown);
        }

        static void ScheduleConfiguration(PointerDownEvent evt) {
            if (!(evt.currentTarget is DropdownField field)) return;
            field.schedule.Execute(() => Configure(field)).ExecuteLater(1);
        }

        static void Configure(DropdownField field) {
            VisualElement panelRoot = field?.panel?.visualTree;
            VisualElement outer = panelRoot?.Q<VisualElement>(className: "unity-base-dropdown__container-outer");
            if (outer == null) return;

            float popupWidth = Mathf.Max(PopupMinWidth, field.resolvedStyle.width);
            outer.style.minWidth = popupWidth;
            outer.style.maxHeight = PopupMaxHeight;

            VisualElement inner = outer.Q<VisualElement>(className: "unity-base-dropdown__container-inner");
            if (inner != null) {
                inner.style.minWidth = popupWidth - 2f;
                inner.style.maxHeight = PopupMaxHeight - 8f;
            }

            ScrollView scrollView = inner as ScrollView ?? inner?.Q<ScrollView>() ?? outer.Q<ScrollView>();
            if (scrollView == null) return;

            scrollView.mode = ScrollViewMode.Vertical;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.contentContainer.style.minWidth = popupWidth - 6f;
        }
    }
}

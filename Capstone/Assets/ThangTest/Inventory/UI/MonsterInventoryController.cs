using System.Collections.Generic;
using System.Linq;
using System;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory {
    [RequireComponent(typeof(UIDocument))]
    public class MonsterInventoryController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] VisualTreeAsset rowTemplate = null;
        [SerializeField] MonsterInventoryAdapter adapter = null;
        [SerializeField] bool autoInstallInputController = true;
        [SerializeField] bool allowPlaceholderActions = true;
        [SerializeField] bool autoInstallPickupActionFeedback = true;
        [SerializeField] bool autoInstallQuestPanelController = true;
        [SerializeField] bool autoInstallMenuHudController = true;

        readonly List<InventoryItemSnapshot> filteredItems = new List<InventoryItemSnapshot>();
        readonly List<Button> categoryButtons = new List<Button>();

        VisualElement root;
        ListView itemList;
        Label noItemsLabel;
        Label categoryValueLabel;
        Label capacityLabel;
        Label categoryCapacityMirror;
        Label detailName;
        Label detailCategory;
        Label detailQuantity;
        Label detailDescription;
        Label detailEffect;
        VisualElement detailIcon;
        Button useButton;
        Button giveButton;
        Button equipButton;
        Button dropButton;
        Button cancelButton;
        ItemQuantityPopup quantityPopup;

        GameItemCategory currentCategory = GameItemCategory.All;
        InventoryItemSnapshot selectedItem;
        InventoryActionType equipSlotActionType = InventoryActionType.EquipItem;
        bool controlsRegistered;
        VisualElement controlsRoot;
        string selectedTargetMonsterId = string.Empty;

        const string QuestPanelControllerTypeName = "Capstone.Game.QuestSystem.UI.QuestPanelController, Assembly-CSharp";
        const string MenuHudControllerTypeName = "Capstone.Game.Inventory.InventoryMenuHudController, Assembly-CSharp";

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
            EnsureMenuHudController();
            CacheElements();
            EnsureQuestPanelController();
            RegisterCategoryButtons();
            RegisterActionButtons();
            SetupListView();
            SubscribeToAdapter();
            SelectCategory(GameItemCategory.All);
        }

        void OnDisable() {
            if (adapter != null) adapter.ItemsChanged -= HandleInventoryChanged;
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            adapter = adapter != null ? adapter : GetComponent<MonsterInventoryAdapter>();
            if (adapter == null) adapter = GetComponentInParent<MonsterInventoryAdapter>();
            if (adapter == null) adapter = FindFirstObjectByType<MonsterInventoryAdapter>();
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

        void EnsureMenuHudController() {
            if (!Application.isPlaying || !autoInstallMenuHudController || document == null) return;

            var menuHudType = Type.GetType(MenuHudControllerTypeName);
            if (menuHudType == null || !typeof(MonoBehaviour).IsAssignableFrom(menuHudType)) return;

            var menuHud = GetComponent(menuHudType);
            if (menuHud == null) menuHud = gameObject.AddComponent(menuHudType);

            var bindMethod = menuHudType.GetMethod("Bind", new[] { typeof(UIDocument) });
            bindMethod?.Invoke(menuHud, new object[] { document });
        }

        void EnsureQuestPanelController() {
            if (!Application.isPlaying || !autoInstallQuestPanelController || document == null) return;

            var questPanelType = Type.GetType(QuestPanelControllerTypeName);
            if (questPanelType == null || !typeof(MonoBehaviour).IsAssignableFrom(questPanelType)) return;
            if (GetComponent(questPanelType) == null) gameObject.AddComponent(questPanelType);
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) {
                Debug.LogWarning("MonsterInventoryController needs a UIDocument.");
                return;
            }

            var rootElement = document.rootVisualElement;
            root = rootElement.Q<VisualElement>("monster-inventory-root");
            itemList = rootElement.Q<ListView>("item-list");
            noItemsLabel = rootElement.Q<Label>("no-items-label");
            categoryValueLabel = rootElement.Q<Label>("category-value-label");
            capacityLabel = rootElement.Q<Label>("capacity-label");
            categoryCapacityMirror = rootElement.Q<Label>("category-capacity-mirror");
            detailIcon = rootElement.Q<VisualElement>("detail-icon");
            detailName = rootElement.Q<Label>("detail-name");
            detailCategory = rootElement.Q<Label>("detail-category");
            detailQuantity = rootElement.Q<Label>("detail-quantity");
            detailDescription = rootElement.Q<Label>("detail-description");
            detailEffect = rootElement.Q<Label>("detail-effect");
            useButton = rootElement.Q<Button>("use-button");
            giveButton = rootElement.Q<Button>("give-button");
            equipButton = rootElement.Q<Button>("equip-button");
            dropButton = rootElement.Q<Button>("drop-button");
            cancelButton = rootElement.Q<Button>("cancel-button");
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

            controlsRegistered = true;
            controlsRoot = document.rootVisualElement;
        }

        void SetupListView() {
            if (itemList == null) return;

            itemList.fixedItemHeight = 62;
            itemList.makeItem = MakeItemRow;
            itemList.bindItem = BindItemRow;
            itemList.selectionChanged -= OnSelectionChanged;
            itemList.selectionChanged += OnSelectionChanged;
        }

        void SubscribeToAdapter() {
            if (adapter == null) {
                Debug.LogWarning("MonsterInventoryController could not find a MonsterInventoryAdapter.");
                return;
            }

            adapter.ItemsChanged -= HandleInventoryChanged;
            adapter.ItemsChanged += HandleInventoryChanged;
        }

        VisualElement MakeItemRow() {
            if (rowTemplate != null) return rowTemplate.Instantiate();

            var row = new VisualElement { name = "monster-item-row" };
            row.AddToClassList("item-row");

            var icon = new VisualElement { name = "row-icon" };
            icon.AddToClassList("item-row-icon");
            row.Add(icon);

            var textGroup = new VisualElement();
            textGroup.AddToClassList("item-row-text");
            row.Add(textGroup);

            var nameLabel = new Label { name = "row-name" };
            nameLabel.AddToClassList("item-row-name");
            textGroup.Add(nameLabel);

            var categoryLabel = new Label { name = "row-category" };
            categoryLabel.AddToClassList("item-row-category");
            textGroup.Add(categoryLabel);

            var quantityLabel = new Label { name = "row-quantity" };
            quantityLabel.AddToClassList("item-row-quantity");
            row.Add(quantityLabel);

            return row;
        }

        void BindItemRow(VisualElement element, int index) {
            if (index < 0 || index >= filteredItems.Count) return;

            var item = filteredItems[index];
            var icon = element.Q<VisualElement>("row-icon");
            var nameLabel = element.Q<Label>("row-name");
            var categoryLabel = element.Q<Label>("row-category");
            var quantityLabel = element.Q<Label>("row-quantity");

            SetIcon(icon, item.Icon, SmallIconColor);
            if (nameLabel != null) nameLabel.text = item.Name;
            if (categoryLabel != null) categoryLabel.text = FormatCategory(item.Category);
            if (quantityLabel != null) quantityLabel.text = $"x{item.Quantity}";
        }

        void OnSelectionChanged(IEnumerable<object> selectedObjects) {
            selectedItem = selectedObjects != null ? selectedObjects.OfType<InventoryItemSnapshot>().FirstOrDefault() : null;
            RefreshDetails();
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
            if (root == null) CacheElements();
            if (root == null) return;

            root.style.display = DisplayStyle.Flex;
            RefreshFromAdapter();
            VisibilityChanged?.Invoke(true);
        }

        public void Close() {
            if (root == null) CacheElements();
            if (root == null) return;

            quantityPopup?.Hide();
            root.style.display = DisplayStyle.None;
            VisibilityChanged?.Invoke(false);
        }

        public void Toggle() {
            if (IsOpen) Close();
            else Open();
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
            int previousIndex = itemList != null ? itemList.selectedIndex : -1;

            filteredItems.Clear();
            if (adapter != null) filteredItems.AddRange(adapter.GetItems(currentCategory));

            if (categoryValueLabel != null) categoryValueLabel.text = FormatCategory(currentCategory);
            string capacityText = adapter != null ? $"{adapter.OccupiedSlotCount} / {adapter.Capacity}" : "0 / 0";
            if (capacityLabel != null) capacityLabel.text = capacityText;
            if (categoryCapacityMirror != null) categoryCapacityMirror.text = capacityText;

            var hasItems = filteredItems.Count > 0;
            int nextSelectedIndex = hasItems ? GetNextSelectionIndex(previousItemBase, previousIndex) : -1;
            if (itemList != null) {
                itemList.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
                itemList.itemsSource = filteredItems;
                itemList.Rebuild();
                itemList.selectedIndex = nextSelectedIndex;
                if (nextSelectedIndex >= 0) itemList.ScrollToItem(nextSelectedIndex);
            }

            if (noItemsLabel != null) noItemsLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;

            selectedItem = nextSelectedIndex >= 0 ? filteredItems[nextSelectedIndex] : null;
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

        void RefreshDetails() {
            var hasItem = selectedItem != null;
            RefreshActionButtons();

            if (!hasItem) {
                SetIcon(detailIcon, null, DetailPlaceholderColor);
                if (detailName != null) detailName.text = "Select an item";
                if (detailCategory != null) detailCategory.text = "Category: -";
                if (detailQuantity != null) detailQuantity.text = "Quantity: -";
                if (detailDescription != null) detailDescription.text = "No item selected.";
                if (detailEffect != null) detailEffect.text = "-";
                return;
            }

            SetIcon(detailIcon, selectedItem.Icon, DetailPlaceholderColor);
            if (detailName != null) detailName.text = selectedItem.Name;
            if (detailCategory != null) detailCategory.text = $"Category: {FormatCategory(selectedItem.Category)}";
            if (detailQuantity != null) detailQuantity.text = $"Quantity: {selectedItem.Quantity}";
            if (detailDescription != null) detailDescription.text = string.IsNullOrWhiteSpace(selectedItem.Description) ? "No description." : selectedItem.Description;
            if (detailEffect != null) detailEffect.text = string.IsNullOrWhiteSpace(selectedItem.Effect) ? "-" : selectedItem.Effect;
        }

        void ShiftSelectedItem(int direction) {
            if (filteredItems.Count == 0 || itemList == null) return;

            var currentIndex = itemList.selectedIndex;
            if (currentIndex < 0) currentIndex = 0;

            var nextIndex = Mathf.Clamp(currentIndex + direction, 0, filteredItems.Count - 1);
            itemList.selectedIndex = nextIndex;
            itemList.ScrollToItem(nextIndex);
            selectedItem = filteredItems[nextIndex];
            RefreshDetails();
        }

        GameItemCategory GetShiftedCategory(int direction) {
            int currentIndex = CategoryOrder.IndexOf(currentCategory);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = (currentIndex + direction + CategoryOrder.Count) % CategoryOrder.Count;
            return CategoryOrder[nextIndex];
        }

        void RefreshActionButtons() {
            SetButtonVisible(useButton, false);
            SetButtonVisible(giveButton, false);
            SetButtonVisible(equipButton, false);
            SetButtonVisible(dropButton, false);

            if (selectedItem == null) return;

            switch (selectedItem.Category) {
                case GameItemCategory.Medicine:
                case GameItemCategory.Food:
                    ConfigureActionButton(useButton, InventoryActionType.UseItem, "Use");
                    ConfigureActionButton(giveButton, InventoryActionType.GiveItem, "Give");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "Drop");
                    break;
                case GameItemCategory.CaptureBall:
                    equipSlotActionType = InventoryActionType.AssignQuickSlot;
                    ConfigureActionButton(equipButton, InventoryActionType.AssignQuickSlot, "Assign");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "Drop");
                    break;
                case GameItemCategory.Material:
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "Drop");
                    break;
                case GameItemCategory.Equipment:
                    equipSlotActionType = InventoryActionType.EquipItem;
                    ConfigureActionButton(equipButton, InventoryActionType.EquipItem, "Equip");
                    ConfigureActionButton(dropButton, InventoryActionType.DropItem, "Drop");
                    break;
                case GameItemCategory.KeyItem:
                    if (SupportsUse(selectedItem)) ConfigureActionButton(useButton, InventoryActionType.UseItem, "Use");
                    break;
            }
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
            return allowPlaceholderActions || HasGameplayHandler(actionType);
        }

        bool IsActionAllowed(InventoryActionType actionType, InventoryItemSnapshot item) {
            if (item == null) return false;

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

            return item.Category == GameItemCategory.KeyItem
                || string.Equals(item.Category.ToString(), "QuestItem", StringComparison.OrdinalIgnoreCase);
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

                    if (DropItem == null) return false;
                    DropItem.Invoke(request);
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
                case InventoryActionType.DropItem: return RequestDropItem != null || DropItem != null;
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

        Button GetCategoryButton(GameItemCategory category) {
            if (document == null || document.rootVisualElement == null) return null;

            switch (category) {
                case GameItemCategory.CaptureBall: return document.rootVisualElement.Q<Button>("category-capture-ball");
                case GameItemCategory.Medicine: return document.rootVisualElement.Q<Button>("category-medicine");
                case GameItemCategory.Food: return document.rootVisualElement.Q<Button>("category-food");
                case GameItemCategory.Material: return document.rootVisualElement.Q<Button>("category-material");
                case GameItemCategory.Equipment: return document.rootVisualElement.Q<Button>("category-equipment");
                case GameItemCategory.KeyItem: return document.rootVisualElement.Q<Button>("category-key-item");
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
                case GameItemCategory.CaptureBall: return "Capture Ball";
                case GameItemCategory.Medicine: return "Medicine";
                case GameItemCategory.Food: return "Food";
                case GameItemCategory.Material: return "Material";
                case GameItemCategory.Equipment: return "Equipment";
                case GameItemCategory.KeyItem: return "Key Item";
                default: return "All";
            }
        }

        static readonly Color SmallIconColor = new Color(0.62f, 0.55f, 0.39f);
        static readonly Color DetailPlaceholderColor = new Color(0.43f, 0.49f, 0.31f);
        static readonly List<GameItemCategory> CategoryOrder = new List<GameItemCategory> {
            GameItemCategory.All,
            GameItemCategory.CaptureBall,
            GameItemCategory.Medicine,
            GameItemCategory.Food,
            GameItemCategory.Material,
            GameItemCategory.Equipment,
            GameItemCategory.KeyItem
        };
    }
}

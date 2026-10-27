using System;
using UnityEngine;
using UnityEngine.UIElements;
using Capstone.Game.Inventory.Bag;

namespace Capstone.Game.Inventory {
    public sealed class ItemQuantityPopup {
        const string PopupName = "item-quantity-popup";

        readonly VisualElement overlay;
        readonly Label actionTitle;
        readonly VisualElement itemIcon;
        readonly Label itemName;
        readonly Label ownedQuantity;
        readonly Button minusButton;
        readonly IntegerField quantityField;
        readonly Button plusButton;
        readonly SliderInt quantitySlider;
        readonly Button oneButton;
        readonly Button halfButton;
        readonly Button allButton;
        readonly Button confirmButton;
        readonly Button cancelButton;

        InventoryItemSnapshot currentItem;
        Action<int> confirmHandler;
        int maxQuantity = 1;
        int quantity = 1;
        bool fixedQuantity;
        bool isUpdating;
        bool dropAction;

        public bool IsOpen => overlay != null && overlay.style.display.value != DisplayStyle.None;

        public ItemQuantityPopup(VisualElement root) {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var oldPopup = root.Q<VisualElement>(PopupName);
            if (oldPopup != null) oldPopup.RemoveFromHierarchy();

            overlay = new VisualElement { name = PopupName };
            overlay.AddToClassList("quantity-popup-overlay");

            var panel = new VisualElement { name = "quantity-popup-panel" };
            panel.AddToClassList("quantity-popup-panel");
            overlay.Add(panel);

            actionTitle = new Label { name = "quantity-action-title", text = "Thao tác" };
            actionTitle.AddToClassList("quantity-popup-title");
            panel.Add(actionTitle);

            itemIcon = new VisualElement { name = "quantity-item-icon" };
            itemIcon.AddToClassList("quantity-popup-icon");
            panel.Add(itemIcon);

            itemName = new Label { name = "quantity-item-name", text = "Vật phẩm" };
            itemName.AddToClassList("quantity-popup-item-name");
            panel.Add(itemName);

            ownedQuantity = new Label { name = "quantity-owned", text = "Sở hữu: 0" };
            ownedQuantity.AddToClassList("quantity-popup-owned");
            panel.Add(ownedQuantity);

            var stepper = new VisualElement { name = "quantity-stepper" };
            stepper.AddToClassList("quantity-stepper");
            panel.Add(stepper);

            minusButton = CreateButton("quantity-minus", "-", "quantity-step-button");
            stepper.Add(minusButton);

            quantityField = new IntegerField { name = "quantity-field", value = 1 };
            quantityField.AddToClassList("quantity-field");
            stepper.Add(quantityField);

            plusButton = CreateButton("quantity-plus", "+", "quantity-step-button");
            stepper.Add(plusButton);

            quantitySlider = new SliderInt { name = "quantity-slider", lowValue = 1, highValue = 1, value = 1 };
            quantitySlider.AddToClassList("quantity-slider");
            panel.Add(quantitySlider);

            var quickRow = new VisualElement { name = "quantity-quick-row" };
            quickRow.AddToClassList("quantity-quick-row");
            panel.Add(quickRow);

            oneButton = CreateButton("quantity-one", "1", "quantity-quick-button");
            halfButton = CreateButton("quantity-half", "Một nửa", "quantity-quick-button");
            allButton = CreateButton("quantity-all", "Tất cả", "quantity-quick-button");
            quickRow.Add(oneButton);
            quickRow.Add(halfButton);
            quickRow.Add(allButton);

            var actionRow = new VisualElement { name = "quantity-action-row" };
            actionRow.AddToClassList("quantity-action-row");
            panel.Add(actionRow);

            confirmButton = CreateButton("quantity-confirm", "Xác nhận", "quantity-confirm-button");
            cancelButton = CreateButton("quantity-cancel", "Hủy", "quantity-cancel-button");
            actionRow.Add(confirmButton);
            actionRow.Add(cancelButton);

            RegisterCallbacks();
            root.Add(overlay);
            BagPresentation.Quantity(overlay, Hide);
            Hide();
        }

        public void Show(string title, InventoryItemSnapshot item, Action<int> onConfirm) {
            currentItem = item;
            confirmHandler = onConfirm;
            maxQuantity = Mathf.Max(1, item != null ? item.Quantity : 1);
            fixedQuantity = item == null || !item.Stackable || maxQuantity <= 1;

            actionTitle.text = title ?? "Thao tác";
            dropAction = title?.IndexOf("BỎ", StringComparison.OrdinalIgnoreCase) >= 0;
            itemName.text = item != null ? item.Name : "Vật phẩm";
            ownedQuantity.text = item != null ? $"Sở hữu: {maxQuantity}" : "Sở hữu: 0";
            SetIcon(item != null ? item.Icon : null);
            ApplyFixedQuantityState();
            SetQuantity(1);

            overlay.style.display = DisplayStyle.Flex;
            Capstone.Game.UISystem.PanelFocusUtility.Open(overlay);
        }

        public void Hide() {
            BagTooltip.HideAll(overlay);
            overlay.style.display = DisplayStyle.None;
            Capstone.Game.UISystem.PanelFocusUtility.Close(overlay);
            currentItem = null;
            confirmHandler = null;
        }

        void RegisterCallbacks() {
            minusButton.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "minusButton", () => SetQuantity(quantity - 1));
            plusButton.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "plusButton", () => SetQuantity(quantity + 1));
            oneButton.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "oneButton", () => SetQuantity(1));
            halfButton.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "halfButton", () => SetQuantity(Mathf.Max(1, maxQuantity / 2)));
            allButton.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "allButton", () => SetQuantity(maxQuantity));
            cancelButton.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "cancel", Hide);

            confirmButton.clicked += Capstone.Game.UISystem.PlayerActionGate.Guard(this, "confirm", () => {
                var confirmedQuantity = Mathf.Clamp(quantity, 1, maxQuantity);
                var handler = confirmHandler;
                Hide();
                handler?.Invoke(confirmedQuantity);
            });

            quantityField.RegisterValueChangedCallback(evt => {
                if (isUpdating) return;
                SetQuantity(evt.newValue);
            });

            quantitySlider.RegisterValueChangedCallback(evt => {
                if (isUpdating) return;
                SetQuantity(evt.newValue);
            });
        }

        void SetQuantity(int value) {
            quantity = fixedQuantity ? 1 : Mathf.Clamp(value, 1, maxQuantity);

            isUpdating = true;
            quantityField.value = quantity;
            quantitySlider.lowValue = 1;
            quantitySlider.highValue = maxQuantity;
            quantitySlider.value = quantity;
            isUpdating = false;

            minusButton.SetEnabled(!fixedQuantity && quantity > 1);
            plusButton.SetEnabled(!fixedQuantity && quantity < maxQuantity);
            oneButton.SetEnabled(!fixedQuantity && quantity != 1);
            confirmButton.SetEnabled(currentItem != null && maxQuantity > 0);
            confirmButton.text = dropAction ? $"Bỏ {quantity:N0}" : "Xác nhận";
        }

        void ApplyFixedQuantityState() {
            quantityField.SetEnabled(!fixedQuantity);
            minusButton.SetEnabled(!fixedQuantity);
            plusButton.SetEnabled(!fixedQuantity);
            quantitySlider.style.display = fixedQuantity ? DisplayStyle.None : DisplayStyle.Flex;
            halfButton.style.display = fixedQuantity ? DisplayStyle.None : DisplayStyle.Flex;
            allButton.style.display = fixedQuantity ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void SetIcon(Sprite sprite) {
            itemIcon.style.backgroundColor = Color.clear;
            if (sprite != null) {
                itemIcon.style.backgroundImage = new StyleBackground(sprite);
            } else {
                itemIcon.style.backgroundImage = StyleKeyword.None;
            }
        }

        static Button CreateButton(string name, string text, string className) {
            var button = new BagButton { name = name, text = text, Kind = name == "quantity-confirm" ? "action-danger" : "action-small" };
            button.AddToClassList(className);
            return button;
        }
    }
}

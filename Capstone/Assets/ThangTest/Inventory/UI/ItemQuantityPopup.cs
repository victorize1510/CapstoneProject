using System;
using UnityEngine;
using UnityEngine.UIElements;

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

            actionTitle = new Label { name = "quantity-action-title", text = "Action" };
            actionTitle.AddToClassList("quantity-popup-title");
            panel.Add(actionTitle);

            itemIcon = new VisualElement { name = "quantity-item-icon" };
            itemIcon.AddToClassList("quantity-popup-icon");
            panel.Add(itemIcon);

            itemName = new Label { name = "quantity-item-name", text = "Item" };
            itemName.AddToClassList("quantity-popup-item-name");
            panel.Add(itemName);

            ownedQuantity = new Label { name = "quantity-owned", text = "Owned: 0" };
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
            halfButton = CreateButton("quantity-half", "Half", "quantity-quick-button");
            allButton = CreateButton("quantity-all", "All", "quantity-quick-button");
            quickRow.Add(oneButton);
            quickRow.Add(halfButton);
            quickRow.Add(allButton);

            var actionRow = new VisualElement { name = "quantity-action-row" };
            actionRow.AddToClassList("quantity-action-row");
            panel.Add(actionRow);

            confirmButton = CreateButton("quantity-confirm", "Confirm", "quantity-confirm-button");
            cancelButton = CreateButton("quantity-cancel", "Cancel", "quantity-cancel-button");
            actionRow.Add(confirmButton);
            actionRow.Add(cancelButton);

            RegisterCallbacks();
            root.Add(overlay);
            Hide();
        }

        public void Show(string title, InventoryItemSnapshot item, Action<int> onConfirm) {
            currentItem = item;
            confirmHandler = onConfirm;
            maxQuantity = Mathf.Max(1, item != null ? item.Quantity : 1);
            fixedQuantity = item == null || !item.Stackable || maxQuantity <= 1;

            actionTitle.text = title ?? "Action";
            itemName.text = item != null ? item.Name : "Item";
            ownedQuantity.text = item != null ? $"Owned: {maxQuantity}" : "Owned: 0";
            SetIcon(item != null ? item.Icon : null);
            ApplyFixedQuantityState();
            SetQuantity(1);

            overlay.style.display = DisplayStyle.Flex;
            quantityField.Focus();
        }

        public void Hide() {
            overlay.style.display = DisplayStyle.None;
            currentItem = null;
            confirmHandler = null;
        }

        void RegisterCallbacks() {
            minusButton.clicked += () => SetQuantity(quantity - 1);
            plusButton.clicked += () => SetQuantity(quantity + 1);
            oneButton.clicked += () => SetQuantity(1);
            halfButton.clicked += () => SetQuantity(Mathf.Max(1, maxQuantity / 2));
            allButton.clicked += () => SetQuantity(maxQuantity);
            cancelButton.clicked += Hide;

            confirmButton.clicked += () => {
                var confirmedQuantity = Mathf.Clamp(quantity, 1, maxQuantity);
                var handler = confirmHandler;
                Hide();
                handler?.Invoke(confirmedQuantity);
            };

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
            itemIcon.style.backgroundColor = new Color(0.43f, 0.49f, 0.31f);
            if (sprite != null) {
                itemIcon.style.backgroundImage = new StyleBackground(sprite);
            } else {
                itemIcon.style.backgroundImage = StyleKeyword.None;
            }
        }

        static Button CreateButton(string name, string text, string className) {
            var button = new Button { name = name, text = text };
            button.AddToClassList(className);
            return button;
        }
    }
}

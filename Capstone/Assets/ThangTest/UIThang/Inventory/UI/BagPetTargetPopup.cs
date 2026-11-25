using System;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory {
    internal sealed class BagPetTargetPopup {
        readonly VisualElement overlay;
        readonly Label title, feedback;
        readonly VisualElement rows;
        internal bool IsOpen => overlay.style.display.value == DisplayStyle.Flex;
        internal bool BelongsTo(VisualElement root) => overlay.parent == root;

        internal BagPetTargetPopup(VisualElement root) {
            overlay = new VisualElement { name = "bag-pet-target-popup" };
            overlay.AddToClassList("quantity-popup-overlay");
            Bag.BagPresentation.Stretch(overlay);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = new Color(.04f, .06f, .1f, .62f);
            var panel = new VisualElement();
            panel.AddToClassList("quantity-popup-panel");
            panel.style.width = new Length(92, LengthUnit.Percent);
            panel.style.maxWidth = 440;
            panel.style.paddingLeft = panel.style.paddingRight = 14;
            panel.style.paddingTop = panel.style.paddingBottom = 12;
            panel.style.backgroundColor = new Color(.10f, .15f, .24f);
            overlay.Add(panel);
            title = new Label();
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(title);
            rows = new VisualElement();
            panel.Add(rows);
            feedback = new Label();
            feedback.style.whiteSpace = WhiteSpace.Normal;
            feedback.style.minHeight = 22;
            panel.Add(feedback);
            var close = new Button(Hide) { text = "Đóng" };
            panel.Add(close);
            overlay.style.display = DisplayStyle.None;
            root.Add(overlay);
        }

        internal void Show(string itemName, Func<int, PetController> petAt,
            Func<PetController, (bool allowed, string reason)> preview, bool stone, Action<PetController> select) {
            title.text = "Chọn thú dùng " + itemName;
            feedback.text = string.Empty;
            rows.Clear();
            bool anyAllowed = false;
            for (int rowIndex = 0; rowIndex < 2; rowIndex++) {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                rows.Add(row);
                for (int column = 0; column < 3; column++) {
                    int index = rowIndex * 3 + column;
                    var pet = petAt(index);
                    var stats = pet != null ? pet.GetComponentInChildren<PetHudRuntimeStats>(true) : null;
                    var meta = pet != null ? pet.GetComponentInChildren<PetCollectionMetadata>(true) : null;
                    var result = stats != null ? preview(pet) : (false, "Ô trống");
                    anyAllowed |= result.Item1;
                    var card = new Button(() => {
                        if (petAt(index) != pet) { Report("Đội hình vừa thay đổi. Hãy mở lại bảng."); return; }
                        var latest = preview(pet);
                        if (!latest.allowed) { Report(latest.reason); return; }
                        select(pet);
                    }) { tooltip = result.Item2 };
                    card.style.flexBasis = 0;
                    card.style.flexGrow = 1;
                    card.style.minWidth = 0;
                    card.style.minHeight = 96;
                    card.style.marginLeft = card.style.marginRight = 3;
                    card.style.marginTop = card.style.marginBottom = 3;
                    card.style.alignItems = Align.Center;
                    card.style.justifyContent = Justify.Center;
                    card.SetEnabled(stats != null && (!stone || result.Item1));
                    if (!result.Item1) card.style.opacity = .55f;
                    var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                    portrait.style.width = portrait.style.height = 40;
                    if (stats != null && stats.Icon != null) portrait.style.backgroundImage = new StyleBackground(stats.Icon);
                    portrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                    card.Add(portrait);
                    string name = stats == null ? "Trống" : meta?.ResolveDisplayName(stats.DisplayName) ?? stats.DisplayName;
                    var label = new Label($"{index + 1} · {name}") { pickingMode = PickingMode.Ignore };
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.maxWidth = new Length(100, LengthUnit.Percent);
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    card.Add(label);
                    row.Add(card);
                }
            }
            if (stone && !anyAllowed) Report("Không có thú trong đội dùng được đá này.");
            overlay.style.display = DisplayStyle.Flex;
            overlay.BringToFront();
            PanelFocusUtility.Open(overlay);
        }

        internal void Report(string message) => feedback.text = message ?? string.Empty;

        internal void Hide() {
            if (!IsOpen) return;
            PanelFocusUtility.Close(overlay);
            overlay.style.display = DisplayStyle.None;
        }
    }
}

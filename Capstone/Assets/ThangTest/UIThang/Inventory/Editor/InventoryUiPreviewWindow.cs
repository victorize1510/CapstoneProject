using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Editor {
    /// <summary>
    /// Shows the real UXML and USS at a game-sized resolution. Sample content is
    /// added only to this editor window; it is never saved into the game UI.
    /// </summary>
    public sealed class InventoryUiPreviewWindow : EditorWindow {
        const string UiFolder = "Assets/ThangTest/UIThang/Inventory/UI/";

        static readonly string[] Panels = {
            "InventoryPanel", "QuestPanel", "PetsPanel", "BoxPanel",
            "SkillsPanel", "PetDetailsPanel", "PetLevelUpPanel", "PetHealPanel",
            "PetEvolutionPanel", "PetReleasePanel", "MenuPlaceholderPanel", "MonsterItemRow", "MonsterInventory"
        };

        static readonly string[] Resolutions = { "1920 × 1080", "1600 × 900", "2560 × 1440" };
        static readonly Vector2Int[] Sizes = {
            new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(2560, 1440)
        };

        VisualElement previewFrame;
        VisualElement preview;
        int panelIndex;
        int resolutionIndex;

        [MenuItem("Tools/ToolCuaThang/UI/Preview Inventory Panels")]
        static void Open() {
            var window = GetWindow<InventoryUiPreviewWindow>();
            window.titleContent = new GUIContent("Inventory UI Preview");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        public void CreateGUI() {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.height = 38;

            var panelPicker = new PopupField<string>(new List<string>(Panels), panelIndex);
            panelPicker.style.width = 215;
            panelPicker.RegisterValueChangedCallback(evt => {
                panelIndex = System.Array.IndexOf(Panels, evt.newValue);
                RefreshPreview();
            });
            toolbar.Add(panelPicker);

            var resolutionPicker = new PopupField<string>(new List<string>(Resolutions), resolutionIndex);
            resolutionPicker.style.width = 150;
            resolutionPicker.style.marginLeft = 8;
            resolutionPicker.RegisterValueChangedCallback(evt => {
                resolutionIndex = System.Array.IndexOf(Resolutions, evt.newValue);
                RefreshPreview();
            });
            toolbar.Add(resolutionPicker);

            var openSource = new Button(() => {
                var asset = LoadSelectedPanel();
                if (asset != null) AssetDatabase.OpenAsset(asset);
            }) { text = "Mở UXML" };
            openSource.style.marginLeft = 8;
            toolbar.Add(openSource);

            var refresh = new Button(RefreshPreview) { text = "Làm mới" };
            refresh.style.marginLeft = 5;
            toolbar.Add(refresh);
            rootVisualElement.Add(toolbar);

            var note = new Label("Xem bằng UXML/USS thật; dữ liệu mẫu chỉ có trong Editor. Sửa USS rồi bấm Làm mới.");
            note.style.marginLeft = 10;
            note.style.marginBottom = 5;
            rootVisualElement.Add(note);

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            scroll.style.backgroundColor = new Color(0.13f, 0.15f, 0.16f);
            rootVisualElement.Add(scroll);

            previewFrame = new VisualElement();
            previewFrame.style.position = Position.Relative;
            previewFrame.style.flexShrink = 0;
            previewFrame.style.overflow = Overflow.Hidden;
            scroll.Add(previewFrame);

            preview = new VisualElement();
            preview.AddToClassList("inventory-editor-preview");
            preview.style.position = Position.Relative;
            preview.style.flexShrink = 0;
            previewFrame.Add(preview);
            RefreshPreview();
        }

        VisualTreeAsset LoadSelectedPanel() {
            if (panelIndex < 0 || panelIndex >= Panels.Length) panelIndex = 0;
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiFolder + Panels[panelIndex] + ".uxml");
        }

        void RefreshPreview() {
            if (preview == null) return;
            if (resolutionIndex < 0 || resolutionIndex >= Sizes.Length) resolutionIndex = 0;

            // CloneTree adds stylesheets to its target. Clear removes only children;
            // retaining those sheets would mix the previous panel with the next.
            preview.Clear();
            preview.styleSheets.Clear();
            // The runtime PanelSettings uses 1920x1080 as its reference and all
            // offered sizes share that aspect ratio, so scale the same layout.
            previewFrame.style.width = Sizes[resolutionIndex].x;
            previewFrame.style.height = Sizes[resolutionIndex].y;
            preview.style.width = Sizes[0].x;
            preview.style.height = Sizes[0].y;
            float scale = Sizes[resolutionIndex].x / (float)Sizes[0].x;
            preview.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
            preview.style.scale = new Scale(new Vector3(scale, scale, 1f));

            var asset = LoadSelectedPanel();
            if (asset == null) {
                preview.Add(new Label("Không tìm thấy UXML: " + Panels[panelIndex]));
                return;
            }

            asset.CloneTree(preview);
            AddEditorSamples();
        }

        void AddEditorSamples() {
            switch (Panels[panelIndex]) {
                case "MonsterInventory":
                case "InventoryPanel": AddInventorySamples(); break;
                case "QuestPanel": AddQuestSamples(); break;
                case "PetsPanel": AddPetsSamples(); break;
                case "BoxPanel": AddBoxSamples(); break;
            }
        }

        void AddInventorySamples() {
            var grid = preview.Q<VisualElement>("item-grid");
            if (grid == null) return;
            grid.RegisterCallback<GeometryChangedEvent>(_ => MonsterInventoryController.LayoutItemGrid(grid, false));
            string[] names = { "Ball Green", "Ball Blue", "Potion", "Berry", "Crystal", "Key" };
            for (int i = 0; i < 18; i++) {
                var card = SampleButton("inventory-item-card");
                var icon = SampleElement("inventory-item-icon");
                icon.Add(Text((i % 3 == 0 ? "◈" : "✦"), "inventory-item-placeholder"));
                card.Add(icon);
                card.Add(Text(names[i % names.Length], "inventory-item-name"));
                card.Add(Text((i + 1).ToString(), "inventory-item-quantity"));
                grid.Add(card);
            }
            SetText("detail-name", "Ball Green");
            SetText("detail-rarity", "COMMON");
            SetText("detail-category", "Danh mục: Dụng cụ bắt");
            SetText("detail-quantity", "Số lượng sở hữu: 12");
            SetText("detail-description", "Vật phẩm mẫu để căn bố cục trong UI Builder.");
            SetText("detail-icon-placeholder", "BALL");
            preview.Q<Label>("inventory-detail-empty").style.display = DisplayStyle.None;
            preview.Q<ScrollView>("detail-panel").style.display = DisplayStyle.Flex;
            preview.Q<VisualElement>("inventory-actions").style.display = DisplayStyle.Flex;
            var source = preview.Q<VisualElement>("detail-source-block");
            if (source != null) source.style.display = DisplayStyle.None;
            var empty = preview.Q<Label>("no-items-label");
            if (empty != null) empty.style.display = DisplayStyle.None;
        }

        void AddQuestSamples() {
            var list = preview.Q<ScrollView>("quest-list-scroll");
            if (list == null) return;
            string[] names = { "Hành trình đầu tiên", "Tìm người bạn đồng hành", "Khám phá khu rừng" };
            for (int i = 0; i < names.Length; i++) {
                var card = SampleElement("quest-card");
                card.Add(SampleElement("quest-card-icon"));
                var copy = SampleElement("quest-card-text");
                copy.Add(Text(names[i], "quest-card-title"));
                copy.Add(Text("Nhiệm vụ chính • Lv. " + (i + 1), "quest-card-meta"));
                card.Add(copy);
                var progress = SampleElement("quest-card-right");
                progress.Add(Text(i == 0 ? "2 / 3" : "0 / 1", "quest-card-progress"));
                card.Add(progress);
                list.Add(card);
            }
            SetText("quest-detail-title", names[0]);
            SetText("quest-description", "Nội dung mẫu để xem độ rộng và khoảng cách của phần chi tiết.");
            var empty = preview.Q<Label>("quest-page-empty");
            if (empty != null) empty.style.display = DisplayStyle.None;
        }

        void AddPetsSamples() {
            var row = preview.Q<VisualElement>("pets-party-row");
            if (row != null) {
                for (int i = 0; i < 6; i++) {
                    var card = SampleButton("pet-party-card");
                    if (i == 0) card.AddToClassList("is-selected");
                    else card.AddToClassList("is-empty");
                    var top = SampleElement("pet-card-top");
                    top.Add(Text((i + 1).ToString(), "pet-card-slot-number"));
                    card.Add(top);
                    var content = SampleElement("pet-card-content");
                    var portrait = SampleElement("pet-card-portrait");
                    portrait.Add(Text(i == 0 ? "DR" : "🐾", "pet-card-portrait-fallback"));
                    portrait.Add(Text(i == 0 ? "Lv. 4" : "", "pet-card-level"));
                    content.Add(portrait);
                    var info = SampleElement("pet-card-info");
                    var name = SampleElement("pet-card-name-row");
                    name.Add(Text(i == 0 ? "Dragon Dusk" : "Trống", "pet-card-name"));
                    info.Add(name);
                    var hp = SampleElement("pet-card-bar-row");
                    hp.Add(Text("HP", "pet-card-bar-label"));
                    var track = SampleElement("pet-card-bar-track");
                    var fill = SampleElement("pet-card-hp-fill");
                    fill.style.width = Length.Percent(100);
                    track.Add(fill);
                    hp.Add(track);
                    info.Add(hp);
                    info.Add(Text(i == 0 ? "76 / 76" : "", "pet-card-hp-value"));
                    content.Add(info);
                    card.Add(content);
                    row.Add(card);
                }
            }

            SetText("pets-selected-name", "Dragon Dusk");
            SetText("pets-selected-species", "Loài: Dragon Dusk Elder");
            SetText("pets-selected-level", "Lv. 4");
            SetText("pets-selected-element", "Hệ: Bóng tối");
            SetText("pets-health-value", "76 / 76");
            SetText("pets-exp-value", "200 / 340");
            SetText("pets-selected-portrait-fallback", "DRAGON DUSK");
            SetPercent("pets-health-fill", 100);
            SetPercent("pets-exp-fill", 59);
            SetSampleStat("health", 76, 96);
            SetSampleStat("attack", 11, 200);
            SetSampleStat("magic-attack", 12, 200);
            SetSampleStat("defense", 9, 200);
            SetSampleStat("magic-defense", 10, 200);
            SetSampleStat("speed", 11, 200);
            SetSampleStat("critical-rate", 10, 100, true);
            SetSampleStat("critical-damage", 50, 100, true);

            var skills = preview.Q<VisualElement>("pets-skills-row");
            if (skills != null) {
                string[] names = { "Blast", "Bite", "Projectile", "Wing Strike" };
                for (int i = 0; i < names.Length; i++) {
                    var card = SampleElement("pet-skill-card");
                    var icon = SampleElement("pet-skill-icon");
                    icon.Add(Text("✦", "pet-skill-icon-fallback"));
                    card.Add(icon);
                    var copy = SampleElement("pet-skill-text");
                    copy.Add(Text(names[i], "pet-skill-name"));
                    copy.Add(Text("Lv. 1", "pet-skill-meta"));
                    card.Add(copy);
                    skills.Add(card);
                }
            }
        }

        void AddBoxSamples() {
            var party = preview.Q<VisualElement>("box-party-row");
            if (party != null) {
                for (int i = 0; i < 6; i++) {
                    var card = SampleButton("box-party-card");
                    if (i > 0) card.AddToClassList("is-empty");
                    if (i == 0) {
                        var portrait = SampleElement("box-party-portrait");
                        portrait.Add(Text("DR", "box-portrait-fallback"));
                        portrait.Add(Text("Lv. 4", "box-party-level"));
                        card.Add(portrait);
                        var info = SampleElement("box-party-info");
                        info.Add(Text("Dragon Dusk", "box-party-name"));
                        var hp = SampleElement("box-party-hp-row");
                        hp.Add(Text("HP", "box-party-hp-label"));
                        var track = SampleElement("box-party-hp-track");
                        var fill = SampleElement("box-health-fill");
                        fill.style.width = Length.Percent(100);
                        track.Add(fill);
                        hp.Add(track);
                        info.Add(hp);
                        info.Add(Text("76 / 76", "box-party-hp-value"));
                        card.Add(info);
                    } else {
                        var emptySlot = SampleElement("box-empty-slot-content");
                        emptySlot.Add(Text("🐾", "box-empty-slot-icon"));
                        emptySlot.Add(Text("Trống", "box-empty-slot-text"));
                        card.Add(emptySlot);
                    }
                    card.Add(Text((i + 1).ToString(), "box-slot-number"));
                    if (i == 0) {
                        card.Add(Text("LEAD", "box-lead-badge"));

                    }
                    party.Add(card);
                }
            }
            var storage = preview.Q<VisualElement>("box-storage-grid");
            if (storage != null) {
                for (int i = 0; i < 28; i++) {
                    var card = SampleButton("box-storage-card");
                    if (i >= 3) card.AddToClassList("is-empty");
                    if (i < 3) {
                        var portrait = SampleElement("box-storage-portrait");
                        portrait.Add(Text("PET", "box-storage-portrait-fallback"));
                        card.Add(portrait);
                        card.Add(Text("Pet " + (i + 1), "box-storage-name"));
                    }
                    storage.Add(card);
                }
            }
            SetText("box-party-count", "ĐỘI HÌNH (1 / 6)");
            SetText("box-page-count", "3 PET • TRANG 1 / 2");
            SetText("box-selected-name", "Dragon Dusk");
            SetText("box-selected-level", "Lv. 4");
            SetText("box-selected-location", "Trong Party (Slot 1)");
            SetText("box-selected-element", "Bóng tối");
            SetText("box-selected-health-value", "76 / 76");
            SetPercent("box-selected-health-fill", 100);
            SetText("box-selected-exp-value", "200 / 340");
            SetPercent("box-selected-exp-fill", 59);
            var empty = preview.Q<Label>("box-empty-label");
            if (empty != null) empty.style.display = DisplayStyle.None;
        }

        void SetText(string name, string value) {
            var label = preview.Q<Label>(name);
            if (label != null) label.text = value;
        }

        void SetSampleStat(string name, int value, int maximum, bool percentage = false) {
            SetText($"pets-stat-{name}", percentage ? $"{value}%" : value.ToString());
            SetPercent($"pets-stat-{name}-fill", 100f * value / maximum);
        }

        void SetPercent(string name, float percent) {
            var element = preview.Q<VisualElement>(name);
            if (element != null) element.style.width = Length.Percent(percent);
        }

        static VisualElement SampleElement(string className) {
            var element = new VisualElement();
            element.AddToClassList(className);
            return element;
        }

        static Button SampleButton(string className) {
            var button = new Button();
            button.AddToClassList(className);
            return button;
        }

        static Label Text(string value, string className) {
            var label = new Label(value);
            label.AddToClassList(className);
            return label;
        }
    }
}

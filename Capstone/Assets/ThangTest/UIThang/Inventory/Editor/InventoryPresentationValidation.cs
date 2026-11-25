using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.HudSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Editor {
    // Uses an isolated adapter. It never opens, modifies or saves the player's inventory.
    public sealed class InventoryPresentationValidation : EditorWindow {
        const string Ui = "Assets/ThangTest/UIThang/Inventory/UI/";
        const string RequestFile = "Temp/InventoryPresentationValidation.request";
        readonly List<MonsterItemDefinition> definitions = new List<MonsterItemDefinition>();
        readonly List<string> checks = new List<string>();
        GameObject fixture;
        PanelSettings settings;
        MonsterInventoryAdapter adapter;
        MonsterInventoryController controller;
        VisualElement view;
        int layoutIndex;
        readonly string[] panels = { "InventoryPanel", "PetsPanel", "BoxPanel", "QuestPanel" };
        readonly Vector2Int[] sizes = { new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(2560, 1440) };
        Rect titleReference, backReference;

        [InitializeOnLoadMethod]
        static void ConsumeRequest() {
            if (!File.Exists(RequestFile)) return;
            File.Delete(RequestFile);
            EditorApplication.delayCall += Run;
        }
        [MenuItem("Tools/ToolCuaThang/UI/Validate Inventory Presentation")]
        static void Run() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogWarning("[Inventory UI] Run the isolated checks in Edit Mode."); return;
            }
            var window = GetWindow<InventoryPresentationValidation>();
            window.titleContent = new GUIContent("Inventory UI Checks");
            window.minSize = new Vector2(900, 600);
            window.Show();
            window.Begin();
        }
        void Begin() {
            Cleanup(); checks.Clear(); layoutIndex = 0;
            rootVisualElement.Clear();
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1; rootVisualElement.Add(scroll);
            view = new VisualElement(); view.style.flexShrink = 0; scroll.Add(view);
            try {
                CheckUnlimitedBag();
                fixture = new GameObject("Inventory presentation test") { hideFlags = HideFlags.HideAndDontSave };
                adapter = fixture.AddComponent<MonsterInventoryAdapter>();
                typeof(MonsterInventoryAdapter).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
                Add("Apple", GameItemCategory.Food, 8, InventoryItemRarity.Common, "", "");
                Add("Berry", GameItemCategory.Food, 4, InventoryItemRarity.Rare, "Restores health", "Forest");
                Add("Quest Token", GameItemCategory.QuestItem, 1, InventoryItemRarity.Common, "", "");
                for (int i = 0; i < 18; i++) Add("Material " + i, GameItemCategory.Material, 1, InventoryItemRarity.Common, "", "");
                Add("Exact Potion", GameItemCategory.Medicine, 5, InventoryItemRarity.Common, "Heal 42 HP", "", 42);
                settings = Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Ui + "MonsterInventoryPanelSettings.asset"));
                settings.hideFlags = HideFlags.HideAndDontSave;
                var doc = fixture.AddComponent<UIDocument>();
                doc.panelSettings = settings;
                doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Ui + "MonsterInventory.uxml");
                fixture.AddComponent<PetHealService>();
                fixture.AddComponent<PetHealPanelController>().Bind(doc, adapter);
                controller = fixture.AddComponent<MonsterInventoryController>();
                // Ordinary MonoBehaviours do not receive OnEnable in Edit Mode.
                Call("OnEnable");
                controller.OpenInventoryPanel();
                var root = doc.rootVisualElement;
                root.style.width = 1920; root.style.height = 1080;
                view.Add(root);
                rootVisualElement.schedule.Execute(() => CheckBehaviour(root)).StartingIn(500);
            } catch (Exception ex) { Fail(ex); }
        }
        void Add(string name, GameItemCategory category, int quantity, InventoryItemRarity rarity, string effect, string source, int healAmount = 0) {
            var item = MonsterItemDefinition.CreateRuntime(name, category, "Description of " + name, effect,
                itemUsableFromInventory: category == GameItemCategory.Food || category == GameItemCategory.Medicine,
                itemHealAmount: healAmount, itemRarity: rarity, itemSource: source);
            definitions.Add(item); adapter.AddItem(item, quantity);
        }
        void CheckBehaviour(VisualElement root) {
            try {
                controller.UseItem += request => request.Complete(true, false, 0);
                Require(root.Q<Button>("category-all").ClassListContains("is-selected"), "Bag starts in ALL");
                Require(root.Q<Button>("category-all").Q<Label>(className: "category-count").text == "22", "ALL counts every owned kind");
                Require(root.Q<Label>("capacity-label") == null && root.Q<VisualElement>("inventory-capacity-fill") == null, "capacity display removed");
                Require(controller.SelectedItem == null && root.Q<ScrollView>("detail-panel").style.display.value == DisplayStyle.None
                    && root.Q<VisualElement>("inventory-actions").style.display.value == DisplayStyle.None
                    && root.Q<Label>("inventory-detail-empty").style.display.value == DisplayStyle.Flex, "unselected detail shows only prompt");
                controller.SelectNextItem();
                Require(controller.SelectedItem != null && root.Q<Button>("inventory-item-0").ClassListContains("is-selected"), "keyboard starts with first item");
                Call("SelectCategory", GameItemCategory.Food);
                Require(controller.SelectedItem.Name == "Apple", "default selection follows category");
                var firstCard = root.Q<Button>("inventory-item-0");
                var search = root.Q<TextField>("inventory-search-field");
                search.value = "bErRy";
                Require(controller.SelectedItem.Name == "Berry", "case-insensitive search");
                Require(ReferenceEquals(firstCard, root.Q<Button>("inventory-item-0")), "search reuses item cards");
                Require(root.Q<Button>("category-food").Q<Label>(className: "category-count").text == "2", "category counts owned item kinds");
                Require(root.Q<VisualElement>("detail-source-block").style.display.value == DisplayStyle.Flex, "real source shown");
                search.value = "missing item";
                Require(controller.SelectedItem == null && root.Q<Button>("use-button").style.display.value == DisplayStyle.None, "empty search hides actions");
                search.value = "";
                Call("SelectItem", 0, false);
                var sort = root.Q<DropdownField>("inventory-sort-field");
                sort.value = "Sắp xếp: Tên Z-A";
                Require(controller.SelectedItem.Name == "Apple", "sort preserves selected identity");
                Require(root.Q<Button>("inventory-item-0").Q<Label>("card-name").text == "Berry", "descending name order");
                var filter = root.Q<DropdownField>("inventory-filter-field");
                filter.value = "Rare trở lên";
                Require(controller.SelectedItem.Name == "Berry", "rarity filter");
                filter.value = "Dùng được";
                Require(root.Q<Button>("inventory-item-1").style.display.value == DisplayStyle.Flex, "use filter resolves real handler");
                adapter.RemoveItem(definitions[0], 3);
                Require(root.Q<Button>("inventory-item-1").Q<Label>("card-quantity").text == "5", "adapter event refreshes quantity");
                Click(root.Q<Button>("inventory-view-button"));
                Require(root.Q<VisualElement>("item-grid").ClassListContains("is-list"), "view button switches to list");
                Click(root.Q<Button>("inventory-view-button"));
                Require(!root.Q<VisualElement>("item-grid").ClassListContains("is-list"), "view button switches back to grid");
                filter.value = "Lọc: không lọc";
                Call("SelectCategory", GameItemCategory.Medicine);
                int assignments = 0;
                controller.AssignQuickSlot += request => { assignments++; request.Complete(true, false, 0); };
                Call("RefreshDetails");
                Require(root.Q<Button>("equip-button").style.display.value == DisplayStyle.Flex, "healing medicine exposes quickslot action");
                Call("RequestAction", InventoryActionType.AssignQuickSlot);
                Require(assignments == 1, "healing medicine uses existing quickslot event");
                Call("RequestAction", InventoryActionType.UseItem);
                var healing = fixture.GetComponent<PetHealPanelController>();
                var exact = typeof(PetHealPanelController).GetField("exactHealingItem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(healing);
                Require(ReferenceEquals(exact, definitions.Last()), "Use passes the selected medicine to existing Heal panel");
                Require(root.Q<VisualElement>("heal-modal-overlay").style.display.value == DisplayStyle.Flex, "Use opens Heal confirmation");
                healing.Close();
                Require(adapter.GetQuantity(definitions.Last()) == 5, "cancelling Heal does not consume medicine");
                Call("SelectCategory", GameItemCategory.QuestItem);
                Require(root.Q<Button>("drop-button").style.display.value == DisplayStyle.None, "quest item cannot be dropped");
                Require(root.Q<VisualElement>("detail-source-block").style.display.value == DisplayStyle.None, "missing source hidden");
                Require(root.Q<Label>("detail-quantity").text == "Số lượng sở hữu: 1", "owned quantity is not stack capacity");
                Add("Test Ball", GameItemCategory.CaptureBall, 1, InventoryItemRarity.Common, "", "");
                Call("SelectCategory", GameItemCategory.CaptureBall);
                controller.ConfirmSelected();
                Call("RequestAction", InventoryActionType.AssignQuickSlot);
                Require(assignments == 1 && root.Q<Button>("equip-button").style.display.value == DisplayStyle.None, "capture items reject quickslots including keyboard/API");
                Click(root.Q<Button>("category-all"));
                Require(root.Q<Button>("inventory-item-22").style.display.value == DisplayStyle.Flex, "ALL button restores items from all categories");
                CheckPartyFavorites(root);
                controller.Close(); Require(!controller.IsOpen, "Back closes through existing flow");
                view.Clear(); Cleanup(); NextLayout();
            } catch (Exception ex) { Fail(ex); }
        }
        void CheckUnlimitedBag() {
            var bagObject = new GameObject("Unlimited Bag check") { hideFlags = HideFlags.HideAndDontSave };
            var single = MonsterItemDefinition.CreateRuntime("Bag growth single", GameItemCategory.Material, "", "", itemStackable: false);
            var stack = MonsterItemDefinition.CreateRuntime("Bag growth stack", GameItemCategory.Material, "", "", itemMaxStackSize: 10);
            try {
                var bag = bagObject.AddComponent<MonsterInventoryAdapter>();
                bag.AddItem(single, 45);
                bag.AddItem(single.CreateItem());
                bag.AddItem(stack, 15);
                int before = bag.Capacity;
                bag.AddItem(stack, 5);
                Require(bag.GetQuantity(single) == 46 && bag.GetQuantity(stack) == 20, "Bag grows beyond 40 without losing existing items");
                Require(bag.Capacity == before, "existing stack space reused before growth");
                var saved = bag.CreateSaveData();
                Require(bag.RestoreFromSaveData(saved, out string error), "expanded Bag save restores: " + error);
                Require(bag.GetQuantity(single) == 46 && bag.GetQuantity(stack) == 20, "expanded Bag quantities survive save roundtrip");
                bag.AddItem(stack, 1);
                Require(bag.GetQuantity(stack) == 21, "restored Bag can grow again");
            } finally { DestroyImmediate(bagObject); DestroyImmediate(single); DestroyImmediate(stack); }
        }
        void CheckPartyFavorites(VisualElement root) {
            var party = fixture.AddComponent<PetPartyPanelController>();
            typeof(PetPartyPanelController).GetField("autoFindProvider", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(party, false);
            party.Bind(fixture.GetComponent<UIDocument>());
            var refresh = typeof(PetPartyPanelController).GetMethod("RefreshPartyCard", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 6; i++) {
                var card = root.Q<Button>("pets-party-card-" + (i + 1));
                refresh.Invoke(party, new object[] { card, i, new PetSlotHudData { occupied = true, favorite = true, displayName = "Pet " + i } });
                var star = card.Q<Button>("favorite");
                Require(star != null && star.text == "★" && star.style.display.value == DisplayStyle.Flex, "favorite visible on party slot " + (i + 1));
                refresh.Invoke(party, new object[] { card, i, new PetSlotHudData { occupied = true, favorite = false, displayName = "Pet " + i } });
                Require(star.text == "☆", "party slot " + (i + 1) + " reflects removing favorite");
                refresh.Invoke(party, new object[] { card, i, default(PetSlotHudData) });
                Require(star.style.display.value == DisplayStyle.None, "empty party slot " + (i + 1) + " has no favorite button");
            }
        }
        void NextLayout() {
            if (layoutIndex >= panels.Length * sizes.Length) { Finish(); return; }
            view.Clear(); view.styleSheets.Clear();
            Vector2Int size = sizes[layoutIndex / panels.Length];
            // Match the game's PanelSettings: all three 16:9 targets share a 1920x1080 logical layout.
            view.style.width = 1920; view.style.height = 1080;
            view.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
            float scale = size.x / 1920f; view.style.scale = new Scale(new Vector3(scale, scale, 1));
            string panel = panels[layoutIndex % panels.Length];
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Ui + "MonsterInventory.uxml").CloneTree(view);
            view.Q<VisualElement>("monster-inventory-root").RemoveFromClassList("ui-builder-preview");
            foreach (string screen in panels) view.Q<VisualElement>(ScreenName(screen)).style.display = screen == panel ? DisplayStyle.Flex : DisplayStyle.None;
            view.Q<VisualElement>("quest-inventory-shell").style.display = panel == "InventoryPanel" || panel == "QuestPanel" ? DisplayStyle.Flex : DisplayStyle.None;
            view.Q<VisualElement>("quest-inventory-shell").EnableInClassList("bag-mode", panel == "InventoryPanel");
            view.Q<VisualElement>("quest-inventory-shell").EnableInClassList("journal-mode", panel == "QuestPanel");
            if (panel == "InventoryPanel") {
                var grid = view.Q<VisualElement>("item-grid");
                for (int i = 0; i < 30; i++) { var card = new Button(); card.AddToClassList("inventory-item-card"); grid.Add(card); }
                grid.RegisterCallback<GeometryChangedEvent>(_ => MonsterInventoryController.LayoutItemGrid(grid, false));
                view.Q<Label>("no-items-label").style.display = DisplayStyle.None;
                view.Q<Label>("detail-name").text = "Vật phẩm có tên dài để kiểm tra xuống dòng";
                view.Q<Label>("inventory-detail-empty").style.display = DisplayStyle.None;
                view.Q<ScrollView>("detail-panel").style.display = DisplayStyle.Flex;
                view.Q<VisualElement>("inventory-actions").style.display = DisplayStyle.Flex;
            }
            if (panel == "PetsPanel") {
                // No live pet fills this template; force overflow to exercise retained scrolling.
                var overflow = new VisualElement();
                overflow.style.height = 2000; overflow.style.flexShrink = 0;
                view.Q<ScrollView>("pets-information-scroll").Add(overflow);
            }
            rootVisualElement.schedule.Execute(CheckLayout).StartingIn(300);
        }
        void CheckLayout() {
            try {
                string panel = panels[layoutIndex % panels.Length];
                var screen = view.Q<VisualElement>(ScreenName(panel));
                var title = screen.Q<Label>(className: "screen-title");
                var back = screen.Q<Button>(className: "screen-back");
                Rect titleBounds = screen.WorldToLocal(title.worldBound), backBounds = screen.WorldToLocal(back.worldBound);
                // Compare header insets within each panel; Bag/PETS now occupy the full viewport.
                backBounds.x = screen.resolvedStyle.width - backBounds.xMax;
                checks.Add(panel + " bounds: title=" + titleBounds + ", back=" + backBounds + ", panel=" + screen.worldBound + ", viewport=" + view.worldBound);
                if (layoutIndex % panels.Length == 0) { titleReference = titleBounds; backReference = backBounds; }
                Require(Mathf.Abs(titleBounds.x - titleReference.x) <= 2 && Mathf.Abs(titleBounds.y - titleReference.y) <= 2,
                    panel + " title alignment at " + sizes[layoutIndex / panels.Length]);
                Require(Mathf.Abs(backBounds.x - backReference.x) <= 2 && Mathf.Abs(backBounds.y - backReference.y) <= 2,
                    panel + " Back alignment");
                Require(title.resolvedStyle.fontSize == 38, panel + " title font size");
                if (panel == "PetsPanel") {
                    Require(Mathf.Abs(screen.worldBound.width - view.worldBound.width) <= 2
                        && Mathf.Abs(screen.worldBound.height - view.worldBound.height) <= 2, "PETS fills viewport at " + sizes[layoutIndex / panels.Length]);
                    var scroll = view.Q<ScrollView>("pets-information-scroll");
                    Require(scroll.verticalScrollerVisibility == ScrollerVisibility.Hidden, "PETS vertical scrollbar hidden");
                    float before = scroll.scrollOffset.y;
                    scroll.scrollOffset = new Vector2(0, 40);
                    Require(scroll.scrollOffset.y > before, "PETS contents remain scrollable");
                    scroll.scrollOffset = Vector2.zero;
                }
                if (panel == "InventoryPanel") {
                    Require(Mathf.Abs(screen.worldBound.width - view.worldBound.width) <= 2
                        && Mathf.Abs(screen.worldBound.height - view.worldBound.height) <= 2, "Bag fills viewport at " + sizes[layoutIndex / panels.Length]);
                    var grid = view.Q<VisualElement>("item-grid");
                    var cards = grid.Children().ToList();
                    int columns = cards.Count(c => Mathf.Abs(c.layout.y - cards[0].layout.y) < 1);
                    Require(columns >= 5 && columns <= 7, "5-7 grid columns");
                    Require(cards.All(c => c.layout.xMax <= grid.layout.width + 1), "no horizontal card overflow");
                    MonsterInventoryController.LayoutItemGrid(grid, true);
                    Require(cards.All(c => Mathf.Abs(c.style.height.value.value - 76f) < 0.1f), "list rows use compact height");
                    MonsterInventoryController.LayoutItemGrid(grid, false);
                    var detail = view.Q<VisualElement>(className: "inventory-detail-column");
                    var body = view.Q<VisualElement>("inventory-body");
                    Require(detail.worldBound.xMax <= body.worldBound.xMax + 1, "detail fits screen");
                    Require(view.Q<VisualElement>("detail-icon").resolvedStyle.width > 300, "large item preview");
                }
                layoutIndex++; NextLayout();
            } catch (Exception ex) { Fail(ex); }
        }
        void Call(string method, params object[] args) => typeof(MonsterInventoryController)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(controller, args);
        static string ScreenName(string panel) => panel.Replace("Panel", "").ToLowerInvariant() + "-panel";
        static void Click(Button button) => typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button.clickable, new object[] { null });
        void Require(bool passed, string message) {
            if (!passed) throw new InvalidOperationException(message);
            checks.Add("PASS " + message);
        }
        void Finish() {
            Cleanup();
            File.WriteAllLines("Temp/InventoryPresentationValidation.txt", checks);
            Debug.Log("[Inventory UI] PASS " + checks.Count(c => c.StartsWith("PASS ")) + " checks; isolated data; 1920x1080 / 1600x900 / 2560x1440.");
        }
        void Fail(Exception ex) {
            checks.Add("FAIL " + ex); Cleanup();
            File.WriteAllLines("Temp/InventoryPresentationValidation.txt", checks);
            Debug.LogError("[Inventory UI] " + ex);
        }
        void Cleanup() {
            if (controller != null) Call("OnDisable");
            controller = null;
            if (fixture != null) DestroyImmediate(fixture);
            if (settings != null) DestroyImmediate(settings);
            foreach (var item in definitions) if (item != null) DestroyImmediate(item);
            definitions.Clear(); fixture = null; settings = null;
        }
        void OnDisable() { Cleanup(); }
    }
}

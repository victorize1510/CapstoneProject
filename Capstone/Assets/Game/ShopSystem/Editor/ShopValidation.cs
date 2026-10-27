using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using GDS.Core;
using GDS.Core.Events;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.ShopSystem.Editor {
    public sealed class ShopPreviewWindow : EditorWindow {
        const string UI = "Assets/ThangTest/UIThang/Inventory/UI/";
        internal VisualElement Surface;
        internal ShopScreenController Controller;
        internal ShopSource Source;
        internal MonsterInventoryAdapter Bag;
        internal PlayerCurrencyWallet Wallet;
        GameObject owner;
        ShopCatalog catalog;
        readonly List<MonsterItemDefinition> items = new List<MonsterItemDefinition>();
        [MenuItem("Game Tools/GameToolThang/HUD/Preview Shop (Editor fixture)")]
        public static ShopPreviewWindow OpenPreview() {
            var window = GetWindow<ShopPreviewWindow>("Shop — dữ liệu xem thử");
            window.position = new Rect(40, 70, 1366, 768); window.Show(); return window;
        }
        public void CreateGUI() {
            rootVisualElement.Add(new Label("CHỈ XEM THỬ TRONG EDITOR — GIÁ MINH HỌA, KHÔNG LƯU VÀO GAME"));
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal); scroll.style.flexGrow = 1; rootVisualElement.Add(scroll);
            Surface = new VisualElement(); Surface.style.width = 1920; Surface.style.height = 1080; Surface.style.flexShrink = 0; scroll.Add(Surface);
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UI + "ShopScreen.uxml").CloneTree(Surface);
            owner = new GameObject("ShopEditorFixture") { hideFlags = HideFlags.HideAndDontSave };
            Bag = owner.AddComponent<MonsterInventoryAdapter>(); Wallet = owner.AddComponent<PlayerCurrencyWallet>(); Wallet.AddGold(12450);
            Source = owner.AddComponent<ShopSource>(); catalog = ShopValidation.Fixture(items); Source.Configure(catalog);
            Controller = owner.AddComponent<ShopScreenController>(); Controller.Bind(Surface, Bag, Wallet); Controller.BackRequested += Close; Controller.Open(Source);
        }
        void OnDisable() {
            if (owner != null) DestroyImmediate(owner);
            if (catalog != null) DestroyImmediate(catalog);
            foreach (var item in items) if (item != null) DestroyImmediate(item); items.Clear();
        }
    }

    public static class ShopValidation {
        static readonly List<string> results = new List<string>();
        static bool running;
        static double nextPoll;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string Output = Capstone.Game.UISystem.QaArtifactPaths.Root + "shop-ui/";
        static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); results.Add("PASS " + label); }
        static void Submit(Button button) { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
        static T Field<T>(object value, string field) => (T)value.GetType().GetField(field, Private).GetValue(value);

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Shop")]
        public static async void Run() {
            if (running || EditorApplication.isPlaying) return;
            running = true; results.Clear(); ShopPreviewWindow window = null;
            try {
                ValidateTransactions();
                var defaults = Resources.Load<ShopCatalog>("Shop/DefaultShopCatalog");
                Check(defaults != null && defaults.Offers.Count == 2, "default catalog uses two existing project definitions");
                foreach (var offer in defaults.Offers) Check(offer.Item != null && offer.GoldPrice == -1, "actual item has unset price: " + offer.Id);
                window = ShopPreviewWindow.OpenPreview(); await Task.Delay(350);
                var root = window.Surface.Q("shop-panel"); var controller = window.Controller;
                Check(root.Query<Button>(className: "shop-category").ToList().Count == 5, "exactly five categories");
                Check(root.Q<Label>(className: "screen-title").text == "CỬA HÀNG", "Vietnamese title");
                var search = root.Q<TextField>("shop-search"); search.value = "thuoc hoi phuc"; await Task.Delay(200);
                Check(Field<IList>(controller, "filtered").Count == 1, "accent-insensitive search within category");
                search.value = "khong_co_item"; await Task.Delay(200);
                Check(root.Q("shop-detail").resolvedStyle.display == DisplayStyle.None, "no results clear details");
                search.value = ""; await Task.Delay(200);
                Submit(root.Q<Button>("shop-category-special")); await Task.Delay(200);
                Check(Field<IList>(controller, "filtered").Count == 0 && root.Q<Label>("shop-empty").text == "Hiện chưa có vật phẩm.", "empty category never falls back to All");
                Submit(root.Q<Button>("shop-category-consumables")); await Task.Delay(200);
                var sort = root.Q<DropdownField>("shop-sort"); sort.value = sort.choices[2]; await Task.Delay(200);
                var filtered = Field<List<ShopOffer>>(controller, "filtered");
                for (int i = 1; i < filtered.Count; i++) Check(filtered[i-1].GoldPrice <= filtered[i].GoldPrice, "ascending price order " + i);
                sort.value = sort.choices[3]; await Task.Delay(200);
                for (int i = 1; i < filtered.Count; i++) Check(filtered[i-1].GoldPrice >= filtered[i].GoldPrice, "descending price order " + i);
                sort.value = sort.choices[1]; await Task.Delay(200);
                string previousName = "";
                foreach (var offer in filtered) { string next = VietnameseUI.SearchKey(offer.Item.Name); Check(string.CompareOrdinal(previousName, next) <= 0, "name sort " + offer.Id); previousName = next; }
                sort.value = sort.choices[0]; await Task.Delay(200);
                foreach (var viewport in new[] { new Vector2(1920,1080), new Vector2(1600,900), new Vector2(2560,1440), new Vector2(1366,768), new Vector2(1920,1200), new Vector2(2560,1080) }) {
                    window.Surface.style.width = viewport.x; window.Surface.style.height = viewport.y; await Task.Delay(300);
                    Check(root.worldBound.width > 0, "layout computed " + viewport);
                    var cards = root.Query<Button>(className: "shop-card").ToList();
                    Check(Mathf.Abs(cards[0].worldBound.y-cards[4].worldBound.y) < 1 && cards[5].worldBound.y > cards[0].worldBound.y + 1, "five columns " + viewport + ": " + cards[0].worldBound + "; " + cards[4].worldBound + "; " + cards[5].worldBound);
                    Check(root.Q<Button>("shop-back").worldBound.xMax <= root.worldBound.xMax + 1, "Back fits " + viewport);
                    Check(root.Q<Button>("shop-buy").worldBound.yMax <= root.worldBound.yMax + 1, "purchase stays inside viewport " + viewport);
                    Check(root.Q("shop-purchase").worldBound.yMin >= root.Q(className: "screen-header").worldBound.yMax, "purchase does not overlap header " + viewport);
                    Check(root.Q<Label>(className: "screen-title").resolvedStyle.fontSize == 38, "shared title typography " + viewport);
                    Check(root.Q<Image>("shop-portrait").resolvedStyle.width <= root.Q<Image>("shop-portrait").sprite.rect.width, "preview does not enlarge low resolution source " + viewport);
                    var columns = root.Query<VisualElement>(className: "shop-column").ToList();
                    Check(columns[0].worldBound.xMax <= columns[1].worldBound.xMin && columns[1].worldBound.xMax <= columns[2].worldBound.xMin, "three columns never overlap " + viewport);
                }
                int callbacks = Field<IList>(controller, "unbind").Count;
                int pool = Field<IList>(controller, "cards").Count;
                for (int i = 0; i < 3; i++) {
                    controller.Close(); controller.Bind(window.Surface, window.Bag, window.Wallet); controller.Open(window.Source); await Task.Delay(200);
                    Check(Field<IList>(controller, "unbind").Count == callbacks, "rebind callback count stable " + i);
                    Check(Field<IList>(controller, "cards").Count == pool, "rebind card count stable " + i);
                }
                var quantity = root.Q<IntegerField>("shop-quantity"); quantity.value = -12; await Task.Delay(180);
                Check(quantity.value == 1, "quantity cannot be negative");
                quantity.value = int.MaxValue; await Task.Delay(180); Check(quantity.value <= 99, "quantity clamps to purchase maximum");
                window.Wallet.TrySpendGold(window.Wallet.Gold); await Task.Delay(180);
                Check(!root.Q<Button>("shop-buy").enabledSelf && !quantity.enabledSelf, "wallet event disables purchase immediately");
                controller.Close(); window.Wallet.AddGold(1000); Check(!controller.IsOpen, "hidden Shop remains closed on wallet changes");
                Write("validation.txt"); Debug.Log("Shop validation: " + results.Count + " checks passed.");
            } catch (Exception e) { results.Add("FAIL " + e); Write("validation.txt"); Debug.LogException(e); }
            finally { window?.Close(); running = false; }
        }

        static void ValidateTransactions() {
            var owner = new GameObject("ShopTransactionFixture") { hideFlags = HideFlags.HideAndDontSave };
            var definitions = new List<MonsterItemDefinition>(); var catalog = Fixture(definitions);
            try {
                var bag = owner.AddComponent<MonsterInventoryAdapter>(); var wallet = owner.AddComponent<PlayerCurrencyWallet>();
                typeof(MonsterInventoryAdapter).GetMethod("OnEnable", Private).Invoke(bag, null); // Editor fixtures do not receive runtime OnEnable.
                var source = owner.AddComponent<ShopSource>(); source.Configure(catalog); wallet.AddGold(1000);
                double time = 0; var service = new ShopPurchaseService(bag, wallet, () => time); var offer = source.Find("fixture-0");
                offer.GoldPrice = 100; offer.Stock = 4;
                Check(service.Maximum(offer, out _) == 4, "maximum respects stock");
                int currencyEvents = 0, bagEvents = 0;
                wallet.GoldChanged += _ => { currencyEvents++; Check(bag.GetQuantity(offer.Item) == 2 && wallet.Gold == 800 && offer.Stock == 2, "wallet observer sees committed transaction"); };
                bag.InventoryChanged += () => { bagEvents++; Check(wallet.Gold == 800 && bag.GetQuantity(offer.Item) == 2 && offer.Stock == 2, "inventory observer sees committed transaction"); };
                Check(service.TryBuy(source, offer.Id, 2, 100, out _), "successful purchase");
                Check(currencyEvents == 1 && bagEvents == 1, "one notification per completed transaction");
                Check(!service.TryBuy(source, offer.Id, 1, 100, out _), "immediate repeat rejected");
                // Fresh owners keep later failure fixtures isolated from observer assertions.
            } finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(catalog); foreach (var item in definitions) Object.DestroyImmediate(item); }

            owner = new GameObject("ShopFailureFixture") { hideFlags = HideFlags.HideAndDontSave }; definitions.Clear(); catalog = Fixture(definitions);
            try {
                var bag = owner.AddComponent<MonsterInventoryAdapter>(); var wallet = owner.AddComponent<PlayerCurrencyWallet>();
                typeof(MonsterInventoryAdapter).GetMethod("OnEnable", Private).Invoke(bag, null);
                var source = owner.AddComponent<ShopSource>(); source.Configure(catalog); wallet.AddGold(1000);
                double time = 0; var service = new ShopPurchaseService(bag, wallet, () => time); var offer = source.Find("fixture-0"); offer.GoldPrice = 100; offer.Stock = 10;
                bool Reject(int count, int price) { time += .2; return !service.TryBuy(source, offer.Id, count, price, out _); }
                Check(Reject(0,100) && Reject(-1,100), "invalid quantities rejected");
                Check(Reject(11,100), "insufficient balance rejected");
                Check(Reject(1,99), "stale displayed price rejected");
                offer.Stock = 0; Check(Reject(1,100), "empty stock rejected"); offer.Stock = 10;
                offer.Unlocked = false; Check(Reject(1,100), "locked item rejected"); offer.Unlocked = true;
                offer.GoldPrice = -1; Check(Reject(1,-1), "unset price rejected"); offer.GoldPrice = 100;
                time += .2; Check(!service.TryBuy(source,"missing",1,100,out _), "unknown listing rejected");
                Check(wallet.Gold == 1000 && bag.GetQuantity(offer.Item) == 0 && offer.Stock == 10, "failures preserve wallet, inventory and stock");
                var rejecting = new RejectingBag { Size = 3 }; bag.SetInventory(rejecting);
                Check(Reject(1,100) && wallet.Gold == 1000 && offer.Stock == 10 && bag.GetQuantity(offer.Item) == 0, "inventory rejection refunds reserved gold");
                var partial = new RejectAfterFirstStackBag { Size = 3 }; bag.SetInventory(partial); offer.Item.MaxStackSize = 1;
                Check(Reject(3,100) && wallet.Gold == 1000 && bag.GetQuantity(offer.Item) == 0, "staged delivery failure leaves no partial items");
                bag.SetInventory(new ListBag { Size = 1 });
                offer.Item.Stackable = false; time += .2; Check(service.TryBuy(source,offer.Id,3,100,out _), "multiple non-stackables occupy separate slots");
                Check(bag.OccupiedSlotCount == 3, "unlimited Bag grows using existing allocation rules");
                offer.GoldPrice = int.MaxValue; Check(service.Maximum(offer,out _) == 0, "overflow-scale price cannot exceed wallet");
                offer.GoldPrice = 0; offer.MaxPerPurchase = 4; Check(service.Maximum(offer,out _) == 4, "free item uses configured limit");
                time += .2; bool free = service.TryBuy(source,offer.Id,1,0,out string freeMessage);
                Check(free && wallet.Gold == 700, "free item does not debit currency: " + free + ", gold=" + wallet.Gold + ", " + freeMessage);
                var save = bag.CreateSaveData(); Check(save.items.Count > 0 && wallet.CreateSaveData().gold == 700, "existing save snapshots contain purchase");
                // A saved runtime definition can have the same stable identity as
                // the catalog asset. Buying must merge with that identity.
                var restoredDefinition = MonsterItemDefinition.CreateRuntime("Tên đã lưu", GameItemCategory.Medicine, "", "");
                definitions.Add(restoredDefinition); restoredDefinition.AssignStableId(offer.Item.StableId);
                bag.SetInventory(new ListBag { Size = 1 }); bag.AddItem(restoredDefinition, 2);
                Check(service.Owned(offer) == 2, "owned count resolves saved definition by stable ID");
                time += .2; Check(service.TryBuy(source,offer.Id,1,0,out _) && bag.GetItems().Count == 1 && bag.GetQuantity(restoredDefinition) == 3, "purchase merges existing saved identity without duplicate definitions");
                var defaults = Resources.Load<ShopCatalog>("Shop/DefaultShopCatalog");
                Check(defaults.Offers[1].Item.StableId == "dusk-stone" && ShopPresentation.Category(defaults.Offers[1]) == ShopCategory.Evolution, "evolution item uses existing stable ID and explicit display grouping");
            } finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(catalog); foreach (var item in definitions) Object.DestroyImmediate(item); }
        }
        sealed class RejectingBag : ListBag { public override bool Accepts(Item item) => false; }
        sealed class RejectAfterFirstStackBag : ListBag {
            int validations;
            public override bool Accepts(Item item) => ++validations <= 4;
        }
        internal static ShopCatalog Fixture(List<MonsterItemDefinition> definitions) {
            var catalog = ScriptableObject.CreateInstance<ShopCatalog>(); catalog.hideFlags = HideFlags.HideAndDontSave;
            var icon = AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>("Assets/Game/Inventory/Potion.asset")?.Icon;
            for (int i = 0; i < 32; i++) {
                var item = MonsterItemDefinition.CreateRuntime(i == 0 ? "Thuốc hồi phục" : "Vật phẩm xem thử " + i,
                    GameItemCategory.Medicine, "Thông tin xem thử để kiểm tra bố cục; không phải dữ liệu Shop chính thức.", "Hồi phục HP cho thú đồng hành.", icon);
                definitions.Add(item); item.hideFlags = HideFlags.HideAndDontSave;
                ((List<ShopOffer>)catalog.Offers).Add(new ShopOffer { Id = "fixture-" + i, Item = item, Category = i < 26 ? ShopCategory.Consumables : ShopCategory.Evolution, GoldPrice = 50 + i * 10, Stock = i == 4 ? 0 : -1 });
            }
            return catalog;
        }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Shop in Play Mode")]
        public static async void RunPlay() {
            if (running || !Application.isPlaying) return;
            running = true; results.Clear();
            var inventory = Object.FindFirstObjectByType<MonsterInventoryController>(); var menu = Object.FindFirstObjectByType<GameMenuController>();
            try {
                menu.CloseMenu(); inventory.Close(); await Task.Delay(220);
                menu.OpenShopFromWorld(); await Task.Delay(350);
                var document = inventory.GetComponent<UIDocument>(); var controller = inventory.GetComponent<ShopScreenController>();
                Check(inventory.IsShopOpen && controller.IsOpen && InventoryInputController.GameplayInputBlocked, "world Shop opens and locks gameplay");
                Check(document.rootVisualElement.Q<Label>("shop-item-name").text == "Potion", "first non-empty category auto-selects actual item");
                Check(!document.rootVisualElement.Q<Button>("shop-buy").enabledSelf, "actual price stays unset and cannot purchase");
                Check(!document.rootVisualElement.Q<Label>("shop-gold").text.Contains("Chưa"), "Shop resolves wallet created after UI OnEnable");
                Check(document.rootVisualElement.Q<Label>("shop-reason").text.Contains("giá mua"), "disabled reason is unset price, not a missing wallet");
                Directory.CreateDirectory(Output + "screenshots"); ScreenCapture.CaptureScreenshot(Output + "screenshots/Shop-actual.png"); await Task.Delay(300);
                for (int i = 0; i < 3; i++) {
                    if (i == 0) Submit(document.rootVisualElement.Q<Button>("shop-back")); else inventory.RequestClose();
                    await Task.Delay(220);
                    Check(!inventory.IsOpen && !InventoryInputController.GameplayInputBlocked, "Back unlocks world " + i);
                    menu.OpenShopFromWorld(); await Task.Delay(220); Check(inventory.IsShopOpen, "reopen world Shop " + i);
                }
                inventory.enabled = false; await Task.Delay(220);
                Check(!controller.IsOpen && !InventoryInputController.GameplayInputBlocked, "disabled owner releases Shop subscriptions and input lock"); inventory.enabled = true;
                await Task.Delay(220); menu.OpenShopFromWorld(); await Task.Delay(220);
                Check(inventory.IsShopOpen, "owner rebind opens Shop again"); inventory.RequestClose(); await Task.Delay(220);
                Write("runtime-validation.txt"); Debug.Log("Shop runtime: " + results.Count + " checks passed.");
            } catch (Exception e) { results.Add("FAIL " + e); Write("runtime-validation.txt"); Debug.LogException(e); }
            finally { inventory?.Close(); running = false; }
        }
        static void Write(string file) { Directory.CreateDirectory(Output); File.WriteAllLines(Output + file, results); }
        [InitializeOnLoadMethod]
        static void Resume() {
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
        }
        static void Poll() {
            if (running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            const string edit = "Temp/RunShop.request", play = "Temp/RunShopPlay.request";
            if (Ready(edit) && !EditorApplication.isPlaying) { File.Delete(edit); Run(); }
            if (Ready(play) && EditorApplication.isPlaying) { File.Delete(play); RunPlay(); }
            const string preview = "Temp/PreviewShop.request", close = "Temp/CloseShopPreview.request";
            if (Ready(preview)) {
                File.Delete(preview); var window = ShopPreviewWindow.OpenPreview();
                window.Surface.style.width = 1300; window.Surface.style.height = 700;
            }
            if (Ready(close)) { File.Delete(close); foreach (var window in Resources.FindObjectsOfTypeAll<ShopPreviewWindow>()) window.Close(); }
        }
        static bool Ready(string path) => File.Exists(path) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds > 1;
    }
}

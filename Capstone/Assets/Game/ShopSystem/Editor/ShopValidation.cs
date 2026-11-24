using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
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
        [MenuItem("Tools/Capstone/Advanced/HUD/Preview Shop (Editor fixture)")]
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

        [MenuItem("Tools/Capstone/Advanced/HUD/Validate expanded item data")]
        public static void ValidateExpandedItems() {
            var path = Path.Combine(QaArtifactPaths.Root, "ShopProgression", "catalog-checks.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            results.Clear();
            try {
                ValidateAuthoredCatalogs();
                var items = InventoryDefinitionCatalog.Definitions;
                Check(items.Count == 81 && items.All(item => item.Icon != null), "81 canonical items have imported sprites");
                var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
                Check(library.families.Count == 11 && items.Count(item => item.StableId.StartsWith("loot-", StringComparison.Ordinal)) == 33,
                    "33 materials for the 11 authored families");
                var familyIcons = new HashSet<Sprite>();
                var chances = new[] { .45f, .35f, .25f };
                foreach (var family in library.families) {
                    var materials = family.forms.Select(form => {
                        int stage = int.Parse(form.formId) - 1;
                        var drop = PveLootCatalog.SelectFamilyMaterial(family.definitionId, form.formId, chances[stage] - .001f);
                        Check(drop != null && PveLootCatalog.SelectFamilyMaterial(family.definitionId, form.formId, chances[stage]) == null,
                            $"{family.definitionId}/{form.formId} material and roll threshold");
                        return drop;
                    }).ToArray();
                    Check(materials.Length == 3 && materials.All(drop => drop.Icon == materials[0].Icon) && familyIcons.Add(materials[0].Icon),
                        $"{family.definitionId} shares a unique sprite across its three materials");
                }
                Check(items.Select(item => item.HealAmount).Where(amount => amount > 0).OrderBy(amount => amount)
                    .SequenceEqual(new[] { 25, 30, 50, 100, 200 }), "HP restoration amounts and existing apple");
                Check(items.Select(item => item.EnergyRestoreAmount).Where(amount => amount > 0).OrderBy(amount => amount)
                    .SequenceEqual(new[] { 25, 50, 100 }), "three fixed MP restoration amounts");
                Check(items.Where(item => item.RevivePercent > 0).Select(item => item.RevivePercent).OrderBy(amount => amount)
                    .SequenceEqual(new[] { 25, 50, 100, 100 }), "four revive values");
                Check(InventoryDefinitionCatalog.Find("revive-purifying").Cleanses == CombatAilment.All
                    && InventoryDefinitionCatalog.Find("remedy-cleanse").Cleanses == CombatAilment.All, "special items clear all ailments");
                Check(items.Where(item => item.GrantsStatBuff).Select(item => item.BuffStat).Distinct().Count() == 5,
                    "five stat fruits for five separate stats");
                Check(items.Where(item => item.EvolutionStoneElement != 0).Select(item => item.EvolutionStoneElement).Distinct().Count() == 8,
                    "eight different elements have evolution stones");
                foreach (string prefix in new[] { "fruit-cure-", "remedy-" }) {
                    var singleCures = items.Where(item => item.StableId.StartsWith(prefix, StringComparison.Ordinal) && item.Cleanses != CombatAilment.All);
                    Check(singleCures.Count() == 9 && singleCures.Aggregate(CombatAilment.None, (mask, item) => mask | item.Cleanses) == CombatAilment.Negative,
                        prefix + "covers all nine debuffs");
                }
                var defaultLoot = Resources.Load<PveLootCatalog>("Pve/LootCatalog");
                var demoLoot = AssetDatabase.LoadAssetAtPath<PveLootCatalog>("Assets/Game/CreatureSystem/Runtime/DemoSceneOne/Loot.asset");
                Check(defaultLoot != null && demoLoot != null && demoLoot.EffectiveCatalog == defaultLoot,
                    "Demo scenes use shared PvE loot data");
                foreach (var source in new[] { defaultLoot, demoLoot }) {
                    var loot = source.EffectiveCatalog;
                    var expectedFruit = items.Where(item => item.StableId == "healing-apple" || item.StableId == "leaf-berry"
                        || item.StableId.StartsWith("fruit-", StringComparison.Ordinal)).ToHashSet();
                    Check(loot.itemChance == .30f && loot.entries.Count == 16
                        && expectedFruit.SetEquals(loot.entries.Select(entry => entry.item)) && loot.entries.All(entry => entry.weight == 1 && entry.quantity == 1),
                        source.name + " drops one of sixteen fruits in an independent 30-percent roll");
                    loot.SelectSmallPotions(.049f, .029f, out var hp, out var mp);
                    Check(hp?.StableId == "hp-potion-small" && mp?.StableId == "mana-potion",
                        loot.name + " can drop both first-tier potions from the same defeat");
                    loot.SelectSmallPotions(.05f, .03f, out hp, out mp);
                    Check(hp == null && mp == null && loot.minimumGold == 2 && loot.maximumGold == 5,
                        loot.name + " respects the 5-percent HP, 3-percent MP thresholds and the gold range");
                    Check(loot.SelectEvolutionStone(PetElement.Fire, .049f)?.StableId == "stone-fire" &&
                        loot.SelectEvolutionStone(PetElement.Dark, .049f)?.StableId == "dusk-stone" &&
                        loot.SelectEvolutionStone(PetElement.Fire, .05f) == null,
                        source.name + " drops the defeated creature's primary stone at 5 percent");
                }
                Check(PveLootCatalog.SelectFamilyMaterial("Dragon Darkness", "dragon-dusk-elder", 0)?.StableId == "loot-dragon-dusk-03",
                    "Legacy dragon ID receives the current form material");
            } catch (Exception exception) {
                results.Add("FAIL " + exception);
                throw;
            } finally {
                File.WriteAllLines(path, results);
            }
            Debug.Log($"Expanded item catalog: {results.Count} checks passed.");
        }

        [MenuItem("Tools/Capstone/Advanced/HUD/Validate Shop")]
        public static async void Run() {
            if (running || EditorApplication.isPlaying) return;
            running = true; results.Clear(); ShopPreviewWindow window = null;
            try {
                ValidateTransactions();
                ValidateAuthoredCatalogs();
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
                    Check(Mathf.Approximately(root.Q<Label>(className: "screen-title").resolvedStyle.fontSize, UnifiedPanelHeader.TitleFont),
                        "shared title typography " + viewport);
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

        static void ValidateAuthoredCatalogs() {
            var defaults = Resources.Load<ShopCatalog>("Shop/DefaultShopCatalog");
            var demo = AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Game/CreatureSystem/Runtime/DemoSceneOne/Shop.asset");
            Check(defaults != null && demo != null && demo.sharedCatalog == defaults, "Demo scenes use shared shop data");
            var balls = new[] { "capture-ball-basic-01", "capture-ball-rare-01", "capture-ball-ultra-01" };
            var basicItems = new[] { balls[0], "hp-potion-small", "mana-potion", "revive-small" };
            var tenItems = basicItems.Concat(new[] { balls[1], "hp-potion-medium", "mp-potion-medium", "revive-medium",
                "remedy-bleed", "remedy-burn", "remedy-shock", "remedy-poison", "remedy-stun", "remedy-root",
                "remedy-slow", "remedy-defense-down", "remedy-magic-defense-down", "remedy-cleanse" }).ToArray();
            var fifteenItems = tenItems.Concat(new[] { balls[2], "Potion", "mp-potion-large", "revive-full" }).ToArray();
            var expected = fifteenItems.Concat(new[] { "hp-potion-super", "revive-purifying" }).ToArray();
            var owner = new GameObject("AuthoredShopFixture") { hideFlags = HideFlags.HideAndDontSave };
            try {
                var bag = owner.AddComponent<MonsterInventoryAdapter>();
                var wallet = owner.AddComponent<PlayerCurrencyWallet>();
                var source = owner.AddComponent<ShopSource>();
                var profile = owner.AddComponent<PlayerProfileRuntimeProvider>();
                double time = 0;
                var service = new ShopPurchaseService(bag, wallet, () => time, profile);
                foreach (var catalog in new[] { defaults, demo }) {
                    Check(catalog != null, "authored shop catalog loads");
                    source.Configure(catalog);
                    bag.SetInventory(new ListBag { Size = 24 });
                    wallet.RestoreFromSaveData(new CurrencySaveData { captured = true, gold = 1000 });
                    profile.ResetForNewGame();
                    var basicBall = source.Find(balls[0]);
                    var rareBall = source.Find(balls[1]);
                    var ultraBall = source.Find(balls[2]);
                    bool Visible(params string[] ids) => source.Offers.Where(service.IsVisible).Select(offer => offer.Id).ToHashSet().SetEquals(ids);
                    Check(source.Offers.Count == expected.Length && source.Offers.All(offer => offer.Item?.Icon != null),
                        catalog.name + $" lists {expected.Length} items with sprites");
                    Check(profile.Level == 1 && basicBall.GoldPrice == 5 && service.Maximum(basicBall, out _) > 0
                        && Visible(basicItems), catalog.name + " starts with only four basic items");
                    time += 1;
                    Check(!service.TryBuy(source, rareBall.Id, 1, rareBall.GoldPrice, out _) && wallet.Gold == 1000,
                        catalog.name + " rejects locked ball purchases with no deduction");
                    profile.RestoreFromSaveData(new PlayerProfileSaveData {
                        captured = true, level = 9, currentExperience = 99, requiredExperience = 100
                    });
                    Check(Visible(basicItems), catalog.name + " keeps higher tiers hidden at level nine");
                    profile.AddExperience(1);
                    Check(profile.Level == 10 && service.Maximum(rareBall, out _) > 0 && Visible(tenItems),
                        catalog.name + " adds second-tier items and remedies at level ten");
                    profile.RestoreFromSaveData(new PlayerProfileSaveData {
                        captured = true, level = 14, currentExperience = 99, requiredExperience = 100
                    });
                    profile.AddExperience(1);
                    Check(profile.Level == 15 && service.Maximum(ultraBall, out _) > 0 && Visible(fifteenItems),
                        catalog.name + " adds third-tier items at level fifteen");
                    profile.SetLevelProgress(20, 3, 100);
                    Check(Visible(expected), catalog.name + " adds the final HP and revive tiers at level twenty");
                    foreach (string id in expected) {
                        var item = InventoryDefinitionCatalog.Find(id);
                        var offer = source.Offers.SingleOrDefault(entry => entry.Item == item);
                        Check(item != null && item.Icon != null && offer != null && offer.Unlocked && offer.GoldPrice > 0,
                            catalog.name + " lists canonical item " + id);
                        time += 1;
                        int gold = wallet.Gold;
                        Check(service.TryBuy(source, offer.Id, 1, offer.GoldPrice, out _)
                            && bag.GetQuantity(item) == 1 && wallet.Gold == gold - offer.GoldPrice,
                            catalog.name + " buys " + id + " into the bag");
                    }
                    var snapshot = bag.CreateSaveData();
                    snapshot.quickItemIds = new[] { "ManaPotion", "" };
                    bag.SetInventory(new ListBag { Size = 24 });
                    Check(bag.RestoreFromSaveData(snapshot, out _) && expected.All(id => bag.GetQuantity(InventoryDefinitionCatalog.Find(id)) == 1),
                        catalog.name + " purchases survive bag reload");
                    Check(bag.GetQuickItemId(0) == "mana-potion" && bag.GetQuickItemId(1) == "", "old quick-item alias migrates to the canonical ID");
                }
                Check(InventoryDefinitionCatalog.Find("Leaf Berry") == InventoryDefinitionCatalog.Find("leaf-berry"),
                    "old growth item resolves to the authored definition");
                var choices = Resources.Load<CaptureBallPresentationCatalog>("Capture/BallPresentations");
                var resultsAt70 = new List<bool>();
                var resultsAt74 = new List<bool>();
                foreach (string id in balls) {
                    var item = InventoryDefinitionCatalog.Find(id);
                    Check(choices.choices.Count(choice => choice.item == item && choice.icon == item.Icon) == 1 && item.IsCaptureBall,
                        "capture picker uses the shop definition and icon for " + id);
                    var context = new CaptureContext(50, 100, 45, item.CaptureRules);
                    var valid70 = CaptureCalculator.TryRoll(context, new FixedCaptureRandom(.7), out var roll70, out _);
                    var valid74 = CaptureCalculator.TryRoll(context, new FixedCaptureRandom(.74), out var roll74, out _);
                    Check(!item.CaptureRules.Guaranteed && valid70 && valid74,
                        "new ball works with real-time capture rules " + id);
                    resultsAt70.Add(roll70.Success);
                    resultsAt74.Add(roll74.Success);
                }
                Check(resultsAt70.SequenceEqual(new[] { false, true, true }) && resultsAt74.SequenceEqual(new[] { false, false, true }),
                    "ball tiers improve capture outcomes for the same target and fixed rolls");
            } finally { Object.DestroyImmediate(owner); }
        }

        sealed class FixedCaptureRandom : ICaptureRandom {
            readonly double value;
            public FixedCaptureRandom(double value) { this.value = value; }
            public double NextFloat01() => value;
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
                Check(defaults.Offers.All(offer => ShopPresentation.Category(offer) != ShopCategory.Evolution)
                    && InventoryDefinitionCatalog.Find("dusk-stone") != null,
                    "default shop excludes evolution stones while old inventory IDs still resolve");
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

        [MenuItem("Tools/Capstone/Advanced/HUD/Validate Shop in Play Mode")]
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

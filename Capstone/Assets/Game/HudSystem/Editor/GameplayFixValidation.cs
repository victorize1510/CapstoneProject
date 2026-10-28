using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Ugui = UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Capstone.Game.HudSystem.Editor {
    /// <summary>Isolated targeting and quick-slot fixtures; live Play checks never save the player's bag.</summary>
    public static class GameplayFixValidation {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Output = Capstone.Game.UISystem.QaArtifactPaths.Root + "gameplay-fixes/";
        static readonly List<string> results = new List<string>();
        static bool running;
        static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        static void Click(Button button) => typeof(Clickable).GetMethod("Invoke", Private).Invoke(button.clickable, new object[] { null });
        static void Check(bool condition, string label) {
            if (!condition) throw new InvalidOperationException(label);
            results.Add("PASS " + label);
        }
        static GameObject Child(Transform parent, string name) {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
        }
        static void TargetingFixture() {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Targeting fixture"); SceneManager.MoveGameObjectToScene(root, scene);
            var registry = (HashSet<DummyEnemy>)typeof(DummyEnemy).GetField("activeEnemies", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var owned = new List<DummyEnemy>();
            try {
                var player = Child(root.transform, "Fixture player"); player.transform.position = new Vector3(5000, 0, 5000);
                var cameraObject = Child(root.transform, "Fixture camera");
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                var follow = cameraObject.AddComponent<BasicCameraFollow>();
                follow.target = player.transform; follow.lockSearchRadius = 1; follow.lockBreakDistance = 35;
                follow.freeLookRig = cameraObject.AddComponent<PlayerFreeLookRig>();
                foreach (float distance in new[] { 5f, 18f, 30f }) {
                    var enemy = Child(root.transform, "Enemy " + distance).AddComponent<DummyEnemy>();
                    enemy.transform.position = player.transform.position + Vector3.forward * distance;
                    owned.Add(enemy); registry.Add(enemy);
                }
                Set(follow, "lockedEnemy", owned[0]);
                var visited = new HashSet<DummyEnemy>();
                for (int i = 0; i < 6; i++) {
                    cameraObject.transform.rotation = Quaternion.Euler(0, i * 70, 0);
                    var next = (DummyEnemy)Invoke(follow, "FindSwitchLockCandidate", 1f);
                    Check(next != null, "collinear cycle step " + i);
                    visited.Add(next); Set(follow, "lockedEnemy", next);
                }
                Check(visited.Count == 3 && (DummyEnemy)Get(follow, "lockedEnemy") == owned[0], "wheel visits all three overlapping targets and wraps");
                var forward = (DummyEnemy)Invoke(follow, "FindSwitchLockCandidate", 1f);
                Set(follow, "lockedEnemy", forward);
                Check((DummyEnemy)Invoke(follow, "FindSwitchLockCandidate", -1f) == owned[0], "reverse wheel restores previous target");
                var newcomer = Child(root.transform, "New enemy").AddComponent<DummyEnemy>();
                newcomer.transform.position = player.transform.position + Vector3.right * 15;
                owned.Add(newcomer); registry.Add(newcomer); visited.Clear();
                for (int i = 0; i < 4; i++) {
                    var next = (DummyEnemy)Invoke(follow, "FindSwitchLockCandidate", 1f);
                    visited.Add(next); Set(follow, "lockedEnemy", next);
                }
                Check(visited.Count == 4 && visited.Contains(newcomer), "new nearby enemy joins stable cycle");
                owned[2].transform.position = player.transform.position + Vector3.forward * 36;
                Set(owned[1], "alive", false); Set(follow, "lockedEnemy", owned[0]);
                Check((DummyEnemy)Invoke(follow, "FindSwitchLockCandidate", 1f) == newcomer, "dead and out-of-range enemies leave cycle");
                newcomer.gameObject.SetActive(false);
                Check(Invoke(follow, "FindSwitchLockCandidate", 1f) == null, "single remaining target has no duplicate selection");
                Set(follow, "currentLockSide", -1f); follow.lockCameraOffset.x = -.8f;
                Check(((Vector3)Invoke(follow, "GetLockCameraLocalOffset", true)).x > 0, "FreeLook always keeps camera on right shoulder / player left");
                player.transform.position += Vector3.left * 2;
                Invoke(follow, "UpdateLockSide", true, Quaternion.identity, .016f);
                Check((float)Get(follow, "currentLockSide") == 1f, "lateral movement does not flip shoulder");

                var pet = Child(root.transform, "Fixture pet").AddComponent<PetController>(); pet.owner = player.transform;
                typeof(PetController).GetProperty("IsSummoned").SetValue(pet, true);
                var command = player.AddComponent<PetCommandInput>(); command.activePet = pet; command.cameraLock = follow; command.commandCamera = camera;
                Invoke(command, "OnEnable");
                Check(command.CanUsePetSkills, "combat fixture can receive an explicit command");
                int notifications = 0; follow.EnemyPinned += _ => notifications++;
                ((Action<DummyEnemy>)Get(follow, "EnemyPinned"))?.Invoke(owned[0]);
                Check(notifications == 1 && !pet.HasCombatTarget, "pin notification never starts pet attack");
                Check(command.TryCommandAttack(owned[0]) && pet.HasCombatTarget, "explicit attack command still reaches pinned enemy");
                var movement = player.AddComponent<BasicPlayerMovement>();
                Check(!movement.allowManualThrowInput && !(bool)Invoke(movement, "WantsToThrow"), "manual click Throw disabled by default");
                Set(movement, "grounded", true);
                Check(movement.TryPlayScriptedThrow() && movement.ActiveScriptedAction == BasicPlayerMovement.ScriptedAction.Throw,
                    "summon scripted Throw still starts normally");

                // Exercise the actual camera tracking path, including destruction and explicit unlock.
                var bosses = new List<DummyEnemy>();
                foreach (float distance in new[] { 22f, 28f }) {
                    var boss = Child(root.transform, "Boss " + distance).AddComponent<DummyEnemy>();
                    boss.isBoss = true; boss.transform.position = player.transform.position + Vector3.forward * distance;
                    owned.Add(boss); registry.Add(boss); bosses.Add(boss);
                }
                var nearest = Child(root.transform, "Nearest regular enemy").AddComponent<DummyEnemy>();
                nearest.transform.position = player.transform.position + Vector3.forward * 4;
                owned.Add(nearest); registry.Add(nearest);
                Check((DummyEnemy)Invoke(follow, "FindLockCandidate") == bosses[0], "initial pin prioritizes nearest Boss over nearer regular enemy");
                Set(owned[0], "alive", false); Set(follow, "lockedEnemy", owned[0]); Set(follow, "enemyLockActive", true);
                Invoke(follow, "UpdateLockedEnemyTracking", player.transform.position, .016f);
                Check(follow.TryGetLockedEnemy(out var selected) && selected == bosses[0], "dead target reacquires Boss without exiting aim");
                Set(bosses[0], "alive", false);
                Invoke(follow, "UpdateLockedEnemyTracking", player.transform.position, .016f);
                Check(follow.TryGetLockedEnemy(out selected) && selected == bosses[1], "next living Boss wins before regular enemies");
                bosses[1].gameObject.SetActive(false);
                Invoke(follow, "UpdateLockedEnemyTracking", player.transform.position, .016f);
                Check(follow.TryGetLockedEnemy(out selected) && selected == nearest, "no Boss falls back to closest enemy around player");
                Set(nearest, "alive", false);
                Invoke(follow, "UpdateLockedEnemyTracking", player.transform.position, .016f);
                Check(!follow.HasLockedEnemy && !(bool)Get(follow, "enemyLockActive"), "no targets within 35m exits aim");
                Set(nearest, "alive", true);
                Set(follow, "lockedEnemy", bosses[0]); Set(follow, "enemyLockActive", true);
                registry.Remove(bosses[0]); Object.DestroyImmediate(bosses[0].gameObject);
                Invoke(follow, "UpdateLockedEnemyTracking", player.transform.position, .016f);
                Check(follow.TryGetLockedEnemy(out selected) && selected == nearest, "destroyed target preserves lock intent and reacquires");
                Invoke(follow, "ClearLockedEnemy");
                Invoke(follow, "UpdateLockedEnemyTracking", player.transform.position, .016f);
                Check(!follow.HasLockedEnemy, "explicit M3 unlock does not immediately reacquire");
            } finally {
                foreach (var enemy in owned) registry.Remove(enemy);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static async Task PlayChecks() {
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            var bag = Object.FindFirstObjectByType<MonsterInventoryController>();
            var quick = Object.FindFirstObjectByType<GameplayHudQuickItemsView>();
            menu.CloseMenu(); bag.Close(); await Task.Delay(250);
            TargetingFixture();
            menu.OpenInventoryDirect(); await Task.Delay(300);
            var data = bag.GetComponent<MonsterInventoryAdapter>();
            var item = data.GetItems().First(x => x.IsRecoveryItem);
            var filtered = (List<InventoryItemSnapshot>)Get(bag, "filteredItems");
            Invoke(bag, "SelectItem", filtered.FindIndex(x => x.ItemId == item.ItemId), false);
            var docRoot = bag.GetComponent<UIDocument>().rootVisualElement;
            var assignButton = docRoot.Q<Button>("equip-button");
            Check(assignButton.enabledInHierarchy && assignButton.text.Contains("PHÍM TẮT"), "real Bag assignment button is enabled for selected recovery item");
            Click(assignButton); // Exercise the registered button callback, not ShowAssignment directly.
            await Task.Delay(250);
            var popup = bag.GetComponent<UIDocument>().rootVisualElement.Q("quick-item-assignment");
            Check(popup != null && popup.Q<Button>("quick-assignment-R").text == "R"
                && popup.Q<Button>("quick-assignment-T").text == "T", "real Bag modal offers R and T explicitly");
            Directory.CreateDirectory(Output);
            ScreenCapture.CaptureScreenshot(Output + "assignment.png"); await Task.Delay(250);
            Check(bag.TryCloseTopmostPanel() && bag.IsBagSectionOpen && !quick.IsAssignmentOpen, "Esc closes assignment before Bag");
            await Task.Delay(200);
            var otherItem = filtered.First(x => x.IsRecoveryItem && x.ItemId != item.ItemId);
            Invoke(bag, "SelectItem", filtered.FindIndex(x => x.ItemId == otherItem.ItemId), false);
            Click(assignButton);
            Check(quick.IsAssignmentOpen && docRoot.Q<Label>(className: "quick-assignment-item").text == otherItem.Name,
                "selecting another recovery item opens the real R/T picker for that item");
            quick.TryCloseAssignment();
            var iconImages = (Ugui.Image[])Get(quick, "icons");
            Check(iconImages.Where(x => x.enabled).All(x => x.color == Color.white), "quick item icons remain fully opaque when unavailable");
            bag.Close(); menu.CloseMenu();
        }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Quick Slots And Target Cycling")]
        public static void RunFixtures() {
            results.Clear(); Directory.CreateDirectory(Output);
            try { TargetingFixture(); QuickAssignmentFixture(); }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            File.WriteAllLines(Output + "fixture-validation.txt", results);
        }
        static void QuickAssignmentFixture() {
            var root = new GameObject("Quick assignment fixture"); root.SetActive(false);
            var item = MonsterItemDefinition.CreateRuntime("Fixture fruit", GameItemCategory.Food, "", "", itemHealAmount:30);
            var replacement = MonsterItemDefinition.CreateRuntime("Fixture potion", GameItemCategory.Medicine, "", "", itemHealAmount:100);
            try {
                var inventory = root.AddComponent<MonsterInventoryAdapter>(); inventory.TryAddItemAtomic(item, 3); inventory.TryAddItemAtomic(replacement, 2);
                var save = root.AddComponent<PlayerSaveController>(); Set(save,"autoFindReferences",false); Set(save,"inventoryAdapter",inventory);
                save.SetSaveStore(new PlayerJsonFileSaveStore(Path.Combine(Path.GetTempPath(),"CapstoneQuickAssignment",Guid.NewGuid().ToString("N"))));
                var view = root.AddComponent<GameplayHudQuickItemsView>();
                var fruit = inventory.GetItems().Single(x => x.ItemId == item.StableId);
                Set(view,"inventory",inventory); Set(view,"save",save); Set(view,"pendingAssignment",fruit);
                Check(view.GetHotkey(0)==KeyCode.R && view.GetHotkey(1)==KeyCode.T,"quick bindings default to R/T");
                // Refresh exits before drawing when the HUD has not been built.
                Check(view.Assign(0) && inventory.GetQuickItemId(0)==item.StableId,"R assignment saves exact item ID");
                Set(view,"pendingAssignment",fruit);
                Check(view.Assign(1) && inventory.GetQuickItemId(1)==item.StableId,"T assignment saves exact item ID");
                Check(inventory.GetQuantity(item)==3,"assignment never consumes item quantity");
                Check(!view.Assign(2),"invalid quick slot rejected");
                var restored = root.AddComponent<MonsterInventoryController>(); Set(restored,"adapter",inventory);
                Check((string)Invoke(restored,"QuickSlotLabel",fruit)=="PHÍM TẮT: R / T","Bag reports assigned keys");
                var replaceView = Child(root.transform,"Replacement assignment").AddComponent<GameplayHudQuickItemsView>();
                Set(replaceView,"inventory",inventory); Set(replaceView,"save",save);
                Set(replaceView,"pendingAssignment",inventory.GetItems().Single(x => x.ItemId == replacement.StableId));
                Check(replaceView.Assign(0) && inventory.GetQuickItemId(0)==replacement.StableId && inventory.GetQuickItemId(1)==item.StableId,
                    "different item replaces existing R binding and preserves T");
                Set(replaceView,"pendingAssignment",inventory.GetItems().Single(x => x.ItemId == replacement.StableId));
                Check(replaceView.Assign(1) && inventory.GetQuickItemId(1)==replacement.StableId,
                    "different item also replaces existing T binding");
                Check(inventory.GetQuantity(item)==3 && inventory.GetQuantity(replacement)==2,"reassignment consumes neither item");
                AssignmentButtonsFixture(root, restored, inventory, save, fruit, replacement.StableId);
                var failedView = Child(root.transform,"Failed assignment").AddComponent<GameplayHudQuickItemsView>();
                Set(failedView,"inventory",inventory); Set(failedView,"save",save); Set(failedView,"pendingAssignment",fruit);
                inventory.AssignQuickItem(0,""); Set(save,"saveBlockedReason","Fixture failed save");
                Check(!failedView.Assign(0) && inventory.GetQuickItemId(0)=="" && inventory.GetQuantity(item)==3,"failed save restores old binding without consuming item");
                var region = new GameObject("Fixture quick HUD", typeof(RectTransform)); region.transform.SetParent(root.transform, false);
                Set(view, "bag", restored); Set(view, "healing", root.AddComponent<PetHealService>());
                view.Build((RectTransform)region.transform, null);
                var displayed = (InventoryItemSnapshot[])Get(view, "items");
                Check(displayed[0] == null, "unassigned R stays empty instead of auto-selecting first recovery item");
                Check(displayed[1]?.ItemId == fruit.ItemId, "HUD shows exact item explicitly assigned to T");
            } finally { Object.DestroyImmediate(root); Object.DestroyImmediate(item); Object.DestroyImmediate(replacement); }
        }
        static void AssignmentButtonsFixture(GameObject owner, MonsterInventoryController bag,
            MonsterInventoryAdapter inventory, PlayerSaveController save, InventoryItemSnapshot fruit, string oldR) {
            const string ui = "Assets/ThangTest/UIThang/Inventory/UI/";
            var uiOwner = new GameObject("Quick assignment UI fixture") { hideFlags = HideFlags.HideAndDontSave };
            var settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(ui + "MonsterInventoryPanelSettings.asset"));
            settings.hideFlags = HideFlags.HideAndDontSave;
            try {
                var doc = uiOwner.AddComponent<UIDocument>(); doc.panelSettings = settings;
                doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ui + "MonsterInventory.uxml");
                bag = uiOwner.AddComponent<MonsterInventoryController>();
                Set(bag, "adapter", inventory);
                Set(bag, "document", doc);
                Invoke(bag, "CacheElements"); Invoke(bag, "RegisterActionButtons");
                var popupView = Child(owner.transform, "Popup assignment").AddComponent<GameplayHudQuickItemsView>();
                Set(popupView, "inventory", inventory); Set(popupView, "save", save); Set(popupView, "bag", bag);
                bag.AssignQuickSlot += request => Invoke(popupView, "ShowAssignment", request);
                Set(bag, "selectedItem", fruit); Invoke(bag, "RefreshDetails");
                Click(doc.rootVisualElement.Q<Button>("equip-button"));
                Check(popupView.IsAssignmentOpen, "registered Bag button opens R/T picker instead of EquipItem");
                var picker = doc.rootVisualElement.Q("quick-item-assignment");
                Check(picker.Q<Label>(className: "quick-assignment-item").text == fruit.Name, "picker owns the selected item");
                Click(picker.Q<Button>("quick-assignment-T"));
                Check(!popupView.IsAssignmentOpen && inventory.GetQuickItemId(1) == fruit.ItemId && inventory.GetQuickItemId(0) == oldR,
                    "clicking actual T button replaces T and keeps R unchanged");
                Check(inventory.GetQuantity(fruit.ItemBase) == fruit.Quantity, "actual picker assignment never consumes the selected item");
                Invoke(bag, "UnregisterControls");
            } finally { Object.DestroyImmediate(uiOwner); Object.DestroyImmediate(settings); }
        }
        [InitializeOnLoadMethod] static void Initialize() { EditorApplication.update-=Poll; EditorApplication.update+=Poll; }
        static void Poll() {
            if(running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            const string fixture="Temp/GameplayFixFixtures.request", play="Temp/GameplayFixPlay.request", start="Temp/GameplayFixStartPlay.request";
            if (!Application.isPlaying && File.Exists(start)) {
                try { File.Delete(start); } catch (IOException) { return; }
                File.WriteAllText(play, "validate");
                EditorApplication.isPlaying = true;
                return;
            }
            if(!Application.isPlaying && File.Exists(fixture)) {
                try { File.Delete(fixture); } catch(IOException) { return; }
                RunFixtures();
            }
            if(Application.isPlaying && File.Exists(play)) {
                try { File.Delete(play); } catch(IOException) { return; }
                RunPlay();
            }
        }
        static async void RunPlay() {
            running=true; results.Clear();
            try { await PlayChecks(); }
            catch(Exception error) { results.Add("FAIL "+error); Debug.LogException(error); }
            finally { File.WriteAllLines(Output+"play-validation.txt",results); running=false; EditorApplication.isPlaying=false; }
        }
    }
}

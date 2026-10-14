using System;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;
using UITK = UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class GameplayHudQuickItemsView : MonoBehaviour {
        [SerializeField] KeyCode firstItemKey = KeyCode.R;
        [SerializeField] KeyCode secondItemKey = KeyCode.T;
        MonsterInventoryAdapter inventory;
        MonsterInventoryController bag;
        PetHealService healing;
        PlayerSaveController save;
        PetCommandHudProvider pets;
        readonly Button[] buttons = new Button[2];
        readonly Image[] icons = new Image[2];
        readonly Text[] quantities = new Text[2], labels = new Text[2], empty = new Text[2];
        readonly InventoryItemSnapshot[] items = new InventoryItemSnapshot[2];
        readonly GDS.Core.ItemBase[] lastDefinitions = new GDS.Core.ItemBase[2];
        MonsterInventoryAdapter subscribedInventory;
        MonsterInventoryController subscribedBag;
        UITK.VisualElement assignmentOverlay;
        UITK.Label assignmentFeedback;
        InventoryItemSnapshot pendingAssignment;
        float nextLookup;
        public bool IsAssignmentOpen => assignmentOverlay != null;

        void OnEnable() { Resolve(); }
        void OnDisable() {
            if (subscribedInventory != null) subscribedInventory.InventoryChanged -= Refresh;
            if (subscribedBag != null) subscribedBag.AssignQuickSlot -= ShowAssignment;
            subscribedInventory = null; subscribedBag = null;
            CloseAssignment();
        }
        public void Build(RectTransform region, PetCommandHudProvider provider) {
            pets = provider; Resolve();
            var line = V.Frame(region, "Connector", HudFrameGraphic.Shape.Chamfered, V.Forest, Color.clear, 0, 0);
            V.Bottom(line.rectTransform, 60, 69, 152, 1.4f);
            for (int i = 0; i < 2; i++) {
                int index = i;
                RectTransform slot = V.Rect(region, "QuickItem" + (i + 1)); V.Bottom(slot, i * 142, 0, 124, 142);
                buttons[i] = V.Button(slot, "Item", HudFrameGraphic.Shape.Circle);
                V.Top((RectTransform)buttons[i].transform, 0, 0, 124, 124);
                buttons[i].onClick.AddListener(() => Use(index));
                var rim = V.Frame(buttons[i].transform, "InsetRim", HudFrameGraphic.Shape.Circle, Color.clear, V.Sage, 1f); V.Stretch(rim.rectTransform, 9);
                V.Diamond(buttons[i].transform, "TopAccent", 0, 62, V.Forest);
                icons[i] = V.Image(buttons[i].transform, "ItemIcon"); V.Stretch(icons[i].rectTransform, 19);
                empty[i] = V.Text(buttons[i].transform, "EmptyOrMissingIcon", "—", 18, TextAnchor.MiddleCenter); V.Stretch(empty[i].rectTransform, 20);
                quantities[i] = V.Text(buttons[i].transform, "Quantity", "", 19, TextAnchor.MiddleRight);
                V.Bottom(quantities[i].rectTransform, 66, 17, 37, 24);
                labels[i] = V.Keycap(slot, Key(i).ToString().ToUpperInvariant(), 38, 0, 48, 38);
            }
            Refresh();
        }
        void Resolve() {
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);
            if (bag == null) bag = FindFirstObjectByType<MonsterInventoryController>(FindObjectsInactive.Include);
            if (healing == null) healing = FindFirstObjectByType<PetHealService>(FindObjectsInactive.Include);
            if (healing == null && bag != null) {
                healing = bag.GetComponent<PetHealService>();
                if (healing == null) healing = bag.gameObject.AddComponent<PetHealService>();
            }
            if (save == null) save = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
            if (pets == null) pets = FindFirstObjectByType<PetCommandHudProvider>();
            healing?.Bind(inventory);
            if (subscribedInventory != inventory) {
                if (subscribedInventory != null) subscribedInventory.InventoryChanged -= Refresh;
                subscribedInventory = inventory;
                if (subscribedInventory != null) subscribedInventory.InventoryChanged += Refresh;
            }
            if (subscribedBag != bag) {
                if (subscribedBag != null) subscribedBag.AssignQuickSlot -= ShowAssignment;
                subscribedBag = bag;
                if (subscribedBag != null) subscribedBag.AssignQuickSlot += ShowAssignment;
            }
        }
        void Update() {
            if (!Application.isPlaying) return;
            if (Time.unscaledTime >= nextLookup) { nextLookup = Time.unscaledTime + 1f; Resolve(); }
            if (assignmentOverlay != null) {
                if (bag == null || !bag.IsOpen) CloseAssignment();
                else {
                    for (int i = 0; i < 2; i++) if (Input.GetKeyDown(Key(i))) { Assign(i); break; }
                    return; // R/T belongs to assignment while its modal is open.
                }
            }
            if (InventoryInputController.GameplayInputBlocked) return;
            for (int i = 0; i < 2; i++) if (Key(i) != KeyCode.None && Input.GetKeyDown(Key(i))) Use(i);
        }
        KeyCode Key(int index) => index == 0 ? firstItemKey : secondItemKey;
        public KeyCode GetHotkey(int index) => Key(index);
        static bool Supported(InventoryItemSnapshot item) => item != null && item.IsRecoveryItem;

        public void Refresh() {
            if (inventory == null || buttons[0] == null) return;
            var snapshots = inventory.GetItems();
            for (int i = 0; i < 2; i++) {
                string id = inventory.GetQuickItemId(i);
                items[i] = null;
                foreach (var item in snapshots) {
                    if (!string.IsNullOrEmpty(id) && string.Equals(item.ItemId, id, StringComparison.OrdinalIgnoreCase)) {
                        items[i] = item;
                        break;
                    }
                }
                if (items[i] != null) lastDefinitions[i] = items[i].ItemBase;
                else if (!string.IsNullOrEmpty(id)) {
                    if (lastDefinitions[i] == null || (lastDefinitions[i] is MonsterItemDefinition d && !d.MatchesId(id)))
                        lastDefinitions[i] = inventory.FindItemBase(id);
                    if (lastDefinitions[i] != null) items[i] = new InventoryItemSnapshot(lastDefinitions[i], null, 0);
                } else lastDefinitions[i] = null;
                InventoryItemSnapshot snapshot = items[i];
                bool usable = Supported(snapshot) && snapshot.Quantity > 0 && healing != null
                    && healing.CreatePreview(pets?.GetSelectedPetController(), snapshot.ItemBase).CanHeal;
                buttons[i].interactable = usable;
                V.Icon(icons[i], snapshot?.Icon);
                icons[i].color = Color.white; // Disabled use must not obscure item identity.
                empty[i].gameObject.SetActive(snapshot?.Icon == null);
                empty[i].text = snapshot != null ? snapshot.Name : "—";
                quantities[i].text = snapshot != null ? snapshot.Quantity.ToString() : string.Empty;
                labels[i].text = Key(i).ToString().ToUpperInvariant();
            }
        }
        public bool Use(int index) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "quick-item:" + index)) return false;
            if (index < 0 || index > 1 || InventoryInputController.GameplayInputBlocked) return false;
            Refresh(); InventoryItemSnapshot item = items[index];
            if (!Supported(item) || item.Quantity <= 0 || healing == null) return false;
            bool success = healing.TryHeal(pets?.GetSelectedPetController(), item.ItemBase, out string message);
            if (!success && !string.IsNullOrWhiteSpace(message)) Debug.Log(message);
            Refresh(); return success;
        }
        void ShowAssignment(InventoryActionRequest request) {
            if (!Supported(request.Item) || bag == null) { request.Complete(false, false, 0, "Vật phẩm này không dùng được qua phím tắt."); return; }
            var document = bag.GetComponent<UITK.UIDocument>();
            if (document == null || document.rootVisualElement == null) {
                request.Complete(false, false, 0, "Không tìm thấy giao diện Túi đồ."); return;
            }
            CloseAssignment(); pendingAssignment = request.Item;
            // Opening the selector succeeds immediately; assigning later never consumes an item.
            request.Complete(true, false, 0);
            assignmentOverlay = new UITK.VisualElement { name = "quick-item-assignment", userData = this };
            assignmentOverlay.AddToClassList("quick-assignment-overlay");
            assignmentOverlay.AddToClassList("menu-theme");
            var panel = new UITK.VisualElement(); panel.AddToClassList("quick-assignment-card");
            var title = new UITK.Label("CHỌN PHÍM TẮT"); title.AddToClassList("quick-assignment-title"); panel.Add(title);
            var name = new UITK.Label(request.Item.Name); name.AddToClassList("quick-assignment-item"); panel.Add(name);
            for (int i = 0; i < 2; i++) {
                int index = i;
                string key = Key(i).ToString().ToUpperInvariant();
                var button = new UITK.Button(() => Assign(index)) { name="quick-assignment-"+key, text=key };
                button.AddToClassList("quick-assignment-choice"); button.tooltip="Gán vật phẩm vào phím "+key; panel.Add(button);
            }
            panel.Add(new UITK.Label("Nhấn R/T hoặc chọn nút. Esc để hủy.") { name="quick-assignment-hint" });
            assignmentFeedback = new UITK.Label(); assignmentFeedback.AddToClassList("quick-assignment-feedback"); panel.Add(assignmentFeedback);
            panel.Add(new UITK.Button(Capstone.Game.UISystem.PlayerActionGate.Guard(this, "assignment-cancel", CloseAssignment)) { text = "HỦY" });
            assignmentOverlay.Add(panel); document.rootVisualElement.Add(assignmentOverlay);
            Capstone.Game.UISystem.PanelFocusUtility.Open(assignmentOverlay);
        }
        public bool Assign(int index) {
            if (index < 0 || index >= 2) return false;
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "assign:" + index)) return false;
            if (!Supported(pendingAssignment) || inventory == null || save == null) return false;
            string oldId = inventory.GetQuickItemId(index);
            if (!inventory.AssignQuickItem(index, pendingAssignment.ItemId)) return false;
            if (!save.SaveNow()) {
                inventory.AssignQuickItem(index, oldId);
                if (assignmentFeedback != null) assignmentFeedback.text = "Không thể lưu phím tắt. Thay đổi đã được hoàn tác.";
                return false;
            }
            CloseAssignment(); Refresh(); return true;
        }
        void CloseAssignment() {
            Capstone.Game.UISystem.PanelFocusUtility.Close(assignmentOverlay);
            assignmentOverlay?.RemoveFromHierarchy(); assignmentOverlay = null; pendingAssignment = null; assignmentFeedback = null;
        }
        public bool TryCloseAssignment() {
            if (!IsAssignmentOpen) return false;
            CloseAssignment(); return true;
        }
    }
}

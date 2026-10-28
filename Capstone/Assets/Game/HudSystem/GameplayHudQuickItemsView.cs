using System;
using System.Linq;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;
using UITK = UnityEngine.UIElements;
using Capstone.Game.Inventory.Bag;

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
            var panel = new UITK.VisualElement { name = "quick-assignment-panel" }; panel.AddToClassList("quick-assignment-card");
            assignmentOverlay.Add(panel);
            BagPresentation.Modal(assignmentOverlay, panel, "popup-assignment", 792, 568);
            var header = new BagSurface { Kind = "action-primary" }; BagPresentation.Place(header, 12, 12, 768, 56); panel.Add(header);
            var title = new UITK.Label("CHỌN PHÍM TẮT"); title.AddToClassList("quick-assignment-title"); panel.Add(title);
            title.AddToClassList("bag-modal-title"); title.style.unityFontDefinition=UITK.FontDefinition.FromFont(Capstone.Game.UISystem.TrainerMenuArt.Serif); BagPresentation.Place(title,24,12,744,56);
            var itemSurface = new BagSurface { Kind = "detail-art" }; BagPresentation.Place(itemSurface,24,88,744,96);panel.Add(itemSurface);
            var icon = new UITK.VisualElement();icon.style.backgroundImage=new UITK.StyleBackground(request.Item.Icon);icon.style.backgroundSize=new UITK.BackgroundSize(UITK.BackgroundSizeType.Contain);BagPresentation.Place(icon,32,92,80,88);panel.Add(icon);
            var name = new UITK.Label(request.Item.Name); name.AddToClassList("quick-assignment-item"); panel.Add(name);
            name.AddToClassList("bag-modal-item-name");BagPresentation.Place(name,124,104,620,38);
            var owned = new UITK.Label("Sở hữu: "+request.Item.Quantity);BagPresentation.Place(owned,124,148,620,28);panel.Add(owned);
            for (int i = 0; i < 2; i++) {
                int index = i;
                string key = Key(i).ToString().ToUpperInvariant();
                var button = new BagButton(() => Assign(index)) { name="quick-assignment-"+key, Kind="quick-choice" };
                button.AddToClassList("quick-assignment-choice"); button.tooltip="Gán vật phẩm vào phím "+key; panel.Add(button);
                BagPresentation.Place(button,24+i*380,220,364,140);
                string currentId=inventory.GetQuickItemId(index);var current=inventory.GetItems().FirstOrDefault(s=>string.Equals(s.ItemId,currentId,StringComparison.OrdinalIgnoreCase));
                var currentName=new UITK.Label(current?.Name??"Chưa gán"){pickingMode=UITK.PickingMode.Ignore};BagPresentation.Place(currentName,88,30,260,30);button.Add(currentName);
                var keyLabel=new UITK.Label(key){pickingMode=UITK.PickingMode.Ignore};BagPresentation.Place(keyLabel,16,12,32,28);button.Add(keyLabel);
                var currentIcon=new UITK.VisualElement{pickingMode=UITK.PickingMode.Ignore};currentIcon.style.backgroundImage=new UITK.StyleBackground(current?.Icon);currentIcon.style.backgroundSize=new UITK.BackgroundSize(UITK.BackgroundSizeType.Contain);BagPresentation.Place(currentIcon,16,54,64,64);button.Add(currentIcon);
                var next=new UITK.Label("→ "+request.Item.Name){pickingMode=UITK.PickingMode.Ignore};BagPresentation.Place(next,88,68,260,28);button.Add(next);
                var state=new UITK.Label(string.Equals(currentId,request.Item.ItemId,StringComparison.OrdinalIgnoreCase)?"Đang gán vật phẩm này":"Thay vật phẩm hiện tại"){pickingMode=UITK.PickingMode.Ignore};BagPresentation.Place(state,16,108,332,24);button.Add(state);
            }
            var hint=new UITK.Label("Chọn R/T sẽ lưu ngay; gán không tiêu hao vật phẩm.\nR/T hoặc click ô · Esc để hủy."){name="quick-assignment-hint"};hint.AddToClassList("bag-modal-note");BagPresentation.Place(hint,24,390,744,64);panel.Add(hint);
            assignmentFeedback = new UITK.Label(); assignmentFeedback.AddToClassList("quick-assignment-feedback"); panel.Add(assignmentFeedback);
            BagPresentation.Place(assignmentFeedback,24,452,744,28);
            var cancel = new BagButton(Capstone.Game.UISystem.PlayerActionGate.Guard(this, "assignment-cancel", CloseAssignment)) { text="Hủy",Kind="action-small",name="quick-assignment-cancel" };BagPresentation.Place(cancel,24,480,744,56);panel.Add(cancel);
            var dismiss=new BagButton(CloseAssignment){text="×",Kind="action-primary",name="quick-assignment-close"};BagPresentation.Place(dismiss,732,20,36,36);panel.Add(dismiss);
            document.rootVisualElement.Add(assignmentOverlay);
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
            BagTooltip.HideAll(assignmentOverlay);
            Capstone.Game.UISystem.PanelFocusUtility.Close(assignmentOverlay);
            assignmentOverlay?.RemoveFromHierarchy(); assignmentOverlay = null; pendingAssignment = null; assignmentFeedback = null;
        }
        public bool TryCloseAssignment() {
            if (!IsAssignmentOpen) return false;
            CloseAssignment(); return true;
        }
    }
}

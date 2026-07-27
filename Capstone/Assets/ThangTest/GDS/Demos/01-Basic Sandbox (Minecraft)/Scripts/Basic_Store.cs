using UnityEngine;
using GDS.Core;
using GDS.Core.Events;


namespace GDS.Demos.Basic {

    public class Basic_Store : Store {

        public Observable<bool> UiOpen = new(false);
        public Observable<object> SideWindow = new(null);
        public CharacterSheet CharacterSheet = new();

        public PlayerInventory PlayerInventory;
        Bag Main => PlayerInventory.Inventory;
        Bag Secondary => SideWindow.Value as Bag;

        EventModifiers MoveModifier = EventModifiers.Control;
        EventModifiers StackModifier = EventModifiers.Shift;

        public bool ShouldMove(EventModifiers mods) => mods.HasFlag(MoveModifier);
        public bool ShouldSplit(EventModifiers mods) => mods.HasFlag(StackModifier);
        public bool ShouldTransfer(EventModifiers mods, IItemContext target) => mods.HasFlag(StackModifier) && ItemExt.CanStackOn(Ghost.Item, target.Slot.Item);
        public Bag GetTargetBag(Bag SourceBag) => SourceBag == Main ? Secondary : Main;

        bool interactedThisFrame = false;

        void Awake() { StoreLocator.Register(this); }
        void Start() { CharacterSheet.Init(PlayerInventory.Equipment); }
        void LateUpdate() { interactedThisFrame = false; }

        void OnEnable() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceGhostItem>(OnPlaceItem);
            Bus.On<OpenWindow>(OnOpenWindow);
            Bus.On<CloseWindow>(OnCloseWindow);
            Bus.On<CollectAll>(OnCollectAll);
            Bus.On<PickWorldItem>(OnPickWorldItem);
            Bus.On<DropGhostItem>(OnDropItem);
        }

        void OnDisable() {
            Bus.Off<PickItem>(OnPickItem);
            Bus.Off<PlaceGhostItem>(OnPlaceItem);
            Bus.Off<OpenWindow>(OnOpenWindow);
            Bus.Off<CloseWindow>(OnCloseWindow);
            Bus.Off<CollectAll>(OnCollectAll);
            Bus.Off<PickWorldItem>(OnPickWorldItem);
            Bus.Off<DropGhostItem>(OnDropItem);
        }

        void OnPickItem(PickItem e) {
            if (interactedThisFrame) return;
            var (bag, slot, item) = e.Context;
            Bag targetBag = GetTargetBag(bag);
            EventModifiers mods = InputUtil.GetModifiers();
            Result result = true switch {
                _ when ShouldMove(mods) => bag.MoveItem(item, targetBag),
                _ when ShouldSplit(mods) => bag.SplitHalf(item),
                _ => bag.Remove(item)
            };

            if (!ShouldMove(mods)) UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

        void OnPlaceItem(PlaceGhostItem e) {
            var (bag, slot, item) = e.Context;
            var result = ShouldTransfer(InputUtil.GetModifiers(), e.Context)
                ? bag.TransferOne(Ghost.Item, slot, slot.Item)
                : bag.AddAt(slot, Ghost.Item);
            UpdateGhost(result, e.Context);
            Bus.Publish(result);
        }

        void OnOpenWindow(OpenWindow e) {
            LogUtil.LogEvent(e);
            SideWindow.SetValue(e.Handle);
            UiOpen.SetValue(true);
            interactedThisFrame = true;
        }

        void OnCloseWindow(CloseWindow e) {
            LogUtil.LogEvent(e);
            SideWindow.SetValue(null);
            if (e.Handle is PlayerInventory) {
                UiOpen.SetValue(false);
            }
        }

        void OnCollectAll(CollectAll e) {
            LogUtil.LogEvent(e);
            var result = BagExt.MoveAllItems(e.Bag, Main);
            Bus.Publish(result);
        }

        void OnDropItem(DropGhostItem e) {
            // Debug.Log("should drop item");
            if (Ghost.Empty) return;
            if (e.IsOverUi) return;
            Bus.Publish(new SpawnWorldItem(Ghost.Item, e.WorldPosition));
            Ghost.Reset();
        }

        void OnPickWorldItem(PickWorldItem e) {
            Result result = Main.Add(e.WorldItem.Item);
            if (result is Success) Bus.Publish(new DespawnWorldItem(e.WorldItem));
            else Bus.Publish(result);
        }

        // PlayerInput methods
        public void OnCloseUi() {
            UiOpen.SetValue(false);
            SideWindow.SetValue(null);
        }

        public void OnToggleInventory() {
            UiOpen.Toggle();
            if (UiOpen.Value == false) SideWindow.SetValue(null);
        }

        public void OnToggleCharacterSheet() {
            if (SideWindow.Value is CharacterSheet) SideWindow.SetValue(null);
            else SideWindow.SetValue(CharacterSheet);
            UiOpen.SetValue(true);
        }

    }

}
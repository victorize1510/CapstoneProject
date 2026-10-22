using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GDS.Core.UGUI {
    public class SetBagView : BaseListBagView {

        [SerializeField] SetBag data;
        [NonSerialized] SetBag bag;
        [NonSerialized] Dictionary<string, SlotView> slotViewsDict = new();

        public SetBag Bag => bag;

        void Awake() {
            if (bag == null) Init(data, false);
            ghost = StoreLocator.Get().Ghost;
        }

        public void Init(SetBag setBag, bool generateSlots = true) {
            Debug.Log($"Init set bag: {setBag}, local data: {showLocalData}");
            bag = setBag;
            RegisterEvents();
            if (generateSlots) {
                container.Clear();
                CreateSlotViewsRuntime();
            } else {
                GetSlotViews();
                BindSlots();
            }
            UpdateWarning();
            UpdateDebug();
        }

        void OnEnable() {
            RegisterEvents();
            OnCollectionReset();
            UpdateGhost(ghost);
        }

        void OnDisable() {
            UnregisterEvents();
        }

        protected void RegisterEvents() {
            if (bag == null) return;
            UnregisterEvents();
            bag.OnItemChanged += OnItemChanged;
            bag.OnCollectionReset += OnCollectionReset;
            if (ghost != null)
                ghost.OnChange += UpdateGhost;
        }

        protected void UnregisterEvents() {
            if (bag == null) return;
            bag.OnItemChanged -= OnItemChanged;
            bag.OnCollectionReset -= OnCollectionReset;
            if (ghost != null)
                ghost.OnChange -= UpdateGhost;
        }

        protected void OnItemChanged(SetSlot slot) {
            if (slotViewsDict.GetValueOrDefault(slot.Key) is not SlotView slotView) {
                Debug.LogWarning($"Cannot find SlotView at key {slot.Key}", this);
                return;
            }
            slotView.Render();
            UpdateDebug();
        }

        protected void OnCollectionReset() {
            if (slotViews == null) return;
            slotViews.ForEach(s => s.Render());
            UpdateDebug();
        }

        public void CreateSlotViewsRuntime() {
            slotViews.Clear();
            slotViewsDict.Clear();
            foreach (var slot in bag.Slots) {
                SlotView slotView = Instantiate(slotPrefab, container).GetComponent<SlotView>();
                slotView.Init(bag, slot);
                slotView.SetName(slot.Key);
                slotViews.Add(slotView);
                slotViewsDict.Add(slot.Key, slotView);
            }
        }

        private void UpdateGhost(IItemContext context) {
            if (lastGhostSlot != null) lastGhostSlot.IsGhost(false);
            if (context.Bag != Bag) return;
            if (context.Slot is not SetSlot slot) return;
            var bagSlot = Bag.GetItemPosition(context.Item);
            if (context.Slot != bagSlot) return;
            var slotView = slotViewsDict.GetValueOrDefault(slot.Key);
            if (slotView == null) return;
            lastGhostSlot = slotView;
            lastGhostSlot.IsGhost(true);
        }

        void GetSlotViews() {
            slotViews.Clear();
            foreach (Transform child in container) {
                if (child.TryGetComponent<SlotView>(out var slotView)) {
                    slotViews.Add(slotView);
                }
            }
        }

        void BindSlots() {
            slotViewsDict.Clear();
            for (int i = 0; i < bag.Size; i++) {
                var slotView = slotViews.ElementAtOrDefault(i);
                if (slotView != null) {
                    slotView.name = bag.Slots[i].Key;
                    slotView.Init(bag, bag.Slots[i]);
                    slotViewsDict.Add(bag.Slots[i].Key, slotView);
                }
            }
        }

        public void GenerateSlots() {
#if UNITY_EDITOR
            int group = UnityEditor.Undo.GetCurrentGroup();
            GenerateSlotsEditor(data.Size);
            UpdateSlotNames();
            UnityEditor.Undo.SetCurrentGroupName("Generate slot views");
            UnityEditor.Undo.CollapseUndoOperations(group);
#endif
        }

        void UpdateSlotNames() {
            var j = 0;
            var count = container.childCount;
            for (int i = 0; i < count; i++) {
                var slotView = container.GetChild(i).GetComponent<SlotView>();
                if (slotView != null) {
                    slotView.SetName(data.Slots[j++].Key);
                }
            }
        }

        void UpdateWarning() {
            if (warning == null) return;
            warning.SetState(bag.Size, slotViews.Count);
        }

        void UpdateDebug() {
            debugText = bag + "\n=====\n" + bag.Slots.NewLineJoin();
        }

    }
}

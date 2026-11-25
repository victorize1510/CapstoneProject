using System;
using System.Linq;
using UnityEngine;
namespace GDS.Core.UGUI {

    public class ListBagView : BaseListBagView {

        [SerializeField] ListBag data;
        [NonSerialized] ListBag bag;
        public ListBag Bag => bag;

        void Awake() {
            if (bag == null) Init(data, false);
            ghost = StoreLocator.Get().Ghost;
        }

        public void Init(ListBag listBag, bool generateSlots = true) {
            Debug.Log($"Init list bag: {listBag}, generate slots: {generateSlots}, local data: {showLocalData}");
            bag = listBag;
            if (generateSlots) {
                container.Clear();
                CreateSlotViewsRuntime();
            } else {
                GetSlotViews();
                BindSlots();
            }
            RegisterEvents();
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

        void RegisterEvents() {
            if (bag == null) return;
            UnregisterEvents();
            bag.OnItemChanged += OnItemChanged;
            bag.OnCollectionReset += OnCollectionReset;
            if (ghost != null)
                ghost.OnChange += UpdateGhost;
        }

        void UnregisterEvents() {
            if (bag == null) return;
            bag.OnItemChanged -= OnItemChanged;
            bag.OnCollectionReset -= OnCollectionReset;
            if (ghost != null)
                ghost.OnChange -= UpdateGhost;
        }

        void OnItemChanged(ListSlot slot) {
            if (slotViews.ElementAtOrDefault(slot.Index) is not SlotView slotView) {
                Debug.LogWarning($"Cannot find SlotView at index {slot.Index}", this);
                return;
            }
            slotView.Render();
            UpdateDebug();
        }

        void OnCollectionReset() {
            if (slotViews == null) return;
            slotViews.ForEach(s => s.Render());
            UpdateDebug();
        }

        public void GenerateSlots() => GenerateSlotsEditor(data.Size);

        public void CreateSlotViewsRuntime() {
            slotViews.Clear();
            foreach (var slot in bag.Slots) {
                SlotView slotView = Instantiate(slotPrefab, container).GetComponent<SlotView>();
                slotView.Init(bag, slot);
                slotViews.Add(slotView);
            }
        }

        void UpdateGhost(IItemContext context) {
            // if (!showLocalGhost) return;
            if (lastGhostSlot != null) lastGhostSlot.IsGhost(false);
            if (context.Bag != Bag) return;
            if (context.Slot is not ListSlot slot) return;
            var bagSlot = Bag.GetItemPosition(context.Item);
            if (context.Slot != bagSlot) return;
            var slotView = slotViews.ElementAtOrDefault(slot.Index);
            if (slotView == null) return;

            lastGhostSlot = slotView;
            lastGhostSlot.IsGhost(true);
        }

        void BindSlots() {
            for (int i = 0; i < bag.Size; i++) {
                var slotView = slotViews.ElementAtOrDefault(i);
                if (slotView != null) { slotView.Init(bag, bag.Slots[i]); }
            }
        }

        protected void GetSlotViews() {
            slotViews.Clear();
            foreach (Transform child in container) {
                if (child.TryGetComponent<SlotView>(out var slotView)) {
                    slotViews.Add(slotView);
                }
            }
        }

        protected void UpdateWarning() {
            if (warning == null) return;
            warning.SetState(bag.Size, slotViews.Count);
        }

        protected void UpdateDebug() {
            debugText = bag + "\n=====\n" + bag.Slots.NewLineJoin();

        }

    }

}
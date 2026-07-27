using System;
using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core.Events;

namespace GDS.Core.UITK {
    public class DragDropManipulator : PointerManipulator {

        public int DragThreshold = 32;

        protected EventBus bus;
        protected GhostContext ghost;
        protected VisualElement ghostView;

        // Pointer position on mouse down, used to compute the drag delta
        Vector2 lastPointerPos = new();
        // Current pointer position 
        Vector2 currentPointerPos = new();
        [NonSerialized] ItemContext lastContext = new();

        public DragDropManipulator(IStore store, BaseItemView itemView = null) {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Control });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Command });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift });

            bus = store.Bus;
            ghost = store.Ghost;

            itemView ??= new ItemView();
            ghostView = Dom.Div("ghost-item absolute", itemView).PickIgnore(true);
            ghostView.Observe(ghost, context => {
                itemView.Item = context.Item;
                ghostView.SetVisible(context.Item != null);
            });

        }

        protected override void RegisterCallbacksOnTarget() {
            target.Add(ghostView);
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        protected override void UnregisterCallbacksFromTarget() {
            target.Remove(ghostView);
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        void OnPointerDown(PointerDownEvent e) {
            if (!CanStartManipulation(e)) return;
            if (!ghost.Empty) return;

            IItemContext context = (e.target as VisualElement).GetFirstOfType<IItemContext>();

            if (context == null) return;
            if (context.Item == null) return;

            lastPointerPos = e.position;
            lastContext.Copy(context);
        }

        void OnPointerUp(PointerUpEvent e) {
            if (!CanStartManipulation(e)) return;
            // TODO: add a temporal threshold between picking and placing items (compare mouse down and up times)
            //       to fix a rare case when you click on the edge of one cell and release immediately in another cell
            //       resulting in item moving instead of being picked
            lastContext.Clear();

            var context = (e.target as VisualElement).GetFirstOfType<IItemContext>();
            if (context == null) return;

            if (ghost.Item == null && context.Item != null) {
                // Debug.Log($"should pick up item {context.Item.Name} from bag {context.Bag.name}");
                bus.Publish(new PickItem(context));
                return;
            }
            if (ghost.Item != null && context.Slot != null) {
                // Debug.Log($"should place item {ghost.Value.Name} into bag {context.Bag.Name} at slot {context.Slot}");
                bus.Publish(new PlaceGhostItem(context));
                return;
            }
        }

        void OnPointerMove(PointerMoveEvent e) {
            currentPointerPos = e.position;
            ghostView.style.left = e.localPosition.x;
            ghostView.style.top = e.localPosition.y;

            if (!ghost.Empty) return;
            if (lastContext.Empty) return;

            Vector2 delta = currentPointerPos - lastPointerPos;
            if (delta.sqrMagnitude < DragThreshold * DragThreshold) return;

            if (ghost.Empty) bus.Publish(new PickItem(lastContext));
            lastContext.Clear();
        }

    }
}
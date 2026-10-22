using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {


    public interface IStore {
        EventBus Bus { get; }
        GhostContext Ghost { get; }
    }

    // Store is an abstract class which holds references to the global event bus and the currently dragged item context (ghost)
    // A store acts as both a model and controller.
    // A store has lower execution order, making it safe to be accessed in other scripts Awake methods.
    // Store subclasses should hold relevant globally accessible state, as well as implement bespoke behavior
    [DisallowMultipleComponent, DefaultExecutionOrder(-1)]
    public abstract class Store : MonoBehaviour, IStore {

        EventBus bus = new();
        GhostContext ghost = new();

        public EventBus Bus => bus;
        public GhostContext Ghost => ghost;

        // [HideInInspector]
        [SerializeField, TextArea(1, 5)] string ghostContextDebug;

        protected virtual void UpdateGhost(Result result, IItemContext context) {
            if (result is PickItemSuccess r1) {
                Ghost.SetValue(context, r1.Item);
            } else if (result is PlaceItemSuccess r2) {
                if (r2.Replaced != null) Ghost.SetValue(context, r2.Replaced);
                else Ghost.Reset();
            }
            ghostContextDebug = Ghost.ToString();

        }
    }


}

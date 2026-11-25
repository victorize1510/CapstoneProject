using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    /// <summary>Observe normal EventSystem submit first; Map can recover a missing keyboard binding once per frame.</summary>
    [DisallowMultipleComponent]
    public sealed class MapR5SubmitGuard : MonoBehaviour, ISubmitHandler {
        int submittedFrame = -1;
        public void OnSubmit(BaseEventData _) => submittedFrame = Time.frameCount;
        internal void SubmitIfMissing() {
            if (submittedFrame == Time.frameCount || EventSystem.current == null) return;
            var control = GetComponent<Selectable>();
            if (control == null || !control.IsActive() || !control.IsInteractable()) return;
            if (!PlayerActionGate.TryAccept(this, "map-keyboard-submit")) return;
            submittedFrame = Time.frameCount;
            ExecuteEvents.Execute(gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
    }
}

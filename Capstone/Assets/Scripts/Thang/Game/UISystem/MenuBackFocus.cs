using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>Matches the gold focus border used by UI Toolkit Back buttons.</summary>
    public sealed class MenuBackFocus : MonoBehaviour, ISelectHandler, IDeselectHandler {
        Outline border;
        Color normal;
        public void Configure(Outline outline, Color normalColor) { border = outline; normal = normalColor; }
        public void OnSelect(BaseEventData _) { if (border != null) border.effectColor = GuiKitPalette.Gold; }
        public void OnDeselect(BaseEventData _) => Restore();
        void OnDisable() => Restore();
        void Restore() { if (border != null) border.effectColor = normal; }
    }
}

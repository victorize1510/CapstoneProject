using Capstone.Game.UISystem;
using UnityEngine.UIElements;
namespace Capstone.Game.Inventory.Bag {
    // Existing input/cancel callers continue to use this facade.
    static class BagChoicePopup {
        public static bool OwnsKey(VisualElement root)=>RuntimeChoiceMenus.OwnsKey(root);
        public static void Close(VisualElement root)=>RuntimeChoiceMenus.Close(root);
        public static void Bind(DropdownField field)=>RuntimeChoiceMenus.Bind(field);
    }
}

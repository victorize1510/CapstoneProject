using System;
using Capstone.Game.UISystem;
using UnityEngine.UIElements;
namespace Capstone.Game.ShopSystem {
    // Preserve the controller's lifetime contract; choice presentation is shared across runtime panels.
    internal sealed class ShopSortPopup:IDisposable {
        readonly DropdownField field;
        public ShopSortPopup(DropdownField field,VisualElement root){this.field=field;RuntimeChoiceMenus.Bind(field);}
        public void Close(bool focus=true)=>RuntimeChoiceMenus.CloseFor(field,focus);
        public void Dispose()=>RuntimeChoiceMenus.Unbind(field);
    }
}

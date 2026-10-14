using UnityEngine.UIElements;

namespace GDS.Core.UITK {

    public class BaseTooltipView : VisualElement {
        protected Label ItemName = new();
        virtual public void Render(IItemContext context) { }
    }

    public class TooltipView : BaseTooltipView {
        public TooltipView() {
            this.Add("tooltip", ItemName.WithClass("tooltip-item-name"));
        }

        override public void Render(IItemContext context) {
            ItemName.text = $"{context.Item.ItemNameWithQuant()}\nid: {context.Item.Id.Gray()}\ntype: " + context.Item.GetType().ToString().Gray();
        }
    }
}
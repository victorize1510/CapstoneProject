using GDS.Core;
using GDS.Core.UITK;
using UnityEngine.UIElements;

namespace GDS.Examples {

    public class CustomTooltipView : TooltipView {
        public CustomTooltipView(VisualTreeAsset uxml) {
            Clear();
            uxml.CloneTree(this);
            Image = this.Q<VisualElement>(nameof(Image));
            Name = this.Q<Label>(nameof(Name));
            Description = this.Q<Label>(nameof(Description));

            this.PickIgnoreAll();
        }

        Label Name;
        Label Description;
        VisualElement Image;

        public override void Render(IItemContext context) {
            Image.BackgroundImage(context.Item.Icon);
            Name.text = context.Item.Name;
            Description.text = context.Item.Stackable ? $"Stackable: true\nStack size: {context.Item.StackSize}" : "Stackable: false";
        }
    }

}
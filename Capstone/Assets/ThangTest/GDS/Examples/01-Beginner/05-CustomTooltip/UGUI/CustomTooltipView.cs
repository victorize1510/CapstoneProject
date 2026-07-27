using GDS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GDS.Examples.UGUI {

    // Extend one of the available TooltipViews and override render and positioning if necessary
    // TooltipFollowMouse - tooltip follows mouse position with an offset
    // BaseTooltipView - tooltip above item
    public class CustomTooltipView : TooltipFollowMouse {
        [SerializeField] TextMeshProUGUI itemName;
        [SerializeField] Image icon;

        override public void Render(IItemContext context) {
            itemName.text = $"{context.Item.ItemNameWithQuant()}";
            icon.sprite = context.Item.Icon;
        }
    }

}
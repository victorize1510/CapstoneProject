using TMPro;
using UnityEngine;

namespace GDS.Core.UGUI {

    public class TooltipView : BaseTooltipView {
        [SerializeField] TextMeshProUGUI ItemName;

        override public void Render(IItemContext context) {
            ItemName.text = $"{context.Item.ItemNameWithQuant()}";
        }
    }

}
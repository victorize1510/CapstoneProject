using GDS.Core;
using GDS.Core.UGUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GDS.Examples {
    public class TooltipFollowMouse : BaseTooltipView {
        [SerializeField] TextMeshProUGUI ItemName;
        [SerializeField] Vector2 offset;
        Vector2 flipY = new Vector2(1, -1);

        override public void Render(IItemContext context) { ItemName.text = $"{context.Item.ItemNameWithQuant()}"; }
        public override void Position(RectTransform canvas, Rect bounds) { Position(); }
        void Update() { Position(); }
        void Position() {
            var pos = Mouse.current.position.ReadValue() + offset * flipY;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasTransform, pos, null, out Vector2 localPos);
            var x = Mathf.Clamp(localPos.x, canvasTransform.rect.xMin, canvasTransform.rect.xMax - rectTransform.rect.width);
            var y = Mathf.Clamp(localPos.y, canvasTransform.rect.yMin + rectTransform.rect.height, canvasTransform.rect.yMax);
            rectTransform.anchoredPosition = new Vector2(x, y);
        }
    }

}
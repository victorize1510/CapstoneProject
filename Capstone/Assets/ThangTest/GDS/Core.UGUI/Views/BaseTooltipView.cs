using UnityEngine;

namespace GDS.Core.UGUI {
    public abstract class BaseTooltipView : MonoBehaviour {
        protected RectTransform canvasTransform;
        protected RectTransform rectTransform;
        void Awake() {
            canvasTransform = (RectTransform)transform.root;
            rectTransform = (RectTransform)transform;
            rectTransform.pivot = Vector2.up;
            rectTransform.anchorMin = rectTransform.anchorMax = Vector2.one * 0.5f;
        }
        public virtual void Render(IItemContext context) { }
        public virtual void Position(RectTransform canvas, Rect bounds) {
            rectTransform.anchoredPosition = DefaultPosition(canvasTransform, rectTransform, bounds);
        }

        // Default positioning is copied from PoE 
        // First, try positioning above the slot/item, centered horizontally
        // If it doesn't fit above, snap to top and try left
        // If it doesn't fit left, snap to top and try right
        // If it still doesn't fit somehow, you probably need a custom positioning strategy
        public static Vector2 DefaultPosition(RectTransform canvas, RectTransform tooltip, Rect bounds) {
            // Note: an alternative to `ScreenPointToLocalPointInRectangle` is to use canvas inverse transform but it doesn't work in all cases apparently
            // Vector2 canvasPosition = canvas.InverseTransformPoint(worldBounds.min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, bounds.min, null, out Vector2 localPoint);

            float x = localPoint.x + (bounds.width - tooltip.rect.width) * 0.5f;
            float y = localPoint.y + tooltip.rect.height;

            if (y > canvas.rect.yMax) { // does not fit above
                x = localPoint.x - tooltip.rect.width;
                y = canvas.rect.yMax;
                if (x < canvas.rect.xMin) { // does not fit to the left
                    x = localPoint.x + bounds.width;
                }
            } else {
                x = Mathf.Clamp(x, canvas.rect.xMin, canvas.rect.xMax - tooltip.rect.width);
            }
            return new Vector2(x, y);
        }
    }
}
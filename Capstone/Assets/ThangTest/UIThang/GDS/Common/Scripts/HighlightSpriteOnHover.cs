using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Common.Scripts {

    /// <summary>
    /// Highlights an object on mouse over (changes material color).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HighlightSpriteOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        [SerializeField] Color Color = new(1, 1, 0, 0.75f);

        SpriteRenderer Renderer;
        Color InitialColor;

        void Awake() {
            Renderer = GetComponent<SpriteRenderer>();
            InitialColor = Renderer.color;
        }

        public void OnPointerEnter(PointerEventData eventData) => Renderer.color = Color;
        public void OnPointerExit(PointerEventData eventData) => Renderer.color = InitialColor;
    }
}
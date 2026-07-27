using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Common.Scripts {

    /// <summary>
    /// Highlights an object on mouse over (changes material color).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class HighlightObjectOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        [SerializeField] Color Color = new(1, 0.75f, 0, 0.75f);

        Renderer Renderer;
        Color InitialColor;

        void Awake() {
            Renderer = GetComponent<Renderer>();
            InitialColor = Renderer.material.color;
        }

        public void OnPointerEnter(PointerEventData eventData) => Renderer.material.color = Color;
        public void OnPointerExit(PointerEventData eventData) => Renderer.material.color = InitialColor;
    }
}
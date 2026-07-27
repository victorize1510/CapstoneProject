using GDS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GDS.Demos.Basic.UGUI {
    public class RecipeView : MonoBehaviour {
        [SerializeField] Sprite arrowSprite;
        int size = 32;
        public void Init(Recipe recipe) {
            recipe.Ingredients.ForEach(i => AddItemImage(i));
            AddArrow();
            AddItemImage(recipe.Outcome);
        }

        void AddItemImage(ItemBase itemBase) {
            var go = new GameObject();
            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(size, size);
            go.transform.SetParent(transform);
            if (itemBase == null) return;
            var image = go.AddComponent<Image>();
            image.sprite = itemBase.Icon;
        }

        void AddArrow() {
            var go = new GameObject();
            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(size, size);
            go.transform.SetParent(transform);
            var image = go.AddComponent<Image>();
            image.sprite = arrowSprite;
            image.color = Color.gray;
        }
    }
}
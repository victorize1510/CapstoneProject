using UnityEngine;
using GDS.Core.Events;
using GDS.Core.UGUI;
using GDS.Core;

namespace GDS.Demos.Basic.UGUI {

    public class CraftingBenchWindow : MonoBehaviour {
        [SerializeField] WindowView windowView;
        [SerializeField] SlotView outcomeSlotView;
        [SerializeField] ListBagView ingredientsListView;
        [SerializeField] Transform recipesContainer;
        [SerializeField] RecipeView recipePrefab;

        CraftingBench bag;
        bool ready;

        public CraftingBenchWindow Init(CraftingBench bag) {
            this.bag = bag;
            ready = true;
            ingredientsListView.Init(bag);
            outcomeSlotView.Init(bag, bag.OutcomeSlot.Value);
            windowView.Init(bag);
            RenderRecipes();
            return this;
        }

        void OnEnable() {
            if (!ready) return;
            OnDisable();
            bag.OutcomeSlot.OnChange += OnOutcomeSlotChange;
        }
        void OnDisable() {
            if (!ready) return;
            bag.OutcomeSlot.OnChange -= OnOutcomeSlotChange;
        }
        void OnOutcomeSlotChange(Slot slot) { outcomeSlotView.Render(); }

        void RenderRecipes() {
            recipesContainer.Clear();
            foreach (var recipe in bag.Recipes) {
                var recipeView = Instantiate(recipePrefab, recipesContainer);
                recipeView.Init(recipe);
            }
        }

    }

}
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class RecipeEvaluator
    {
        public bool MatchesRecipeExact(ItemState pizza, RecipeData recipe)
        {
            if (pizza == null || recipe == null) return false;
            if (pizza.DefinitionId != recipe.BaseIngredientId && pizza.Category != ItemCategory.Pizza)
                return false;

            var pizzaIngs = pizza.Ingredients;
            var recipeIngs = recipe.Ingredients;

            if (pizzaIngs.Count != recipeIngs.Count)
                return false;

            for (int i = 0; i < recipeIngs.Count; i++)
            {
                var r = recipeIngs[i];
                var p = pizzaIngs[i];

                if (p.IngredientDefId != r.IngredientDefId ||
                    p.Stage != r.Stage ||
                    p.Quantity != r.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        public RecipeData FindMatchingRecipe(ItemState pizza, DefinitionCatalog catalog)
        {
            if (pizza == null || catalog == null) return null;

            foreach (var kvp in catalog.Recipes)
            {
                if (MatchesRecipeExact(pizza, kvp.Value))
                    return kvp.Value;
            }
            return null;
        }
    }
}

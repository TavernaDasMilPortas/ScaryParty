using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class RecipeEvaluator
    {
        private readonly PizzaSpecificationEvaluator _specEvaluator = new PizzaSpecificationEvaluator();

        public PizzaMatchDetail EvaluateSpecification(ItemState pizzaItem, PizzaSpecification spec)
            => _specEvaluator.Evaluate(pizzaItem, spec);

        public bool MatchesSpecificationExact(ItemState pizzaItem, PizzaSpecification spec)
            => _specEvaluator.MatchesExact(pizzaItem, spec);

        /// <summary>
        /// Compatibilidade com RecipeData convertendo para PizzaSpecification.
        /// </summary>
        public bool MatchesRecipeExact(ItemState pizzaItem, RecipeData recipe)
        {
            if (pizzaItem == null || recipe == null)
                return false;

            if (recipe.Specification != null)
            {
                return _specEvaluator.MatchesExact(pizzaItem, recipe.Specification);
            }

            if (pizzaItem.Category != ItemCategory.Pizza &&
                pizzaItem.Category != ItemCategory.PizzaBase &&
                pizzaItem.Category != ItemCategory.Box)
                return false;

            if (pizzaItem.Ingredients.Count != recipe.Ingredients.Count)
                return false;

            var matched = new bool[recipe.Ingredients.Count];

            for (int i = 0; i < pizzaItem.Ingredients.Count; i++)
            {
                var actual = pizzaItem.Ingredients[i];
                bool found = false;

                for (int j = 0; j < recipe.Ingredients.Count; j++)
                {
                    if (matched[j]) continue;

                    var expected = recipe.Ingredients[j];
                    if (actual.IngredientDefId == expected.IngredientDefId &&
                        actual.Stage == expected.Stage &&
                        actual.Count == expected.Count)
                    {
                        matched[j] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;
            }

            return true;
        }

        public RecipeData FindMatchingRecipe(ItemState pizzaItem, DefinitionCatalog catalog)
        {
            if (catalog == null) return null;
            foreach (var kvp in catalog.Recipes)
            {
                if (MatchesRecipeExact(pizzaItem, kvp.Value))
                    return kvp.Value;
            }
            return null;
        }
    }
}

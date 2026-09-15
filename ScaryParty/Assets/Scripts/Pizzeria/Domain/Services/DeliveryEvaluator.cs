using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class DeliveryEvaluationResult
    {
        public OrderId OrderId { get; set; }
        public int DestinationId { get; set; }
        public int PersonalEarned { get; set; }
        public int RestaurantEarned { get; set; }
        public int TotalPotential { get; set; }
        public int DeliveredCount { get; set; }
        public int CorrectBakedCount { get; set; }
        public int BurnedCount { get; set; }
        public int WrongOrIncompleteCount { get; set; }
    }

    public class DeliveryEvaluator
    {
        private readonly RecipeEvaluator _recipeEvaluator = new RecipeEvaluator();

        public DeliveryEvaluationResult EvaluateDelivery(
            OrderState order,
            IReadOnlyList<ItemState> deliveredItems,
            DefinitionCatalog catalog,
            float burnedPayoutFactor = 0f,
            float unbakedPayoutFactor = 0f,
            int restaurantBaseBonus = 10)
        {
            var result = new DeliveryEvaluationResult
            {
                OrderId = order?.Id ?? OrderId.None,
                DestinationId = order?.DestinationId ?? 0,
                DeliveredCount = deliveredItems?.Count ?? 0
            };

            if (order == null)
            {
                // Delivery to destination with no active order -> payout is 0, all consumed
                return result;
            }

            // Expand requested lines into requested unit requirements
            var requestedUnits = new List<RecipeData>();
            for (int i = 0; i < order.Lines.Count; i++)
            {
                var line = order.Lines[i];
                if (catalog.Recipes.TryGetValue(line.RecipeId, out var recipe))
                {
                    for (int q = 0; q < line.RequestedQuantity; q++)
                    {
                        requestedUnits.Add(recipe);
                        result.TotalPotential += recipe.BasePrice;
                    }
                }
            }

            // Sort requested units descending by price for greedy pairing
            requestedUnits.Sort((a, b) => b.BasePrice.CompareTo(a.BasePrice));

            var matchedDeliveredIndices = new HashSet<int>();
            int totalPersonalPayout = 0;

            for (int r = 0; r < requestedUnits.Count; r++)
            {
                var targetRecipe = requestedUnits[r];
                int bestDeliveredIdx = -1;
                float bestContribution = -1f;

                for (int d = 0; d < deliveredItems.Count; d++)
                {
                    if (matchedDeliveredIndices.Contains(d)) continue;
                    var item = deliveredItems[d];

                    if (_recipeEvaluator.MatchesRecipeExact(item, targetRecipe))
                    {
                        float contrib = 0f;
                        if (item.CookingStage == CookingStage.Baked)
                            contrib = 1.0f;
                        else if (item.CookingStage == CookingStage.Burned)
                            contrib = burnedPayoutFactor;
                        else
                            contrib = unbakedPayoutFactor;

                        if (contrib > bestContribution)
                        {
                            bestContribution = contrib;
                            bestDeliveredIdx = d;
                            if (contrib >= 1.0f) break; // Optimal match
                        }
                    }
                }

                if (bestDeliveredIdx >= 0)
                {
                    matchedDeliveredIndices.Add(bestDeliveredIdx);
                    var item = deliveredItems[bestDeliveredIdx];
                    int unitPay = (int)Math.Round(targetRecipe.BasePrice * bestContribution, MidpointRounding.AwayFromZero);
                    totalPersonalPayout += unitPay;

                    if (item.CookingStage == CookingStage.Baked)
                        result.CorrectBakedCount++;
                    else if (item.CookingStage == CookingStage.Burned)
                        result.BurnedCount++;
                    else
                        result.WrongOrIncompleteCount++;
                }
            }

            // Any delivered item that was not paired counts as wrong/extra
            for (int d = 0; d < deliveredItems.Count; d++)
            {
                if (!matchedDeliveredIndices.Contains(d))
                {
                    result.WrongOrIncompleteCount++;
                }
            }

            // Clamp personal payout between 0 and total potential
            result.PersonalEarned = Math.Min(totalPersonalPayout, result.TotalPotential);

            // Restaurant receives base reward scaled by fraction of order completed
            float fractionCompleted = result.TotalPotential > 0
                ? (float)result.PersonalEarned / result.TotalPotential
                : 0f;
            result.RestaurantEarned = (int)Math.Round(restaurantBaseBonus * fractionCompleted, MidpointRounding.AwayFromZero);

            // Mark order lifecycle as completed
            order.Lifecycle = OrderLifecycle.Completed;
            order.FinalEarnedReward = result.PersonalEarned;
            order.Revision++;

            return result;
        }
    }
}

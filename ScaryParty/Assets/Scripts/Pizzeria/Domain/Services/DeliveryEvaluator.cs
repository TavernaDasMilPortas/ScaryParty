using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public struct ItemVerdict
    {
        public ItemId ItemId;
        public int MatchedRecipeId;
        public CookingStage Stage;
        public float Contribution;
        public int Paid;
    }

    public class DeliveryEvaluationResult
    {
        public OrderId OrderId { get; set; }
        public AddressId DestinationId { get; set; }
        public int PersonalEarned { get; set; }
        public int RestaurantEarned { get; set; }
        public int TotalPotential { get; set; }
        public int DeliveredCount { get; set; }
        public int CorrectBakedCount { get; set; }
        public int BurnedCount { get; set; }
        public int WrongOrIncompleteCount { get; set; }
        public List<ItemVerdict> Verdicts { get; set; } = new List<ItemVerdict>();
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
                DestinationId = order != null ? order.DestinationId : AddressId.None,
                DeliveredCount = deliveredItems?.Count ?? 0
            };

            if (order == null)
            {
                // Delivery to destination with no active order -> payout is 0, all consumed
                for (int i = 0; i < deliveredItems.Count; i++)
                {
                    result.Verdicts.Add(new ItemVerdict { ItemId = deliveredItems[i].Id, MatchedRecipeId = 0, Stage = deliveredItems[i].CookingStage, Contribution = 0, Paid = 0 });
                    result.WrongOrIncompleteCount++;
                }
                return result;
            }

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
                            if (contrib >= 1.0f) break; 
                        }
                    }
                }

                if (bestDeliveredIdx >= 0)
                {
                    matchedDeliveredIndices.Add(bestDeliveredIdx);
                    var item = deliveredItems[bestDeliveredIdx];
                    int unitPay = (int)Math.Round(targetRecipe.BasePrice * bestContribution, MidpointRounding.AwayFromZero);
                    totalPersonalPayout += unitPay;

                    result.Verdicts.Add(new ItemVerdict { ItemId = item.Id, MatchedRecipeId = targetRecipe.Id, Stage = item.CookingStage, Contribution = bestContribution, Paid = unitPay });

                    if (item.CookingStage == CookingStage.Baked)
                        result.CorrectBakedCount++;
                    else if (item.CookingStage == CookingStage.Burned)
                        result.BurnedCount++;
                    else
                        result.WrongOrIncompleteCount++;
                }
            }

            for (int d = 0; d < deliveredItems.Count; d++)
            {
                if (!matchedDeliveredIndices.Contains(d))
                {
                    result.Verdicts.Add(new ItemVerdict { ItemId = deliveredItems[d].Id, MatchedRecipeId = 0, Stage = deliveredItems[d].CookingStage, Contribution = 0, Paid = 0 });
                    result.WrongOrIncompleteCount++;
                }
            }

            result.PersonalEarned = Math.Min(totalPersonalPayout, result.TotalPotential);

            float fractionCompleted = result.TotalPotential > 0
                ? (float)result.PersonalEarned / result.TotalPotential
                : 0f;
            result.RestaurantEarned = (int)Math.Round(restaurantBaseBonus * fractionCompleted, MidpointRounding.AwayFromZero);

            return result;
        }
    }

    public class DeliveryService
    {
        private readonly DeliveryEvaluator _evaluator = new DeliveryEvaluator();

        public CommandResult<DeliveryEvaluationResult> SubmitDelivery(
            PizzeriaState state,
            AddressId destinationId,
            IReadOnlyList<ItemId> itemIds,
            ulong attemptId,
            ulong requesterId,
            DefinitionCatalog catalog,
            OrderService orderService)
        {
            if (itemIds == null || itemIds.Count == 0)
                return CommandResult<DeliveryEvaluationResult>.Fail(CommandError.InvalidOperation, "Nenhum item selecionado.");

            if (state.DeliveryAttempts.HasAttempt(attemptId))
                return CommandResult<DeliveryEvaluationResult>.Fail(CommandError.InvalidOperation, "Tentativa já processada (replay)."); // Handled at network level to return cache

            var deliveredItems = new List<ItemState>();
            var uniqueIds = new HashSet<ItemId>();

            foreach (var id in itemIds)
            {
                if (!uniqueIds.Add(id))
                    return CommandResult<DeliveryEvaluationResult>.Fail(CommandError.InvalidOperation, "ID duplicado na entrega.");

                if (!state.Items.TryGetValue(id, out var item))
                    return CommandResult<DeliveryEvaluationResult>.Fail(CommandError.ItemNotFound, "Item não encontrado.");

                if (!item.IsBoxed)
                    return CommandResult<DeliveryEvaluationResult>.Fail(CommandError.Incompatible, "Apenas caixas fechadas podem ser entregues.");

                bool inHand = item.Location.Type == LocationType.Hand;
                bool inBackpack = item.Location.Type == LocationType.Backpack && state.Backpacks.TryGetValue((BackpackId)item.Location.HolderId, out var bp) && bp.EquippedBy == requesterId;

                if (!inHand && !inBackpack)
                    return CommandResult<DeliveryEvaluationResult>.Fail(CommandError.NotOwner, "Você não possui a caixa selecionada.");

                deliveredItems.Add(item);
            }

            var order = orderService.GetActiveOrderForDestination(destinationId, state);

            var result = _evaluator.EvaluateDelivery(order, deliveredItems, catalog);

            foreach (var item in deliveredItems)
            {
                state.Items.Remove(item.Id);
                if (item.Location.Type == LocationType.Backpack)
                {
                    if (state.Backpacks.TryGetValue((BackpackId)item.Location.HolderId, out var backpack))
                        backpack.Remove(item.Id);
                }
            }

            if (order != null)
            {
                order.Lifecycle = OrderLifecycle.Completed;
                order.FinalEarnedReward = result.PersonalEarned;
                order.Revision++;
            }

            state.RestaurantBudget += result.RestaurantEarned;
            state.DeliveryAttempts.Record(attemptId);
            state.IncrementRevision();

            return CommandResult<DeliveryEvaluationResult>.Ok(result);
        }
    }
}

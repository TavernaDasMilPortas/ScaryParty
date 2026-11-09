using System;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public struct CustomerSatisfactionResult
    {
        public bool Accepted;
        public float CompositionScore;
        public floatCookingScoreStruct CookingFactor;
        public float PatienceRatio;
        public float FinalSatisfaction;
        public int BasePrice;
        public int TipAmount;
        public int PerfectionBonus;
        public int PersonalEarned;
        public int RestaurantEarned;
        public string SummaryMessage;
    }

    public struct floatCookingScoreStruct
    {
        public float Value;
        public bool IsBurned;
        public bool IsBaked;
    }

    /// <summary>
    /// Avalia a satisfação de um cliente presencial ao receber uma pizza sem caixa na mesa
    /// e calcula o pagamento único (pessoal + bônus do restaurante) considerando os traços do cliente.
    /// </summary>
    public class CustomerSatisfactionEvaluator
    {
        public CustomerSatisfactionResult EvaluateDineInService(
            CustomerState customer,
            ItemState servedPizza,
            DefinitionCatalog catalog,
            float burnedPayoutFactor = 0f,
            float unbakedPayoutFactor = 0f,
            int restaurantBaseBonus = 10)
        {
            var res = new CustomerSatisfactionResult();
            if (customer == null || servedPizza == null)
            {
                res.Accepted = false;
                res.SummaryMessage = "Pizza ou cliente inválido.";
                return res;
            }

            var spec = customer.RequestedPizza ?? PizzaSpecification.CreateDefaultCheese();
            var match = PizzaSpecificationEvaluator.EvaluateDetailed(servedPizza, spec);

            CustomerTraitData posTrait = null;
            CustomerTraitData negTrait = null;
            if (catalog != null)
            {
                catalog.CustomerTraits.TryGetValue(customer.PositiveTraitId, out posTrait);
                catalog.CustomerTraits.TryGetValue(customer.NegativeTraitId, out negTrait);
            }

            float errorTolerance = (posTrait?.ErrorToleranceDelta ?? 0f) + (negTrait?.ErrorToleranceDelta ?? 0f);
            float tipMult = (posTrait?.TipMultiplier ?? 1f) * (negTrait?.TipMultiplier ?? 1f);
            int perfectionFlat = (posTrait?.PerfectionFlatBonus ?? 0) + (negTrait?.PerfectionFlatBonus ?? 0);

            // Pontuação de composição
            float compScore;
            if (match.IsExactCompositionMatch)
            {
                compScore = 1.0f;
            }
            else
            {
                compScore = Math.Clamp(match.PartialAccuracy + errorTolerance * 0.5f, 0f, 0.85f);
                // Se for Exigente (errorTolerance < -0.1f) e não bateu exato, reduz severamente
                if (errorTolerance < -0.1f && !match.IsExactCompositionMatch)
                {
                    compScore *= 0.35f;
                }
            }

            // Fator de cocção
            float cookFactor = 0f;
            bool isBaked = servedPizza.CookingStage == CookingStage.Baked;
            bool isBurned = servedPizza.CookingStage == CookingStage.Burned;
            if (isBaked)
            {
                cookFactor = 1.0f;
            }
            else if (isBurned)
            {
                cookFactor = Math.Max(0f, burnedPayoutFactor);
            }
            else
            {
                cookFactor = Math.Max(0f, unbakedPayoutFactor);
            }

            float patienceRatio = customer.FoodPatienceMax > 0.01f
                ? Math.Clamp(customer.FoodPatienceRemaining / customer.FoodPatienceMax, 0f, 1f)
                : 1f;

            float finalSatisfaction = Math.Clamp(compScore * cookFactor * (0.65f + 0.35f * patienceRatio), 0f, 1f);

            int basePrice = spec.BasePrice > 0 ? spec.BasePrice : 18;
            int earnedBase = (int)Math.Round(basePrice * compScore * cookFactor);

            int rawTip = 0;
            if (isBaked && compScore >= 0.7f)
            {
                rawTip = (int)Math.Round(basePrice * 0.30f * patienceRatio * tipMult);
            }

            int perfBonus = (match.IsExactCompositionMatch && isBaked) ? perfectionFlat : 0;

            int personalEarned = Math.Max(0, earnedBase + rawTip + perfBonus);
            int restaurantEarned = (isBaked && compScore >= 0.5f)
                ? Math.Max(1, (int)Math.Round(restaurantBaseBonus * finalSatisfaction))
                : 0;

            res.Accepted = true;
            res.CompositionScore = compScore;
            res.CookingFactor = new floatCookingScoreStruct { Value = cookFactor, IsBaked = isBaked, IsBurned = isBurned };
            res.PatienceRatio = patienceRatio;
            res.FinalSatisfaction = finalSatisfaction;
            res.BasePrice = basePrice;
            res.TipAmount = rawTip;
            res.PerfectionBonus = perfBonus;
            res.PersonalEarned = personalEarned;
            res.RestaurantEarned = restaurantEarned;

            if (match.IsExactCompositionMatch && isBaked)
            {
                res.SummaryMessage = $"Mesa {customer.AssignedTableId} servida com perfeição! +R${personalEarned} (Gorjeta: R${rawTip + perfBonus})";
            }
            else if (isBurned)
            {
                res.SummaryMessage = $"Pizza queimada servida na Mesa {customer.AssignedTableId}! +R${personalEarned}";
            }
            else
            {
                res.SummaryMessage = $"Mesa {customer.AssignedTableId} servida! +R${personalEarned}";
            }

            return res;
        }
    }
}

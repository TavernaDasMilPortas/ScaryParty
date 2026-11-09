using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public readonly struct PizzaMatchDetail
    {
        public readonly bool IsExactMatch;
        public readonly bool HasCorrectBaseDough;
        public readonly bool HasRequiredSauce;
        public readonly int MissingToppingsCount;
        public readonly int ExtraToppingsCount;
        public readonly int WrongStageCount;
        public readonly int PortionMismatchCount;
        public readonly float MatchScore;

        public bool IsExactCompositionMatch => IsExactMatch;
        public float PartialAccuracy => MatchScore;

        public PizzaMatchDetail(
            bool isExactMatch,
            bool hasCorrectBaseDough,
            bool hasRequiredSauce,
            int missingToppingsCount,
            int extraToppingsCount,
            int wrongStageCount,
            int portionMismatchCount,
            float matchScore)
        {
            IsExactMatch = isExactMatch;
            HasCorrectBaseDough = hasCorrectBaseDough;
            HasRequiredSauce = hasRequiredSauce;
            MissingToppingsCount = missingToppingsCount;
            ExtraToppingsCount = extraToppingsCount;
            WrongStageCount = wrongStageCount;
            PortionMismatchCount = portionMismatchCount;
            MatchScore = Math.Clamp(matchScore, 0f, 1f);
        }
    }

    /// <summary>
    /// Avalia uma pizza física (ItemState) contra uma PizzaSpecification solicitada.
    /// Verifica explicitamente massa base, estágio da massa, molho e lista canônica de toppings (incluindo Queijo).
    /// </summary>
    public class PizzaSpecificationEvaluator
    {
        public PizzaMatchDetail Evaluate(ItemState pizzaItem, PizzaSpecification spec)
        {
            if (pizzaItem == null || spec == null)
            {
                return new PizzaMatchDetail(false, false, false, 1, 0, 0, 0, 0f);
            }

            if (pizzaItem.Category != ItemCategory.Pizza &&
                pizzaItem.Category != ItemCategory.PizzaBase &&
                pizzaItem.Category != ItemCategory.Box)
            {
                return new PizzaMatchDetail(false, false, false, spec.Toppings.Count, 0, 0, 0, 0f);
            }

            int actualBaseDoughId = pizzaItem.BaseDoughDefinitionId > 0
                ? pizzaItem.BaseDoughDefinitionId
                : pizzaItem.DefinitionId;
            ProcessingStage actualDoughStage = pizzaItem.BaseDoughDefinitionId > 0
                ? pizzaItem.BaseDoughStage
                : pizzaItem.PrepStage;

            bool hasCorrectBaseDough = actualBaseDoughId == spec.BaseDoughId &&
                                       actualDoughStage == spec.RequiredDoughStage;

            bool hasSauceInPizza = false;
            int sauceCountInPizza = 0;
            for (int i = 0; i < pizzaItem.Ingredients.Count; i++)
            {
                var ing = pizzaItem.Ingredients[i];
                if (ing.IngredientDefId == spec.SauceId)
                {
                    hasSauceInPizza = true;
                    sauceCountInPizza += ing.Count;
                }
            }

            bool hasRequiredSauce = spec.RequiresSauce
                ? (hasSauceInPizza && sauceCountInPizza == spec.SaucePortions)
                : !hasSauceInPizza;

            int missingToppings = 0;
            int extraToppings = 0;
            int wrongStage = 0;
            int portionMismatch = 0;

            var matchedPizzaIndices = new HashSet<int>();

            // Marcar entradas de molho como tratadas
            for (int i = 0; i < pizzaItem.Ingredients.Count; i++)
            {
                if (pizzaItem.Ingredients[i].IngredientDefId == spec.SauceId)
                {
                    matchedPizzaIndices.Add(i);
                    if (!spec.RequiresSauce)
                        extraToppings++;
                    else if (pizzaItem.Ingredients[i].Count != spec.SaucePortions)
                        portionMismatch++;
                }
            }

            // Verificar cada topping exigido na especificação
            for (int r = 0; r < spec.Toppings.Count; r++)
            {
                var req = spec.Toppings[r];
                int exactIdx = -1;
                int sameIdWrongStageIdx = -1;
                int sameIdWrongPortionIdx = -1;

                for (int p = 0; p < pizzaItem.Ingredients.Count; p++)
                {
                    if (matchedPizzaIndices.Contains(p)) continue;
                    var actual = pizzaItem.Ingredients[p];
                    if (actual.IngredientDefId == req.IngredientDefId)
                    {
                        if (actual.Stage == req.RequiredStage && actual.Count == req.PortionCount)
                        {
                            exactIdx = p;
                            break;
                        }
                        if (actual.Stage != req.RequiredStage && sameIdWrongStageIdx < 0)
                        {
                            sameIdWrongStageIdx = p;
                        }
                        else if (actual.Count != req.PortionCount && sameIdWrongPortionIdx < 0)
                        {
                            sameIdWrongPortionIdx = p;
                        }
                    }
                }

                if (exactIdx >= 0)
                {
                    matchedPizzaIndices.Add(exactIdx);
                }
                else if (sameIdWrongStageIdx >= 0)
                {
                    matchedPizzaIndices.Add(sameIdWrongStageIdx);
                    wrongStage++;
                }
                else if (sameIdWrongPortionIdx >= 0)
                {
                    matchedPizzaIndices.Add(sameIdWrongPortionIdx);
                    portionMismatch++;
                }
                else
                {
                    missingToppings++;
                }
            }

            // Quaisquer ingredientes restantes na pizza que não pertencem à especificação são extras
            for (int p = 0; p < pizzaItem.Ingredients.Count; p++)
            {
                if (!matchedPizzaIndices.Contains(p))
                {
                    extraToppings++;
                }
            }

            bool isExact = hasCorrectBaseDough &&
                           hasRequiredSauce &&
                           missingToppings == 0 &&
                           extraToppings == 0 &&
                           wrongStage == 0 &&
                           portionMismatch == 0;

            float score = 1.0f;
            if (!hasCorrectBaseDough) score -= 0.6f;
            if (!hasRequiredSauce) score -= 0.35f;
            score -= missingToppings * 0.35f;
            score -= extraToppings * 0.25f;
            score -= wrongStage * 0.30f;
            score -= portionMismatch * 0.15f;

            return new PizzaMatchDetail(
                isExact,
                hasCorrectBaseDough,
                hasRequiredSauce,
                missingToppings,
                extraToppings,
                wrongStage,
                portionMismatch,
                Math.Clamp(score, 0f, 1f));
        }

        public bool MatchesExact(ItemState pizzaItem, PizzaSpecification spec)
        {
            return Evaluate(pizzaItem, spec).IsExactMatch;
        }

        public static PizzaMatchDetail EvaluateDetailed(PizzaSpecification spec, ItemState pizzaItem, DefinitionCatalog catalog = null)
        {
            return new PizzaSpecificationEvaluator().Evaluate(pizzaItem, spec);
        }

        public static PizzaMatchDetail EvaluateDetailed(ItemState pizzaItem, PizzaSpecification spec, DefinitionCatalog catalog = null)
        {
            return new PizzaSpecificationEvaluator().Evaluate(pizzaItem, spec);
        }
    }

    /// <summary>
    /// Gerador de especificações de pizzas baseado em PizzaCompositionRulesData e nos ingredientes
    /// disponíveis/desbloqueados na sessão atual.
    /// </summary>
    public class PizzaOrderGenerator
    {
        private readonly Random _rng;

        public PizzaOrderGenerator()
        {
            _rng = new Random(Guid.NewGuid().GetHashCode());
        }

        public PizzaOrderGenerator(int seed)
        {
            _rng = new Random(seed);
        }

        public static PizzaSpecification GenerateRandomSpecification(DefinitionCatalog catalog, Random rng = null)
        {
            var gen = rng != null ? new PizzaOrderGenerator(rng.Next()) : new PizzaOrderGenerator();
            return gen.GenerateSpecification(catalog);
        }

        public PizzaSpecification GenerateSpecification(
            DefinitionCatalog catalog,
            PizzeriaState state = null,
            UpgradeService upgradeService = null,
            int? overrideMinToppings = null,
            int? overrideMaxToppings = null)
        {
            var rules = catalog?.CompositionRules ?? PizzaCompositionRulesData.CreateDefault();
            var availableCandidates = GetAvailableCandidates(rules, catalog, state, upgradeService);

            if (availableCandidates.Count == 0)
            {
                return PizzaSpecification.CreateDefaultCheese(rules.BasePizzaPrice + 5, rules.DefaultBakeDuration);
            }

            int minT = overrideMinToppings ?? rules.MinToppings;
            int maxT = overrideMaxToppings ?? rules.MaxToppings;
            minT = Math.Clamp(minT, 1, Math.Min(availableCandidates.Count, rules.MaxSupportedToppings));
            maxT = Math.Clamp(maxT, minT, Math.Min(availableCandidates.Count, rules.MaxSupportedToppings));

            int targetToppingCount = _rng.Next(minT, maxT + 1);
            var chosenToppings = new List<ToppingRequirement>(targetToppingCount);
            var pool = new List<ToppingCandidateData>(availableCandidates);

            int totalPrice = rules.BasePizzaPrice;

            for (int i = 0; i < targetToppingCount && pool.Count > 0; i++)
            {
                int pickedIdx = PickWeightedIndex(pool);
                var picked = pool[pickedIdx];
                pool.RemoveAt(pickedIdx);

                chosenToppings.Add(new ToppingRequirement(picked.IngredientId, picked.RequiredStage, picked.PortionCount));
                totalPrice += picked.PriceContribution * picked.PortionCount;
            }

            // Resolver LegacyRecipeId se coincidir com Mussarela (1), Calabresa (2) ou Cogumelo (3) para compatibilidade
            int legacyId = ResolveLegacyRecipeId(chosenToppings);

            return new PizzaSpecification(
                rules.BaseDoughIngredientId,
                rules.SauceIngredientId,
                rules.RequireSauce,
                chosenToppings,
                totalPrice,
                rules.DefaultBakeDuration,
                rules.RequiredDoughStage,
                rules.SaucePortions,
                legacyId);
        }

        public List<ToppingCandidateData> GetAvailableCandidates(
            PizzaCompositionRulesData rules,
            DefinitionCatalog catalog,
            PizzeriaState state,
            UpgradeService upgradeService)
        {
            var result = new List<ToppingCandidateData>();
            if (rules == null || rules.CandidateToppings == null) return result;

            for (int i = 0; i < rules.CandidateToppings.Count; i++)
            {
                var cand = rules.CandidateToppings[i];
                if (cand == null || cand.IngredientId <= 0) continue;

                if (IsCandidateAvailableInSession(cand, catalog, state, upgradeService))
                {
                    result.Add(cand);
                }
            }

            return result;
        }

        public static bool IsCandidateAvailableInSession(
            ToppingCandidateData candidate,
            DefinitionCatalog catalog,
            PizzeriaState state,
            UpgradeService upgradeService)
        {
            if (candidate == null) return false;
            if (candidate.UnlockedByDefault) return true;

            if (state != null)
            {
                int rawId = candidate.RawStorageIngredientId > 0 ? candidate.RawStorageIngredientId : candidate.IngredientId;
                foreach (var kvp in state.StorageSlots)
                {
                    if ((kvp.Key.ingredientDefId == rawId || kvp.Key.ingredientDefId == candidate.IngredientId) &&
                        kvp.Value.Quantity > 0)
                    {
                        return true;
                    }
                }

                // Também verifica se há upgrade desbloqueado para este ingrediente (ex: cogumelo ID 3/8/9)
                if (upgradeService != null && catalog != null)
                {
                    if (upgradeService.IsIngredientOrRecipeUnlocked(candidate.IngredientId, state, catalog) ||
                        (candidate.RawStorageIngredientId > 0 && upgradeService.IsIngredientOrRecipeUnlocked(candidate.RawStorageIngredientId, state, catalog)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int PickWeightedIndex(List<ToppingCandidateData> pool)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Math.Max(0.01f, pool[i].SelectionWeight);
            }

            double roll = _rng.NextDouble() * totalWeight;
            float accum = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                accum += Math.Max(0.01f, pool[i].SelectionWeight);
                if (roll <= accum)
                    return i;
            }

            return pool.Count - 1;
        }

        private static int ResolveLegacyRecipeId(List<ToppingRequirement> toppings)
        {
            if (toppings == null || toppings.Count == 0) return 1;
            bool hasCheese = false;
            bool hasCalabresa = false;
            bool hasMushroom = false;

            for (int i = 0; i < toppings.Count; i++)
            {
                int id = toppings[i].IngredientDefId;
                if (id == 5 || id == 4) hasCheese = true;
                else if (id == 7 || id == 6) hasCalabresa = true;
                else if (id == 9 || id == 8) hasMushroom = true;
            }

            if (hasMushroom) return 3;
            if (hasCalabresa) return 2;
            if (hasCheese) return 1;
            return 1;
        }
    }

    /// <summary>
    /// Formatador compartilhado para exibir especificações de pizza por ingredientes em toda a UI:
    /// comandas 3D (TicketSheetView), modal de comanda (TicketModalPresenter), quadro (TicketBoardPresenter),
    /// guia de montagem (RecipeBookPresenter), entrega e mochila.
    /// </summary>
    public static class PizzaOrderFormatter
    {
        public static string GetIngredientDisplayName(int ingredientDefId, DefinitionCatalog catalog = null, bool shortForm = false)
        {
            if (catalog != null && catalog.Ingredients.TryGetValue(ingredientDefId, out var ing) && !string.IsNullOrEmpty(ing.Name))
            {
                if (shortForm)
                {
                    return ShortenName(ing.Name);
                }
                return ing.Name;
            }

            return ingredientDefId switch
            {
                1 => shortForm ? "Massa Crua" : "Massa Crua",
                2 => shortForm ? "Massa Aberta" : "Massa Aberta",
                3 => shortForm ? "Molho" : "Molho de Tomate",
                4 => shortForm ? "Queijo (Cru)" : "Queijo Inteiro",
                5 => shortForm ? "Queijo" : "Queijo Ralado",
                6 => shortForm ? "Calabresa (Crua)" : "Calabresa Inteira",
                7 => shortForm ? "Calabresa" : "Calabresa Fatiada",
                8 => shortForm ? "Cogumelo (Cru)" : "Cogumelo Inteiro",
                9 => shortForm ? "Cogumelo" : "Cogumelo Fatiado",
                _ => $"Ingrediente #{ingredientDefId}"
            };
        }

        private static string ShortenName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "Item";
            if (fullName.StartsWith("Queijo", StringComparison.OrdinalIgnoreCase)) return "Queijo";
            if (fullName.StartsWith("Calabresa", StringComparison.OrdinalIgnoreCase)) return "Calabresa";
            if (fullName.StartsWith("Cogumelo", StringComparison.OrdinalIgnoreCase)) return "Cogumelo";
            if (fullName.StartsWith("Molho", StringComparison.OrdinalIgnoreCase)) return "Molho";
            return fullName;
        }

        /// <summary>
        /// Título curto da pizza (ex: "Pizza: Queijo + Calabresa" ou "Queijo + Calabresa").
        /// </summary>
        public static string FormatShortTitle(PizzaSpecification spec, DefinitionCatalog catalog = null, bool includePrefix = false)
        {
            if (spec == null)
                return includePrefix ? "Pizza: Padrão" : "Padrão";

            if (spec.Toppings == null || spec.Toppings.Count == 0)
            {
                string sauceOnly = spec.RequiresSauce ? "Molho" : "Massa";
                return includePrefix ? $"Pizza: {sauceOnly}" : sauceOnly;
            }

            var parts = new List<string>(spec.Toppings.Count);
            for (int i = 0; i < spec.Toppings.Count; i++)
            {
                var t = spec.Toppings[i];
                string name = GetIngredientDisplayName(t.IngredientDefId, catalog, shortForm: true);
                if (t.PortionCount > 1)
                    parts.Add($"{t.PortionCount}x {name}");
                else
                    parts.Add(name);
            }

            string joined = string.Join(" + ", parts);
            return includePrefix ? $"Pizza: {joined}" : joined;
        }

        public static string FormatShortSummary(PizzaSpecification spec, DefinitionCatalog catalog = null, bool includePrefix = false)
        {
            return FormatShortTitle(spec, catalog, includePrefix);
        }

        /// <summary>
        /// Resumo compacto para TicketSheetView 3D (ex: "1x Queijo + Calabresa (c/ Molho)").
        /// </summary>
        public static string FormatTicketLineCompact(int quantity, PizzaSpecification spec, DefinitionCatalog catalog = null)
        {
            string title = FormatShortTitle(spec, catalog, includePrefix: false);
            return $"{Math.Max(1, quantity)}x {title}";
        }

        /// <summary>
        /// Descrição completa dos ingredientes para o modal de comanda (ex: "Massa Aberta + Molho + Queijo Ralado, Calabresa Fatiada").
        /// </summary>
        public static string FormatDetailedIngredients(PizzaSpecification spec, DefinitionCatalog catalog = null)
        {
            if (spec == null)
                return "Massa Aberta + Molho de Tomate + Queijo Ralado";

            var items = new List<string>();
            items.Add(GetIngredientDisplayName(spec.BaseDoughId, catalog, shortForm: false));
            if (spec.RequiresSauce)
            {
                items.Add(GetIngredientDisplayName(spec.SauceId, catalog, shortForm: false));
            }

            for (int i = 0; i < spec.Toppings.Count; i++)
            {
                var t = spec.Toppings[i];
                string name = GetIngredientDisplayName(t.IngredientDefId, catalog, shortForm: false);
                items.Add(t.PortionCount > 1 ? $"{t.PortionCount}x {name}" : name);
            }

            return string.Join(" + ", items);
        }

        /// <summary>
        /// Formata os ingredientes presentes em uma pizza física ou DTO de item.
        /// </summary>
        public static string FormatItemCompositionSummary(int ingredientMask, int ingredientCount)
        {
            if (ingredientMask == 0 && ingredientCount == 0)
                return "Sem cobertura";

            var parts = new List<string>();
            if ((ingredientMask & 1) != 0) parts.Add("Molho");
            if ((ingredientMask & 2) != 0) parts.Add("Queijo");
            if ((ingredientMask & 4) != 0) parts.Add("Calabresa");
            if ((ingredientMask & 8) != 0) parts.Add("Cogumelo");

            if (parts.Count == 0)
                return $"{ingredientCount} ingr.";

            return string.Join(" + ", parts);
        }
    }
}

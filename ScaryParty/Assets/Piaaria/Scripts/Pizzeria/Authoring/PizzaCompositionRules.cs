using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class ToppingCandidateEntry
    {
        [Tooltip("ID do ingrediente já preparado (ex: 5 = Queijo Ralado, 7 = Calabresa Fatiada, 9 = Cogumelo Fatiado).")]
        public int ingredientId = 5;

        [Tooltip("Referência opcional ao IngredientDef (se atribuído, usa seu id).")]
        public IngredientDef ingredientAsset;

        public ProcessingStage requiredStage = ProcessingStage.Prepared;

        [Min(1)]
        public int portionCount = 1;

        [Min(0.01f)]
        public float selectionWeight = 1.0f;

        [Min(0)]
        public int priceContribution = 5;

        [Tooltip("Se verdadeiro, está disponível na configuração inicial sem exigir desbloqueio/estoque especial.")]
        public bool unlockedByDefault = true;

        [Tooltip("ID do ingrediente bruto correspondente no estoque (ex: 4 para Queijo, 6 para Calabresa, 8 para Cogumelo) para checagem de disponibilidade na sessão.")]
        public int rawStorageIngredientId = 4;

        public int EffectiveIngredientId => ingredientAsset != null ? ingredientAsset.id : ingredientId;
    }

    /// <summary>
    /// Regras de composição de pizzas por ingredientes.
    /// Queijo ralado conta como topping na composição (não é obrigatório além do limite de toppings),
    /// preservando sua categoria operacional Cheese no preparo/estoque.
    /// </summary>
    [CreateAssetMenu(fileName = "PizzaCompositionRules", menuName = "Scary Party/Pizzeria/Pizza Composition Rules")]
    public class PizzaCompositionRules : ScriptableObject
    {
        [Header("Base e Molho")]
        public int baseDoughIngredientId = 2; // Massa Aberta
        public ProcessingStage requiredDoughStage = ProcessingStage.Prepared;
        public int sauceIngredientId = 3;     // Molho de Tomate
        public bool requireSauce = true;
        public int saucePortions = 1;

        [Header("Limites de Toppings")]
        [Tooltip("Mínimo de toppings distintos gerados inicialmente.")]
        [Range(1, 6)]
        public int minToppings = 1;

        [Tooltip("Máximo de toppings distintos gerados na configuração inicial (padrão: 2).")]
        [Range(1, 6)]
        public int maxToppings = 2;

        [Tooltip("Máximo de toppings suportados pelo sistema e pela rede sem alterar código (padrão: 4, suportando 1..3+ por dados).")]
        [Range(1, 8)]
        public int maxSupportedToppings = 4;

        [Tooltip("Limite máximo de tipos distintos de ingredientes que podem ser colocados em uma pizza durante a montagem.")]
        [Range(4, 16)]
        public int maxDistinctIngredientsOnPizza = 8;

        [Header("Precificação e Forno")]
        [Min(1)]
        public int basePizzaPrice = 10;

        [Min(1f)]
        public float defaultBakeDuration = 15f;

        [Header("Candidatos de Topping (Queijo conta como Topping)")]
        public List<ToppingCandidateEntry> candidateToppings = new List<ToppingCandidateEntry>
        {
            new ToppingCandidateEntry
            {
                ingredientId = 5, // Queijo Ralado
                requiredStage = ProcessingStage.Prepared,
                portionCount = 1,
                selectionWeight = 1.0f,
                priceContribution = 5,
                unlockedByDefault = true,
                rawStorageIngredientId = 4
            },
            new ToppingCandidateEntry
            {
                ingredientId = 7, // Calabresa Fatiada
                requiredStage = ProcessingStage.Prepared,
                portionCount = 1,
                selectionWeight = 1.0f,
                priceContribution = 10,
                unlockedByDefault = true,
                rawStorageIngredientId = 6
            },
            new ToppingCandidateEntry
            {
                ingredientId = 9, // Cogumelo Fatiado
                requiredStage = ProcessingStage.Prepared,
                portionCount = 1,
                selectionWeight = 0.8f,
                priceContribution = 12,
                unlockedByDefault = false, // Só entra quando desbloqueado ou com estoque > 0 na sessão
                rawStorageIngredientId = 8
            }
        };

        public PizzaCompositionRulesData ToDomainData()
        {
            var candidates = new List<ToppingCandidateData>();
            if (candidateToppings != null)
            {
                for (int i = 0; i < candidateToppings.Count; i++)
                {
                    var c = candidateToppings[i];
                    if (c == null || c.EffectiveIngredientId <= 0) continue;
                    candidates.Add(new ToppingCandidateData(
                        c.EffectiveIngredientId,
                        c.requiredStage,
                        Mathf.Max(1, c.portionCount),
                        Mathf.Max(0.01f, c.selectionWeight),
                        Mathf.Max(0, c.priceContribution),
                        c.unlockedByDefault,
                        c.rawStorageIngredientId));
                }
            }

            return new PizzaCompositionRulesData(
                baseDoughIngredientId > 0 ? baseDoughIngredientId : 2,
                requiredDoughStage,
                sauceIngredientId > 0 ? sauceIngredientId : 3,
                requireSauce,
                Mathf.Max(1, saucePortions),
                Mathf.Max(1, minToppings),
                Mathf.Max(minToppings, maxToppings),
                Mathf.Max(3, maxSupportedToppings),
                Mathf.Max(4, maxDistinctIngredientsOnPizza),
                Mathf.Max(1, basePizzaPrice),
                Mathf.Max(1f, defaultBakeDuration),
                candidates);
        }
    }
}

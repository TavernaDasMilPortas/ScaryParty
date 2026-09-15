using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class IngredientRequirement
    {
        public int ingredientId;
        public ProcessingStage requiredStage;
        public int quantity = 1;
    }

    [CreateAssetMenu(fileName = "Recipe_", menuName = "Scary Party/Pizzeria/Recipe Def")]
    public class RecipeDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável")]
        public int id;
        public string recipeName;
        [Tooltip("ID do ingrediente que serve como base da pizza (ex: Massa)")]
        public int baseIngredientId = 1;
        public List<IngredientRequirement> ingredients = new List<IngredientRequirement>();
        [Tooltip("Preço base em R$")]
        public int basePrice = 20;
        [Tooltip("Duração base de forno em segundos")]
        public float baseBakeDuration = 15f;
    }
}

using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Authoring
{
    [CreateAssetMenu(fileName = "Ingredient_", menuName = "Scary Party/Pizzeria/Ingredient Def")]
    public class IngredientDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável")]
        public int id;
        public string ingredientName;
        public ItemCategory category;
        public bool requiresPrep;
        [Tooltip("Asset do ingrediente resultante após preparo (se houver)")]
        public IngredientDef preparedIngredient;
        [Tooltip("ID de fallback para migração e compatibilidade")]
        public int preparedIngredientId;
        public Color placeholderColor = Color.white;
        public GameObject placeholderPrefab;

        public int EffectivePreparedIngredientId => preparedIngredient != null ? preparedIngredient.id : preparedIngredientId;
    }
}

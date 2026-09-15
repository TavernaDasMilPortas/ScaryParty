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
        [Tooltip("ID do ingrediente resultante após preparo (se houver)")]
        public int preparedIngredientId;
        public Color placeholderColor = Color.white;
        public GameObject placeholderPrefab;
    }
}

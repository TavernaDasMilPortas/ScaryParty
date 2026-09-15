using UnityEngine;

namespace ScaryParty.Pizzeria.Authoring
{
    [CreateAssetMenu(fileName = "SupplyItem_", menuName = "Scary Party/Pizzeria/Supply Item Def")]
    public class SupplyItemDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável")]
        public int id;
        public string itemName;
        public int ingredientId;
        public int unitsPerPack = 5;
        public int packPrice = 10;
        public float baseETA = 20.0f;
    }
}

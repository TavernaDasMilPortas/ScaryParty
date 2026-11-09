using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Authoring
{
    [CreateAssetMenu(fileName = "Upgrade_", menuName = "Scary Party/Pizzeria/Upgrade Def")]
    public class UpgradeDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável")]
        public int id;
        public string upgradeName;
        public UpgradeScope scope;
        [Tooltip("Nome da estatística ou ID de receita (ex: cookDuration, slotCount, 3 para cogumelo)")]
        public string statName;
        public UpgradeOperation operation;
        [Tooltip("Valor da contribuição por nível (ex: -0.20 para 20% de redução, +1 para slot extra)")]
        public float valuePerLevel;
        public int maxLevel = 3;
    }
}

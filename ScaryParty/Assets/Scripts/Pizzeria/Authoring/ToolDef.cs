using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Authoring
{
    [CreateAssetMenu(fileName = "Tool_", menuName = "Scary Party/Pizzeria/Tool Def")]
    public class ToolDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável")]
        public int id;
        public string toolName;
        public ToolCapability capabilities;
        [Tooltip("Quantidade inicial disponibilizada na bancada de utensílios")]
        public int initialSharedCount = 1;
        public GameObject placeholderPrefab;
    }
}

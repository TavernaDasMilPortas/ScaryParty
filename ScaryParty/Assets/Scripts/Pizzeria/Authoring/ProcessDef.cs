using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Authoring
{
    [CreateAssetMenu(fileName = "Process_", menuName = "Scary Party/Pizzeria/Process Def")]
    public class ProcessDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável")]
        public int id;
        public string processName;
        public int inputIngredientId;
        public ProcessingStage inputStage;
        public int outputIngredientId;
        public ProcessingStage outputStage;
        [Tooltip("Ferramenta exigida para o processo (None se manual)")]
        public ToolCapability requiredToolCapability = ToolCapability.None;
        [Tooltip("Duração base em segundos")]
        public float baseDuration = 3.0f;
    }
}

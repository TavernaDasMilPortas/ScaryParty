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
        public IngredientDef inputIngredient;
        public ProcessingStage inputStage;
        public IngredientDef outputIngredient;
        public ProcessingStage outputStage;
        [Tooltip("Ferramenta exigida para o processo (None se manual)")]
        public ToolCapability requiredToolCapability = ToolCapability.None;
        [Tooltip("Duração base em segundos")]
        public float baseDuration = 3.0f;

        // Fallbacks para migração
        public int inputIngredientId;
        public int outputIngredientId;

        public int EffectiveInputIngredientId => inputIngredient != null ? inputIngredient.id : inputIngredientId;
        public int EffectiveOutputIngredientId => outputIngredient != null ? outputIngredient.id : outputIngredientId;
    }
}

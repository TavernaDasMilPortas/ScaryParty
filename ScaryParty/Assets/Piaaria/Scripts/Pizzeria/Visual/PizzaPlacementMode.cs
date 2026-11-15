using System;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Modos de posicionamento modular de coberturas e ingredientes na pizza.
    /// Define a regra espacial que o PizzaLayoutGenerator utiliza para distribuir as peças.
    /// </summary>
    public enum PizzaPlacementMode
    {
        /// <summary>
        /// Distribui várias peças individuais pelo espaço útil da pizza (cogumelo, tomate, calabresa),
        /// recalculando conjuntamente com rotação Y e restrição de sobreposição mútua.
        /// </summary>
        Sliced = 0,

        /// <summary>
        /// Aplica uma cobertura contínua sobre a área útil (molho de tomate, queijo fundido).
        /// Não bloqueia fatias como um obstáculo sólido.
        /// </summary>
        Total = 1,

        /// <summary>
        /// Coloca uma peça no centro da pizza (ex: folha de manjericão ou decoração central).
        /// Reserva sua área circular para evitar que outros toppings automáticos sobreponham.
        /// </summary>
        Central = 2,

        /// <summary>
        /// Usa uma lista de posições explícitas coordenadas manualmente no editor, sem sorteio ou correção silenciosa.
        /// </summary>
        Specific = 3,

        /// <summary>
        /// Distribui peças em uma circunferência/anel (ex: azeitonas ou fatias periféricas).
        /// Participa do teste de ocupação circular.
        /// </summary>
        Ring = 4,

        /// <summary>
        /// Distribui muitas peças pequenas com densidade e orçamento próprios (ervas, queijo ralado fino).
        /// </summary>
        Scatter = 5,

        /// <summary>
        /// Ingrediente sem representação geométrica adicional deliberadamente (presença lógica mantida).
        /// </summary>
        None = 6
    }

    [Serializable]
    public struct SpecificPlacementPose
    {
        public UnityEngine.Vector3 localPosition;
        public UnityEngine.Vector3 localRotationEuler;
        public UnityEngine.Vector3 localScale;

        public static SpecificPlacementPose Default => new SpecificPlacementPose
        {
            localPosition = UnityEngine.Vector3.zero,
            localRotationEuler = UnityEngine.Vector3.zero,
            localScale = UnityEngine.Vector3.one
        };
    }
}

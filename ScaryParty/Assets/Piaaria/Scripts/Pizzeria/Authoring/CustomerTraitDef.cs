using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Definitions;

namespace ScaryParty.Pizzeria.Authoring
{
    /// <summary>
    /// Definição de um Traço (Trait) de cliente presencial.
    /// Cada cliente recebe exatamente 1 traço positivo e 1 traço negativo compatíveis entre si.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerTraitDef", menuName = "Scary Party/Pizzeria/Customer Trait Def")]
    public class CustomerTraitDef : ScriptableObject
    {
        [Header("Identificação")]
        public int traitId = 1;
        public string displayName = "Paciente";
        [TextArea(2, 4)]
        public string description = "Espera com mais calma na fila e na mesa.";
        public TraitPolarity polarity = TraitPolarity.Positive;

        [Header("Modificadores de Paciência")]
        [Tooltip("Multiplicador do tempo máximo de paciência na fila (ex: 1.25 = +25% paciência).")]
        [Range(0.4f, 2.0f)]
        public float queuePatienceMultiplier = 1.0f;

        [Tooltip("Multiplicador do tempo máximo de paciência aguardando a pizza na mesa (ex: 1.25 = +25% paciência).")]
        [Range(0.4f, 2.0f)]
        public float foodPatienceMultiplier = 1.0f;

        [Tooltip("Sensibilidade à fila parada (ex: 1.5 = perde paciência 50% mais rápido se a fila estagnar).")]
        [Range(0.5f, 2.5f)]
        public float stagnationSensitivityMultiplier = 1.0f;

        [Header("Modificadores de Pagamento e Satisfação")]
        [Tooltip("Multiplicador de gorjeta quando satisfeito (ex: 1.4 = +40% gorjeta).")]
        [Range(0.2f, 2.5f)]
        public float tipMultiplier = 1.0f;

        [Tooltip("Bônus extra de pagamento quando a pizza vem exatamente certa e bem assada (ex: Apreciador do capricho).")]
        [Range(0, 15)]
        public int perfectionFlatBonus = 0;

        [Tooltip("Tolerância a pequenos erros na composição (ex: Compreensivo reduz penalidade; Exigente aumenta penalidade).")]
        [Range(-0.5f, 0.5f)]
        public float errorToleranceDelta = 0.0f;

        [Header("Movimentação")]
        [Tooltip("Multiplicador da velocidade de caminhada do NPC (ex: Apressado = 1.2, Tranquilo = 0.9).")]
        [Range(0.7f, 1.5f)]
        public float walkSpeedMultiplier = 1.0f;

        [Header("Incompatibilidades")]
        [Tooltip("IDs de traços opostos que nunca devem ser sorteados junto com este traço no mesmo NPC.")]
        public List<int> incompatibleTraitIds = new List<int>();
    }
}

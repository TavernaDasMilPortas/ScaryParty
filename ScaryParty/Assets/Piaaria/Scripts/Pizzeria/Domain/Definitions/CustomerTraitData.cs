using System;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Domain.Definitions
{
    public enum TraitPolarity : byte
    {
        Positive = 0,
        Negative = 1
    }

    /// <summary>
    /// Dados imutáveis de um traço no catálogo de domínio, independentes de UnityEngine/ScriptableObject
    /// para respeitar noEngineReferences: true no assembly ScaryParty.Pizzeria.Domain.
    /// </summary>
    [Serializable]
    public sealed class CustomerTraitData
    {
        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public TraitPolarity Polarity { get; }
        public float QueuePatienceMultiplier { get; }
        public float FoodPatienceMultiplier { get; }
        public float StagnationSensitivityMultiplier { get; }
        public float TipMultiplier { get; }
        public int PerfectionFlatBonus { get; }
        public float ErrorToleranceDelta { get; }
        public float WalkSpeedMultiplier { get; }
        public IReadOnlyList<int> IncompatibleTraitIds { get; }

        public CustomerTraitData(
            int id,
            string name,
            string description,
            TraitPolarity polarity,
            float queuePatienceMultiplier = 1f,
            float foodPatienceMultiplier = 1f,
            float stagnationSensitivityMultiplier = 1f,
            float tipMultiplier = 1f,
            int perfectionFlatBonus = 0,
            float errorToleranceDelta = 0f,
            float walkSpeedMultiplier = 1f,
            IEnumerable<int> incompatibleTraitIds = null)
        {
            Id = id;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Polarity = polarity;
            QueuePatienceMultiplier = Clamp(queuePatienceMultiplier, 0.3f, 2.5f);
            FoodPatienceMultiplier = Clamp(foodPatienceMultiplier, 0.3f, 2.5f);
            StagnationSensitivityMultiplier = Clamp(stagnationSensitivityMultiplier, 0.3f, 3.0f);
            TipMultiplier = Clamp(tipMultiplier, 0.1f, 3.0f);
            PerfectionFlatBonus = Math.Max(0, perfectionFlatBonus);
            ErrorToleranceDelta = Clamp(errorToleranceDelta, -0.8f, 0.8f);
            WalkSpeedMultiplier = Clamp(walkSpeedMultiplier, 0.6f, 1.6f);
            IncompatibleTraitIds = incompatibleTraitIds != null ? new List<int>(incompatibleTraitIds) : (IReadOnlyList<int>)Array.Empty<int>();
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public bool IsCompatibleWith(CustomerTraitData other)
        {
            if (other == null) return true;
            if (other.Id == Id) return false;
            for (int i = 0; i < IncompatibleTraitIds.Count; i++)
            {
                if (IncompatibleTraitIds[i] == other.Id) return false;
            }
            for (int i = 0; i < other.IncompatibleTraitIds.Count; i++)
            {
                if (other.IncompatibleTraitIds[i] == Id) return false;
            }
            return true;
        }

        /// <summary>
        /// Retorna o catálogo padrão com os 10 traços iniciais (5 positivos e 5 negativos) e suas incompatibilidades.
        /// </summary>
        public static List<CustomerTraitData> CreateDefaultTenTraits()
        {
            return new List<CustomerTraitData>
            {
                // 5 Positivos
                new CustomerTraitData(
                    1, "Paciente", "Espera com mais calma na fila e na mesa (+30% paciência na fila, +25% no preparo).",
                    TraitPolarity.Positive, queuePatienceMultiplier: 1.30f, foodPatienceMultiplier: 1.25f,
                    incompatibleTraitIds: new[] { 101, 102 }),

                new CustomerTraitData(
                    2, "Generoso", "Paga gorjeta bem maior quando recebe sua pizza (+45% gorjeta).",
                    TraitPolarity.Positive, tipMultiplier: 1.45f,
                    incompatibleTraitIds: new[] { 104 }),

                new CustomerTraitData(
                    3, "Compreensivo", "Tolera pequenas falhas na montagem sem cortar tanto o pagamento.",
                    TraitPolarity.Positive, errorToleranceDelta: 0.35f,
                    incompatibleTraitIds: new[] { 103 }),

                new CustomerTraitData(
                    4, "Tranquilo", "Caminha sem pressa e não se irrita facilmente quando a fila demora para andar.",
                    TraitPolarity.Positive, queuePatienceMultiplier: 1.15f, stagnationSensitivityMultiplier: 0.70f, walkSpeedMultiplier: 0.92f,
                    incompatibleTraitIds: new[] { 101, 105 }),

                new CustomerTraitData(
                    5, "Apreciador do capricho", "Valoriza pizza perfeita e bem assada com bônus extra de R$6.",
                    TraitPolarity.Positive, tipMultiplier: 1.20f, perfectionFlatBonus: 6,
                    incompatibleTraitIds: Array.Empty<int>()),

                // 5 Negativos
                new CustomerTraitData(
                    101, "Apressado", "Anda rápido e tem menos paciência na fila (-25%) e na mesa (-20%).",
                    TraitPolarity.Negative, queuePatienceMultiplier: 0.75f, foodPatienceMultiplier: 0.80f, walkSpeedMultiplier: 1.20f,
                    incompatibleTraitIds: new[] { 1, 4 }),

                new CustomerTraitData(
                    102, "Impaciente", "Esgota a paciência bem mais depressa se o pedido demorar (-30% fila, -25% mesa).",
                    TraitPolarity.Negative, queuePatienceMultiplier: 0.70f, foodPatienceMultiplier: 0.75f,
                    incompatibleTraitIds: new[] { 1 }),

                new CustomerTraitData(
                    103, "Exigente", "Detesta ingredientes faltando, trocados ou mal preparados.",
                    TraitPolarity.Negative, errorToleranceDelta: -0.35f,
                    incompatibleTraitIds: new[] { 3 }),

                new CustomerTraitData(
                    104, "Pão-duro", "Dá metade da gorjeta normal mesmo quando bem atendido.",
                    TraitPolarity.Negative, tipMultiplier: 0.50f,
                    incompatibleTraitIds: new[] { 2 }),

                new CustomerTraitData(
                    105, "Inquieto", "Fica muito irritado se a fila ficar parada sem avançar (1.5x desgaste de fila parada).",
                    TraitPolarity.Negative, stagnationSensitivityMultiplier: 1.50f,
                    incompatibleTraitIds: new[] { 4 })
            };
        }
    }
}

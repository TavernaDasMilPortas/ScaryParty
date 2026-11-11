using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Snapshot imutável da composição visual de uma pizza (ingredientes, semente determinística e progresso de cozimento).
    /// </summary>
    public sealed class PizzaVisualComposition
    {
        public ulong VisualSeed { get; }
        public int BaseDoughDefinitionId { get; }
        public CookingStage CookingStage { get; }
        public float CookProgress { get; }
        public float BurnProgress { get; }
        public float UsableRadius => SurfaceMetrics.UsableRadius;
        public PizzaSurfaceMetrics SurfaceMetrics { get; }
        public IReadOnlyList<NetIngredientDto> Ingredients => _ingredients;

        private readonly List<NetIngredientDto> _ingredients;

        public PizzaVisualComposition(
            ulong visualSeed,
            int baseDoughDefinitionId,
            CookingStage cookingStage,
            float cookProgress,
            float burnProgress,
            IEnumerable<NetIngredientDto> ingredients,
            PizzaSurfaceMetrics surfaceMetrics)
        {
            VisualSeed = visualSeed != 0 ? visualSeed : 1337UL;
            BaseDoughDefinitionId = baseDoughDefinitionId > 0 ? baseDoughDefinitionId : 2;
            CookingStage = cookingStage;
            CookProgress = cookProgress;
            BurnProgress = burnProgress;
            SurfaceMetrics = surfaceMetrics.UsableRadius > 0.02f ? surfaceMetrics : PizzaSurfaceMetrics.Default;

            _ingredients = new List<NetIngredientDto>();
            if (ingredients != null)
            {
                foreach (var ing in ingredients)
                {
                    if (ing.DefinitionId > 0)
                    {
                        _ingredients.Add(ing);
                    }
                }
            }
        }

        public PizzaVisualComposition(
            ulong visualSeed,
            int baseDoughDefinitionId,
            CookingStage cookingStage,
            float cookProgress,
            float burnProgress,
            IEnumerable<NetIngredientDto> ingredients,
            float usableRadius = 0.145f)
            : this(visualSeed, baseDoughDefinitionId, cookingStage, cookProgress, burnProgress, ingredients,
                   BuildMetricsFromRadius(usableRadius))
        {
        }

        private static PizzaSurfaceMetrics BuildMetricsFromRadius(float usableRadius)
        {
            if (usableRadius > 0.20f) usableRadius *= 0.5f;
            float r = (usableRadius > 0.05f && usableRadius <= 0.25f) ? usableRadius : 0.145f;
            var m = PizzaSurfaceMetrics.Default;
            m.UsableRadius = r;
            m.OuterRadius = r / 0.80f;
            return m;
        }

        public static PizzaVisualComposition FromNetItemDto(NetItemDto dto, float usableRadius = 0.36f)
        {
            ulong seed = dto.ItemId != 0 ? dto.ItemId : (ulong)dto.GetHashCode();
            var list = new List<NetIngredientDto>();

            if (dto.DistinctIngredientCount > 0)
            {
                for (int i = 0; i < dto.DistinctIngredientCount && i < NetItemDto.MaxDistinctIngredients; i++)
                {
                    var ing = dto.GetIngredient(i);
                    if (ing.DefinitionId > 0)
                    {
                        list.Add(ing);
                    }
                }
            }
            else
            {
                // Fallback para IngredientMask legada
                if ((dto.IngredientMask & 1) != 0) list.Add(new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                if ((dto.IngredientMask & 2) != 0) list.Add(new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                if ((dto.IngredientMask & 4) != 0) list.Add(new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                if ((dto.IngredientMask & 8) != 0) list.Add(new NetIngredientDto { DefinitionId = 9, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
            }

            return new PizzaVisualComposition(
                seed,
                dto.BaseDoughDefinitionId,
                (CookingStage)dto.CookingStage,
                dto.CookProgress,
                dto.BurnProgress,
                list,
                usableRadius
            );
        }

        public int GetCompositionFingerprint()
        {
            int hash = HashCode.Combine(VisualSeed, BaseDoughDefinitionId, (byte)CookingStage);
            for (int i = 0; i < _ingredients.Count; i++)
            {
                hash = HashCode.Combine(hash, _ingredients[i].DefinitionId, _ingredients[i].Stage, _ingredients[i].Count);
            }
            return hash;
        }
    }
}

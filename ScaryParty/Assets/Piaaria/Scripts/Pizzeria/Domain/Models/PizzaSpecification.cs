using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    [Serializable]
    public struct ToppingRequirement : IEquatable<ToppingRequirement>, IComparable<ToppingRequirement>
    {
        public int IngredientDefId;
        public ProcessingStage RequiredStage;
        public int PortionCount;

        public int IngredientId => IngredientDefId;
        public int Quantity => PortionCount;

        public ToppingRequirement(int ingredientDefId, ProcessingStage requiredStage, int portionCount = 1)
        {
            IngredientDefId = ingredientDefId;
            RequiredStage = requiredStage;
            PortionCount = Math.Max(1, portionCount);
        }

        public bool Equals(ToppingRequirement other)
        {
            return IngredientDefId == other.IngredientDefId &&
                   RequiredStage == other.RequiredStage &&
                   PortionCount == other.PortionCount;
        }

        public override bool Equals(object obj) => obj is ToppingRequirement other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IngredientDefId, (int)RequiredStage, PortionCount);

        public int CompareTo(ToppingRequirement other)
        {
            int cmp = IngredientDefId.CompareTo(other.IngredientDefId);
            if (cmp != 0) return cmp;
            cmp = ((int)RequiredStage).CompareTo((int)other.RequiredStage);
            if (cmp != 0) return cmp;
            return PortionCount.CompareTo(other.PortionCount);
        }

        public static bool operator ==(ToppingRequirement left, ToppingRequirement right) => left.Equals(right);
        public static bool operator !=(ToppingRequirement left, ToppingRequirement right) => !left.Equals(right);

        public override string ToString() => $"Ing({IngredientDefId}, {RequiredStage}, x{PortionCount})";
    }

    /// <summary>
    /// Especificação canônica de uma pizza solicitada por telefone ou cliente presencial.
    /// Substitui RecipeId como verdade de pedido no domínio.
    /// A lista de toppings é normalizada (ordenada por IngredientDefId e consolidada) na construção.
    /// </summary>
    [Serializable]
    public sealed class PizzaSpecification : IEquatable<PizzaSpecification>
    {
        public const int DefaultBaseDoughId = 2; // Massa Aberta
        public const int DefaultSauceId = 3;     // Molho de Tomate

        public int BaseDoughId { get; }
        public ProcessingStage RequiredDoughStage { get; }
        public int SauceId { get; }
        public bool RequiresSauce { get; }
        public int SaucePortions { get; }
        public IReadOnlyList<ToppingRequirement> Toppings { get; }
        public int QuotedPrice { get; }
        public int BasePrice => QuotedPrice;
        public int ToppingCount => Toppings != null ? Toppings.Count : 0;
        public ToppingRequirement GetTopping(int index) => Toppings[index];
        public bool IsValid => BaseDoughId > 0 && Toppings != null;
        public float BakeDuration { get; }
        public int LegacyRecipeId { get; }

        public PizzaSpecification(
            int baseDoughId,
            int sauceId,
            IEnumerable<ToppingRequirement> toppings,
            int quotedPrice = 20,
            float bakeDuration = 15f)
            : this(baseDoughId, sauceId, sauceId > 0, toppings, quotedPrice, bakeDuration)
        {
        }

        public PizzaSpecification(
            int baseDoughId,
            int sauceId,
            bool requiresSauce,
            IEnumerable<ToppingRequirement> toppings,
            int quotedPrice = 20,
            float bakeDuration = 15f,
            ProcessingStage requiredDoughStage = ProcessingStage.Prepared,
            int saucePortions = 1,
            int legacyRecipeId = 0)
        {
            BaseDoughId = baseDoughId > 0 ? baseDoughId : DefaultBaseDoughId;
            RequiredDoughStage = requiredDoughStage;
            SauceId = sauceId > 0 ? sauceId : DefaultSauceId;
            RequiresSauce = requiresSauce;
            SaucePortions = requiresSauce ? Math.Max(1, saucePortions) : 0;
            Toppings = NormalizeToppings(toppings, BaseDoughId, requiresSauce ? SauceId : -1);
            QuotedPrice = Math.Max(0, quotedPrice);
            BakeDuration = bakeDuration > 0.1f ? bakeDuration : 15f;
            LegacyRecipeId = legacyRecipeId;
        }

        public static PizzaSpecification CreateDefaultCheese(int quotedPrice = 15, float bakeDuration = 15f)
        {
            return new PizzaSpecification(
                DefaultBaseDoughId,
                DefaultSauceId,
                true,
                new[] { new ToppingRequirement(5, ProcessingStage.Prepared, 1) },
                quotedPrice,
                bakeDuration,
                ProcessingStage.Prepared,
                1,
                1);
        }

        public static PizzaSpecification CreateFromToppings(
            IEnumerable<ToppingRequirement> toppings,
            int quotedPrice = 20,
            float bakeDuration = 15f,
            int baseDoughId = DefaultBaseDoughId,
            int sauceId = DefaultSauceId,
            bool requiresSauce = true)
        {
            return new PizzaSpecification(
                baseDoughId,
                sauceId,
                requiresSauce,
                toppings,
                quotedPrice,
                bakeDuration);
        }

        private static IReadOnlyList<ToppingRequirement> NormalizeToppings(
            IEnumerable<ToppingRequirement> rawToppings,
            int baseDoughId,
            int sauceId)
        {
            if (rawToppings == null)
                return Array.Empty<ToppingRequirement>();

            var consolidated = new Dictionary<(int id, ProcessingStage stage), int>();
            foreach (var req in rawToppings)
            {
                if (req.IngredientDefId <= 0 || req.PortionCount <= 0)
                    continue;
                if (req.IngredientDefId == baseDoughId)
                    continue;
                if (sauceId > 0 && req.IngredientDefId == sauceId)
                    continue;

                var key = (req.IngredientDefId, req.RequiredStage);
                if (consolidated.TryGetValue(key, out int existing))
                    consolidated[key] = existing + req.PortionCount;
                else
                    consolidated[key] = req.PortionCount;
            }

            if (consolidated.Count == 0)
                return Array.Empty<ToppingRequirement>();

            var list = new List<ToppingRequirement>(consolidated.Count);
            foreach (var kvp in consolidated)
            {
                list.Add(new ToppingRequirement(kvp.Key.id, kvp.Key.stage, kvp.Value));
            }

            list.Sort();
            return list;
        }

        public bool ContainsTopping(int ingredientDefId)
        {
            for (int i = 0; i < Toppings.Count; i++)
            {
                if (Toppings[i].IngredientDefId == ingredientDefId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Igualdade estrutural da composição da pizza (independente da ordem de inserção dos toppings).
        /// </summary>
        public bool HasSameComposition(PizzaSpecification other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;

            if (BaseDoughId != other.BaseDoughId ||
                RequiredDoughStage != other.RequiredDoughStage ||
                RequiresSauce != other.RequiresSauce ||
                (RequiresSauce && (SauceId != other.SauceId || SaucePortions != other.SaucePortions)) ||
                Toppings.Count != other.Toppings.Count)
            {
                return false;
            }

            for (int i = 0; i < Toppings.Count; i++)
            {
                if (!Toppings[i].Equals(other.Toppings[i]))
                    return false;
            }

            return true;
        }

        public bool Equals(PizzaSpecification other) => HasSameComposition(other);

        public override bool Equals(object obj) => obj is PizzaSpecification other && Equals(other);

        public override int GetHashCode()
        {
            int hash = HashCode.Combine(BaseDoughId, (int)RequiredDoughStage, RequiresSauce, RequiresSauce ? SauceId : 0, Toppings.Count);
            for (int i = 0; i < Toppings.Count; i++)
            {
                hash = HashCode.Combine(hash, Toppings[i].GetHashCode());
            }
            return hash;
        }
    }
}

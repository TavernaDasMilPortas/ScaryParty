using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public readonly struct IngredientEntry : IEquatable<IngredientEntry>
    {
        public readonly int IngredientDefId;
        public readonly ProcessingStage Stage;
        public readonly int Count;

        public IngredientEntry(int ingredientDefId, ProcessingStage stage, int count)
        {
            IngredientDefId = ingredientDefId;
            Stage = stage;
            Count = count;
        }

        public bool Equals(IngredientEntry other) =>
            IngredientDefId == other.IngredientDefId && Stage == other.Stage && Count == other.Count;

        public override bool Equals(object obj) => obj is IngredientEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IngredientDefId, (byte)Stage, Count);
        public override string ToString() => $"Ing({IngredientDefId},{Stage},x{Count})";
    }

    public class ItemState
    {
        public const int DefaultMaxDistinctIngredients = 8;

        public ItemId Id { get; }
        public int DefinitionId { get; set; }
        public int BaseDoughDefinitionId { get; set; }
        public ProcessingStage BaseDoughStage { get; set; }
        public int MaxDistinctIngredients { get; set; } = DefaultMaxDistinctIngredients;
        public ItemCategory Category { get; set; }
        public LocationRef Location { get; set; }
        public ProcessingStage PrepStage { get; set; }
        public CookingStage CookingStage { get; set; }
        public float CookProgress { get; set; }
        public float BurnProgress { get; set; }
        public float CapturedCookDuration { get; set; } = 0f;
        public PackagingState PackagingState { get; set; }
        public AddressId LabelAddress { get; set; } = AddressId.None;

        public bool IsBoxed => PackagingState == PackagingState.Boxed || Category == ItemCategory.Box;
        public bool IsPizzaLike => Category == ItemCategory.Pizza || Category == ItemCategory.PizzaBase || (Category == ItemCategory.Dough && PrepStage == ProcessingStage.Prepared);

        public int LabelAddressId
        {
            get => LabelAddress.Value;
            set => LabelAddress = new AddressId(value);
        }

        // Alias for Legacy Compatibility
        public int LabelDestinationId
        {
            get => LabelAddress.Value;
            set => LabelAddress = new AddressId(value);
        }

        public ulong ReservedByHold { get; set; } = 0; // 0 = None
        public uint Revision { get; set; } = 1;

        private readonly List<IngredientEntry> _ingredients = new List<IngredientEntry>(8);
        public IReadOnlyList<IngredientEntry> Ingredients => _ingredients;

        public ItemState(ItemId id, int definitionId, ItemCategory category, LocationRef location, int maxDistinctIngredients = DefaultMaxDistinctIngredients)
        {
            Id = id;
            DefinitionId = definitionId;
            Category = category;
            Location = location;
            PrepStage = definitionId == 2 || category == ItemCategory.Pizza || category == ItemCategory.PizzaBase
                ? ProcessingStage.Prepared
                : ProcessingStage.Raw;
            CookingStage = CookingStage.Uncooked;
            PackagingState = PackagingState.Unboxed;
            MaxDistinctIngredients = Math.Max(4, maxDistinctIngredients);

            if (category == ItemCategory.Dough || category == ItemCategory.PizzaBase || category == ItemCategory.Pizza || definitionId == 1 || definitionId == 2)
            {
                BaseDoughDefinitionId = definitionId;
                BaseDoughStage = PrepStage;
            }
        }

        public bool CanAddIngredient(int ingredientDefId, ProcessingStage stage = ProcessingStage.Prepared)
        {
            if (CookingStage != CookingStage.Uncooked || PackagingState != PackagingState.Unboxed || CookProgress > 0f)
                return false;

            if (Category != ItemCategory.Dough && Category != ItemCategory.PizzaBase && Category != ItemCategory.Pizza)
                return false;

            if (Category == ItemCategory.Dough && DefinitionId == 1 && PrepStage == ProcessingStage.Raw)
                return false;

            for (int i = 0; i < _ingredients.Count; i++)
            {
                if (_ingredients[i].IngredientDefId == ingredientDefId && _ingredients[i].Stage == stage)
                    return true;
            }

            return _ingredients.Count < MaxDistinctIngredients;
        }

        public bool AddIngredient(int ingredientDefId, ProcessingStage stage, int count = 1)
        {
            if (CookingStage != CookingStage.Uncooked || PackagingState != PackagingState.Unboxed || CookProgress > 0f)
                return false;

            if (BaseDoughDefinitionId <= 0 && (Category == ItemCategory.Dough || Category == ItemCategory.PizzaBase || Category == ItemCategory.Pizza))
            {
                BaseDoughDefinitionId = DefinitionId > 0 ? DefinitionId : 2;
                BaseDoughStage = PrepStage;
            }

            if (Category == ItemCategory.Dough || Category == ItemCategory.PizzaBase)
            {
                Category = ItemCategory.Pizza;
            }

            for (int i = 0; i < _ingredients.Count; i++)
            {
                var entry = _ingredients[i];
                if (entry.IngredientDefId == ingredientDefId && entry.Stage == stage)
                {
                    _ingredients[i] = new IngredientEntry(ingredientDefId, stage, entry.Count + count);
                    Revision++;
                    return true;
                }
            }

            if (_ingredients.Count >= MaxDistinctIngredients)
                return false;

            _ingredients.Add(new IngredientEntry(ingredientDefId, stage, count));
            Revision++;
            return true;
        }
    }

    public class ToolItemState
    {
        public ToolItemId Id { get; }
        public int DefinitionId { get; }
        public ToolCapability Capabilities { get; }
        public LocationRef Location { get; set; }
        public ulong ReservedByWorkerId { get; private set; }
        public double LeaseExpiresAt { get; private set; }
        public uint Revision { get; set; } = 1;

        public ToolItemState(ToolItemId id, int definitionId, ToolCapability capabilities, LocationRef location)
        {
            Id = id;
            DefinitionId = definitionId;
            Capabilities = capabilities;
            Location = location;
            ReservedByWorkerId = 0;
            LeaseExpiresAt = 0;
        }

        public bool IsReserved(double currentTime) => ReservedByWorkerId != 0 && currentTime < LeaseExpiresAt;

        public bool TryAcquireLease(ulong workerId, double currentTime, double leaseDuration = 0.75)
        {
            if (IsReserved(currentTime) && ReservedByWorkerId != workerId)
                return false;

            ReservedByWorkerId = workerId;
            LeaseExpiresAt = currentTime + leaseDuration;
            Revision++;
            return true;
        }

        public void ReleaseLease(ulong workerId)
        {
            if (ReservedByWorkerId == workerId)
            {
                ReservedByWorkerId = 0;
                LeaseExpiresAt = 0;
                Revision++;
            }
        }

        public void ForceReleaseLease()
        {
            if (ReservedByWorkerId != 0)
            {
                ReservedByWorkerId = 0;
                LeaseExpiresAt = 0;
                Revision++;
            }
        }
    }
}

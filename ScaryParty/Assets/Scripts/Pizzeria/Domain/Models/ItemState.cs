using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public readonly struct IngredientEntry : IEquatable<IngredientEntry>
    {
        public readonly int IngredientDefId;
        public readonly ProcessingStage Stage;
        public readonly int Quantity;

        public IngredientEntry(int ingredientDefId, ProcessingStage stage, int quantity = 1)
        {
            IngredientDefId = ingredientDefId;
            Stage = stage;
            Quantity = quantity;
        }

        public bool Equals(IngredientEntry other)
            => IngredientDefId == other.IngredientDefId && Stage == other.Stage && Quantity == other.Quantity;

        public override bool Equals(object obj) => obj is IngredientEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IngredientDefId, (byte)Stage, Quantity);
        public override string ToString() => $"{IngredientDefId}({Stage})x{Quantity}";
    }

    public class ItemState
    {
        public ItemId Id { get; }
        public int DefinitionId { get; set; }
        public ItemCategory Category { get; set; }
        public ProcessingStage PrepStage { get; set; }
        public CookingStage CookingStage { get; set; }
        public PackagingState PackagingState { get; set; }
        public LocationRef Location { get; set; }
        public float Quality { get; set; } = 1.0f;
        public float CookProgress { get; set; } = 0f;
        public float BurnProgress { get; set; } = 0f;
        public int LabelDestinationId { get; set; } = 0;
        public uint Revision { get; set; } = 1;

        private readonly List<IngredientEntry> _ingredients = new List<IngredientEntry>(8);
        public IReadOnlyList<IngredientEntry> Ingredients => _ingredients;

        public ItemState(ItemId id, int definitionId, ItemCategory category, LocationRef location)
        {
            Id = id;
            DefinitionId = definitionId;
            Category = category;
            Location = location;
            PrepStage = ProcessingStage.Raw;
            CookingStage = CookingStage.Uncooked;
            PackagingState = PackagingState.Unboxed;
        }

        public bool CanAddIngredient(int ingredientDefId)
        {
            if (Category != ItemCategory.Pizza && Category != ItemCategory.Dough)
                return false;
            if (CookingStage != CookingStage.Uncooked)
                return false; // Frozen after cooking starts

            // If already present, we can increment quantity
            for (int i = 0; i < _ingredients.Count; i++)
            {
                if (_ingredients[i].IngredientDefId == ingredientDefId)
                    return true;
            }
            return _ingredients.Count < 8; // Max 8 distinct ingredients
        }

        public bool AddIngredient(int ingredientDefId, ProcessingStage stage, int quantity = 1)
        {
            if (!CanAddIngredient(ingredientDefId))
                return false;

            Category = ItemCategory.Pizza; // Once ingredients are added to dough, it's a pizza
            for (int i = 0; i < _ingredients.Count; i++)
            {
                if (_ingredients[i].IngredientDefId == ingredientDefId && _ingredients[i].Stage == stage)
                {
                    _ingredients[i] = new IngredientEntry(ingredientDefId, stage, _ingredients[i].Quantity + quantity);
                    Revision++;
                    return true;
                }
            }

            _ingredients.Add(new IngredientEntry(ingredientDefId, stage, quantity));
            // Keep sorted for canonical comparison
            _ingredients.Sort((a, b) =>
            {
                int c = a.IngredientDefId.CompareTo(b.IngredientDefId);
                return c != 0 ? c : a.Stage.CompareTo(b.Stage);
            });
            Revision++;
            return true;
        }

        public void SetIngredientsDirect(IEnumerable<IngredientEntry> entries)
        {
            _ingredients.Clear();
            if (entries != null)
            {
                _ingredients.AddRange(entries);
                _ingredients.Sort((a, b) =>
                {
                    int c = a.IngredientDefId.CompareTo(b.IngredientDefId);
                    return c != 0 ? c : a.Stage.CompareTo(b.Stage);
                });
            }
            Revision++;
        }
    }

    public class ToolItemState
    {
        public ToolItemId Id { get; }
        public int DefinitionId { get; }
        public ToolCapability Capabilities { get; }
        public LocationRef Location { get; set; }
        public ulong ReservedByWorkerId { get; set; } = 0;
        public double LeaseExpiration { get; set; } = 0;
        public uint Revision { get; set; } = 1;

        public bool IsReserved(double now) => ReservedByWorkerId != 0 && LeaseExpiration > now;

        public ToolItemState(ToolItemId id, int definitionId, ToolCapability capabilities, LocationRef location)
        {
            Id = id;
            DefinitionId = definitionId;
            Capabilities = capabilities;
            Location = location;
        }

        public bool TryAcquireLease(ulong workerId, double now, double duration)
        {
            if (IsReserved(now) && ReservedByWorkerId != workerId)
                return false;
            ReservedByWorkerId = workerId;
            LeaseExpiration = now + duration;
            Revision++;
            return true;
        }

        public void ReleaseLease(ulong workerId)
        {
            if (ReservedByWorkerId == workerId)
            {
                ReservedByWorkerId = 0;
                LeaseExpiration = 0;
                Revision++;
            }
        }
    }
}

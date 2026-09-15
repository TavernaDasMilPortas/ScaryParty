using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class StorageSlotState
    {
        public int StorageId { get; }
        public int IngredientDefId { get; }
        public int Quantity { get; set; }
        public int MaxCapacity { get; set; }
        public uint Revision { get; set; } = 1;

        public StorageSlotState(int storageId, int ingredientDefId, int quantity, int maxCapacity)
        {
            StorageId = storageId;
            IngredientDefId = ingredientDefId;
            Quantity = quantity;
            MaxCapacity = maxCapacity;
        }

        public int AvailableSpace => MaxCapacity > Quantity ? MaxCapacity - Quantity : 0;
    }

    public class SupplyOrderLine
    {
        public int SupplyItemId { get; }
        public int IngredientDefId { get; }
        public int UnitsPerPack { get; }
        public int PackQuantity { get; }
        public int TotalUnits => PackQuantity * UnitsPerPack;
        public int RemainingUnits { get; set; }

        public SupplyOrderLine(int supplyItemId, int ingredientDefId, int unitsPerPack, int packQuantity)
        {
            SupplyItemId = supplyItemId;
            IngredientDefId = ingredientDefId;
            UnitsPerPack = unitsPerPack;
            PackQuantity = packQuantity;
            RemainingUnits = TotalUnits;
        }
    }

    public class SupplyOrderState
    {
        public SupplyOrderId Id { get; }
        public SupplyOrderLifecycle State { get; set; }
        public double OrderedTime { get; }
        public double ExpectedArrivalTime { get; }
        public int TotalCost { get; }
        public int ReceivingSlotIndex { get; set; } = -1;
        public uint Revision { get; set; } = 1;

        private readonly List<SupplyOrderLine> _lines = new List<SupplyOrderLine>();
        public IReadOnlyList<SupplyOrderLine> Lines => _lines;

        public SupplyOrderState(SupplyOrderId id, double orderedTime, double arrivalTime, int cost)
        {
            Id = id;
            State = SupplyOrderLifecycle.InTransit;
            OrderedTime = orderedTime;
            ExpectedArrivalTime = arrivalTime;
            TotalCost = cost;
        }

        public void AddLine(int supplyItemId, int ingredientDefId, int unitsPerPack, int packQuantity)
        {
            _lines.Add(new SupplyOrderLine(supplyItemId, ingredientDefId, unitsPerPack, packQuantity));
            Revision++;
        }

        public bool IsFullyUnloaded()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].RemainingUnits > 0) return false;
            }
            return true;
        }
    }
}

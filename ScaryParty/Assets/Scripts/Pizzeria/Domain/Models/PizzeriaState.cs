using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class PizzeriaState
    {
        public uint GenerationId { get; set; } = 1;
        public uint Revision { get; set; } = 1;
        public int RestaurantBudget { get; set; } = 500;

        private ulong _nextItemId = 1;
        private ulong _nextToolId = 1;
        private ulong _nextOrderId = 1;
        private ulong _nextOrderLineId = 1;
        private ulong _nextOperationId = 1;
        private ulong _nextSupplyOrderId = 1;

        public readonly Dictionary<ItemId, ItemState> Items = new Dictionary<ItemId, ItemState>();
        public readonly Dictionary<ToolItemId, ToolItemState> Tools = new Dictionary<ToolItemId, ToolItemState>();
        public readonly Dictionary<OrderId, OrderState> Orders = new Dictionary<OrderId, OrderState>();
        public readonly Dictionary<StationSlotId, OperationState> ActiveOperations = new Dictionary<StationSlotId, OperationState>();
        public readonly Dictionary<(int storageId, int ingredientDefId), StorageSlotState> StorageSlots = new Dictionary<(int, int), StorageSlotState>();
        public readonly Dictionary<SupplyOrderId, SupplyOrderState> SupplyOrders = new Dictionary<SupplyOrderId, SupplyOrderState>();
        public readonly Dictionary<int, int> UpgradeLevels = new Dictionary<int, int>();

        public ItemId GenerateItemId() => new ItemId(_nextItemId++);
        public ToolItemId GenerateToolId() => new ToolItemId(_nextToolId++);
        public OrderId GenerateOrderId() => new OrderId(_nextOrderId++);
        public OrderLineId GenerateOrderLineId() => new OrderLineId(_nextOrderLineId++);
        public OperationId GenerateOperationId() => new OperationId(_nextOperationId++);
        public SupplyOrderId GenerateSupplyOrderId() => new SupplyOrderId(_nextSupplyOrderId++);

        public void IncrementRevision() => Revision++;

        public void Reset(uint newGenerationId, int startingBudget = 500)
        {
            GenerationId = newGenerationId;
            Revision = 1;
            RestaurantBudget = startingBudget;
            _nextItemId = 1;
            _nextToolId = 1;
            _nextOrderId = 1;
            _nextOrderLineId = 1;
            _nextOperationId = 1;
            _nextSupplyOrderId = 1;

            Items.Clear();
            Tools.Clear();
            Orders.Clear();
            ActiveOperations.Clear();
            StorageSlots.Clear();
            SupplyOrders.Clear();
            UpgradeLevels.Clear();
        }
    }
}

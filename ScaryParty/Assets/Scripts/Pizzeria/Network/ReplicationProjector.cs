using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public class ReplicationProjector
    {
        private readonly PizzeriaNetworkState _netState;

        public ReplicationProjector(PizzeriaNetworkState netState)
        {
            _netState = netState;
        }

        public void ProjectFullState(PizzeriaState domainState)
        {
            if (_netState == null || domainState == null) return;

            // Project Restaurant Budget
            if (_netState.RestaurantBudget.Value != domainState.RestaurantBudget)
            {
                _netState.RestaurantBudget.Value = domainState.RestaurantBudget;
            }

            // Project Items
            _netState.Items.Clear();
            foreach (var kvp in domainState.Items)
            {
                var item = kvp.Value;
                _netState.Items.Add(new NetItemDto
                {
                    ItemId = item.Id.Value,
                    DefinitionId = item.DefinitionId,
                    Category = (byte)item.Category,
                    PrepStage = (byte)item.PrepStage,
                    CookingStage = (byte)item.CookingStage,
                    PackagingState = (byte)item.PackagingState,
                    LocationType = (byte)item.Location.Type,
                    HolderId = item.Location.HolderId,
                    SlotId = item.Location.SlotId,
                    Quality = item.Quality,
                    CookProgress = item.CookProgress,
                    BurnProgress = item.BurnProgress,
                    LabelDestinationId = item.LabelDestinationId,
                    Revision = item.Revision
                });
            }

            // Project Tools
            _netState.Tools.Clear();
            foreach (var kvp in domainState.Tools)
            {
                var tool = kvp.Value;
                _netState.Tools.Add(new NetToolDto
                {
                    ToolId = tool.Id.Value,
                    DefinitionId = tool.DefinitionId,
                    Capabilities = (byte)tool.Capabilities,
                    LocationType = (byte)tool.Location.Type,
                    HolderId = tool.Location.HolderId,
                    SlotId = tool.Location.SlotId,
                    WorkerId = tool.ReservedByWorkerId,
                    LeaseExpiration = tool.LeaseExpiration,
                    Revision = tool.Revision
                });
            }

            // Project Storage Slots
            _netState.StorageSlots.Clear();
            foreach (var kvp in domainState.StorageSlots)
            {
                var slot = kvp.Value;
                _netState.StorageSlots.Add(new NetStorageDto
                {
                    StorageId = slot.StorageId,
                    IngredientDefId = slot.IngredientDefId,
                    Quantity = slot.Quantity,
                    MaxCapacity = slot.MaxCapacity,
                    Revision = slot.Revision
                });
            }

            // Project Station Slots
            _netState.StationSlots.Clear();
            foreach (var kvp in domainState.ActiveOperations)
            {
                var op = kvp.Value;
                _netState.StationSlots.Add(new NetStationSlotDto
                {
                    StationId = op.SlotId.StationId,
                    SlotIndex = op.SlotId.SlotIndex,
                    HeldItemId = op.InputItemId.Value,
                    ToolItemId = op.ToolItemId.Value,
                    WorkerId = op.WorkerId,
                    Progress = op.AccumulatedProgress,
                    ProcessId = op.ProcessId,
                    Revision = op.Revision
                });
            }

            // Project Orders
            _netState.Orders.Clear();
            foreach (var kvp in domainState.Orders)
            {
                var order = kvp.Value;
                var dto = new NetOrderDto
                {
                    OrderId = order.Id.Value,
                    DestinationId = order.DestinationId,
                    Lifecycle = (byte)order.Lifecycle,
                    DeadlineTime = order.DeadlineTime,
                    QuotedReward = order.QuotedPotentialReward,
                    FinalEarnedReward = order.FinalEarnedReward,
                    LineCount = order.Lines.Count,
                    Revision = order.Revision
                };

                if (order.Lines.Count > 0)
                {
                    dto.RecipeId_0 = order.Lines[0].RecipeId;
                    dto.Qty_0 = order.Lines[0].RequestedQuantity;
                }
                if (order.Lines.Count > 1)
                {
                    dto.RecipeId_1 = order.Lines[1].RecipeId;
                    dto.Qty_1 = order.Lines[1].RequestedQuantity;
                }
                if (order.Lines.Count > 2)
                {
                    dto.RecipeId_2 = order.Lines[2].RecipeId;
                    dto.Qty_2 = order.Lines[2].RequestedQuantity;
                }

                _netState.Orders.Add(dto);
            }

            // Project Supply Orders
            _netState.SupplyOrders.Clear();
            foreach (var kvp in domainState.SupplyOrders)
            {
                var sup = kvp.Value;
                _netState.SupplyOrders.Add(new NetSupplyDto
                {
                    SupplyOrderId = sup.Id.Value,
                    State = (byte)sup.State,
                    ExpectedArrivalTime = sup.ExpectedArrivalTime,
                    TotalCost = sup.TotalCost,
                    ReceivingSlotIndex = sup.ReceivingSlotIndex,
                    Revision = sup.Revision
                });
            }

            // Project Upgrades
            _netState.Upgrades.Clear();
            foreach (var kvp in domainState.UpgradeLevels)
            {
                _netState.Upgrades.Add(new NetUpgradeDto
                {
                    UpgradeId = kvp.Key,
                    Level = kvp.Value
                });
            }

            _netState.PublishedRevision.Value = domainState.Revision;
        }
    }
}

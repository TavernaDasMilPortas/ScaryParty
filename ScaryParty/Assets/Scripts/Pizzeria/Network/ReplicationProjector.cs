using System;
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

            ProjectItemsDelta(domainState);
            ProjectToolsDelta(domainState);
            ProjectStorageSlotsDelta(domainState);
            ProjectStationSlotsDelta(domainState);
            ProjectOrdersDelta(domainState);
            ProjectOrderLinesDelta(domainState);
            ProjectCallsDelta(domainState);
            ProjectStacksDelta(domainState);
            ProjectBackpacksDelta(domainState);
            ProjectSupplyOrdersDelta(domainState);
            ProjectUpgradesDelta(domainState);

            _netState.PublishedRevision.Value = domainState.Revision;
        }

        private void ProjectItemsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Items;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Items) domainKeys.Add(kvp.Key.Value);

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].ItemId)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.Items)
            {
                var dto = CreateItemDto(kvp.Value);
                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].ItemId == dto.ItemId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectToolsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Tools;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Tools) domainKeys.Add(kvp.Key.Value);

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].ToolId)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.Tools)
            {
                var dto = CreateToolDto(kvp.Value);
                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].ToolId == dto.ToolId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectStorageSlotsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.StorageSlots;
            var domainKeys = new HashSet<(int, int)>();
            foreach (var kvp in domainState.StorageSlots) domainKeys.Add((kvp.Value.StorageId, kvp.Value.IngredientDefId));

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                var key = (networkList[i].StorageId, networkList[i].IngredientDefId);
                if (!domainKeys.Contains(key)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.StorageSlots)
            {
                var dto = CreateStorageDto(kvp.Value);
                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].StorageId == dto.StorageId && networkList[i].IngredientDefId == dto.IngredientDefId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectStationSlotsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.StationSlots;
            var domainKeys = new HashSet<(int, int)>();
            foreach (var kvp in domainState.ActiveOperations)
            {
                var op = kvp.Value;
                domainKeys.Add((op.SlotId.StationId, op.SlotId.SlotIndex));
            }

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                var key = (networkList[i].StationId, networkList[i].SlotIndex);
                if (!domainKeys.Contains(key)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.ActiveOperations)
            {
                var dto = CreateStationSlotDto(kvp.Value);
                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].StationId == dto.StationId && networkList[i].SlotIndex == dto.SlotIndex)
                    {
                        existingIdx = i; break;
                    }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectOrdersDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Orders;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Orders) domainKeys.Add(kvp.Key.Value);

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].OrderId)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.Orders)
            {
                var dto = CreateOrderDto(kvp.Value);
                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].OrderId == dto.OrderId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectOrderLinesDelta(PizzeriaState domainState)
        {
            var networkList = _netState.OrderLines;
            var domainKeys = new HashSet<ulong>();
            foreach (var order in domainState.Orders.Values)
            {
                foreach (var line in order.Lines)
                {
                    domainKeys.Add(line.Id.Value);
                }
            }

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].OrderLineId)) networkList.RemoveAt(i);
            }

            foreach (var order in domainState.Orders.Values)
            {
                foreach (var line in order.Lines)
                {
                    var dto = new NetOrderLineDto
                    {
                        OrderLineId = line.Id.Value,
                        OrderId = order.Id.Value,
                        RecipeId = line.RecipeId,
                        RequestedQuantity = line.RequestedQuantity,
                        DeliveredQuantity = line.DeliveredQuantity,
                        Revision = order.Revision
                    };

                    int existingIdx = -1;
                    for (int i = 0; i < networkList.Count; i++)
                    {
                        if (networkList[i].OrderLineId == dto.OrderLineId) { existingIdx = i; break; }
                    }

                    if (existingIdx >= 0)
                    {
                        if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                    }
                    else
                    {
                        networkList.Add(dto);
                    }
                }
            }
        }

        private void ProjectCallsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Calls;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Calls) domainKeys.Add(kvp.Key.Value);

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].CallId)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.Calls)
            {
                var call = kvp.Value;
                var dto = new NetCallDto
                {
                    CallId = call.Id.Value,
                    AddressId = call.AddressId.Value,
                    ExpiresAt = call.ExpiresAt,
                    Revision = domainState.Revision
                };

                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].CallId == dto.CallId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectStacksDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Stacks;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Stacks) domainKeys.Add(kvp.Key.Value);

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].StackId)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.Stacks)
            {
                var stack = kvp.Value;
                var dto = new NetStackDto
                {
                    StackId = stack.Id.Value,
                    StationId = stack.StationId,
                    Capacity = stack.Capacity,
                    ItemCount = stack.Items.Count,
                    TopItemId = stack.TopItem.Value,
                    Revision = stack.Revision
                };

                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].StackId == dto.StackId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectBackpacksDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Backpacks;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Backpacks) domainKeys.Add(kvp.Key.Value);

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].BackpackId)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.Backpacks)
            {
                var bp = kvp.Value;
                var dto = new NetBackpackDto
                {
                    BackpackId = bp.Id.Value,
                    EquippedBy = bp.EquippedBy,
                    PosX = bp.WorldPos.X,
                    PosY = bp.WorldPos.Y,
                    PosZ = bp.WorldPos.Z,
                    RotX = bp.WorldRot.X,
                    RotY = bp.WorldRot.Y,
                    RotZ = bp.WorldRot.Z,
                    RotW = bp.WorldRot.W,
                    Capacity = bp.Capacity,
                    ItemCount = bp.Items.Count,
                    Revision = bp.Revision
                };

                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].BackpackId == dto.BackpackId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectSupplyOrdersDelta(PizzeriaState domainState)
        {
            var networkList = _netState.SupplyOrders;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.SupplyOrders) domainKeys.Add(kvp.Key.Value);

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].SupplyOrderId)) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.SupplyOrders)
            {
                var dto = CreateSupplyDto(kvp.Value);
                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].SupplyOrderId == dto.SupplyOrderId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private void ProjectUpgradesDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Upgrades;
            var domainKeysList = new List<object>();
            foreach (var kvp in domainState.UpgradeLevels)
            {
                domainKeysList.Add(kvp.Key);
            }

            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                bool found = false;
                foreach (var k in domainKeysList)
                {
                    if (k.Equals(networkList[i].UpgradeId))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) networkList.RemoveAt(i);
            }

            foreach (var kvp in domainState.UpgradeLevels)
            {
                var dto = new NetUpgradeDto
                {
                    UpgradeId = kvp.Key,
                    Level = kvp.Value
                };
                
                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].UpgradeId.Equals(dto.UpgradeId))
                    {
                        existingIdx = i;
                        break;
                    }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) networkList[existingIdx] = dto;
                }
                else
                {
                    networkList.Add(dto);
                }
            }
        }

        private NetItemDto CreateItemDto(ItemState item)
        {
            byte mask = 0;
            byte count = 0;
            if (item.Ingredients != null)
            {
                var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.Catalog;
                foreach (var entry in item.Ingredients)
                {
                    count++;
                    if (catalog.Ingredients.TryGetValue(entry.IngredientDefId, out var ingData))
                    {
                        var cat = ingData.Category;
                        if (cat == ItemCategory.Sauce) mask |= 1;
                        else if (cat == ItemCategory.Cheese) mask |= 2;
                        else if (cat == ItemCategory.Topping) mask |= 4;
                        else if (cat == ItemCategory.Dough) mask |= 8;
                    }
                }
            }

            return new NetItemDto
            {
                ItemId = item.Id.Value,
                DefinitionId = item.DefinitionId,
                Category = (byte)item.Category,
                PrepStage = (byte)item.PrepStage,
                CookingStage = (byte)item.CookingStage,
                PackagingState = (byte)item.PackagingState,
                LocationType = (byte)item.Location.Type,
                HolderId = item.Location.HolderId,
                SlotId = item.Location.SlotIndex,
                Quality = item.Quality,
                CookProgress = item.CookProgress,
                BurnProgress = item.BurnProgress,
                LabelAddressId = item.LabelAddressId,
                ReservedByHold = item.ReservedByHold,
                Revision = item.Revision,
                IngredientMask = mask,
                IngredientCount = count
            };
        }

        private NetToolDto CreateToolDto(ToolItemState tool)
        {
            return new NetToolDto
            {
                ToolId = tool.Id.Value,
                DefinitionId = tool.DefinitionId,
                Capabilities = (byte)tool.Capabilities,
                LocationType = (byte)tool.Location.Type,
                HolderId = tool.Location.HolderId,
                SlotId = tool.Location.SlotIndex,
                WorkerId = tool.ReservedByWorkerId,
                LeaseExpiration = tool.LeaseExpiration,
                Revision = tool.Revision
            };
        }

        private NetStorageDto CreateStorageDto(StorageSlotState slot)
        {
            return new NetStorageDto
            {
                StorageId = slot.StorageId,
                IngredientDefId = slot.IngredientDefId,
                Quantity = slot.Quantity,
                MaxCapacity = slot.MaxCapacity,
                Revision = slot.Revision
            };
        }

        private NetStationSlotDto CreateStationSlotDto(OperationState op)
        {
            return new NetStationSlotDto
            {
                StationId = op.SlotId.StationId,
                SlotIndex = op.SlotId.SlotIndex,
                HeldItemId = op.InputItemId.Value,
                ToolItemId = op.ToolItemId.Value,
                WorkerId = op.WorkerId,
                Phase = (byte)op.Phase,
                Accumulated = op.Progress.Accumulated,
                Duration = op.Progress.Duration,
                ServerTime = op.Progress.LastTick,
                ProcessId = op.ProcessId,
                Revision = op.Revision
            };
        }

        private NetOrderDto CreateOrderDto(OrderState order)
        {
            var dto = new NetOrderDto
            {
                OrderId = order.Id.Value,
                DestinationId = order.DestinationId.Value,
                Lifecycle = (byte)order.Lifecycle,
                DeadlineTime = order.DeadlineTime,
                QuotedReward = order.QuotedPotentialReward,
                FinalEarnedReward = order.FinalEarnedReward,
                TicketArchived = order.TicketArchived,
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
            
            return dto;
        }

        private NetSupplyDto CreateSupplyDto(SupplyOrderState sup)
        {
            return new NetSupplyDto
            {
                SupplyOrderId = sup.Id.Value,
                State = (byte)sup.State,
                ExpectedArrivalTime = sup.ExpectedArrivalTime,
                TotalCost = sup.TotalCost,
                ReceivingSlotIndex = sup.ReceivingSlotIndex,
                Revision = sup.Revision
            };
        }
    }
}

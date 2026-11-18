using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
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

            bool anyChanged = false;

            if (_netState.RestaurantBudget.Value != domainState.RestaurantBudget)
            {
                _netState.RestaurantBudget.Value = domainState.RestaurantBudget;
                anyChanged = true;
            }

            var nightDto = CreateNightStateDto(domainState);
            if (!_netState.NightState.Value.Equals(nightDto))
            {
                _netState.NightState.Value = nightDto;
                anyChanged = true;
            }

            var runDto = CreateRunStateDto(domainState);
            if (!_netState.RunState.Value.Equals(runDto))
            {
                _netState.RunState.Value = runDto;
                anyChanged = true;
            }

            anyChanged |= ProjectItemsDelta(domainState);
            anyChanged |= ProjectToolsDelta(domainState);
            anyChanged |= ProjectStorageSlotsDelta(domainState);
            anyChanged |= ProjectStationSlotsDelta(domainState);
            anyChanged |= ProjectOrdersDelta(domainState);
            anyChanged |= ProjectOrderLinesDelta(domainState);
            anyChanged |= ProjectCallsDelta(domainState);
            anyChanged |= ProjectStacksDelta(domainState);
            anyChanged |= ProjectBackpacksDelta(domainState);
            anyChanged |= ProjectSupplyOrdersDelta(domainState);
            anyChanged |= ProjectUpgradesDelta(domainState);
            anyChanged |= ProjectCustomersDelta(domainState);
            anyChanged |= ProjectTablesDelta(domainState);
            anyChanged |= ProjectRunVoteOptionsDelta(domainState);
            anyChanged |= ProjectRunShopOffersDelta(domainState);
            anyChanged |= ProjectRunPlayerStatesDelta(domainState);

            if (anyChanged || _netState.PublishedRevision.Value == 0)
            {
                _netState.PublishedRevision.Value = domainState.Revision;
            }
        }

        private bool ProjectItemsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Items;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Items) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].ItemId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectToolsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Tools;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Tools) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].ToolId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectStorageSlotsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.StorageSlots;
            var domainKeys = new HashSet<(int, int)>();
            foreach (var kvp in domainState.StorageSlots) domainKeys.Add((kvp.Value.StorageId, kvp.Value.IngredientDefId));

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                var key = (networkList[i].StorageId, networkList[i].IngredientDefId);
                if (!domainKeys.Contains(key))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectStationSlotsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.StationSlots;
            var domainKeys = new HashSet<(int, int)>();
            foreach (var kvp in domainState.ActiveOperations)
            {
                var op = kvp.Value;
                domainKeys.Add((op.SlotId.StationId, op.SlotId.SlotIndex));
            }

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                var key = (networkList[i].StationId, networkList[i].SlotIndex);
                if (!domainKeys.Contains(key))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectOrdersDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Orders;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Orders) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].OrderId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectOrderLinesDelta(PizzeriaState domainState)
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

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].OrderLineId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                        Spec = NetPizzaSpecDto.FromDomain(line.Specification),
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
                        if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                    }
                    else
                    {
                        networkList.Add(dto);
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private bool ProjectCallsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Calls;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Calls) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].CallId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
            }

            foreach (var kvp in domainState.Calls)
            {
                var call = kvp.Value;
                var primaryLine = call.Lines != null && call.Lines.Count > 0 ? call.Lines[0] : null;
                var dto = new NetCallDto
                {
                    CallId = call.Id.Value,
                    AddressId = call.AddressId.Value,
                    ExpiresAt = call.ExpiresAt,
                    PrimarySpec = NetPizzaSpecDto.FromDomain(primaryLine?.Specification),
                    PrimaryQty = primaryLine != null ? primaryLine.RequestedQuantity : 1,
                    Revision = 0
                };

                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].CallId == dto.CallId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectStacksDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Stacks;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Stacks) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].StackId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectBackpacksDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Backpacks;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Backpacks) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].BackpackId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    Integrity = bp.Integrity,
                    MaxIntegrity = bp.MaxIntegrity,
                    IsTorn = bp.IsTorn,
                    Revision = bp.Revision
                };

                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].BackpackId == dto.BackpackId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectSupplyOrdersDelta(PizzeriaState domainState)
        {
            var networkList = _netState.SupplyOrders;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.SupplyOrders) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].SupplyOrderId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectUpgradesDelta(PizzeriaState domainState)
        {
            var networkList = _netState.Upgrades;
            var domainKeysList = new List<object>();
            foreach (var kvp in domainState.UpgradeLevels)
            {
                domainKeysList.Add(kvp.Key);
            }

            bool changed = false;
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
                if (!found)
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
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
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private NetItemDto CreateItemDto(ItemState item)
        {
            byte mask = 0;
            byte count = 0;
            byte distinctCount = 0;

            var dto = new NetItemDto
            {
                ItemId = item.Id.Value,
                DefinitionId = item.DefinitionId,
                BaseDoughDefinitionId = item.BaseDoughDefinitionId > 0 ? item.BaseDoughDefinitionId : item.DefinitionId,
                Category = (byte)item.Category,
                PrepStage = (byte)item.PrepStage,
                CookingStage = (byte)item.CookingStage,
                PackagingState = (byte)item.PackagingState,
                LocationType = (byte)item.Location.Type,
                HolderId = item.Location.HolderId,
                SlotId = item.Location.SlotIndex,
                WorldX = item.Location.WorldX,
                WorldY = item.Location.WorldY,
                WorldZ = item.Location.WorldZ,
                Quality = 1.0f,
                CookProgress = item.CookProgress,
                BurnProgress = item.BurnProgress,
                LabelAddressId = item.LabelAddressId,
                ReservedByHold = item.ReservedByHold,
                Revision = item.Revision
            };

            if (item.Ingredients != null)
            {
                var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null
                    ? ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.Catalog
                    : null;

                distinctCount = (byte)Math.Min(item.Ingredients.Count, NetItemDto.MaxDistinctIngredients);
                dto.DistinctIngredientCount = distinctCount;

                for (int i = 0; i < item.Ingredients.Count; i++)
                {
                    var entry = item.Ingredients[i];
                    count += (byte)Math.Max(1, entry.Count);

                    if (i < NetItemDto.MaxDistinctIngredients)
                    {
                        dto.SetIngredient(i, new NetIngredientDto
                        {
                            DefinitionId = entry.IngredientDefId,
                            Stage = (byte)entry.Stage,
                            Count = (byte)Math.Clamp(entry.Count, 1, 255)
                        });
                    }

                    if (entry.IngredientDefId == 3) mask |= 1; // Molho
                    else if (entry.IngredientDefId == 4 || entry.IngredientDefId == 5) mask |= 2; // Queijo
                    else if (entry.IngredientDefId == 6 || entry.IngredientDefId == 7) mask |= 4; // Calabresa
                    else if (entry.IngredientDefId == 8 || entry.IngredientDefId == 9) mask |= 8; // Cogumelo
                    else if (catalog != null && catalog.Ingredients.TryGetValue(entry.IngredientDefId, out var ingData))
                    {
                        var cat = ingData.Category;
                        if (cat == ItemCategory.Sauce) mask |= 1;
                        else if (cat == ItemCategory.Cheese) mask |= 2;
                        else if (cat == ItemCategory.Topping) mask |= 4;
                    }
                }
            }

            dto.IngredientMask = mask;
            dto.IngredientCount = count;
            return dto;
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
                LeaseExpiration = tool.LeaseExpiresAt,
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
                SecondaryItemId = op.SecondaryItemId.Value,
                Kind = (byte)op.Kind,
                ToolItemId = op.ToolItemId.Value,
                WorkerId = op.WorkerId,
                InitiatorClientId = op.InitiatorClientId,
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
                SourceType = (byte)order.SourceType,
                DestinationId = order.DestinationId.Value,
                TableId = order.TableId,
                CustomerId = order.CustomerId.Value,
                Lifecycle = (byte)order.Lifecycle,
                DeadlineTime = order.DeadlineTime,
                QuotedReward = order.QuotedPotentialReward,
                FinalEarnedReward = order.FinalEarnedReward,
                TicketArchived = order.TicketArchived,
                ClaimedByClientId = order.ClaimedByClientId,
                TaskStage = (byte)order.TaskStage,
                LineCount = order.Lines.Count,
                Revision = order.Revision
            };

            if (order.Lines.Count > 0)
            {
                dto.Spec_0 = NetPizzaSpecDto.FromDomain(order.Lines[0].Specification);
                dto.RecipeId_0 = order.Lines[0].RecipeId;
                dto.Qty_0 = order.Lines[0].RequestedQuantity;
            }
            if (order.Lines.Count > 1)
            {
                dto.Spec_1 = NetPizzaSpecDto.FromDomain(order.Lines[1].Specification);
                dto.RecipeId_1 = order.Lines[1].RecipeId;
                dto.Qty_1 = order.Lines[1].RequestedQuantity;
            }
            if (order.Lines.Count > 2)
            {
                dto.Spec_2 = NetPizzaSpecDto.FromDomain(order.Lines[2].Specification);
                dto.RecipeId_2 = order.Lines[2].RecipeId;
                dto.Qty_2 = order.Lines[2].RequestedQuantity;
            }
            
            return dto;
        }

        private NetSupplyDto CreateSupplyDto(SupplyOrderState sup)
        {
            int primaryIngId = 0;
            int totalUnits = 0;
            string summary = string.Empty;
            if (sup.Lines != null && sup.Lines.Count > 0)
            {
                primaryIngId = sup.Lines[0].IngredientDefId;
                for (int i = 0; i < sup.Lines.Count; i++)
                {
                    totalUnits += sup.Lines[i].TotalUnits;
                }
                summary = sup.Lines.Count == 1
                    ? $"Ing #{primaryIngId} (+{totalUnits}un)"
                    : $"{sup.Lines.Count} itens (+{totalUnits}un)";
            }

            return new NetSupplyDto
            {
                SupplyOrderId = sup.Id.Value,
                State = (byte)sup.State,
                OrderedTime = sup.OrderedTime,
                ExpectedArrivalTime = sup.ExpectedArrivalTime,
                TotalCost = sup.TotalCost,
                ReceivingSlotIndex = sup.ReceivingSlotIndex,
                PrimaryIngredientDefId = primaryIngId,
                TotalOrderedUnits = totalUnits,
                Summary = TruncateFs64(summary),
                Revision = sup.Revision
            };
        }

        private bool ProjectCustomersDelta(PizzeriaState domainState)
        {
            if (_netState.Customers == null) return false;
            var networkList = _netState.Customers;
            var domainKeys = new HashSet<ulong>();
            foreach (var kvp in domainState.Customers) domainKeys.Add(kvp.Key.Value);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].CustomerId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
            }

            foreach (var kvp in domainState.Customers)
            {
                var cust = kvp.Value;
                float attendNorm = 0f;
                if (cust.AttendedByPlayerId != 0 && domainState.PlayerHolds.TryGetValue(cust.AttendedByPlayerId, out var hold) &&
                    hold.Kind == PlayerHoldKind.AttendCustomer && hold.TargetCustomerId == cust.Id)
                {
                    attendNorm = hold.Progress.Normalized;
                }

                var dto = new NetCustomerDto
                {
                    CustomerId = cust.Id.Value,
                    Phase = (byte)cust.Phase,
                    QueueIndex = cust.QueueIndex,
                    QueueSlotIndex = cust.QueueSlotIndex,
                    AssignedTableId = cust.AssignedTableId,
                    OrderId = cust.OrderId.Value,
                    RequestedSpec = NetPizzaSpecDto.FromDomain(cust.RequestedPizza),
                    PositiveTraitId = cust.PositiveTraitId,
                    NegativeTraitId = cust.NegativeTraitId,
                    VisualColorIndex = cust.VisualColorIndex,
                    QueuePatienceMax = cust.QueuePatienceMax,
                    QueuePatienceRemaining = cust.QueuePatienceRemaining,
                    FoodPatienceMax = cust.FoodPatienceMax,
                    FoodPatienceRemaining = cust.FoodPatienceRemaining,
                    AttendedByPlayerId = cust.AttendedByPlayerId,
                    AttendProgressNormalized = attendNorm,
                    IsMonsterCustomer = cust.IsMonsterCustomer,
                    Revision = cust.Revision
                };

                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].CustomerId == dto.CustomerId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ProjectTablesDelta(PizzeriaState domainState)
        {
            if (_netState.Tables == null) return false;
            var networkList = _netState.Tables;
            var domainKeys = new HashSet<int>();
            foreach (var kvp in domainState.Tables) domainKeys.Add(kvp.Key);

            bool changed = false;
            for (int i = networkList.Count - 1; i >= 0; i--)
            {
                if (!domainKeys.Contains(networkList[i].TableId))
                {
                    networkList.RemoveAt(i);
                    changed = true;
                }
            }

            foreach (var kvp in domainState.Tables)
            {
                var tbl = kvp.Value;
                var dto = new NetTableDto
                {
                    TableId = tbl.TableId,
                    ReservedByCustomerId = tbl.ReservedByCustomerId.Value,
                    ActiveOrderId = tbl.ActiveOrderId.Value,
                    ServedPizzaItemId = tbl.ServedPizzaItemId.Value,
                    Revision = tbl.Revision
                };

                int existingIdx = -1;
                for (int i = 0; i < networkList.Count; i++)
                {
                    if (networkList[i].TableId == dto.TableId) { existingIdx = i; break; }
                }

                if (existingIdx >= 0)
                {
                    if (!networkList[existingIdx].Equals(dto)) { networkList[existingIdx] = dto; changed = true; }
                }
                else
                {
                    networkList.Add(dto);
                    changed = true;
                }
            }
            return changed;
        }

        private NetNightStateDto CreateNightStateDto(PizzeriaState domainState)
        {
            return new NetNightStateDto
            {
                NightNumber = domainState.CurrentNightNumber,
                TargetNightNumber = domainState.TargetNightNumber > 0 ? domainState.TargetNightNumber : domainState.CurrentNightNumber,
                Phase = (byte)domainState.CurrentNightPhase,
                DefeatReason = (byte)domainState.NightDefeatReason,
                RemainingSeconds = domainState.NightRemainingSeconds,
                ElapsedSeconds = domainState.NightElapsedSeconds,
                PrepRemainingSeconds = domainState.NightPrepRemainingSeconds,
                GraceRemainingSeconds = domainState.NightGraceRemainingSeconds,
                UnlimitedShiftTime = domainState.UnlimitedShiftTime,
                IsShiftClockPaused = domainState.IsShiftClockPaused,
                ShiftClockSpeedMultiplier = domainState.ShiftClockSpeedMultiplier,
                IsForcedDebugOutcome = domainState.IsForcedDebugOutcome,
                EarnedRevenue = domainState.NightEarnedRevenue,
                RevenueGoal = domainState.NightRevenueGoal,
                OrdersCompleted = domainState.NightOrdersCompleted,
                OrdersFailed = domainState.NightOrdersFailed,
                LostCustomers = domainState.NightLostCustomers,
                Reputation = domainState.Reputation,
                LastReputationDelta = domainState.LastReputationDelta,
                LastReputationReason = new Unity.Collections.FixedString64Bytes(
                    string.IsNullOrEmpty(domainState.LastReputationReason)
                        ? string.Empty
                        : (domainState.LastReputationReason.Length > 60 ? domainState.LastReputationReason.Substring(0, 60) : domainState.LastReputationReason)),
                FrozenHumanCount = domainState.FrozenHumanCount,
                BaseGoal = domainState.ResolvedBaseGoal,
                HumanScaleFactor = domainState.ResolvedHumanScaleFactor,
                ModifierMultiplier = domainState.ResolvedModifierMultiplier,
                GoalSource = domainState.ResolvedGoalSource,
                NightSupplySpent = domainState.NightSupplySpent,
                NightShopSpent = domainState.NightShopSpent,
                NightWagesSpent = domainState.NightWagesSpent,
                Revision = domainState.Revision
            };
        }

        private NetRunStateDto CreateRunStateDto(PizzeriaState domainState)
        {
            var root = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance;
            var run = root != null ? root.RunProgressionService : null;
            var stats = run != null ? run.RecalculateEffectiveStats() : new Domain.Services.RunEffectiveStats();

            int totalHumans = 1;
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
            {
                totalHumans = Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count;
            }

            string optAId = string.Empty, optATitle = string.Empty, optADesc = string.Empty;
            string optBId = string.Empty, optBTitle = string.Empty, optBDesc = string.Empty;
            int optAVotes = 0, optBVotes = 0;

            if (run != null && run.CurrentVoteOptions != null)
            {
                if (run.CurrentVoteOptions.Count > 0)
                {
                    optAId = run.CurrentVoteOptions[0].Id ?? string.Empty;
                    optATitle = run.CurrentVoteOptions[0].DisplayName ?? optAId;
                    optADesc = RunDescriptionFormatter.FormatModifierDescription(run.CurrentVoteOptions[0]);
                }
                if (run.CurrentVoteOptions.Count > 1)
                {
                    optBId = run.CurrentVoteOptions[1].Id ?? string.Empty;
                    optBTitle = run.CurrentVoteOptions[1].DisplayName ?? optBId;
                    optBDesc = RunDescriptionFormatter.FormatModifierDescription(run.CurrentVoteOptions[1]);
                }
                foreach (var v in run.Votes.Values)
                {
                    if (!string.IsNullOrEmpty(optAId) && v == optAId) optAVotes++;
                    else if (!string.IsNullOrEmpty(optBId) && v == optBId) optBVotes++;
                }
            }

            string winTitle = string.Empty;
            if (run != null && run.IsVoteResolved && !string.IsNullOrEmpty(run.LastResolvedOptionId))
            {
                var def = run.GetModifierDefinition(run.LastResolvedOptionId);
                winTitle = def != null ? def.DisplayName : run.LastResolvedOptionId;
            }

            return new NetRunStateDto
            {
                CurrentRunNight = domainState.CurrentNightNumber,
                TargetNightNumber = domainState.TargetNightNumber > 0 ? domainState.TargetNightNumber : Math.Max(1, domainState.CurrentNightNumber),
                MaxRunNights = run != null ? run.MaxNights : 3,
                IsRunCompleted = run != null && run.IsRunCompleted,
                IsRunVictory = run != null && run.IsRunVictory,
                IsPreparationOpen = domainState.CurrentNightPhase == Domain.Types.NightPhase.PreparingNextNight,
                IsVoteResolved = run != null && run.IsVoteResolved,
                OptionAId = TruncateFs32(optAId),
                OptionATitle = TruncateFs64(optATitle),
                OptionADesc = TruncateFs128(optADesc),
                OptionAVotes = optAVotes,
                OptionBId = TruncateFs32(optBId),
                OptionBTitle = TruncateFs64(optBTitle),
                OptionBDesc = TruncateFs128(optBDesc),
                OptionBVotes = optBVotes,
                WinningOptionTitle = TruncateFs64(winTitle),
                ReadyCount = run != null ? run.ReadyPlayers.Count : 0,
                TotalHumans = totalHumans,
                HasOvenTimer = stats.OvenAutoShutoffEnabled,
                ExtraBackpackCapacity = stats.ExtraBackpackCapacity,
                EmployeeErrorMultiplier = stats.EmployeeErrorMultiplier,
                VampireSpawnMultiplier = stats.VampireSpawnMultiplier,
                DemandMultiplier = stats.DemandMultiplier,
                ActivePresetId = TruncateFs32(domainState.ActivePresetId),
                AllowDebugPanel = domainState.AllowDebugPanel,
                IsDebugTaintedRun = domainState.IsDebugTaintedRun,
                IntermissionStage = run != null ? (byte)run.IntermissionStage : (byte)0,
                RevealStartedAtServerTime = run != null ? run.RevealStartedAtServerTime : 0d,
                CountdownStartsAtServerTime = run != null ? run.CountdownStartsAtServerTime : 0d,
                StartNightAtServerTime = run != null ? run.StartNightAtServerTime : 0d,
                WinningHindranceId = TruncateFs64(run?.LastWinningHindranceId),
                WinningOpportunityId = TruncateFs64(run?.LastWinningOpportunityId),
                Revision = domainState.Revision
            };
        }

        private bool ProjectRunPlayerStatesDelta(PizzeriaState domainState)
        {
            var networkList = _netState.RunPlayerStates;
            var run = Composition.PizzeriaRoot.Instance != null ? Composition.PizzeriaRoot.Instance.RunProgressionService : null;
            var nm = Unity.Netcode.NetworkManager.Singleton;

            if (run == null || nm == null || !nm.IsListening ||
                domainState.CurrentNightPhase != Domain.Types.NightPhase.PreparingNextNight)
            {
                if (networkList.Count > 0)
                {
                    networkList.Clear();
                    return true;
                }
                return false;
            }

            var desired = new List<NetPlayerIntermissionDto>(nm.ConnectedClientsList.Count);
            var readySet = run.ReadyPlayers;
            for (int i = 0; i < nm.ConnectedClientsList.Count; i++)
            {
                var client = nm.ConnectedClientsList[i];
                ulong clientId = client.ClientId;
                string playerName = $"Jogador {clientId}";
                if (client.PlayerObject != null &&
                    client.PlayerObject.TryGetComponent<PlayerState>(out var ps))
                {
                    string rawName = ps.PlayerName.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        playerName = rawName;
                    }
                }

                bool hasH = run.TryGetPlayerVote(clientId, Domain.Services.RunModifierKind.Hindrance, out var hId);
                bool hasO = run.TryGetPlayerVote(clientId, Domain.Services.RunModifierKind.Opportunity, out var oId);
                bool isReady = readySet != null && System.Linq.Enumerable.Contains(readySet, clientId);

                desired.Add(new NetPlayerIntermissionDto
                {
                    ClientId = clientId,
                    PlayerName = TruncateFs32(playerName),
                    HasVotedHindrance = hasH,
                    HasVotedOpportunity = hasO,
                    IsReady = isReady,
                    VotedHindranceId = TruncateFs64(hId),
                    VotedOpportunityId = TruncateFs64(oId)
                });
            }

            if (networkList.Count == desired.Count)
            {
                bool identical = true;
                for (int i = 0; i < desired.Count; i++)
                {
                    if (!networkList[i].Equals(desired[i]))
                    {
                        identical = false;
                        break;
                    }
                }
                if (identical) return false;
            }

            networkList.Clear();
            for (int i = 0; i < desired.Count; i++)
            {
                networkList.Add(desired[i]);
            }
            return true;
        }

        private bool ProjectRunVoteOptionsDelta(PizzeriaState domainState)
        {
            var networkList = _netState.RunVoteOptions;
            var root = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance;
            var run = root != null ? root.RunProgressionService : null;
            var desired = new List<NetRunVoteOptionDto>();

            if (run != null && domainState.CurrentNightPhase == NightPhase.PreparingNextNight)
            {
                if (run.CurrentVoteStages != null && run.CurrentVoteStages.Count > 0)
                {
                    for (int s = 0; s < run.CurrentVoteStages.Count; s++)
                    {
                        var stage = run.CurrentVoteStages[s];
                        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                        foreach (var v in stage.Votes.Values)
                        {
                            counts[v] = (counts.TryGetValue(v, out var c) ? c : 0) + 1;
                        }

                        for (int o = 0; o < stage.Options.Count; o++)
                        {
                            var opt = stage.Options[o];
                            desired.Add(new NetRunVoteOptionDto
                            {
                                StageIndex = (byte)stage.StageIndex,
                                OptionIndex = (byte)o,
                                Category = (byte)stage.Category,
                                IsMandatory = stage.IsMandatory,
                                IsResolvedWinner = !string.IsNullOrEmpty(stage.ResolvedOptionId) && string.Equals(stage.ResolvedOptionId, opt.Id, StringComparison.Ordinal),
                                VoteCount = counts.TryGetValue(opt.Id, out var vc) ? vc : 0,
                                OptionId = TruncateFs64(opt.Id),
                                Title = TruncateFs64(opt.DisplayName),
                                Description = TruncateFs128(RunDescriptionFormatter.FormatModifierDescription(opt)),
                                DurationTag = TruncateFs32(RunDescriptionFormatter.FormatDurationTag(opt.Duration))
                            });
                        }
                    }
                }
                else if (run.CurrentVoteOptions != null && run.CurrentVoteOptions.Count > 0)
                {
                    for (int o = 0; o < run.CurrentVoteOptions.Count; o++)
                    {
                        var opt = run.CurrentVoteOptions[o];
                        int vc = 0;
                        foreach (var v in run.Votes.Values)
                        {
                            if (v == opt.Id) vc++;
                        }
                        desired.Add(new NetRunVoteOptionDto
                        {
                            StageIndex = 0,
                            OptionIndex = (byte)o,
                            Category = (byte)opt.Kind,
                            IsMandatory = true,
                            IsResolvedWinner = run.IsVoteResolved && run.LastResolvedOptionId == opt.Id,
                            VoteCount = vc,
                            OptionId = TruncateFs64(opt.Id),
                            Title = TruncateFs64(opt.DisplayName),
                            Description = TruncateFs128(RunDescriptionFormatter.FormatModifierDescription(opt)),
                            DurationTag = TruncateFs32(RunDescriptionFormatter.FormatDurationTag(opt.Duration))
                        });
                    }
                }
            }

            return SyncNetworkListExact(networkList, desired);
        }

        private bool ProjectRunShopOffersDelta(PizzeriaState domainState)
        {
            var networkList = _netState.RunShopOffers;
            var root = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance;
            var run = root != null ? root.RunProgressionService : null;
            var snap = run != null ? run.BalancingSnapshot : null;
            var employees = root != null ? root.EmployeeTaskService : null;
            var desired = new List<NetRunShopOfferDto>();

            if (run != null && snap != null)
            {
                for (int i = 0; i < snap.ShopOffers.Count; i++)
                {
                    var offer = snap.ShopOffers[i];
                    if (offer == null || !offer.Enabled) continue;

                    bool canBuy = run.TryEvaluateShopOffer(domainState, offer, employees, out int effCost, out string lockReason);
                    int curLevel = 0;
                    int maxLevel = offer.MaxPurchasesPerRun;
                    if (offer.BenefitKind == ShopBenefitKind.GrantModifier)
                    {
                        var mod = snap.FindModifierById(offer.ModifierId);
                        curLevel = run.GetModifierStacks(offer.ModifierId);
                        if (mod != null) maxLevel = mod.MaxStacks;
                    }
                    else
                    {
                        curLevel = run.GetShopOfferRunPurchases(offer.OfferId);
                    }

                    string desc = RunDescriptionFormatter.FormatShopOfferDescription(offer, snap, id => ResolveIngredientName(root, id), curLevel);
                    desired.Add(new NetRunShopOfferDto
                    {
                        OfferId = TruncateFs64(offer.OfferId),
                        LegacyByteId = offer.LegacyByteId,
                        BenefitKind = (byte)offer.BenefitKind,
                        DisplayName = TruncateFs64(offer.DisplayName),
                        Description = TruncateFs128(desc),
                        EffectiveCost = effCost,
                        CurrentLevel = curLevel,
                        MaxLevel = maxLevel,
                        CanPurchase = canBuy,
                        LockReason = TruncateFs64(lockReason)
                    });
                }
            }

            return SyncNetworkListExact(networkList, desired);
        }

        private static string ResolveIngredientName(ScaryParty.Pizzeria.Composition.PizzeriaRoot root, int ingredientId)
        {
            var ings = root != null && root.Config != null ? root.Config.ingredients : null;
            if (ings != null)
            {
                for (int i = 0; i < ings.Count; i++)
                {
                    if (ings[i] != null && ings[i].id == ingredientId && !string.IsNullOrEmpty(ings[i].ingredientName))
                        return ings[i].ingredientName;
                }
            }
            return RunDescriptionFormatter.DefaultIngredientName(ingredientId);
        }

        private static bool SyncNetworkListExact<T>(Unity.Netcode.NetworkList<T> networkList, List<T> desired)
            where T : unmanaged, IEquatable<T>
        {
            bool changed = false;
            if (networkList.Count != desired.Count)
            {
                networkList.Clear();
                for (int i = 0; i < desired.Count; i++)
                {
                    networkList.Add(desired[i]);
                }
                return true;
            }

            for (int i = 0; i < desired.Count; i++)
            {
                if (!networkList[i].Equals(desired[i]))
                {
                    networkList[i] = desired[i];
                    changed = true;
                }
            }
            return changed;
        }

        private static Unity.Collections.FixedString32Bytes TruncateFs32(string s)
            => new Unity.Collections.FixedString32Bytes(TruncateUtf8(s, 29));

        private static Unity.Collections.FixedString64Bytes TruncateFs64(string s)
            => new Unity.Collections.FixedString64Bytes(TruncateUtf8(s, 61));

        private static Unity.Collections.FixedString128Bytes TruncateFs128(string s)
            => new Unity.Collections.FixedString128Bytes(TruncateUtf8(s, 125));

        private static string TruncateUtf8(string s, int maxBytes)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            int byteCount = System.Text.Encoding.UTF8.GetByteCount(s);
            if (byteCount <= maxBytes) return s;

            int len = Math.Min(s.Length, maxBytes);
            while (len > 0 && System.Text.Encoding.UTF8.GetByteCount(s, 0, len) > maxBytes)
            {
                len--;
            }
            return s.Substring(0, len);
        }
    }
}

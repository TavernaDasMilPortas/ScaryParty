using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    /// <summary>
    /// Serviço autoritativo de P2 que mantém identidade exclusiva de unidade de pedido (GenerationId + OrderId + LineIndex + UnitIndex),
    /// vínculo 1-para-1 com ItemId físico, reconciliação por entrega/descarte/queima/cancelamento e reserva de slots de bancada.
    /// </summary>
    public static class OrderUnitFulfillmentService
    {
        private static readonly PizzaSpecificationEvaluator DefaultEvaluator = new PizzaSpecificationEvaluator();

        public static void EnsureUnitsForOrder(PizzeriaState state, OrderState order)
        {
            if (state == null || order == null || order.Lines == null || state.GenerationId == 0) return;

            bool requiresBox = !order.IsDineIn;
            for (int lineIdx = 0; lineIdx < order.Lines.Count; lineIdx++)
            {
                var line = order.Lines[lineIdx];
                if (line == null) continue;
                int qty = Math.Max(1, line.RequestedQuantity);
                for (int unitIdx = 0; unitIdx < qty; unitIdx++)
                {
                    var key = new OrderUnitKey(state.GenerationId, order.Id, lineIdx, unitIdx);
                    if (!state.OrderUnits.ContainsKey(key))
                    {
                        state.OrderUnits[key] = new OrderUnitState(key, line.Specification, requiresBox, order.DestinationId);
                    }
                }
            }
        }

        public static bool IsItemEligibleForUnit(ItemState item, OrderUnitState unit, PizzaSpecificationEvaluator evaluator = null)
        {
            if (item == null || unit == null) return false;
            if (item.Location.Type == LocationType.Destroyed) return false;
            if (!item.IsPizzaLike && item.Category != ItemCategory.Box) return false;
            if (item.CookingStage != CookingStage.Baked) return false;
            if (unit.RequiresBox && !item.IsBoxed) return false;
            if (!unit.RequiresBox && item.IsBoxed) return false;

            var eval = (evaluator ?? DefaultEvaluator).Evaluate(item, unit.Spec);
            return eval.IsExactMatch;
        }

        public static bool IsItemBoundToActiveUnit(PizzeriaState state, ItemId itemId, out OrderUnitKey unitKey)
        {
            unitKey = default;
            if (state == null || !itemId.IsValid) return false;
            if (!state.ItemToOrderUnit.TryGetValue(itemId, out unitKey)) return false;
            if (unitKey.GenerationId != state.GenerationId) return false;
            if (!state.OrderUnits.TryGetValue(unitKey, out var unit)) return false;
            return unit.Status == OrderUnitStatus.BoundToReadyItem;
        }

        public static bool TryBindSpecificPizzaToUnit(
            PizzeriaState state,
            OrderUnitKey unitKey,
            ItemId itemId,
            EmployeeTaskService taskService = null,
            PizzaSpecificationEvaluator evaluator = null)
        {
            if (state == null || !unitKey.IsValid || !itemId.IsValid) return false;
            if (unitKey.GenerationId != state.GenerationId) return false;
            if (!state.OrderUnits.TryGetValue(unitKey, out var unit)) return false;
            if (unit.Status != OrderUnitStatus.Pending && unit.Status != OrderUnitStatus.InProgress) return false;
            if (!state.Orders.TryGetValue(unitKey.OrderId, out var order) || order.Lifecycle != OrderLifecycle.Accepted) return false;
            if (!state.Items.TryGetValue(itemId, out var item)) return false;
            if (!IsItemEligibleForUnit(item, unit, evaluator)) return false;

            // Garante exclusividade 1-para-1: um ItemId jamais pertence a duas unidades ativas
            if (state.ItemToOrderUnit.TryGetValue(itemId, out var existingKey) && !existingKey.Equals(unitKey))
            {
                if (existingKey.GenerationId == state.GenerationId &&
                    state.OrderUnits.TryGetValue(existingKey, out var existingUnit) &&
                    existingUnit.Status == OrderUnitStatus.BoundToReadyItem &&
                    existingUnit.BoundItemId.Equals(itemId))
                {
                    return false;
                }
            }

            state.ItemToOrderUnit[itemId] = unitKey;
            unit.BoundItemId = itemId;
            unit.Status = OrderUnitStatus.BoundToReadyItem;
            unit.AssignedEmployeeId = -1;
            unit.Revision++;
            state.IncrementRevision();

            if (taskService != null)
            {
                ReconcileOrderTaskProgress(state, order, taskService);
            }
            return true;
        }

        public static void BindReadyPizzasAndReconcileOrder(
            PizzeriaState state,
            OrderState order,
            EmployeeTaskService taskService = null,
            PizzaSpecificationEvaluator evaluator = null)
        {
            if (state == null || order == null) return;
            EnsureUnitsForOrder(state, order);
            ValidateBoundUnitsForOrder(state, order, evaluator);

            if (order.Lifecycle != OrderLifecycle.Accepted)
            {
                if (taskService != null)
                {
                    ReconcileOrderTaskProgress(state, order, taskService);
                }
                return;
            }

            // Coleta pizzas elegíveis e ainda não vinculadas a nenhuma unidade ativa desta RUN
            var unboundItems = new List<ItemState>();
            foreach (var kvp in state.Items)
            {
                var item = kvp.Value;
                if (item == null || item.Location.Type == LocationType.Destroyed) continue;
                if (!item.IsPizzaLike && item.Category != ItemCategory.Box) continue;
                if (item.CookingStage != CookingStage.Baked) continue;
                if (IsItemBoundToActiveUnit(state, item.Id, out _)) continue;
                unboundItems.Add(item);
            }

            // Prioridade determinística: itens apoiados em bancadas/pratos antes de itens em mãos/mochila, depois por ItemId
            unboundItems.Sort((a, b) =>
            {
                int pa = GetBindLocationPriority(a.Location.Type);
                int pb = GetBindLocationPriority(b.Location.Type);
                int cmp = pa.CompareTo(pb);
                return cmp != 0 ? cmp : a.Id.Value.CompareTo(b.Id.Value);
            });

            var orderUnits = CollectSortedUnitsForOrder(state, order.Id);
            var usedItems = new HashSet<ItemId>();

            for (int i = 0; i < orderUnits.Count; i++)
            {
                var unit = orderUnits[i];
                if (unit.Status != OrderUnitStatus.Pending &&
                    !(unit.Status == OrderUnitStatus.InProgress && unit.AssignedEmployeeId <= 0))
                {
                    continue;
                }

                for (int j = 0; j < unboundItems.Count; j++)
                {
                    var candidate = unboundItems[j];
                    if (usedItems.Contains(candidate.Id)) continue;
                    if (!IsItemEligibleForUnit(candidate, unit, evaluator)) continue;

                    if (TryBindSpecificPizzaToUnit(state, unit.Key, candidate.Id, taskService: null, evaluator: evaluator))
                    {
                        usedItems.Add(candidate.Id);
                        break;
                    }
                }
            }

            ReconcileOrderTaskProgress(state, order, taskService);
        }

        public static bool TryClaimNextPendingUnitForEmployee(
            PizzeriaState state,
            OrderState order,
            int employeeId,
            EmployeeTaskService taskService,
            double now,
            out OrderUnitState claimedUnit)
        {
            claimedUnit = null;
            if (state == null || order == null || order.Lifecycle != OrderLifecycle.Accepted || employeeId <= 0)
                return false;

            BindReadyPizzasAndReconcileOrder(state, order, taskService);
            ReleaseDeadEmployeeAssignments(state, taskService);

            var units = CollectSortedUnitsForOrder(state, order.Id);

            // Se o funcionário já detém uma unidade em progresso para este pedido, retoma a mesma
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Status == OrderUnitStatus.InProgress && u.AssignedEmployeeId == employeeId)
                {
                    u.ReservedAtSimTime = now;
                    claimedUnit = u;
                    return true;
                }
            }

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Status == OrderUnitStatus.Pending)
                {
                    u.Status = OrderUnitStatus.InProgress;
                    u.AssignedEmployeeId = employeeId;
                    u.ReservedAtSimTime = now;
                    u.Revision++;
                    state.IncrementRevision();
                    claimedUnit = u;
                    return true;
                }
            }

            return false;
        }

        public static void ReconcileOrderTaskProgress(PizzeriaState state, OrderState order, EmployeeTaskService taskService)
        {
            if (state == null || order == null || taskService == null) return;
            EnsureUnitsForOrder(state, order);

            var units = CollectSortedUnitsForOrder(state, order.Id);
            int totalRequired = units.Count;
            int totalCompleted = 0;
            int firstIncompleteLine = order.Lines != null ? order.Lines.Count : 0;
            int unitsCompletedInCurrentLine = 0;

            if (order.Lines != null)
            {
                bool foundIncomplete = false;
                for (int lineIdx = 0; lineIdx < order.Lines.Count; lineIdx++)
                {
                    int lineDone = 0;
                    int lineTotal = 0;
                    for (int uIdx = 0; uIdx < units.Count; uIdx++)
                    {
                        var u = units[uIdx];
                        if (u.Key.LineIndex != lineIdx) continue;
                        lineTotal++;
                        bool isDone = u.Status == OrderUnitStatus.BoundToReadyItem || u.Status == OrderUnitStatus.Delivered;
                        if (isDone)
                        {
                            lineDone++;
                            totalCompleted++;
                        }
                    }

                    if (!foundIncomplete && lineDone < lineTotal)
                    {
                        foundIncomplete = true;
                        firstIncompleteLine = lineIdx;
                        unitsCompletedInCurrentLine = lineDone;
                    }
                }
            }

            SharedEmployeeTask task = FindTaskForOrderAnyState(taskService, order.Id, EmployeeRole.Pizzaiolo);
            if (task == null) return;

            task.TotalUnitsRequired = totalRequired;
            task.TotalUnitsCompleted = totalCompleted;
            task.CurrentLineIndex = Math.Min(firstIncompleteLine, order.Lines != null ? order.Lines.Count : 0);
            task.UnitsCompletedInLine = unitsCompletedInCurrentLine;

            if (order.Lifecycle == OrderLifecycle.Cancelled || order.Lifecycle == OrderLifecycle.Expired)
            {
                task.State = EmployeeTaskState.Cancelled;
                return;
            }

            if (order.Lifecycle == OrderLifecycle.Completed)
            {
                task.State = EmployeeTaskState.Completed;
                return;
            }

            if (totalRequired > 0 && totalCompleted >= totalRequired)
            {
                if (!task.IsReservedByHuman && task.ReservedByActorId > 0 &&
                    taskService.Employees.TryGetValue((int)task.ReservedByActorId, out var emp) &&
                    emp.AssignedTaskId == task.TaskId)
                {
                    emp.AssignedTaskId = -1;
                }
                task.ReservedByActorId = 0;
                task.IsReservedByHuman = false;
                task.BlockReason = EmployeeBlockReason.None;
                task.State = EmployeeTaskState.Completed;
            }
            else if (task.State == EmployeeTaskState.Completed)
            {
                // Reaberta após consumo/descarte/entrega cruzada da pizza antes vinculada
                task.State = EmployeeTaskState.Available;
                task.ReservedByActorId = 0;
                task.IsReservedByHuman = false;
                task.BlockReason = EmployeeBlockReason.None;
            }
        }

        public static void SyncAllActiveOrderTasks(PizzeriaState state, EmployeeTaskService taskService)
        {
            if (state == null || taskService == null) return;
            foreach (var kvp in state.Orders)
            {
                if (kvp.Value != null && kvp.Value.Lifecycle == OrderLifecycle.Accepted)
                {
                    BindReadyPizzasAndReconcileOrder(state, kvp.Value, taskService);
                }
            }
        }

        public static void ReleaseEmployeeAssignedUnits(PizzeriaState state, int employeeId)
        {
            if (state == null || employeeId <= 0) return;
            foreach (var unit in state.OrderUnits.Values)
            {
                if (unit.Key.GenerationId == state.GenerationId &&
                    unit.Status == OrderUnitStatus.InProgress &&
                    unit.AssignedEmployeeId == employeeId)
                {
                    unit.Status = OrderUnitStatus.Pending;
                    unit.AssignedEmployeeId = -1;
                    unit.Revision++;
                }
            }
        }

        public static void OnItemDeliveredToOrder(
            PizzeriaState state,
            OrderId targetOrderId,
            IReadOnlyList<ItemId> deliveredItemIds,
            EmployeeTaskService taskService = null)
        {
            if (state == null || deliveredItemIds == null || deliveredItemIds.Count == 0) return;

            var touchedOrders = new HashSet<OrderId>();
            for (int i = 0; i < deliveredItemIds.Count; i++)
            {
                var itemId = deliveredItemIds[i];
                if (!itemId.IsValid) continue;

                if (state.ItemToOrderUnit.TryGetValue(itemId, out var boundKey))
                {
                    state.ItemToOrderUnit.Remove(itemId);
                    if (state.OrderUnits.TryGetValue(boundKey, out var boundUnit))
                    {
                        if (targetOrderId.IsValid && boundKey.OrderId.Equals(targetOrderId))
                        {
                            boundUnit.Status = OrderUnitStatus.Delivered;
                            boundUnit.AssignedEmployeeId = -1;
                            boundUnit.Revision++;
                        }
                        else
                        {
                            // Humano entregou esta pizza em outro pedido: reabre a unidade original para refazer.
                            boundUnit.BoundItemId = ItemId.None;
                            boundUnit.Status = OrderUnitStatus.Pending;
                            boundUnit.AssignedEmployeeId = -1;
                            boundUnit.Revision++;
                            touchedOrders.Add(boundKey.OrderId);
                        }
                    }
                }
            }

            if (targetOrderId.IsValid)
            {
                foreach (var unit in state.OrderUnits.Values)
                {
                    if (unit.Key.GenerationId == state.GenerationId &&
                        unit.Key.OrderId.Equals(targetOrderId) &&
                        unit.Status != OrderUnitStatus.Cancelled)
                    {
                        if (unit.BoundItemId.IsValid)
                        {
                            state.ItemToOrderUnit.Remove(unit.BoundItemId);
                        }
                        unit.Status = OrderUnitStatus.Delivered;
                        unit.AssignedEmployeeId = -1;
                        unit.Revision++;
                    }
                }
            }

            if (taskService != null)
            {
                foreach (var oid in touchedOrders)
                {
                    if (state.Orders.TryGetValue(oid, out var affectedOrder))
                    {
                        ReconcileOrderTaskProgress(state, affectedOrder, taskService);
                    }
                }
                if (targetOrderId.IsValid && state.Orders.TryGetValue(targetOrderId, out var deliveredOrder))
                {
                    ReconcileOrderTaskProgress(state, deliveredOrder, taskService);
                }
            }
            state.IncrementRevision();
        }

        public static void OnItemDestroyedOrBurned(
            PizzeriaState state,
            ItemId itemId,
            EmployeeTaskService taskService = null)
        {
            if (state == null || !itemId.IsValid) return;
            if (!state.ItemToOrderUnit.TryGetValue(itemId, out var boundKey)) return;

            state.ItemToOrderUnit.Remove(itemId);
            if (state.OrderUnits.TryGetValue(boundKey, out var unit) &&
                unit.Status == OrderUnitStatus.BoundToReadyItem)
            {
                unit.BoundItemId = ItemId.None;
                unit.Status = OrderUnitStatus.Pending;
                unit.AssignedEmployeeId = -1;
                unit.Revision++;
                state.IncrementRevision();

                if (taskService != null &&
                    state.Orders.TryGetValue(boundKey.OrderId, out var order) &&
                    order.Lifecycle == OrderLifecycle.Accepted)
                {
                    ReconcileOrderTaskProgress(state, order, taskService);
                }
            }
        }

        public static void OnOrderCancelledOrExpired(
            PizzeriaState state,
            OrderId orderId,
            EmployeeTaskService taskService = null)
        {
            if (state == null || !orderId.IsValid) return;

            foreach (var unit in state.OrderUnits.Values)
            {
                if (unit.Key.GenerationId == state.GenerationId && unit.Key.OrderId.Equals(orderId))
                {
                    if (unit.BoundItemId.IsValid &&
                        state.ItemToOrderUnit.TryGetValue(unit.BoundItemId, out var mapped) &&
                        mapped.Equals(unit.Key))
                    {
                        // Libera o item físico sem destruí-lo (outra comanda compatível poderá assumi-lo)
                        state.ItemToOrderUnit.Remove(unit.BoundItemId);
                    }
                    unit.BoundItemId = ItemId.None;
                    unit.AssignedEmployeeId = -1;
                    unit.Status = OrderUnitStatus.Cancelled;
                    unit.Revision++;
                }
            }

            var resKeys = new List<(int, int)>();
            foreach (var kvp in state.StationSlotReservations)
            {
                if (kvp.Value != null &&
                    kvp.Value.UnitKey.GenerationId == state.GenerationId &&
                    kvp.Value.UnitKey.OrderId.Equals(orderId))
                {
                    resKeys.Add(kvp.Key);
                }
            }
            for (int i = 0; i < resKeys.Count; i++)
            {
                state.StationSlotReservations.Remove(resKeys[i]);
            }

            if (taskService != null && state.Orders.TryGetValue(orderId, out var order))
            {
                ReconcileOrderTaskProgress(state, order, taskService);
            }
        }

        public static bool TryReserveSlot(
            PizzeriaState state,
            int stationId,
            int slotIndex,
            ulong actorKey,
            int employeeId,
            OrderUnitKey unitKey,
            double now,
            double leaseSeconds = 12.0)
        {
            if (state == null || stationId <= 0 || slotIndex < 0) return false;
            PruneExpiredReservations(state, now);

            var k = (stationId, slotIndex);
            if (state.StationSlotReservations.TryGetValue(k, out var existing))
            {
                if (existing.ActorKey != actorKey && now < existing.ExpiresAtSimTime)
                    return false;
            }

            state.StationSlotReservations[k] = new StationSlotReservation(
                stationId, slotIndex, actorKey, employeeId, unitKey, now + Math.Max(1.0, leaseSeconds));
            return true;
        }

        public static bool IsSlotFreeForActor(PizzeriaState state, int stationId, int slotIndex, ulong actorKey, double now)
        {
            if (state == null || stationId <= 0 || slotIndex < 0) return false;
            var loc = LocationRef.InStation(stationId, slotIndex);
            if (TransferService.IsLocationOccupied(state, loc)) return false;

            var slotKey = new StationSlotId(stationId, slotIndex);
            if (state.ActiveOperations.TryGetValue(slotKey, out var op) &&
                (op.Phase == OperationPhase.Running || op.Phase == OperationPhase.Paused) &&
                op.WorkerId != actorKey)
            {
                return false;
            }

            if (state.StationSlotReservations.TryGetValue((stationId, slotIndex), out var res) &&
                res != null &&
                res.ActorKey != actorKey &&
                now < res.ExpiresAtSimTime)
            {
                return false;
            }

            return true;
        }

        public static void ReleaseActorSlotReservations(PizzeriaState state, ulong actorKey)
        {
            if (state == null) return;
            var toRemove = new List<(int, int)>();
            foreach (var kvp in state.StationSlotReservations)
            {
                if (kvp.Value == null || kvp.Value.ActorKey == actorKey)
                    toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                state.StationSlotReservations.Remove(toRemove[i]);
            }
        }

        public static void PruneExpiredReservations(PizzeriaState state, double now)
        {
            if (state == null) return;
            var toRemove = new List<(int, int)>();
            foreach (var kvp in state.StationSlotReservations)
            {
                if (kvp.Value == null || now >= kvp.Value.ExpiresAtSimTime || kvp.Value.UnitKey.GenerationId != state.GenerationId)
                    toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                state.StationSlotReservations.Remove(toRemove[i]);
            }
        }

        public static void PruneInvalidatedGenerations(PizzeriaState state)
        {
            if (state == null) return;

            var staleUnits = new List<OrderUnitKey>();
            foreach (var kvp in state.OrderUnits)
            {
                if (kvp.Key.GenerationId != state.GenerationId)
                    staleUnits.Add(kvp.Key);
            }
            for (int i = 0; i < staleUnits.Count; i++)
            {
                state.OrderUnits.Remove(staleUnits[i]);
            }

            var staleItems = new List<ItemId>();
            foreach (var kvp in state.ItemToOrderUnit)
            {
                if (kvp.Value.GenerationId != state.GenerationId || !state.OrderUnits.ContainsKey(kvp.Value))
                    staleItems.Add(kvp.Key);
            }
            for (int i = 0; i < staleItems.Count; i++)
            {
                state.ItemToOrderUnit.Remove(staleItems[i]);
            }
        }

        public static int CountUnitsWithStatus(PizzeriaState state, OrderId orderId, OrderUnitStatus status)
        {
            if (state == null || !orderId.IsValid) return 0;
            int count = 0;
            foreach (var u in state.OrderUnits.Values)
            {
                if (u.Key.GenerationId == state.GenerationId && u.Key.OrderId.Equals(orderId) && u.Status == status)
                    count++;
            }
            return count;
        }

        private static void ValidateBoundUnitsForOrder(
            PizzeriaState state,
            OrderState order,
            PizzaSpecificationEvaluator evaluator)
        {
            foreach (var unit in state.OrderUnits.Values)
            {
                if (unit.Key.GenerationId != state.GenerationId || !unit.Key.OrderId.Equals(order.Id))
                    continue;
                if (unit.Status != OrderUnitStatus.BoundToReadyItem)
                    continue;

                bool stillValid = unit.BoundItemId.IsValid &&
                                  state.Items.TryGetValue(unit.BoundItemId, out var item) &&
                                  IsItemEligibleForUnit(item, unit, evaluator) &&
                                  state.ItemToOrderUnit.TryGetValue(unit.BoundItemId, out var mapped) &&
                                  mapped.Equals(unit.Key);

                if (!stillValid)
                {
                    if (unit.BoundItemId.IsValid &&
                        state.ItemToOrderUnit.TryGetValue(unit.BoundItemId, out var oldMapped) &&
                        oldMapped.Equals(unit.Key))
                    {
                        state.ItemToOrderUnit.Remove(unit.BoundItemId);
                    }
                    unit.BoundItemId = ItemId.None;
                    unit.AssignedEmployeeId = -1;
                    unit.Status = OrderUnitStatus.Pending;
                    unit.Revision++;
                    state.IncrementRevision();
                }
            }
        }

        private static void ReleaseDeadEmployeeAssignments(PizzeriaState state, EmployeeTaskService taskService)
        {
            if (state == null || taskService == null) return;
            foreach (var unit in state.OrderUnits.Values)
            {
                if (unit.Key.GenerationId != state.GenerationId || unit.Status != OrderUnitStatus.InProgress) continue;
                if (unit.AssignedEmployeeId <= 0 ||
                    !taskService.Employees.TryGetValue(unit.AssignedEmployeeId, out var emp) ||
                    !emp.IsAlive)
                {
                    unit.AssignedEmployeeId = -1;
                    unit.Status = OrderUnitStatus.Pending;
                    unit.Revision++;
                }
            }
        }

        private static List<OrderUnitState> CollectSortedUnitsForOrder(PizzeriaState state, OrderId orderId)
        {
            var list = new List<OrderUnitState>();
            foreach (var kvp in state.OrderUnits)
            {
                if (kvp.Key.GenerationId == state.GenerationId && kvp.Key.OrderId.Equals(orderId))
                    list.Add(kvp.Value);
            }
            list.Sort((a, b) =>
            {
                int cmp = a.Key.LineIndex.CompareTo(b.Key.LineIndex);
                return cmp != 0 ? cmp : a.Key.UnitIndex.CompareTo(b.Key.UnitIndex);
            });
            return list;
        }

        private static SharedEmployeeTask FindTaskForOrderAnyState(EmployeeTaskService taskService, OrderId orderId, EmployeeRole role)
        {
            if (taskService == null) return null;
            foreach (var t in taskService.Tasks.Values)
            {
                if (t.TargetOrderId.Equals(orderId) && t.RequiredRole == role && t.State != EmployeeTaskState.Cancelled)
                    return t;
            }
            return null;
        }

        private static int GetBindLocationPriority(LocationType type)
        {
            return type switch
            {
                LocationType.StationSlot => 0,
                LocationType.Stack => 1,
                LocationType.Backpack => 2,
                LocationType.Hand => 3,
                LocationType.World => 4,
                _ => 5
            };
        }
    }
}

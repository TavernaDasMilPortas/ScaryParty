using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class PlayerHoldService
    {
        public CommandResult BeginDiscard(PizzeriaState state, ulong playerId, ItemId itemId, float duration, float now, float leaseDuration)
        {
            if (state.PlayerHolds.TryGetValue(playerId, out var existing))
                return CommandResult.Fail(CommandError.Busy, "Jogador já está realizando uma ação.");

            if (!state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (item.Location.Type != LocationType.Hand || (ulong)item.Location.HolderId != playerId)
                return CommandResult.Fail(CommandError.NotOwner, "Você precisa segurar o item para descartá-lo.");

            if (item.ReservedByHold != 0)
                return CommandResult.Fail(CommandError.Blocked, "Item reservado.");

            item.ReservedByHold = playerId;
            item.Revision++;

            var hold = new PlayerHold(playerId, PlayerHoldKind.Discard, itemId, OrderId.None, duration, now, leaseDuration);
            state.PlayerHolds[playerId] = hold;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public CommandResult BeginArchiveTicket(PizzeriaState state, ulong playerId, OrderId orderId, float duration, float now, float leaseDuration)
        {
            if (state.PlayerHolds.TryGetValue(playerId, out var existing))
                return CommandResult.Fail(CommandError.Busy, "Jogador já está realizando uma ação.");

            if (!state.Orders.TryGetValue(orderId, out var order))
                return CommandResult.Fail(CommandError.ItemNotFound, "Pedido não encontrado.");

            var hold = new PlayerHold(playerId, PlayerHoldKind.ArchiveTicket, ItemId.None, orderId, duration, now, leaseDuration);
            state.PlayerHolds[playerId] = hold;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public CommandResult BeginAttendCustomer(
            PizzeriaState state,
            ulong playerId,
            CustomerId customerId,
            float duration,
            float now,
            float leaseDuration,
            TableService tableService = null,
            int maxActiveOrders = 6)
        {
            if (state.PlayerHolds.TryGetValue(playerId, out var existing))
                return CommandResult.Fail(CommandError.Busy, "Jogador já está realizando uma ação.");

            if (!state.Customers.TryGetValue(customerId, out var customer))
                return CommandResult.Fail(CommandError.ItemNotFound, "Cliente não encontrado.");

            if (!customer.IsInQueue || customer.QueueSlotIndex != 0)
                return CommandResult.Fail(CommandError.InvalidOperation, "Apenas o primeiro cliente da fila pode ser atendido.");

            if (customer.AttendedByPlayerId != 0 && customer.AttendedByPlayerId != playerId)
                return CommandResult.Fail(CommandError.Blocked, "Outro jogador já está atendendo este cliente.");

            // Verificar se há mesa livre ANTES de aceitar o atendimento
            if (tableService != null)
            {
                tableService.EnsureDefaultTablesIfEmpty(state, 4);
                if (!tableService.HasFreeTable(state))
                {
                    return CommandResult.Fail(CommandError.SlotFull, "Não há mesa livre! Aguarde uma mesa desocupar antes de atender.");
                }
            }

            // Verificar limite de comandas ativas no quadro
            int activeOrders = 0;
            foreach (var ord in state.Orders.Values)
            {
                if (ord.Lifecycle == OrderLifecycle.Active && !ord.IsArchivedFromBoard)
                    activeOrders++;
            }
            if (activeOrders >= maxActiveOrders)
            {
                return CommandResult.Fail(CommandError.SlotFull, "O quadro de comandas está cheio!");
            }

            customer.AttendedByPlayerId = playerId;
            customer.Phase = CustomerPhase.BeingAttended;
            customer.Revision++;

            var hold = new PlayerHold(playerId, PlayerHoldKind.AttendCustomer, ItemId.None, OrderId.None, duration, now, leaseDuration, customerId);
            state.PlayerHolds[playerId] = hold;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public CommandResult Heartbeat(PizzeriaState state, ulong playerId, float now, float leaseDuration)
        {
            if (!state.PlayerHolds.TryGetValue(playerId, out var hold))
                return CommandResult.Fail(CommandError.InvalidOperation, "Nenhuma ação em andamento.");

            if (hold.Phase != OperationPhase.Running)
                return CommandResult.Fail(CommandError.InvalidOperation, "Ação não está rodando.");

            hold.Progress.ExtendLease(now, leaseDuration);
            hold.Revision++;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public CommandResult Cancel(PizzeriaState state, ulong playerId)
        {
            if (!state.PlayerHolds.TryGetValue(playerId, out var hold))
                return CommandResult.Ok(); // Idempotent

            ReleaseHoldTarget(state, hold);

            state.PlayerHolds.Remove(playerId);
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        private static void ReleaseHoldTarget(PizzeriaState state, PlayerHold hold)
        {
            if (hold == null) return;

            if (hold.Kind == PlayerHoldKind.Discard && hold.TargetItemId != ItemId.None)
            {
                if (state.Items.TryGetValue(hold.TargetItemId, out var item))
                {
                    item.ReservedByHold = 0;
                    item.Revision++;
                }
            }
            else if (hold.Kind == PlayerHoldKind.AttendCustomer && hold.TargetCustomerId.IsValid)
            {
                if (state.Customers.TryGetValue(hold.TargetCustomerId, out var customer))
                {
                    if (customer.AttendedByPlayerId == hold.PlayerId)
                        customer.AttendedByPlayerId = 0;
                    if (customer.Phase == CustomerPhase.BeingAttended)
                        customer.Phase = CustomerPhase.WaitingInQueue;
                    customer.Revision++;
                }
            }
        }

        public void TickHolds(
            PizzeriaState state,
            float now,
            OrderService orderService,
            CustomerService customerService = null,
            IClock clock = null,
            int maxActiveOrders = 6)
        {
            var completed = new List<ulong>();

            foreach (var kvp in state.PlayerHolds)
            {
                var hold = kvp.Value;
                if (hold.Phase == OperationPhase.Running)
                {
                    if (now > hold.Progress.LeaseExpiry)
                    {
                        hold.Cancel();
                        ReleaseHoldTarget(state, hold);
                        completed.Add(kvp.Key);
                        continue;
                    }

                    hold.Progress.Materialize(now);
                    if (hold.Progress.IsComplete)
                    {
                        hold.Complete(now);
                        if (hold.Kind == PlayerHoldKind.Discard)
                        {
                            state.Items.Remove(hold.TargetItemId);
                        }
                        else if (hold.Kind == PlayerHoldKind.ArchiveTicket)
                        {
                            orderService?.ArchiveTicket(hold.TargetOrderId, state);
                        }
                        else if (hold.Kind == PlayerHoldKind.AttendCustomer && hold.TargetCustomerId.IsValid)
                        {
                            if (customerService != null && orderService != null)
                            {
                                var res = customerService.CompleteAttendCustomer(
                                    state, hold.TargetCustomerId, orderService, clock, maxActiveOrders);
                                if (!res.Success)
                                {
                                    ReleaseHoldTarget(state, hold);
                                }
                            }
                            else
                            {
                                ReleaseHoldTarget(state, hold);
                            }
                        }
                        completed.Add(kvp.Key);
                    }
                }
            }

            foreach (var id in completed)
            {
                state.PlayerHolds.Remove(id);
                state.IncrementRevision();
            }
        }
    }
}

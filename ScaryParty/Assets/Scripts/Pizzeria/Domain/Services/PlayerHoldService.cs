using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
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

            if (hold.Kind == PlayerHoldKind.Discard && hold.TargetItemId != ItemId.None)
            {
                if (state.Items.TryGetValue(hold.TargetItemId, out var item))
                {
                    item.ReservedByHold = 0;
                    item.Revision++;
                }
            }

            state.PlayerHolds.Remove(playerId);
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public void TickHolds(PizzeriaState state, float now, OrderService orderService)
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
                        if (hold.Kind == PlayerHoldKind.Discard)
                        {
                            if (state.Items.TryGetValue(hold.TargetItemId, out var item))
                                item.ReservedByHold = 0;
                        }
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
                            orderService.ArchiveTicket(hold.TargetOrderId, state);
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

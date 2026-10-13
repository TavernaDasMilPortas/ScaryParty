using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public static class StackService
    {
        public static CommandResult DepositStack(PizzeriaState state, StackId stackId, ItemId itemId, LocationRef handLocation, AddressId addressId)
        {
            if (!state.Stacks.TryGetValue(stackId, out var stack))
                return CommandResult.Fail(CommandError.InvalidOperation, "Pilha inválida.");

            if (!stack.CanAdd)
                return CommandResult.Fail(CommandError.SlotFull, "Pilha cheia.");

            if (!state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (!item.Location.Equals(handLocation))
                return CommandResult.Fail(CommandError.Blocked, "Item não está na mão.");

            if (!item.IsBoxed)
                return CommandResult.Fail(CommandError.Incompatible, "Apenas caixas podem ser empilhadas.");

            item.LabelAddressId = addressId.Value;
            
            return TransferService.TransferItem(state, itemId, new LocationRef(LocationType.Stack, stackId.Value), 0, bypassReservation: true);
        }

        public static CommandResult TakeTop(PizzeriaState state, StackId stackId, ItemId expectedTopItemId, LocationRef handLocation)
        {
            if (!state.Stacks.TryGetValue(stackId, out var stack))
                return CommandResult.Fail(CommandError.InvalidOperation, "Pilha inválida.");

            if (stack.IsEmpty)
                return CommandResult.Fail(CommandError.SlotEmpty, "Pilha vazia.");

            var actualTop = stack.TopItem;
            if (actualTop != expectedTopItemId)
                return CommandResult.Fail(CommandError.Blocked, "O topo da pilha mudou.");

            return TransferService.TransferItem(state, actualTop, handLocation, 0, bypassReservation: true);
        }
    }

    public static class BackpackService
    {
        public static CommandResult Equip(PizzeriaState state, BackpackId backpackId, ulong playerId)
        {
            if (!state.Backpacks.TryGetValue(backpackId, out var backpack))
                return CommandResult.Fail(CommandError.InvalidOperation, "Mochila inválida.");

            if (backpack.IsEquipped)
                return CommandResult.Fail(CommandError.Blocked, "Mochila já equipada.");

            foreach (var bp in state.Backpacks.Values)
            {
                if (bp.EquippedBy == playerId)
                    return CommandResult.Fail(CommandError.Blocked, "Você já tem uma mochila equipada.");
            }

            backpack.EquippedBy = playerId;
            backpack.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public static CommandResult Drop(PizzeriaState state, ulong playerId, DomainVector3 safePos, DomainQuaternion safeRot)
        {
            foreach (var backpack in state.Backpacks.Values)
            {
                if (backpack.EquippedBy == playerId)
                {
                    backpack.EquippedBy = BackpackState.NoOwner;
                    backpack.WorldPos = safePos;
                    backpack.WorldRot = safeRot;
                    backpack.Revision++;
                    state.IncrementRevision();
                    return CommandResult.Ok();
                }
            }
            return CommandResult.Fail(CommandError.InvalidOperation, "Nenhuma mochila equipada.");
        }

        public static CommandResult StoreItem(PizzeriaState state, BackpackId backpackId, ItemId itemId, LocationRef handLocation)
        {
            if (!state.Backpacks.TryGetValue(backpackId, out var backpack))
                return CommandResult.Fail(CommandError.InvalidOperation, "Mochila inválida.");

            if (!backpack.CanAdd)
                return CommandResult.Fail(CommandError.SlotFull, "Mochila cheia.");

            if (!state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (!item.Location.Equals(handLocation))
                return CommandResult.Fail(CommandError.Blocked, "Item não está na mão.");

            if (!item.IsBoxed)
                return CommandResult.Fail(CommandError.Incompatible, "Apenas caixas prontas podem ser guardadas na mochila.");

            return TransferService.TransferItem(state, itemId, new LocationRef(LocationType.Backpack, backpackId.Value), 0, bypassReservation: true);
        }

        public static CommandResult RetrieveItem(PizzeriaState state, BackpackId backpackId, ItemId itemId, LocationRef handLocation)
        {
            if (!state.Backpacks.TryGetValue(backpackId, out var backpack))
                return CommandResult.Fail(CommandError.InvalidOperation, "Mochila inválida.");

            if (!backpack.Items.Contains(itemId))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não está na mochila.");

            return TransferService.TransferItem(state, itemId, handLocation, 0, bypassReservation: true);
        }

        public static void ReleaseOnDisconnect(PizzeriaState state, ulong playerId, DomainVector3 recoveryPos)
        {
            Drop(state, playerId, recoveryPos, DomainQuaternion.Identity);
        }
    }
}

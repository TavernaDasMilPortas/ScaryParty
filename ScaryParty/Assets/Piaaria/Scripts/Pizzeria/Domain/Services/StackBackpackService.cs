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
        public static CommandResult Register(PizzeriaState state, BackpackId backpackId, int capacity, DomainVector3 position, DomainQuaternion rotation)
        {
            if (!backpackId.IsValid || capacity <= 0 || state.Backpacks.ContainsKey(backpackId))
                return CommandResult.Fail(CommandError.InvalidOperation, "Registro de mochila inválido ou repetido.");

            state.Backpacks.Add(backpackId, new BackpackState(backpackId, capacity)
            {
                WorldPos = position,
                WorldRot = rotation
            });
            state.IncrementRevision();
            return CommandResult.Ok();
        }

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

            if (backpack.IsTorn)
                return CommandResult.Fail(CommandError.Blocked, "Mochila rasgada! Repare antes de guardar caixas.");

            if (!backpack.CanAdd)
                return CommandResult.Fail(CommandError.SlotFull, "Mochila cheia.");

            if (!state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (handLocation.Type != LocationType.Hand || handLocation.SlotIndex < 0 || handLocation.SlotIndex > 1 ||
                !item.Location.Equals(handLocation))
                return CommandResult.Fail(CommandError.Blocked, "Item não está na mão selecionada.");

            if (!item.IsBoxed)
                return CommandResult.Fail(CommandError.Incompatible, "Apenas caixas prontas podem ser guardadas na mochila.");

            return TransferService.TransferItem(state, itemId, new LocationRef(LocationType.Backpack, backpackId.Value), handLocation.HolderId);
        }

        public static CommandResult RetrieveItem(PizzeriaState state, BackpackId backpackId, ItemId itemId, LocationRef handLocation)
        {
            if (!state.Backpacks.TryGetValue(backpackId, out var backpack))
                return CommandResult.Fail(CommandError.InvalidOperation, "Mochila inválida.");

            if (handLocation.Type != LocationType.Hand || handLocation.SlotIndex < 0 || handLocation.SlotIndex > 1)
                return CommandResult.Fail(CommandError.InvalidOperation, "Mão inválida.");

            if (!backpack.Items.Contains(itemId) || !state.Items.TryGetValue(itemId, out var item) ||
                !item.Location.Equals(new LocationRef(LocationType.Backpack, backpackId.Value)))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não está na mochila.");

            return TransferService.TransferItem(state, itemId, handLocation, handLocation.HolderId);
        }

        public static bool ApplyBackpackDamage(
            PizzeriaState state,
            BackpackId backpackId,
            int amount,
            DomainVector3 dropWorldPos,
            out ItemId droppedBoxId)
        {
            droppedBoxId = default;
            if (state == null || !state.Backpacks.TryGetValue(backpackId, out var backpack))
                return false;

            int damage = System.Math.Max(0, amount);
            if (damage > 0)
            {
                backpack.Integrity = System.Math.Max(0, backpack.Integrity - damage);
            }

            bool droppedAny = false;
            if (backpack.IsTorn && !backpack.HasDroppedBoxOnCurrentRupture)
            {
                backpack.HasDroppedBoxOnCurrentRupture = true;
                if (backpack.Items.Count > 0)
                {
                    int lastIdx = backpack.Items.Count - 1;
                    ItemId targetId = backpack.Items[lastIdx];
                    backpack.Items.RemoveAt(lastIdx);
                    if (state.Items.TryGetValue(targetId, out var item))
                    {
                        item.Location = LocationRef.InWorld(dropWorldPos.X, dropWorldPos.Y, dropWorldPos.Z);
                        item.Revision++;
                        droppedBoxId = targetId;
                        droppedAny = true;
                    }
                }
            }

            backpack.Revision++;
            state.IncrementRevision();
            return droppedAny;
        }

        public static CommandResult RepairBackpack(PizzeriaState state, BackpackId backpackId)
        {
            if (state == null || !state.Backpacks.TryGetValue(backpackId, out var backpack))
                return CommandResult.Fail(CommandError.InvalidOperation, "Mochila inválida.");

            backpack.Integrity = backpack.MaxIntegrity;
            backpack.HasDroppedBoxOnCurrentRupture = false;
            backpack.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public static void ResetAllBackpacksForNight(PizzeriaState state)
        {
            if (state == null) return;
            foreach (var bp in state.Backpacks.Values)
            {
                bp.Integrity = bp.MaxIntegrity;
                bp.HasDroppedBoxOnCurrentRupture = false;
                bp.Revision++;
            }
        }

        public static void ReleaseOnDisconnect(PizzeriaState state, ulong playerId, DomainVector3 recoveryPos)
        {
            Drop(state, playerId, recoveryPos, DomainQuaternion.Identity);
        }
    }
}

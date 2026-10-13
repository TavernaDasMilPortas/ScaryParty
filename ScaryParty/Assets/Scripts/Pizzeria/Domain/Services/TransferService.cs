using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class TransferService
    {
        private readonly PizzeriaState _state;
        private readonly DefinitionCatalog _catalog;

        public TransferService(PizzeriaState state, DefinitionCatalog catalog)
        {
            _state = state;
            _catalog = catalog;
        }

        public bool IsLocationOccupiedByItem(LocationRef location) => IsLocationOccupiedByItem(_state, location);
        public bool IsLocationOccupiedByTool(LocationRef location) => IsLocationOccupiedByTool(_state, location);
        public bool IsLocationOccupied(LocationRef location) => IsLocationOccupied(_state, location);

        public CommandResult<ItemId> DispenseFromStorage(int storageId, int ingredientDefId, ulong playerId, HandSlotIndex hand)
            => DispenseFromStorage(_state, _catalog, storageId, ingredientDefId, playerId, hand);

        public CommandResult TransferItem(ItemId itemId, LocationRef targetLocation, ulong requesterId = 0, bool bypassReservation = false)
            => TransferItem(_state, itemId, targetLocation, requesterId, bypassReservation);

        public CommandResult TransferTool(ToolItemId toolId, LocationRef targetLocation, double now, ulong requesterId = 0)
            => TransferTool(_state, toolId, targetLocation, requesterId, now);

        public CommandResult DestroyItem(ItemId itemId)
            => DestroyItem(_state, itemId);

        // --- Static Implementation ---

        public static bool IsLocationOccupiedByItem(PizzeriaState state, LocationRef location)
        {
            if (location.Type == LocationType.World || location.Type == LocationType.Destroyed)
                return false;

            foreach (var kvp in state.Items)
            {
                if (kvp.Value.Location.Equals(location))
                    return true;
            }
            return false;
        }

        public static bool IsLocationOccupiedByTool(PizzeriaState state, LocationRef location)
        {
            if (location.Type == LocationType.World || location.Type == LocationType.Destroyed)
                return false;

            foreach (var kvp in state.Tools)
            {
                if (kvp.Value.Location.Equals(location))
                    return true;
            }
            return false;
        }

        public static bool IsLocationOccupied(PizzeriaState state, LocationRef location)
            => IsLocationOccupiedByItem(state, location) || IsLocationOccupiedByTool(state, location);

        public static CommandResult<ItemId> DispenseFromStorage(
            PizzeriaState state, 
            DefinitionCatalog catalog, 
            int storageId, 
            int ingredientDefId, 
            ulong playerId, 
            HandSlotIndex hand)
        {
            var key = (storageId, ingredientDefId);
            if (!state.StorageSlots.TryGetValue(key, out var slot) || slot.Quantity <= 0)
                return CommandResult<ItemId>.Fail(CommandError.StockEmpty, "Estoque esgotado.");

            LocationRef targetHand = LocationRef.InHand(playerId, hand);
            if (IsLocationOccupied(state, targetHand))
                return CommandResult<ItemId>.Fail(CommandError.SlotFull, "Mão selecionada já está ocupada.");

            if (!catalog.Ingredients.TryGetValue(ingredientDefId, out var ingData))
                return CommandResult<ItemId>.Fail(CommandError.Incompatible, "Ingrediente desconhecido.");

            slot.Quantity--;
            slot.Revision++;

            ItemId newItemId = state.GenerateItemId();
            var newItem = new ItemState(newItemId, ingredientDefId, ingData.Category, targetHand);
            state.Items[newItemId] = newItem;
            state.IncrementRevision();

            return CommandResult<ItemId>.Ok(newItemId);
        }

        public static CommandResult TransferItem(
            PizzeriaState state, 
            ItemId itemId, 
            LocationRef newLocation,
            ulong requesterId = 0,
            bool bypassReservation = false)
        {
            if (!state.Items.TryGetValue(itemId, out var itemState))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (!bypassReservation && itemState.ReservedByHold != 0 && itemState.ReservedByHold != requesterId)
                return CommandResult.Fail(CommandError.Blocked, "Item reservado por outro processo.");

            // Se o item está em um StationSlot com operação ativa
            if (itemState.Location.Type == LocationType.StationSlot)
            {
                var currentSlotId = new StationSlotId((int)itemState.Location.HolderId, itemState.Location.SlotIndex);
                if (state.ActiveOperations.TryGetValue(currentSlotId, out var op))
                {
                    if (op.Phase == OperationPhase.Running)
                        return CommandResult.Fail(CommandError.Busy, "Não é possível mover o item enquanto a operação está em andamento.");

                    if (op.Phase == OperationPhase.Paused)
                    {
                        op.Cancel();
                        state.ActiveOperations.Remove(currentSlotId);
                    }
                }
            }

            if (!ValidateCapacity(state, newLocation))
                return CommandResult.Fail(CommandError.SlotFull, "Destino sem capacidade.");

            // Remover do local anterior
            if (itemState.Location.Type == LocationType.Stack)
            {
                var stackId = new StackId(itemState.Location.HolderId);
                if (state.Stacks.TryGetValue(stackId, out var stack))
                    stack.Remove(itemId);
            }
            else if (itemState.Location.Type == LocationType.Backpack)
            {
                var backpackId = new BackpackId(itemState.Location.HolderId);
                if (state.Backpacks.TryGetValue(backpackId, out var backpack))
                    backpack.Remove(itemId);
            }
            
            // Adicionar ao novo local
            if (newLocation.Type == LocationType.Stack)
            {
                var stackId = new StackId(newLocation.HolderId);
                if (state.Stacks.TryGetValue(stackId, out var stack))
                    stack.Add(itemId);
            }
            else if (newLocation.Type == LocationType.Backpack)
            {
                var backpackId = new BackpackId(newLocation.HolderId);
                if (state.Backpacks.TryGetValue(backpackId, out var backpack))
                    backpack.Add(itemId);
            }

            itemState.Location = newLocation;
            itemState.ReservedByHold = 0;
            itemState.Revision++;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public static CommandResult TransferTool(
            PizzeriaState state, 
            ToolItemId toolId, 
            LocationRef newLocation,
            ulong requesterId,
            double now)
        {
            if (!state.Tools.TryGetValue(toolId, out var toolState))
                return CommandResult.Fail(CommandError.ItemNotFound, "Utensílio não encontrado.");

            if (toolState.IsReserved(now) && toolState.ReservedByWorkerId != requesterId)
                return CommandResult.Fail(CommandError.ToolBusy, "Utensílio em uso.");

            if (!ValidateCapacity(state, newLocation))
                return CommandResult.Fail(CommandError.SlotFull, "Destino sem capacidade.");

            toolState.Location = newLocation;
            toolState.Revision++;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public static CommandResult DestroyItem(PizzeriaState state, ItemId itemId)
        {
            if (!state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (item.Location.Type == LocationType.Stack)
            {
                var stackId = new StackId(item.Location.HolderId);
                if (state.Stacks.TryGetValue(stackId, out var stack))
                    stack.Remove(itemId);
            }
            else if (item.Location.Type == LocationType.Backpack)
            {
                var backpackId = new BackpackId(item.Location.HolderId);
                if (state.Backpacks.TryGetValue(backpackId, out var backpack))
                    backpack.Remove(itemId);
            }

            item.Location = LocationRef.Destroyed;
            state.Items.Remove(itemId);
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        private static bool ValidateCapacity(PizzeriaState state, LocationRef location)
        {
            if (location.Type == LocationType.Hand)
            {
                int count = 0;
                foreach (var item in state.Items.Values)
                    if (item.Location.Equals(location)) count++;
                foreach (var tool in state.Tools.Values)
                    if (tool.Location.Equals(location)) count++;
                return count == 0;
            }
            if (location.Type == LocationType.StationSlot || location.Type == LocationType.StorageSlot)
            {
                int count = 0;
                foreach (var item in state.Items.Values)
                    if (item.Location.Equals(location)) count++;
                foreach (var tool in state.Tools.Values)
                    if (tool.Location.Equals(location)) count++;
                return count == 0;
            }
            if (location.Type == LocationType.Stack)
            {
                var stackId = new StackId(location.HolderId);
                return state.Stacks.TryGetValue(stackId, out var stack) && stack.CanAdd;
            }
            if (location.Type == LocationType.Backpack)
            {
                var backpackId = new BackpackId(location.HolderId);
                return state.Backpacks.TryGetValue(backpackId, out var backpack) && backpack.CanAdd;
            }
            return true;
        }
    }
}

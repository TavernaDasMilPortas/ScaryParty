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

        public bool IsLocationOccupiedByItem(LocationRef location)
        {
            if (location.Type == LocationType.World || location.Type == LocationType.Destroyed)
                return false;

            foreach (var kvp in _state.Items)
            {
                if (kvp.Value.Location == location)
                    return true;
            }
            return false;
        }

        public bool IsLocationOccupiedByTool(LocationRef location)
        {
            if (location.Type == LocationType.World || location.Type == LocationType.Destroyed)
                return false;

            foreach (var kvp in _state.Tools)
            {
                if (kvp.Value.Location == location)
                    return true;
            }
            return false;
        }

        public bool IsLocationOccupied(LocationRef location)
            => IsLocationOccupiedByItem(location) || IsLocationOccupiedByTool(location);

        public CommandResult<ItemId> DispenseFromStorage(int storageId, int ingredientDefId, ulong playerId, HandSlotIndex hand)
        {
            var key = (storageId, ingredientDefId);
            if (!_state.StorageSlots.TryGetValue(key, out var slot) || slot.Quantity <= 0)
                return CommandResult<ItemId>.Fail(CommandError.StockEmpty, "Estoque esgotado.");

            LocationRef targetHand = LocationRef.InHand(playerId, hand);
            if (IsLocationOccupied(targetHand))
                return CommandResult<ItemId>.Fail(CommandError.SlotFull, "Mão selecionada já está ocupada.");

            if (!_catalog.Ingredients.TryGetValue(ingredientDefId, out var ingData))
                return CommandResult<ItemId>.Fail(CommandError.Incompatible, "Ingrediente desconhecido.");

            // Atomic decrement + creation
            slot.Quantity--;
            slot.Revision++;

            ItemId newItemId = _state.GenerateItemId();
            var newItem = new ItemState(newItemId, ingredientDefId, ingData.Category, targetHand);
            _state.Items[newItemId] = newItem;
            _state.IncrementRevision();

            return CommandResult<ItemId>.Ok(newItemId);
        }

        public CommandResult TransferItem(ItemId itemId, LocationRef targetLocation)
        {
            if (!_state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (targetLocation.Type != LocationType.World && targetLocation.Type != LocationType.Destroyed)
            {
                if (IsLocationOccupied(targetLocation))
                    return CommandResult.Fail(CommandError.SlotFull, "Destino ocupado.");
            }

            item.Location = targetLocation;
            item.Revision++;
            _state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult TransferTool(ToolItemId toolId, LocationRef targetLocation, double now)
        {
            if (!_state.Tools.TryGetValue(toolId, out var tool))
                return CommandResult.Fail(CommandError.ItemNotFound, "Utensílio não encontrado.");

            if (targetLocation.Type != LocationType.World && targetLocation.Type != LocationType.Destroyed)
            {
                if (IsLocationOccupied(targetLocation))
                    return CommandResult.Fail(CommandError.SlotFull, "Destino ocupado.");
            }

            tool.Location = targetLocation;
            tool.Revision++;
            _state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult DestroyItem(ItemId itemId)
        {
            if (!_state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            item.Location = LocationRef.Destroyed;
            _state.Items.Remove(itemId);
            _state.IncrementRevision();
            return CommandResult.Ok();
        }
    }
}

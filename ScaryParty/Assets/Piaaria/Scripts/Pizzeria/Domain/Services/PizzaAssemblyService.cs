using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public static class PizzaAssemblyService
    {
        public static CommandResult Begin(PizzeriaState state, ItemId pizzaId, ItemId ingredientId,
            ulong playerId, float now, float duration)
        {
            if (duration <= 0f || pizzaId == ingredientId || !state.Items.TryGetValue(pizzaId, out var pizza) ||
                !state.Items.TryGetValue(ingredientId, out var ingredient))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");
            if (pizza.Location.Type != LocationType.StationSlot ||
                ingredient.Location.Type != LocationType.Hand || ingredient.Location.HolderId != playerId)
                return CommandResult.Fail(CommandError.Incompatible, "A pizza deve estar na bancada e o ingrediente na sua mão.");
            if (pizza.ReservedByHold != 0 || ingredient.ReservedByHold != 0)
                return CommandResult.Fail(CommandError.Busy, "Item em uso.");
            if ((ingredient.Category != ItemCategory.Sauce && ingredient.Category != ItemCategory.Cheese &&
                 ingredient.Category != ItemCategory.Topping) ||
                (pizza.Category == ItemCategory.Dough && pizza.PrepStage == ProcessingStage.Raw) ||
                (ingredient.Category != ItemCategory.Sauce && ingredient.PrepStage == ProcessingStage.Raw) ||
                !pizza.CanAddIngredient(ingredient.DefinitionId))
                return CommandResult.Fail(CommandError.Incompatible, "Ingrediente ou pizza não está pronto para montagem.");

            var slot = new StationSlotId((int)pizza.Location.HolderId, pizza.Location.SlotId);
            if (state.ActiveOperations.ContainsKey(slot))
                return CommandResult.Fail(CommandError.Busy, "Bancada ocupada.");

            var op = new OperationState(state.GenerateOperationId(), slot, OperationKind.Assemble,
                ResumePolicy.ResetOnCancel, pizzaId, ToolItemId.None, 0,
                OperationState.ServerWorker, duration, now, duration + 1f, playerId)
            {
                SecondaryItemId = ingredientId
            };
            state.ActiveOperations.Add(slot, op);
            pizza.ReservedByHold = ingredient.ReservedByHold = OperationState.ServerWorker;
            pizza.Revision++;
            ingredient.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public static bool Complete(PizzeriaState state, StationSlotId slot)
        {
            if (!state.ActiveOperations.TryGetValue(slot, out var op) || op.Kind != OperationKind.Assemble)
                return false;

            if (state.Items.TryGetValue(op.InputItemId, out var pizza) &&
                state.Items.TryGetValue(op.SecondaryItemId, out var ingredient) &&
                pizza.Location == LocationRef.InStation(slot.StationId, slot.SlotIndex) &&
                pizza.CanAddIngredient(ingredient.DefinitionId))
            {
                pizza.AddIngredient(ingredient.DefinitionId, ingredient.PrepStage);
                state.IncrementRevision();
                ingredient.ReservedByHold = 0;
                TransferService.DestroyItem(state, ingredient.Id);
            }

            Cancel(state, slot);
            return true;
        }

        public static bool Cancel(PizzeriaState state, StationSlotId slot)
        {
            if (!state.ActiveOperations.TryGetValue(slot, out var op) || op.Kind != OperationKind.Assemble)
                return false;
            if (state.Items.TryGetValue(op.InputItemId, out var pizza))
            {
                pizza.ReservedByHold = 0;
                pizza.Revision++;
            }
            if (state.Items.TryGetValue(op.SecondaryItemId, out var ingredient))
            {
                ingredient.ReservedByHold = 0;
                ingredient.Revision++;
            }
            state.ActiveOperations.Remove(slot);
            state.IncrementRevision();
            return true;
        }
    }
}

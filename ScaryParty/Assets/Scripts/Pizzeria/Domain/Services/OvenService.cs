using System;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class OvenService
    {
        public CommandResult InsertPizza(
            PizzeriaState state, 
            StationSlotId slotId, 
            ItemId itemId, 
            ulong requesterId, 
            DefinitionCatalog catalog,
            float defaultBakeDuration)
        {
            if (!state.Items.TryGetValue(itemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

            if (!item.IsPizzaLike || item.IsBoxed)
                return CommandResult.Fail(CommandError.Incompatible, "Apenas pizzas desembaladas podem ser inseridas no forno.");

            if (catalog.Stations.TryGetValue(slotId.StationId, out var stationData))
            {
                if (slotId.SlotIndex >= stationData.SlotCount)
                    return CommandResult.Fail(CommandError.SlotFull, "Índice de slot inválido para este forno.");
            }

            var destLocation = new LocationRef(LocationType.StationSlot, (ulong)slotId.StationId, slotId.SlotIndex);
            var transferResult = TransferService.TransferItem(state, itemId, destLocation, requesterId, bypassReservation: false);
            if (!transferResult.Success)
                return transferResult;

            // Capturar duração de cozimento se ainda não estiver definida
            if (item.CapturedCookDuration <= 0f)
            {
                // Tentar encontrar receita correspondente ou usar default
                float duration = defaultBakeDuration;
                foreach (var recipe in catalog.Recipes.Values)
                {
                    if (recipe.BaseIngredientId == item.DefinitionId)
                    {
                        duration = recipe.BaseBakeDuration;
                        break;
                    }
                }
                item.CapturedCookDuration = duration;
            }

            if (item.CookProgress > 0f && item.CookProgress < 1f)
            {
                item.CookingStage = CookingStage.Cooking;
            }

            item.Revision++;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public CommandResult RemovePizza(
            PizzeriaState state, 
            StationSlotId slotId, 
            LocationRef handLocation, 
            ulong requesterId)
        {
            ItemId targetItem = ItemId.None;
            foreach (var item in state.Items.Values)
            {
                if (item.Location.Type == LocationType.StationSlot && 
                    item.Location.HolderId == (ulong)slotId.StationId && 
                    item.Location.SlotIndex == slotId.SlotIndex)
                {
                    targetItem = item.Id;
                    break;
                }
            }

            if (targetItem == ItemId.None)
                return CommandResult.Fail(CommandError.SlotEmpty, "Slot do forno está vazio.");

            return TransferService.TransferItem(state, targetItem, handLocation, requesterId, bypassReservation: false);
        }

        public void TickOven(
            PizzeriaState state, 
            int ovenStationId, 
            float delta, 
            float defaultBakeDuration, 
            float burnDuration)
        {
            if (delta <= 0f) return;

            foreach (var item in state.Items.Values)
            {
                if (item.Location.Type == LocationType.StationSlot && item.Location.HolderId == (ulong)ovenStationId)
                {
                    if (item.IsPizzaLike && !item.IsBoxed && item.CookingStage != CookingStage.Burned)
                    {
                        float cookDur = item.CapturedCookDuration > 0f ? item.CapturedCookDuration : defaultBakeDuration;
                        if (cookDur <= 0.01f) cookDur = 15f;
                        if (burnDuration <= 0.01f) burnDuration = 10f;

                        if (item.CookProgress < 1f)
                        {
                            item.CookProgress += delta / cookDur;
                            if (item.CookProgress >= 1f)
                            {
                                float leftover = (item.CookProgress - 1f) * cookDur;
                                item.CookProgress = 1f;
                                item.CookingStage = CookingStage.Baked;
                                if (leftover > 0f)
                                {
                                    item.BurnProgress += leftover / burnDuration;
                                }
                            }
                            else
                            {
                                item.CookingStage = CookingStage.Cooking;
                            }
                        }
                        else
                        {
                            item.BurnProgress += delta / burnDuration;
                        }

                        if (item.BurnProgress >= 1f)
                        {
                            item.BurnProgress = 1f;
                            item.CookingStage = CookingStage.Burned;
                        }

                        item.Revision++;
                        state.IncrementRevision();
                    }
                }
            }
        }
    }
}

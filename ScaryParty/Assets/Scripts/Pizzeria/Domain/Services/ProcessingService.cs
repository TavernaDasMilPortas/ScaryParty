using System;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class ProcessingService
    {
        private const double DefaultLeaseDuration = 0.75;

        public CommandResult<OperationId> StartWork(
            StationSlotId slotId,
            int processId,
            ulong workerId,
            PizzeriaState state,
            DefinitionCatalog catalog,
            IClock clock)
        {
            if (!catalog.Processes.TryGetValue(processId, out var process))
                return CommandResult<OperationId>.Fail(CommandError.Incompatible, "Processo desconhecido.");

            // Find item in station slot
            ItemState itemInSlot = null;
            foreach (var kvp in state.Items)
            {
                if (kvp.Value.Location == LocationRef.InStation(slotId.StationId, slotId.SlotIndex))
                {
                    itemInSlot = kvp.Value;
                    break;
                }
            }

            if (itemInSlot == null)
                return CommandResult<OperationId>.Fail(CommandError.SlotEmpty, "Nenhum item na bancada de preparo.");

            if (itemInSlot.DefinitionId != process.InputIngredientId || itemInSlot.PrepStage != process.InputStage)
                return CommandResult<OperationId>.Fail(CommandError.Incompatible, "Item incompatível com o processo.");

            // Check if slot already has active operation
            if (state.ActiveOperations.TryGetValue(slotId, out var existingOp) && existingOp.IsActive(clock.Now))
            {
                if (existingOp.WorkerId != workerId)
                    return CommandResult<OperationId>.Fail(CommandError.Busy, "Bancada ocupada por outro jogador.");
            }

            // Check tool requirement
            ToolItemState requiredTool = null;
            if (process.RequiredCapability != ToolCapability.None)
            {
                // Check worker hands for tool
                var leftHand = LocationRef.InHand(workerId, HandSlotIndex.Left);
                var rightHand = LocationRef.InHand(workerId, HandSlotIndex.Right);

                foreach (var kvp in state.Tools)
                {
                    var tool = kvp.Value;
                    if ((tool.Location == leftHand || tool.Location == rightHand) &&
                        (tool.Capabilities & process.RequiredCapability) != 0)
                    {
                        requiredTool = tool;
                        break;
                    }
                }

                if (requiredTool == null)
                    return CommandResult<OperationId>.Fail(CommandError.MissingTool, "Utensílio necessário não está nas mãos.");

                if (!requiredTool.TryAcquireLease(workerId, clock.Now, DefaultLeaseDuration))
                    return CommandResult<OperationId>.Fail(CommandError.ToolBusy, "Utensílio ocupado.");
            }

            // Determine duration (checking station override)
            float duration = process.BaseDuration;
            if (catalog.Stations.TryGetValue(slotId.StationId, out var stationData))
            {
                duration = stationData.GetProcessDuration(process);
            }

            OperationId opId;
            if (existingOp != null)
            {
                opId = existingOp.Id;
                existingOp.ToolItemId = requiredTool != null ? requiredTool.Id : ToolItemId.None;
                existingOp.Resume(workerId, clock.Now, DefaultLeaseDuration);
            }
            else
            {
                opId = state.GenerateOperationId();
                var newOp = new OperationState(
                    opId, slotId, itemInSlot.Id,
                    requiredTool != null ? requiredTool.Id : ToolItemId.None,
                    processId, workerId, duration, clock.Now, DefaultLeaseDuration);
                state.ActiveOperations[slotId] = newOp;
            }

            state.IncrementRevision();
            return CommandResult<OperationId>.Ok(opId);
        }

        public CommandResult HeartbeatWork(StationSlotId slotId, ulong workerId, PizzeriaState state, IClock clock)
        {
            if (!state.ActiveOperations.TryGetValue(slotId, out var op))
                return CommandResult.Fail(CommandError.InvalidOperation, "Nenhuma operação ativa neste slot.");

            if (op.WorkerId != workerId)
                return CommandResult.Fail(CommandError.NotOwner, "Worker mismatch.");

            op.LeaseExpirationTime = clock.Now + DefaultLeaseDuration;

            if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
            {
                tool.LeaseExpiration = clock.Now + DefaultLeaseDuration;
            }

            return CommandResult.Ok();
        }

        public CommandResult CancelWork(StationSlotId slotId, ulong workerId, PizzeriaState state, IClock clock)
        {
            if (!state.ActiveOperations.TryGetValue(slotId, out var op))
                return CommandResult.Ok(); // Already cancelled or completed

            if (op.WorkerId != workerId && op.WorkerId != 0)
                return CommandResult.Fail(CommandError.NotOwner, "Worker mismatch.");

            if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
            {
                tool.ReleaseLease(workerId);
            }

            op.Pause(clock.Now);
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult CompleteWork(StationSlotId slotId, PizzeriaState state, DefinitionCatalog catalog)
        {
            if (!state.ActiveOperations.TryGetValue(slotId, out var op))
                return CommandResult.Fail(CommandError.InvalidOperation, "Nenhuma operação para concluir.");

            if (!catalog.Processes.TryGetValue(op.ProcessId, out var process))
                return CommandResult.Fail(CommandError.Incompatible, "Processo desconhecido.");

            if (!state.Items.TryGetValue(op.InputItemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item de entrada não encontrado.");

            // Release tool lease
            if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
            {
                tool.ReleaseLease(op.WorkerId);
            }

            // Transform item in place
            item.DefinitionId = process.OutputIngredientId;
            item.PrepStage = process.OutputStage;
            if (catalog.Ingredients.TryGetValue(process.OutputIngredientId, out var outIng))
            {
                item.Category = outIng.Category;
            }
            item.Revision++;

            state.ActiveOperations.Remove(slotId);
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult InsertOven(ItemId pizzaId, StationSlotId ovenSlot, PizzeriaState state, float bakeDuration, IClock clock)
        {
            if (!state.Items.TryGetValue(pizzaId, out var pizza))
                return CommandResult.Fail(CommandError.ItemNotFound, "Pizza não encontrada.");

            var ovenLocation = LocationRef.InStation(ovenSlot.StationId, ovenSlot.SlotIndex);
            foreach (var kvp in state.Items)
            {
                if (kvp.Value.Location == ovenLocation)
                    return CommandResult.Fail(CommandError.SlotFull, "Slot do forno ocupado.");
            }

            pizza.Location = ovenLocation;
            pizza.CookingStage = CookingStage.Cooking;
            pizza.Revision++;

            // Create autonomous oven operation
            var opId = state.GenerateOperationId();
            var op = new OperationState(opId, ovenSlot, pizzaId, ToolItemId.None, 0, 1 /* server worker */, bakeDuration, clock.Now, 999999);
            op.AccumulatedProgress = pizza.CookProgress;
            state.ActiveOperations[ovenSlot] = op;

            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult<ItemId> RemoveOven(StationSlotId ovenSlot, LocationRef targetLocation, PizzeriaState state, IClock clock)
        {
            var ovenLocation = LocationRef.InStation(ovenSlot.StationId, ovenSlot.SlotIndex);
            ItemState pizza = null;
            foreach (var kvp in state.Items)
            {
                if (kvp.Value.Location == ovenLocation)
                {
                    pizza = kvp.Value;
                    break;
                }
            }

            if (pizza == null)
                return CommandResult<ItemId>.Fail(CommandError.SlotEmpty, "Forno vazio.");

            // Materialize progress before removing
            if (state.ActiveOperations.TryGetValue(ovenSlot, out var op))
            {
                pizza.CookProgress = op.ComputeCurrentProgress(clock.Now);
                state.ActiveOperations.Remove(ovenSlot);
            }

            pizza.Location = targetLocation;
            if (pizza.CookProgress < 1.0f)
                pizza.CookingStage = CookingStage.Uncooked; // partial, can be reinserted
            pizza.Revision++;

            state.IncrementRevision();
            return CommandResult<ItemId>.Ok(pizza.Id);
        }

        public void TickOven(StationSlotId ovenSlot, PizzeriaState state, float burnGraceDuration, IClock clock)
        {
            if (!state.ActiveOperations.TryGetValue(ovenSlot, out var op)) return;
            if (!state.Items.TryGetValue(op.InputItemId, out var pizza)) return;

            float p = op.ComputeCurrentProgress(clock.Now);
            pizza.CookProgress = p;

            if (p >= 1.0f)
            {
                if (pizza.CookingStage != CookingStage.Baked && pizza.CookingStage != CookingStage.Burned)
                {
                    pizza.CookingStage = CookingStage.Baked;
                    pizza.Revision++;
                }

                // Check burn progress past ready
                double timePastBaked = clock.Now - (op.OperationStartTime + op.CapturedDuration);
                if (timePastBaked > 0 && burnGraceDuration > 0.001f)
                {
                    float burnP = (float)(timePastBaked / burnGraceDuration);
                    pizza.BurnProgress = burnP > 1f ? 1f : burnP;

                    if (burnP >= 1.0f && pizza.CookingStage != CookingStage.Burned)
                    {
                        pizza.CookingStage = CookingStage.Burned;
                        pizza.Revision++;
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class ProcessingService
    {
        public const float DefaultLeaseDuration = 0.75f;

        // --- New Architecture Methods (Etapa 1+) ---

        public CommandResult BeginOperation(
            PizzeriaState state, 
            StationSlotId slotId, 
            OperationKind kind, 
            ResumePolicy policy,
            ItemId inputItemId, 
            ToolItemId toolItemId, 
            int processId, 
            ulong workerId, 
            DefinitionCatalog catalog,
            float now, 
            float leaseDuration = DefaultLeaseDuration,
            float fallbackDuration = 3.0f)
        {
            if (state.ActiveOperations.TryGetValue(slotId, out var existing))
            {
                if (existing.Phase == OperationPhase.Running || existing.Phase == OperationPhase.Paused)
                {
                    if (existing.Phase == OperationPhase.Paused && existing.InputItemId == inputItemId && existing.ToolItemId == toolItemId && existing.ProcessId == processId)
                    {
                        existing.Resume(workerId, now, leaseDuration);
                        state.IncrementRevision();
                        return CommandResult.Ok();
                    }
                    return CommandResult.Fail(CommandError.Busy, "Slot ocupado com outra operação.");
                }
            }

            float duration = fallbackDuration;

            if (kind == OperationKind.Process)
            {
                if (!catalog.Processes.TryGetValue(processId, out var procData))
                    return CommandResult.Fail(CommandError.InvalidOperation, "Processo não encontrado no catálogo.");

                if (catalog.Stations.TryGetValue(slotId.StationId, out var stationData))
                {
                    if (!stationData.AcceptsProcess(processId))
                        return CommandResult.Fail(CommandError.Incompatible, "Esta bancada não aceita este processo.");

                    duration = stationData.GetProcessDuration(procData);
                }
                else
                {
                    duration = procData.BaseDuration;
                }

                if (!state.Items.TryGetValue(inputItemId, out var inputItem))
                    return CommandResult.Fail(CommandError.ItemNotFound, "Item não encontrado.");

                if (inputItem.DefinitionId != procData.InputIngredientId || inputItem.PrepStage != procData.InputStage)
                    return CommandResult.Fail(CommandError.Incompatible, "Ingrediente incompatível com o processo.");

                if (procData.RequiredCapability != ToolCapability.None)
                {
                    if (!state.Tools.TryGetValue(toolItemId, out var tool) || !tool.Capabilities.HasFlag(procData.RequiredCapability))
                        return CommandResult.Fail(CommandError.MissingTool, "Ferramenta necessária ausente ou incompatível.");

                    if (!tool.TryAcquireLease(workerId, now, leaseDuration))
                        return CommandResult.Fail(CommandError.ToolBusy, "Ferramenta em uso.");
                }
            }

            if (state.Items.TryGetValue(inputItemId, out var item))
            {
                item.ReservedByHold = workerId;
                item.Revision++;
            }

            var opId = state.GenerateOperationId();
            var op = new OperationState(opId, slotId, kind, policy, inputItemId, toolItemId, processId, workerId, duration, now, leaseDuration);
            state.ActiveOperations[slotId] = op;
            state.IncrementRevision();

            return CommandResult.Ok();
        }

        public CommandResult Heartbeat(PizzeriaState state, StationSlotId slotId, ulong workerId, float now, float leaseDuration = DefaultLeaseDuration)
        {
            if (!state.ActiveOperations.TryGetValue(slotId, out var op))
                return CommandResult.Fail(CommandError.InvalidOperation, "Nenhuma operação em andamento.");

            if (op.Phase != OperationPhase.Running)
                return CommandResult.Fail(CommandError.InvalidOperation, "Operação não está rodando.");

            if (op.WorkerId != workerId)
                return CommandResult.Fail(CommandError.NotOwner, "Você não é o executor desta operação.");

            op.Progress.ExtendLease(now, leaseDuration);

            if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
            {
                tool.TryAcquireLease(workerId, now, leaseDuration);
            }

            op.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public CommandResult CancelOperation(PizzeriaState state, StationSlotId slotId, ulong workerId, float now)
        {
            if (!state.ActiveOperations.TryGetValue(slotId, out var op))
                return CommandResult.Ok();

            if (op.WorkerId != workerId && op.WorkerId != OperationState.NoWorker)
                return CommandResult.Fail(CommandError.NotOwner, "Você não é o executor desta operação.");

            op.Pause(now);

            if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
            {
                tool.ReleaseLease(workerId);
            }

            if (op.ResumePolicy == ResumePolicy.ResetOnCancel)
            {
                op.Cancel();
                if (state.Items.TryGetValue(op.InputItemId, out var item))
                {
                    item.ReservedByHold = 0;
                    item.Revision++;
                }
                state.ActiveOperations.Remove(slotId);
            }

            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public void TickOperations(PizzeriaState state, DefinitionCatalog catalog, float now)
        {
            var completedSlots = new List<StationSlotId>();

            foreach (var kvp in state.ActiveOperations)
            {
                var op = kvp.Value;
                if (op.Phase == OperationPhase.Running)
                {
                    if (now > op.Progress.LeaseExpiry)
                    {
                        op.Pause(now);
                        if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
                        {
                            tool.ReleaseLease(op.WorkerId);
                        }

                        if (op.ResumePolicy == ResumePolicy.ResetOnCancel)
                        {
                            op.Cancel();
                            if (state.Items.TryGetValue(op.InputItemId, out var item))
                            {
                                item.ReservedByHold = 0;
                                item.Revision++;
                            }
                            completedSlots.Add(kvp.Key);
                        }
                        state.IncrementRevision();
                        continue;
                    }

                    op.Progress.Materialize(now);
                    if (op.Progress.IsComplete)
                    {
                        op.Complete(now);

                        if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
                        {
                            tool.ReleaseLease(op.WorkerId);
                        }

                        if (op.Kind == OperationKind.Process)
                        {
                            if (catalog.Processes.TryGetValue(op.ProcessId, out var procData) && state.Items.TryGetValue(op.InputItemId, out var item))
                            {
                                item.DefinitionId = procData.OutputIngredientId;
                                item.PrepStage = procData.OutputStage;
                                if (catalog.Ingredients.TryGetValue(procData.OutputIngredientId, out var outIng))
                                {
                                    item.Category = outIng.Category;
                                }
                                item.ReservedByHold = 0;
                                item.Revision++;
                            }
                        }
                        else if (op.Kind == OperationKind.Package)
                        {
                            if (state.Items.TryGetValue(op.InputItemId, out var item))
                            {
                                item.PackagingState = PackagingState.Boxed;
                                item.ReservedByHold = 0;
                                item.Revision++;
                            }
                        }

                        completedSlots.Add(kvp.Key);
                    }
                    op.Revision++;
                }
            }

            foreach (var slot in completedSlots)
            {
                state.ActiveOperations.Remove(slot);
                state.IncrementRevision();
            }
        }

        // --- Compatibility Methods (For Existing PizzeriaCommandHandler / Tests) ---

        public CommandResult<OperationId> StartWork(StationSlotId slotId, int processId, ulong workerId, PizzeriaState state, DefinitionCatalog catalog, IClock clock)
        {
            var itemLoc = LocationRef.InStation(slotId.StationId, slotId.SlotIndex);
            ItemState itemInSlot = null;
            foreach (var kvp in state.Items)
            {
                if (kvp.Value.Location == itemLoc)
                {
                    itemInSlot = kvp.Value;
                    break;
                }
            }
            if (itemInSlot == null)
                return CommandResult<OperationId>.Fail(CommandError.SlotEmpty, "Bancada vazia.");

            ToolItemId toolId = ToolItemId.None;
            if (catalog.Processes.TryGetValue(processId, out var proc) && proc.RequiredCapability != ToolCapability.None)
            {
                LocationRef rightHand = LocationRef.InHand(workerId, HandSlotIndex.Right);
                LocationRef leftHand = LocationRef.InHand(workerId, HandSlotIndex.Left);
                foreach (var kvp in state.Tools)
                {
                    if (kvp.Value.Location == rightHand || kvp.Value.Location == leftHand)
                    {
                        if (kvp.Value.Capabilities.HasFlag(proc.RequiredCapability))
                        {
                            toolId = kvp.Value.Id;
                            break;
                        }
                    }
                }
            }

            var beginRes = BeginOperation(state, slotId, OperationKind.Process, ResumePolicy.Resumable, itemInSlot.Id, toolId, processId, workerId, catalog, (float)clock.Now, DefaultLeaseDuration);
            if (!beginRes.Success)
                return CommandResult<OperationId>.Fail(beginRes.Error, beginRes.Message);

            if (state.ActiveOperations.TryGetValue(slotId, out var op))
                return CommandResult<OperationId>.Ok(op.Id);

            return CommandResult<OperationId>.Fail(CommandError.InvalidOperation, "Falha ao registrar operação.");
        }

        public CommandResult HeartbeatWork(StationSlotId slotId, ulong workerId, PizzeriaState state, IClock clock)
            => Heartbeat(state, slotId, workerId, (float)clock.Now, DefaultLeaseDuration);

        public CommandResult CancelWork(StationSlotId slotId, ulong workerId, PizzeriaState state, IClock clock)
            => CancelOperation(state, slotId, workerId, (float)clock.Now);

        public CommandResult CompleteWork(StationSlotId slotId, PizzeriaState state, DefinitionCatalog catalog)
        {
            if (!state.ActiveOperations.TryGetValue(slotId, out var op))
                return CommandResult.Fail(CommandError.InvalidOperation, "Nenhuma operação para concluir.");

            if (!catalog.Processes.TryGetValue(op.ProcessId, out var process))
                return CommandResult.Fail(CommandError.Incompatible, "Processo desconhecido.");

            if (!state.Items.TryGetValue(op.InputItemId, out var item))
                return CommandResult.Fail(CommandError.ItemNotFound, "Item de entrada não encontrado.");

            if (op.ToolItemId.IsValid && state.Tools.TryGetValue(op.ToolItemId, out var tool))
            {
                tool.ReleaseLease(op.WorkerId);
            }

            item.DefinitionId = process.OutputIngredientId;
            item.PrepStage = process.OutputStage;
            if (catalog.Ingredients.TryGetValue(process.OutputIngredientId, out var outIng))
            {
                item.Category = outIng.Category;
            }
            item.ReservedByHold = 0;
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
            pizza.CapturedCookDuration = bakeDuration;
            pizza.Revision++;

            var opId = state.GenerateOperationId();
            var op = new OperationState(opId, ovenSlot, OperationKind.Process, ResumePolicy.Resumable, pizzaId, ToolItemId.None, 0, OperationState.ServerWorker, bakeDuration, (float)clock.Now, 999999f);
            op.Progress.Accumulated = pizza.CookProgress * bakeDuration;
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

            if (state.ActiveOperations.TryGetValue(ovenSlot, out var op))
            {
                op.Progress.Materialize((float)clock.Now);
                pizza.CookProgress = op.Progress.Normalized;
                state.ActiveOperations.Remove(ovenSlot);
            }

            pizza.Location = targetLocation;
            if (pizza.CookProgress < 1.0f)
                pizza.CookingStage = CookingStage.Cooking;
            pizza.Revision++;

            state.IncrementRevision();
            return CommandResult<ItemId>.Ok(pizza.Id);
        }

        public void TickOven(StationSlotId ovenSlot, PizzeriaState state, float burnGraceDuration, IClock clock)
        {
            if (!state.ActiveOperations.TryGetValue(ovenSlot, out var op)) return;
            if (!state.Items.TryGetValue(op.InputItemId, out var pizza)) return;

            op.Progress.Materialize((float)clock.Now);
            float p = op.Progress.Normalized;
            if (Math.Abs(pizza.CookProgress - p) > 0.005f)
            {
                pizza.CookProgress = p;
                pizza.Revision++;
            }

            if (p >= 1.0f)
            {
                if (pizza.CookingStage != CookingStage.Baked && pizza.CookingStage != CookingStage.Burned)
                {
                    pizza.CookingStage = CookingStage.Baked;
                    pizza.Revision++;
                }

                float timePastBaked = op.Progress.Accumulated - op.Progress.Duration;
                if (timePastBaked > 0 && burnGraceDuration > 0.001f)
                {
                    float burnP = timePastBaked / burnGraceDuration;
                    float clampedBurn = burnP > 1f ? 1f : burnP;
                    if (Math.Abs(pizza.BurnProgress - clampedBurn) > 0.005f)
                    {
                        pizza.BurnProgress = clampedBurn;
                        pizza.Revision++;
                    }

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

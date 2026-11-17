using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public readonly struct NightGoalParameters
    {
        public readonly int NightNumber;
        public readonly float DurationSeconds;
        public readonly int RevenueGoal;
        public readonly float PrepSeconds;
        public readonly float ClosingGraceSeconds;

        public NightGoalParameters(
            int nightNumber,
            float durationSeconds,
            int revenueGoal,
            float prepSeconds = 4f,
            float closingGraceSeconds = 20f)
        {
            NightNumber = Math.Max(1, nightNumber);
            DurationSeconds = Math.Max(30f, durationSeconds);
            RevenueGoal = Math.Max(10, revenueGoal);
            PrepSeconds = Math.Max(0f, prepSeconds);
            ClosingGraceSeconds = Math.Max(0f, closingGraceSeconds);
        }
    }

    /// <summary>
    /// Controlador puro de domínio para o ciclo de noites (O5/N1), derrota operacional,
    /// snapshots de retry e estágios de tarefa em equipe (O7/N5).
    /// </summary>
    public static class NightCycleService
    {
        public static NightGoalParameters GetDefaultParameters(int nightNumber)
        {
            int n = Math.Max(1, nightNumber);
            if (n == 1) return new NightGoalParameters(1, 360f, 60, 4f, 20f);
            if (n == 2) return new NightGoalParameters(2, 390f, 110, 4f, 20f);
            if (n == 3) return new NightGoalParameters(3, 420f, 170, 4f, 20f);
            return new NightGoalParameters(n, 420f, 170 + (n - 3) * 65, 4f, 20f);
        }

        public static NightStartSnapshot CaptureSnapshot(PizzeriaState state)
        {
            if (state == null) return null;

            var snap = new NightStartSnapshot(state.CurrentNightNumber, state.RestaurantBudget);
            foreach (var kvp in state.StorageSlots)
            {
                snap.SavedStock[kvp.Key] = kvp.Value.Quantity;
            }
            foreach (var kvp in state.UpgradeLevels)
            {
                snap.SavedUpgrades[kvp.Key] = kvp.Value;
            }

            state.CurrentSnapshot = snap;
            return snap;
        }

        public static void RestoreSnapshot(PizzeriaState state)
        {
            if (state == null || state.CurrentSnapshot == null) return;

            var snap = state.CurrentSnapshot;
            state.RestaurantBudget = snap.RestaurantBudget;

            foreach (var kvp in snap.SavedStock)
            {
                if (state.StorageSlots.TryGetValue(kvp.Key, out var slot))
                {
                    slot.Quantity = kvp.Value;
                    slot.Revision++;
                }
            }

            state.UpgradeLevels.Clear();
            foreach (var kvp in snap.SavedUpgrades)
            {
                state.UpgradeLevels[kvp.Key] = kvp.Value;
            }

            ClearTransientNightEntities(state, repairBackpacks: true);
            state.IncrementRevision();
        }

        public static void ClearTransientNightEntities(PizzeriaState state, bool repairBackpacks)
        {
            if (state == null) return;

            state.Items.Clear();
            state.Orders.Clear();
            state.ActiveOperations.Clear();
            state.SupplyOrders.Clear();
            state.Calls.Clear();
            state.PlayerHolds.Clear();
            state.Customers.Clear();
            state.QueueLastAdvanceTime.Clear();

            foreach (var tbl in state.Tables.Values)
            {
                tbl.Release();
            }

            foreach (var stack in state.Stacks.Values)
            {
                stack.Items.Clear();
                stack.Revision++;
            }

            foreach (var bp in state.Backpacks.Values)
            {
                bp.Items.Clear();
                bp.EquippedBy = BackpackState.NoOwner;
                if (repairBackpacks)
                {
                    bp.Integrity = bp.MaxIntegrity;
                    bp.HasDroppedBoxOnCurrentRupture = false;
                }
                bp.Revision++;
            }

            int rackStationId = state.ToolRackStationId > 0 ? state.ToolRackStationId : 3;
            var occupiedRackSlots = new HashSet<int>();
            foreach (var tool in state.Tools.Values)
            {
                tool.ForceReleaseLease();
                if (tool.Location.Type == LocationType.StationSlot && tool.Location.StationId == rackStationId)
                {
                    occupiedRackSlots.Add(tool.Location.SlotIndex);
                }
            }

            int fallbackRackIdx = 0;
            foreach (var tool in state.Tools.Values)
            {
                if (tool.Location.Type != LocationType.StationSlot)
                {
                    while (occupiedRackSlots.Contains(fallbackRackIdx))
                        fallbackRackIdx++;

                    tool.Location = LocationRef.InStation(rackStationId, fallbackRackIdx);
                    occupiedRackSlots.Add(fallbackRackIdx);
                    fallbackRackIdx++;
                }
                tool.Revision++;
            }
        }

        public static void StartNight(PizzeriaState state, int nightNumber, NightGoalParameters? customParams = null)
        {
            if (state == null) return;

            var parameters = customParams ?? GetDefaultParameters(nightNumber);
            state.CurrentNightNumber = parameters.NightNumber;
            state.NightDurationSeconds = parameters.DurationSeconds;
            state.NightRemainingSeconds = parameters.DurationSeconds;
            state.NightPrepRemainingSeconds = parameters.PrepSeconds;
            state.NightGraceRemainingSeconds = parameters.ClosingGraceSeconds;
            state.NightRevenueGoal = parameters.RevenueGoal;
            state.NightEarnedRevenue = 0;
            state.NightOrdersCompleted = 0;
            state.NightOrdersFailed = 0;
            state.NightLostCustomers = 0;
            state.NightDefeatReason = NightDefeatReason.None;
            state.CurrentNightPhase = parameters.PrepSeconds > 0f ? NightPhase.Preparing : NightPhase.Active;

            BackpackService.ResetAllBackpacksForNight(state);
            CaptureSnapshot(state);
            state.IncrementRevision();
        }

        public static bool RetryCurrentNight(PizzeriaState state, NightGoalParameters? customParams = null)
        {
            if (state == null) return false;

            int targetNight = state.CurrentSnapshot != null
                ? state.CurrentSnapshot.NightNumber
                : Math.Max(1, state.CurrentNightNumber);

            RestoreSnapshot(state);
            StartNight(state, targetNight, customParams);
            return true;
        }

        public static bool AdvanceToNextNight(PizzeriaState state, NightGoalParameters? customParams = null)
        {
            if (state == null) return false;
            if (state.CurrentNightPhase != NightPhase.Results) return false;

            ClearTransientNightEntities(state, repairBackpacks: true);
            StartNight(state, state.CurrentNightNumber + 1, customParams);
            return true;
        }

        public static bool ForceOperationalDefeat(PizzeriaState state, NightDefeatReason reason)
        {
            if (state == null) return false;
            if (state.CurrentNightPhase == NightPhase.Defeat) return false;

            state.CurrentNightPhase = NightPhase.Defeat;
            state.NightDefeatReason = reason == NightDefeatReason.None
                ? NightDefeatReason.CriticalOperationalFailure
                : reason;
            state.IncrementRevision();
            return true;
        }

        public static bool TickNight(PizzeriaState state, float dt, NightGoalParameters? closingParams = null)
        {
            if (state == null || dt <= 0f) return false;

            bool dirty = false;

            switch (state.CurrentNightPhase)
            {
                case NightPhase.Preparing:
                    state.NightPrepRemainingSeconds = Math.Max(0f, state.NightPrepRemainingSeconds - dt);
                    if (state.NightPrepRemainingSeconds <= 0f)
                    {
                        state.CurrentNightPhase = NightPhase.Active;
                        state.IncrementRevision();
                        dirty = true;
                    }
                    break;

                case NightPhase.Active:
                    state.NightRemainingSeconds = Math.Max(0f, state.NightRemainingSeconds - dt);
                    dirty |= RefreshOrderTaskStages(state);

                    if (state.NightRemainingSeconds <= 0f)
                    {
                        if (HasOpenOrders(state))
                        {
                            float grace = closingParams.HasValue ? closingParams.Value.ClosingGraceSeconds : 20f;
                            state.CurrentNightPhase = NightPhase.Closing;
                            state.NightGraceRemainingSeconds = grace;
                        }
                        else
                        {
                            EvaluateNightOutcome(state);
                        }
                        state.IncrementRevision();
                        dirty = true;
                    }
                    break;

                case NightPhase.Closing:
                    state.NightGraceRemainingSeconds = Math.Max(0f, state.NightGraceRemainingSeconds - dt);
                    dirty |= RefreshOrderTaskStages(state);

                    if (!HasOpenOrders(state) || state.NightGraceRemainingSeconds <= 0f)
                    {
                        EvaluateNightOutcome(state);
                        state.IncrementRevision();
                        dirty = true;
                    }
                    break;
            }

            return dirty;
        }

        public static void EvaluateNightOutcome(PizzeriaState state)
        {
            if (state == null) return;

            // Regra canônica N1: derrota operacional usa apenas receita obtida nesta noite vs meta da noite.
            if (state.NightEarnedRevenue >= state.NightRevenueGoal)
            {
                state.CurrentNightPhase = NightPhase.Results;
                state.NightDefeatReason = NightDefeatReason.None;
            }
            else
            {
                state.CurrentNightPhase = NightPhase.Defeat;
                state.NightDefeatReason = NightDefeatReason.RevenueBelowGoal;
            }
        }

        private static bool HasOpenOrders(PizzeriaState state)
        {
            foreach (var kvp in state.Orders)
            {
                if (kvp.Value.Lifecycle == OrderLifecycle.Accepted)
                    return true;
            }
            return false;
        }

        public static CommandResult ToggleOrderClaim(PizzeriaState state, OrderId orderId, ulong clientId)
        {
            if (state == null || !state.Orders.TryGetValue(orderId, out var order))
                return CommandResult.Fail(CommandError.ItemNotFound, "Comanda não encontrada.");

            if (order.Lifecycle != OrderLifecycle.Accepted)
                return CommandResult.Fail(CommandError.InvalidOperation, "Apenas comandas ativas podem ser assumidas.");

            if (order.ClaimedByClientId == clientId)
            {
                order.ClaimedByClientId = OrderState.UnclaimedClientId;
            }
            else if (order.ClaimedByClientId == OrderState.UnclaimedClientId)
            {
                order.ClaimedByClientId = clientId;
            }
            else
            {
                return CommandResult.Fail(CommandError.Blocked, "Comanda já assumida por outro membro da equipe.");
            }

            order.TaskStage = ComputeOrderTaskStage(state, order);
            order.Revision++;
            state.IncrementRevision();
            return CommandResult.Ok();
        }

        public static bool RefreshOrderTaskStages(PizzeriaState state)
        {
            if (state == null) return false;
            bool changed = false;

            foreach (var kvp in state.Orders)
            {
                var order = kvp.Value;
                var computed = ComputeOrderTaskStage(state, order);
                if (order.TaskStage != computed)
                {
                    order.TaskStage = computed;
                    order.Revision++;
                    changed = true;
                }
            }

            if (changed) state.IncrementRevision();
            return changed;
        }

        public static OrderTaskBoardStage ComputeOrderTaskStage(PizzeriaState state, OrderState order)
        {
            if (order == null) return OrderTaskBoardStage.New;

            if (order.Lifecycle == OrderLifecycle.Completed || order.Lifecycle == OrderLifecycle.Delivered)
                return OrderTaskBoardStage.Completed;

            if (order.Lifecycle == OrderLifecycle.Cancelled || order.Lifecycle == OrderLifecycle.Expired)
                return OrderTaskBoardStage.Failed;

            if (state == null) return OrderTaskBoardStage.New;

            bool hasMatchingLabeledBoxOutForDelivery = false;
            bool hasMatchingReadyBox = false;
            bool hasReadyUnboxedPizzaForDineIn = false;
            bool hasAnyPizzaWorkInProgress = state.ActiveOperations.Count > 0;

            foreach (var item in state.Items.Values)
            {
                if (item.Location.Type == LocationType.Destroyed || item.Location.Type == LocationType.StorageSlot)
                    continue;

                if (item.IsBoxed && item.LabelAddress == order.DestinationId && !order.IsDineIn)
                {
                    if (item.Location.Type == LocationType.Backpack)
                    {
                        hasMatchingLabeledBoxOutForDelivery = true;
                        break;
                    }
                    hasMatchingReadyBox = true;
                }
                else if (order.IsDineIn && item.IsPizzaLike && !item.IsBoxed && item.CookingStage == CookingStage.Baked)
                {
                    hasReadyUnboxedPizzaForDineIn = true;
                }
                else if (item.IsPizzaLike && item.CookingStage != CookingStage.Baked)
                {
                    hasAnyPizzaWorkInProgress = true;
                }
            }

            if (hasMatchingLabeledBoxOutForDelivery)
                return OrderTaskBoardStage.OutForDelivery;

            if (hasMatchingReadyBox || (order.IsDineIn && hasReadyUnboxedPizzaForDineIn))
                return OrderTaskBoardStage.Ready;

            if (order.ClaimedByClientId != OrderState.UnclaimedClientId || hasAnyPizzaWorkInProgress)
                return OrderTaskBoardStage.InPrep;

            return OrderTaskBoardStage.New;
        }
    }
}

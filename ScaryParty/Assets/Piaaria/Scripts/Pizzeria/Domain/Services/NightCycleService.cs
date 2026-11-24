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
        public readonly bool UnlimitedShiftTime;

        public NightGoalParameters(
            int nightNumber,
            float durationSeconds,
            int revenueGoal,
            float prepSeconds = 4f,
            float closingGraceSeconds = 20f,
            bool unlimitedShiftTime = false)
        {
            NightNumber = Math.Max(0, nightNumber);
            DurationSeconds = Math.Max(30f, durationSeconds);
            RevenueGoal = Math.Max(1, revenueGoal);
            PrepSeconds = Math.Max(0f, prepSeconds);
            ClosingGraceSeconds = Math.Max(0f, closingGraceSeconds);
            UnlimitedShiftTime = unlimitedShiftTime;
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
            if (nightNumber <= 0) return new NightGoalParameters(0, 600f, 15, 2f, 0f);
            int n = nightNumber;
            if (n == 1) return new NightGoalParameters(1, 360f, 60, 4f, 20f);
            if (n == 2) return new NightGoalParameters(2, 390f, 110, 4f, 20f);
            if (n == 3) return new NightGoalParameters(3, 420f, 170, 4f, 20f);
            return new NightGoalParameters(n, 420f, 170 + (n - 3) * 65, 4f, 20f);
        }

        public static NightStartSnapshot CaptureSnapshot(PizzeriaState state)
        {
            if (state == null) return null;

            var snap = new NightStartSnapshot(state.CurrentNightNumber, state.RestaurantBudget, state.Reputation);
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
            state.Reputation = Math.Clamp(snap.Reputation, 0, 100);

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

        public static void RebaseRelativeDeadlines(PizzeriaState state, double deltaTimeShift)
        {
            if (state == null || Math.Abs(deltaTimeShift) < 0.0001d) return;

            foreach (var supply in state.SupplyOrders.Values)
            {
                if (supply != null && supply.State == SupplyOrderLifecycle.InTransit)
                {
                    supply.OrderedTime += deltaTimeShift;
                    supply.ExpectedArrivalTime += deltaTimeShift;
                    supply.Revision++;
                }
            }

            foreach (var order in state.Orders.Values)
            {
                if (order != null && order.Lifecycle == OrderLifecycle.Accepted)
                {
                    order.DeadlineTime += deltaTimeShift;
                    order.Revision++;
                }
            }

            foreach (var call in state.Calls.Values)
            {
                if (call != null)
                {
                    call.ExpiresAt += (float)deltaTimeShift;
                }
            }

            if (state.QueueLastAdvanceTime.Count > 0)
            {
                var qKeys = new List<int>(state.QueueLastAdvanceTime.Keys);
                for (int i = 0; i < qKeys.Count; i++)
                {
                    int qk = qKeys[i];
                    state.QueueLastAdvanceTime[qk] += deltaTimeShift;
                }
            }
        }

        public static void ClearTransientNightEntities(PizzeriaState state, bool repairBackpacks, bool preservePendingSupplies = true)
        {
            if (state == null) return;

            state.Items.Clear();
            state.Orders.Clear();
            state.ActiveOperations.Clear();
            if (!preservePendingSupplies)
            {
                state.SupplyOrders.Clear();
            }
            state.Calls.Clear();
            state.PlayerHolds.Clear();
            state.Customers.Clear();
            state.QueueLastAdvanceTime.Clear();
            state.OrderUnits.Clear();
            state.ItemToOrderUnit.Clear();
            state.StationSlotReservations.Clear();

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

            RestoreCanonicalTools(state);
        }

        public static void RegisterCanonicalToolOrigin(
            PizzeriaState state,
            ToolItemState tool,
            int homeStationId,
            int homeSlotIndex)
        {
            if (state == null || tool == null || !tool.Id.IsValid) return;
            state.CanonicalToolOrigins[tool.Id] = new ToolOriginRecord(
                tool.Id,
                tool.DefinitionId,
                tool.Capabilities,
                homeStationId,
                Math.Max(0, homeSlotIndex));
        }

        public static int AllocateNightEntryAttemptId(PizzeriaState state)
        {
            if (state == null) return 1;
            state.NightEntryAttemptCounter++;
            return state.NightEntryAttemptCounter;
        }

        public static bool TryBeginNightEntry(
            PizzeriaState state,
            int nightNumber,
            int attemptId,
            out NightEntryKey entryKey)
        {
            if (state == null)
            {
                entryKey = default;
                return false;
            }

            entryKey = new NightEntryKey(state.GenerationId, nightNumber, attemptId);
            if (state.LastAppliedNightEntryKey.Equals(entryKey))
            {
                return false;
            }

            state.LastAppliedNightEntryKey = entryKey;
            return true;
        }

        public static void RestoreCanonicalTools(PizzeriaState state)
        {
            if (state == null) return;

            if (state.CanonicalToolOrigins.Count > 0)
            {
                var strayIds = new List<ToolItemId>();
                foreach (var kvp in state.Tools)
                {
                    if (!state.CanonicalToolOrigins.ContainsKey(kvp.Key))
                        strayIds.Add(kvp.Key);
                }
                for (int i = 0; i < strayIds.Count; i++)
                {
                    state.Tools.Remove(strayIds[i]);
                }

                foreach (var kvp in state.CanonicalToolOrigins)
                {
                    var origin = kvp.Value;
                    var homeLoc = LocationRef.InStation(origin.HomeStationId, origin.HomeSlotIndex);
                    if (state.Tools.TryGetValue(origin.ToolId, out var existing) && existing != null)
                    {
                        existing.ForceReleaseLease();
                        existing.Location = homeLoc;
                        existing.Revision++;
                    }
                    else
                    {
                        var recreated = new ToolItemState(
                            origin.ToolId,
                            origin.DefinitionId,
                            origin.Capabilities,
                            homeLoc);
                        state.Tools[origin.ToolId] = recreated;
                    }
                }
                return;
            }

            int rackStationId = state.ToolRackStationId > 0 ? state.ToolRackStationId : 3;
            var occupiedRackSlots = new HashSet<int>();
            var needsRelocation = new List<ToolItemState>();

            foreach (var tool in state.Tools.Values)
            {
                if (tool == null) continue;
                tool.ForceReleaseLease();
                if (tool.Location.Type == LocationType.StationSlot &&
                    tool.Location.StationId == rackStationId &&
                    tool.Location.SlotIndex >= 0 &&
                    occupiedRackSlots.Add(tool.Location.SlotIndex))
                {
                    tool.Revision++;
                }
                else
                {
                    needsRelocation.Add(tool);
                }
            }

            int fallbackRackIdx = 0;
            for (int i = 0; i < needsRelocation.Count; i++)
            {
                var tool = needsRelocation[i];
                while (occupiedRackSlots.Contains(fallbackRackIdx))
                    fallbackRackIdx++;

                tool.Location = LocationRef.InStation(rackStationId, fallbackRackIdx);
                occupiedRackSlots.Add(fallbackRackIdx);
                fallbackRackIdx++;
                tool.Revision++;
            }
        }

        public static void StartNight(PizzeriaState state, ResolvedNightParameters resolved)
        {
            if (state == null) return;
            state.FrozenHumanCount = resolved.FrozenHumanCount;
            state.ResolvedBaseGoal = resolved.BaseGoal;
            state.ResolvedHumanScaleFactor = resolved.HumanScaleFactor;
            state.ResolvedModifierMultiplier = resolved.ModifierGoalMultiplier;
            state.ResolvedGoalSource = (byte)resolved.GoalSource;
            state.ActivePresetId = resolved.PresetId;
            state.IsNonLethalTutorialNight = resolved.NonLethalDefeat;
            state.NightReputationMinGoal = resolved.ReputationMinGoal;
            state.NightPizzasMinGoal = resolved.PizzasMinGoal;
            StartNight(state, resolved.NightNumber, resolved.ToNightGoalParameters());
        }

        public static void StartNight(PizzeriaState state, int nightNumber, NightGoalParameters? customParams = null)
        {
            if (state == null) return;

            var parameters = customParams ?? GetDefaultParameters(nightNumber);
            state.CurrentNightNumber = parameters.NightNumber;
            state.TargetNightNumber = parameters.NightNumber;
            state.NightDurationSeconds = parameters.DurationSeconds;
            state.NightRemainingSeconds = parameters.DurationSeconds;
            state.NightElapsedSeconds = 0f;
            state.UnlimitedShiftTime = parameters.UnlimitedShiftTime;
            state.IsShiftClockPaused = false;
            state.ShiftClockSpeedMultiplier = 1f;
            state.IsForcedDebugOutcome = false;
            state.NightPrepRemainingSeconds = parameters.PrepSeconds;
            state.NightGraceRemainingSeconds = parameters.ClosingGraceSeconds;
            state.NightRevenueGoal = parameters.RevenueGoal;
            state.NightEarnedRevenue = 0;
            state.NightSupplySpent = 0;
            state.NightShopSpent = 0;
            state.NightWagesSpent = 0;
            state.NightOrdersCompleted = 0;
            state.NightPizzasDelivered = 0;
            state.NightOrdersFailed = 0;
            state.NightLostCustomers = 0;
            if (parameters.NightNumber <= 0)
            {
                state.IsNonLethalTutorialNight = true;
            }
            if (state.ResolvedBaseGoal <= 0)
            {
                state.ResolvedBaseGoal = parameters.RevenueGoal;
                state.ResolvedHumanScaleFactor = 1f;
                state.ResolvedModifierMultiplier = 1f;
            }
            state.NightDefeatReason = NightDefeatReason.None;
            state.CurrentNightPhase = parameters.PrepSeconds > 0f ? NightPhase.Preparing : NightPhase.Active;

            BackpackService.ResetAllBackpacksForNight(state);
            EnsureMinimumIngredientFloor(state);
            CaptureSnapshot(state);
            state.IncrementRevision();
        }

        public static void EnsureMinimumIngredientFloor(PizzeriaState state, int minUnitsPerBaseSlot = 2)
        {
            if (state == null || minUnitsPerBaseSlot <= 0) return;
            bool changed = false;
            foreach (var kvp in state.StorageSlots)
            {
                int defId = kvp.Key.ingredientDefId;
                // Ingredientes base (Massa=1, Molho=3, QueijoInteiro=4) recebem piso mínimo para evitar soft-lock
                if ((defId == 1 || defId == 3 || defId == 4) && kvp.Value.Quantity < minUnitsPerBaseSlot)
                {
                    kvp.Value.Quantity = Math.Min(kvp.Value.MaxCapacity, minUnitsPerBaseSlot);
                    kvp.Value.Revision++;
                    changed = true;
                }
            }
            if (changed) state.IncrementRevision();
        }

        public static bool TryPlaceSupplyOrder(
            PizzeriaState state,
            ScaryParty.Pizzeria.Domain.Definitions.DefinitionCatalog catalog,
            int supplyItemId,
            int packQuantity,
            double now,
            int maxPendingOrders,
            float etaMultiplier,
            out SupplyOrderState createdOrder,
            out string failureReason)
        {
            createdOrder = null;
            failureReason = null;

            if (state == null || catalog == null)
            {
                failureReason = "Estado da pizzaria indisponível.";
                return false;
            }

            if (packQuantity <= 0 || packQuantity > 10)
            {
                failureReason = "Quantidade de pacotes inválida (1 a 10).";
                return false;
            }

            if (!catalog.SupplyItems.TryGetValue(supplyItemId, out var def) || def == null)
            {
                failureReason = "Item de reposição não encontrado no catálogo.";
                return false;
            }

            int activeCount = 0;
            foreach (var existing in state.SupplyOrders.Values)
            {
                if (existing != null && existing.State != SupplyOrderLifecycle.Unloaded)
                {
                    activeCount++;
                }
            }

            int safeMaxPending = Math.Max(1, maxPendingOrders);
            if (activeCount >= safeMaxPending)
            {
                failureReason = $"Limite de encomendas ativas atingido ({activeCount}/{safeMaxPending}).";
                return false;
            }

            int totalCost = def.PackPrice * packQuantity;
            if (state.RestaurantBudget < totalCost)
            {
                failureReason = $"Saldo insuficiente no caixa coletivo (custo: R$ {totalCost}, caixa: R$ {state.RestaurantBudget}).";
                return false;
            }

            float safeEtaMult = etaMultiplier > 0.05f ? etaMultiplier : 1f;
            float duration = Math.Max(2f, def.BaseETA * safeEtaMult);
            state.RestaurantBudget -= totalCost;
            state.NightSupplySpent += totalCost;

            var id = state.GenerateSupplyOrderId();
            var order = new SupplyOrderState(id, now, now + duration, totalCost);
            order.AddLine(def.Id, def.IngredientId, def.UnitsPerPack, packQuantity);
            order.ReceivingSlotIndex = 0;
            state.SupplyOrders[id] = order;
            state.IncrementRevision();

            createdOrder = order;
            return true;
        }

        public static bool TickSupplies(PizzeriaState state, double now)
        {
            if (state == null || state.SupplyOrders.Count == 0) return false;
            bool changed = false;
            foreach (var order in state.SupplyOrders.Values)
            {
                bool shouldUnload = order.State == SupplyOrderLifecycle.Arrived ||
                                    (order.State == SupplyOrderLifecycle.InTransit && now >= order.ExpectedArrivalTime);
                if (shouldUnload)
                {
                    if (TryUnloadSupplyOrder(state, order, markArrivedIfRemaining: true))
                        changed = true;
                }
            }
            if (changed) state.IncrementRevision();
            return changed;
        }

        public static int TryUnloadAllArrivedSupplies(PizzeriaState state)
        {
            if (state == null) return 0;
            int unloadedUnits = 0;
            foreach (var order in state.SupplyOrders.Values)
            {
                if (order == null || order.State != SupplyOrderLifecycle.Arrived) continue;
                int before = 0;
                foreach (var l in order.Lines) before += l.RemainingUnits;
                TryUnloadSupplyOrder(state, order, markArrivedIfRemaining: true);
                int after = 0;
                foreach (var l in order.Lines) after += l.RemainingUnits;
                unloadedUnits += Math.Max(0, before - after);
            }
            if (unloadedUnits > 0) state.IncrementRevision();
            return unloadedUnits;
        }

        private static bool TryUnloadSupplyOrder(PizzeriaState state, SupplyOrderState order, bool markArrivedIfRemaining)
        {
            var prevState = order.State;
            int movedTotal = 0;

            foreach (var line in order.Lines)
            {
                int remaining = line.RemainingUnits;
                if (remaining <= 0) continue;
                foreach (var slot in state.StorageSlots.Values)
                {
                    if (slot.IngredientDefId == line.IngredientDefId && slot.Quantity < slot.MaxCapacity)
                    {
                        int add = Math.Min(remaining, slot.MaxCapacity - slot.Quantity);
                        slot.Quantity += add;
                        slot.Revision++;
                        remaining -= add;
                        movedTotal += add;
                        if (remaining <= 0) break;
                    }
                }
                line.RemainingUnits = remaining;
            }

            if (markArrivedIfRemaining)
                order.State = order.IsFullyUnloaded() ? SupplyOrderLifecycle.Unloaded : SupplyOrderLifecycle.Arrived;

            bool changed = movedTotal > 0 || order.State != prevState;
            if (changed) order.Revision++;
            return changed;
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

            // Política própria da Noite 0 (Tutorial): não-letal, não encerra nem consome RUN
            if (state.CurrentNightNumber <= 0 || state.IsNonLethalTutorialNight)
            {
                if (state.Reputation <= 0)
                {
                    state.Reputation = 25;
                    state.LastReputationReason = "Tutorial protegido contra derrota.";
                }
                state.IncrementRevision();
                return false;
            }

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
                    dirty |= RefreshOrderTaskStages(state);
                    if (!state.IsShiftClockPaused)
                    {
                        float scaledDt = dt * Math.Max(0.05f, state.ShiftClockSpeedMultiplier);
                        state.NightElapsedSeconds += scaledDt;
                        if (!state.UnlimitedShiftTime)
                        {
                            state.NightRemainingSeconds = Math.Max(0f, state.NightRemainingSeconds - scaledDt);
                            if (state.NightRemainingSeconds <= 0f)
                            {
                                // A06: ao zerar o relógio, liquidar pedidos abertos uma única vez e avaliar resultado sem prorrogação.
                                SettleOpenOrdersAtNightEnd(state);
                                EvaluateNightOutcome(state);
                                state.IsForcedDebugOutcome = false;
                                state.IncrementRevision();
                                dirty = true;
                            }
                        }
                    }
                    break;
            }

            return dirty;
        }

        public static void SettleOpenOrdersAtNightEnd(PizzeriaState state)
        {
            if (state == null) return;

            int expiredCount = 0;
            foreach (var kvp in state.Orders)
            {
                var order = kvp.Value;
                if (order.Lifecycle == OrderLifecycle.Accepted)
                {
                    order.Lifecycle = OrderLifecycle.Expired;
                    order.TaskStage = OrderTaskBoardStage.Failed;
                    expiredCount++;
                    OrderUnitFulfillmentService.OnOrderCancelledOrExpired(state, order.Id);
                }
            }

            if (expiredCount > 0)
            {
                state.NightOrdersFailed += expiredCount;
                AdjustReputation(state, -10 * expiredCount, $"Fim do expediente com {expiredCount} pedido(s) em aberto");
            }
        }

        public static void AdjustReputation(PizzeriaState state, int delta, string reason)
        {
            if (state == null) return;
            state.Reputation = Math.Clamp(state.Reputation + delta, 0, 100);
            state.LastReputationDelta = delta;
            state.LastReputationReason = reason ?? string.Empty;
            if (state.Reputation <= 0 && state.CurrentNightPhase == NightPhase.Active)
            {
                ForceOperationalDefeat(state, NightDefeatReason.ReputationZeroed);
            }
            state.IncrementRevision();
        }

        public static void RecordNightRevenue(PizzeriaState state, int amount)
        {
            if (state == null || amount <= 0) return;
            state.NightEarnedRevenue += amount;
            state.RunTotalRevenue += amount;
            state.RestaurantBudget += amount;
            state.IncrementRevision();
        }

        public static void EvaluateNightOutcome(PizzeriaState state)
        {
            if (state == null) return;
            if (state.CurrentNightPhase == NightPhase.Defeat) return;

            // Tutorial (Noite 0) é não-letal e conclui para tela de resultados/transição para Noite 1
            if (state.CurrentNightNumber <= 0 || state.IsNonLethalTutorialNight)
            {
                if (state.Reputation <= 0) state.Reputation = 25;
                state.CurrentNightPhase = NightPhase.Results;
                state.NightDefeatReason = NightDefeatReason.None;
                return;
            }

            if (state.Reputation <= 0)
            {
                state.CurrentNightPhase = NightPhase.Defeat;
                state.NightDefeatReason = NightDefeatReason.ReputationZeroed;
                return;
            }

            // Derrota operacional usa receita obtida nesta noite vs meta (independente do saldo gasto),
            // além de metas mínimas de reputação e pizzas quando configuradas.
            bool revenueOk = state.NightEarnedRevenue >= state.NightRevenueGoal;
            bool repOk = state.Reputation >= state.NightReputationMinGoal;
            bool pizzasOk = state.NightPizzasDelivered >= state.NightPizzasMinGoal;

            if (revenueOk && repOk && pizzasOk)
            {
                state.CurrentNightPhase = NightPhase.Results;
                state.NightDefeatReason = NightDefeatReason.None;
            }
            else
            {
                state.CurrentNightPhase = NightPhase.Defeat;
                state.NightDefeatReason = revenueOk ? NightDefeatReason.CriticalOperationalFailure : NightDefeatReason.RevenueBelowGoal;
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

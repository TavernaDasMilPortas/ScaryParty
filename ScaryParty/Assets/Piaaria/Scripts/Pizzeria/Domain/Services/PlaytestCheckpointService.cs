#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public enum PlaytestCheckpointKind : byte
    {
        PrePreparation = 0,
        NightStart = 1
    }

    public sealed class PlaytestCheckpointSnapshot
    {
        public PlaytestCheckpointKind Kind { get; set; }
        public int TargetNightNumber { get; set; }
        public int CurrentNightNumber { get; set; }
        public NightPhase Phase { get; set; }
        public int RestaurantBudget { get; set; }
        public int Reputation { get; set; }
        public bool UnlimitedShiftTime { get; set; }
        public int FrozenHumanCount { get; set; }
        public int ResolvedBaseGoal { get; set; }
        public float ResolvedHumanScaleFactor { get; set; }
        public float ResolvedModifierMultiplier { get; set; }
        public byte ResolvedGoalSource { get; set; }
        public string ActivePresetId { get; set; } = string.Empty;
        public int NightRevenueGoal { get; set; }
        public int NightReputationMinGoal { get; set; }
        public int NightPizzasMinGoal { get; set; }
        public bool IsNonLethalTutorialNight { get; set; }
        public float NightDurationSeconds { get; set; }
        public float NightPrepRemainingSeconds { get; set; }
        public int MaxNights { get; set; }
        public RunDifficulty Difficulty { get; set; }
        public double CapturedAtNow { get; set; }

        public Dictionary<(int storageId, int ingredientDefId), int> StorageQuantities { get; } = new Dictionary<(int, int), int>();
        public Dictionary<int, int> UpgradeLevels { get; } = new Dictionary<int, int>();
        public List<(string Id, int Stacks)> ActiveModifiers { get; } = new List<(string, int)>();
        public Dictionary<string, int> ShopRunPurchases { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<(EmployeeRole Role, int Level, bool IsAlive, float BaseErrorRate)> HiredEmployees { get; } = new List<(EmployeeRole, int, bool, float)>();
    }

    /// <summary>
    /// Armazenamento autoritativo em memoria de checkpoints reais no servidor para o Editor Playtest (B5.1).
    /// Isolado via #if UNITY_EDITOR para garantir ausencia total em builds finais.
    /// </summary>
    public sealed class PlaytestCheckpointStore
    {
        private PlaytestCheckpointSnapshot _prePreparationCheckpoint;
        private PlaytestCheckpointSnapshot _nightStartCheckpoint;

        public PlaytestCheckpointSnapshot PrePreparationCheckpoint => _prePreparationCheckpoint;
        public PlaytestCheckpointSnapshot NightStartCheckpoint => _nightStartCheckpoint;

        public void Clear()
        {
            _prePreparationCheckpoint = null;
            _nightStartCheckpoint = null;
        }

        public void Capture(
            PlaytestCheckpointKind kind,
            PizzeriaState state,
            RunProgressionService run,
            EmployeeTaskService employees,
            double now)
        {
            if (state == null) return;
            var snap = new PlaytestCheckpointSnapshot
            {
                Kind = kind,
                TargetNightNumber = state.TargetNightNumber,
                CurrentNightNumber = state.CurrentNightNumber,
                Phase = state.CurrentNightPhase,
                RestaurantBudget = state.RestaurantBudget,
                Reputation = state.Reputation,
                UnlimitedShiftTime = state.UnlimitedShiftTime,
                FrozenHumanCount = state.FrozenHumanCount,
                ResolvedBaseGoal = state.ResolvedBaseGoal,
                ResolvedHumanScaleFactor = state.ResolvedHumanScaleFactor,
                ResolvedModifierMultiplier = state.ResolvedModifierMultiplier,
                ResolvedGoalSource = state.ResolvedGoalSource,
                ActivePresetId = state.ActivePresetId ?? string.Empty,
                NightRevenueGoal = state.NightRevenueGoal,
                NightReputationMinGoal = state.NightReputationMinGoal,
                NightPizzasMinGoal = state.NightPizzasMinGoal,
                IsNonLethalTutorialNight = state.IsNonLethalTutorialNight,
                NightDurationSeconds = state.NightDurationSeconds,
                NightPrepRemainingSeconds = state.NightPrepRemainingSeconds,
                MaxNights = run != null ? run.MaxNights : 3,
                Difficulty = run != null ? run.Difficulty : RunDifficulty.Normal,
                CapturedAtNow = now
            };

            foreach (var kvp in state.StorageSlots)
            {
                if (kvp.Value != null)
                {
                    snap.StorageQuantities[kvp.Key] = kvp.Value.Quantity;
                }
            }

            foreach (var kvp in state.UpgradeLevels)
            {
                snap.UpgradeLevels[kvp.Key] = kvp.Value;
            }

            if (run != null)
            {
                foreach (var mod in run.ActiveModifiers.Values)
                {
                    if (mod != null && mod.Definition != null)
                    {
                        snap.ActiveModifiers.Add((mod.Definition.Id, mod.Stacks));
                    }
                }

                if (run.BalancingSnapshot != null)
                {
                    for (int i = 0; i < run.BalancingSnapshot.ShopOffers.Count; i++)
                    {
                        var o = run.BalancingSnapshot.ShopOffers[i];
                        int count = run.GetShopOfferRunPurchases(o.OfferId);
                        if (count > 0) snap.ShopRunPurchases[o.OfferId] = count;
                    }
                }
            }

            if (employees != null)
            {
                foreach (var emp in employees.Employees.Values)
                {
                    snap.HiredEmployees.Add((emp.Role, emp.Level, emp.IsAlive, emp.BaseErrorRate));
                }
            }

            if (kind == PlaytestCheckpointKind.PrePreparation)
                _prePreparationCheckpoint = snap;
            else
                _nightStartCheckpoint = snap;
        }

        public bool TryRestore(
            PlaytestCheckpointKind kind,
            PizzeriaState state,
            RunProgressionService run,
            EmployeeTaskService employees,
            double currentNow,
            bool preserveCurrentBudget)
        {
            var snap = kind == PlaytestCheckpointKind.PrePreparation ? _prePreparationCheckpoint : _nightStartCheckpoint;
            if (snap == null || state == null) return false;

            int preservedBudget = state.RestaurantBudget;

            NightCycleService.ClearTransientNightEntities(state, repairBackpacks: true, preservePendingSupplies: false);
            state.GenerationId++;
            state.TargetNightNumber = snap.TargetNightNumber;
            state.CurrentNightNumber = snap.CurrentNightNumber;
            state.CurrentNightPhase = snap.Phase;
            state.NightDefeatReason = NightDefeatReason.None;
            state.RestaurantBudget = preserveCurrentBudget ? preservedBudget : snap.RestaurantBudget;
            state.Reputation = snap.Reputation;
            state.UnlimitedShiftTime = snap.UnlimitedShiftTime;
            state.NightElapsedSeconds = 0f;
            state.IsShiftClockPaused = false;
            state.ShiftClockSpeedMultiplier = 1f;
            state.IsForcedDebugOutcome = false;
            state.IsDebugTaintedRun = true;

            state.FrozenHumanCount = snap.FrozenHumanCount;
            state.ResolvedBaseGoal = snap.ResolvedBaseGoal;
            state.ResolvedHumanScaleFactor = snap.ResolvedHumanScaleFactor;
            state.ResolvedModifierMultiplier = snap.ResolvedModifierMultiplier;
            state.ResolvedGoalSource = snap.ResolvedGoalSource;
            state.ActivePresetId = snap.ActivePresetId;
            state.NightRevenueGoal = snap.NightRevenueGoal;
            state.NightReputationMinGoal = snap.NightReputationMinGoal;
            state.NightPizzasMinGoal = snap.NightPizzasMinGoal;
            state.IsNonLethalTutorialNight = snap.IsNonLethalTutorialNight;
            state.NightDurationSeconds = snap.NightDurationSeconds;
            state.NightRemainingSeconds = snap.NightDurationSeconds;
            state.NightPrepRemainingSeconds = snap.NightPrepRemainingSeconds;
            state.NightEarnedRevenue = 0;
            state.NightSupplySpent = 0;
            state.NightShopSpent = 0;
            state.NightWagesSpent = 0;
            state.NightOrdersCompleted = 0;
            state.NightOrdersFailed = 0;
            state.NightLostCustomers = 0;

            foreach (var kvp in snap.StorageQuantities)
            {
                if (state.StorageSlots.TryGetValue(kvp.Key, out var slot))
                {
                    slot.Quantity = Math.Clamp(kvp.Value, 0, slot.MaxCapacity);
                    slot.Revision++;
                }
            }

            state.UpgradeLevels.Clear();
            foreach (var kvp in snap.UpgradeLevels)
            {
                state.UpgradeLevels[kvp.Key] = kvp.Value;
            }

            if (run != null)
            {
                run.RestoreFromCheckpoint(snap.MaxNights, snap.Difficulty, snap.ActiveModifiers, snap.ShopRunPurchases);
            }

            if (employees != null)
            {
                employees.RestoreFromCheckpoint(snap.HiredEmployees);
            }

            state.IncrementRevision();
            return true;
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
        }
    }
}
#endif

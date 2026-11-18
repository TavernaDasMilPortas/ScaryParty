using System.Collections.Generic;
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Tests
{
    [TestFixture]
    public class P3_ConfigurableGoalsAndRunResetTests
    {
        private static RunPresetData CreateCustomPreset(
            string id = "TestPreset",
            bool includeNight0 = false,
            int goalN1_1P = 137,
            int goalN1_2P = 219)
        {
            var rows = new List<NightPresetRowData>
            {
                new NightPresetRowData(1, 300f, new[] { goalN1_1P, goalN1_2P, 295, 360 }, 4f, 15f),
                new NightPresetRowData(2, 300f, new[] { 180, 280, 380, 460 }, 4f, 15f),
                new NightPresetRowData(3, 300f, new[] { 240, 380, 510, 620 }, 4f, 15f)
            };
            return new RunPresetData(
                id,
                totalNights: 3,
                includeNight0Tutorial: includeNight0,
                allowDebugPanel: true,
                difficulty: RunDifficulty.Normal,
                startingBudget: 500,
                startingReputation: 100,
                night0DurationSeconds: 600f,
                night0GoalsByHumanCount: new[] { 15, 15, 15, 15 },
                night0PrepSeconds: 2f,
                night0ClosingGraceSeconds: 0f,
                night0NonLethalDefeat: true,
                nights: rows);
        }

        [Test]
        public void CustomGoal137_InPresetAndNightConfig_AppliesVerbatimWithoutTabulatedOverwrite()
        {
            var state = new PizzeriaState();
            var run = new RunProgressionService(101, 3, RunDifficulty.Normal);
            var preset = CreateCustomPreset(goalN1_1P: 137);

            var resolved = NightParameterResolver.ResolveEffectiveParameters(
                nightNumber: 1,
                connectedHumanCount: 1,
                difficulty: RunDifficulty.Normal,
                preset: preset);

            NightCycleService.StartNight(state, resolved);
            run.FreezeResolvedNightParameters(state, resolved);

            Assert.AreEqual(137, state.NightRevenueGoal, "Meta customizada 137 deve prevalecer sobre fórmula.");
            Assert.AreEqual(GoalResolutionSource.PresetTable, (GoalResolutionSource)state.ResolvedGoalSource);

            // Mesmo com NightConfig puro sem preset (tabela editável com 137), também preserva 137 (sem override por ComputeTabulatedGoal)
            var nightCfgRows = new Dictionary<int, NightPresetRowData>
            {
                { 1, new NightPresetRowData(1, 360f, new[] { 137 }, 4f, 15f) }
            };
            var resolvedCfg = NightParameterResolver.ResolveEffectiveParameters(1, 1, RunDifficulty.Normal, null, nightCfgRows);
            Assert.AreEqual(137, resolvedCfg.FinalGoal);
            Assert.AreEqual(GoalResolutionSource.NightConfigTable, resolvedCfg.GoalSource);

            // Metas pequenas como 5 (no mínimo 1) não sofrem clamp artificial para 10
            var resolvedSmall = NightParameterResolver.ResolveEffectiveParameters(
                1, 1, RunDifficulty.Normal,
                CreateCustomPreset(goalN1_1P: 5));
            Assert.AreEqual(5, resolvedSmall.FinalGoal);
        }

        [Test]
        public void TutorialNight0_Goal15_Remains15AcrossStartupAndRestart_AndIsNonLethal()
        {
            var state = new PizzeriaState();
            var run = new RunProgressionService(202, 3, RunDifficulty.Normal);
            var preset = CreateCustomPreset(includeNight0: true);

            // Startup
            var rStartup = NightParameterResolver.ResolveEffectiveParameters(0, 2, RunDifficulty.Hard, preset);
            NightCycleService.StartNight(state, rStartup);
            run.FreezeResolvedNightParameters(state, rStartup);
            Assert.AreEqual(0, state.CurrentNightNumber);
            Assert.AreEqual(15, state.NightRevenueGoal);

            // Política não-letal: reputação zerada ou derrota operacional não joga tutorial para Defeat
            NightCycleService.ForceOperationalDefeat(state, NightDefeatReason.AllPlayersIncapacitated);
            Assert.AreNotEqual(NightPhase.Defeat, state.CurrentNightPhase);
            NightCycleService.EvaluateNightOutcome(state);
            Assert.AreEqual(NightPhase.Results, state.CurrentNightPhase);

            // Restart da Noite 0 mantém 15 (não cai na fórmula da Noite 1)
            run.ResetRun(state, 303, 3, RunDifficulty.Hard, 500, 100);
            var rRestart = NightParameterResolver.ResolveEffectiveParameters(0, 2, RunDifficulty.Hard, preset);
            NightCycleService.StartNight(state, rRestart);
            run.FreezeResolvedNightParameters(state, rRestart);
            Assert.AreEqual(0, state.CurrentNightNumber);
            Assert.AreEqual(15, state.NightRevenueGoal, "Tutorial reiniciado não pode herdar meta da Noite 1.");
        }

        [Test]
        public void TwoHumans_StartupVsRestartParity_AndEmployeesNeverInflateHumanCount()
        {
            var preset = CreateCustomPreset(goalN1_1P: 60, goalN1_2P: 96);
            var tasks = new EmployeeTaskService();
            var s1 = new PizzeriaState { RestaurantBudget = 1000 };
            tasks.TryHireEmployee(s1, EmployeeRole.Cashier);
            tasks.TryHireEmployee(s1, EmployeeRole.Pizzaiolo);

            // Contagem de humanos deve ignorar os 2 funcionários contratados (2 humanos reais, não 4)
            var rStartup = NightParameterResolver.ResolveEffectiveParameters(1, connectedHumanCount: 2, RunDifficulty.Normal, preset);
            NightCycleService.StartNight(s1, rStartup);

            var s2 = new PizzeriaState { RestaurantBudget = 1000 };
            var run = new RunProgressionService(1, 3, RunDifficulty.Normal);
            run.ResetRun(s2, 2, 3, RunDifficulty.Normal);
            var rRestart = NightParameterResolver.ResolveEffectiveParameters(1, connectedHumanCount: 2, RunDifficulty.Normal, preset);
            NightCycleService.StartNight(s2, rRestart);
            run.FreezeResolvedNightParameters(s2, rRestart);

            Assert.AreEqual(s1.NightRevenueGoal, s2.NightRevenueGoal);
            Assert.AreEqual(96, s1.NightRevenueGoal);
            Assert.AreEqual(2, s1.FrozenHumanCount);
        }

        [Test]
        public void Night0ToNight1Transition_PreservesAllThreeProductionNights()
        {
            var preset = CreateCustomPreset(includeNight0: true, goalN1_1P: 60);
            var state = new PizzeriaState();
            var run = new RunProgressionService(55, 3, RunDifficulty.Normal);

            // Começa em Noite 0
            var r0 = NightParameterResolver.ResolveEffectiveParameters(0, 1, RunDifficulty.Normal, preset);
            NightCycleService.StartNight(state, r0);
            Assert.AreEqual(0, state.CurrentNightNumber);

            // Sai do tutorial para Noite 1 (nova geração limpa, 3 noites intactas: 1, 2, 3)
            run.ResetRun(state, 56, preset.TotalNights, RunDifficulty.Normal, preset.StartingBudget, preset.StartingReputation);
            var r1 = NightParameterResolver.ResolveEffectiveParameters(1, 1, RunDifficulty.Normal, preset);
            NightCycleService.StartNight(state, r1);
            run.FreezeResolvedNightParameters(state, r1);

            Assert.AreEqual(1, state.CurrentNightNumber);
            Assert.AreEqual(3, run.MaxNights);
            state.NightEarnedRevenue = state.NightRevenueGoal;
            NightCycleService.EvaluateNightOutcome(state);
            Assert.IsTrue(run.TryCompleteNightAndAdvance(state), "Após Noite 1 deve haver Noite 2.");
            Assert.AreEqual(2, state.CurrentNightNumber);
            state.NightEarnedRevenue = state.NightRevenueGoal;
            NightCycleService.EvaluateNightOutcome(state);
            Assert.IsTrue(run.TryCompleteNightAndAdvance(state), "Após Noite 2 deve haver Noite 3.");
            Assert.AreEqual(3, state.CurrentNightNumber);
            state.NightEarnedRevenue = state.NightRevenueGoal;
            NightCycleService.EvaluateNightOutcome(state);
            Assert.IsFalse(run.TryCompleteNightAndAdvance(state));
            Assert.IsTrue(run.IsRunVictory);
        }

        [Test]
        public void RepeatedNewRun_ClearsModifiersBudgetExpenses_AndEnforcesShopMaxStacks()
        {
            var state = new PizzeriaState { RestaurantBudget = 500 };
            var run = new RunProgressionService(10, 3, RunDifficulty.Normal);

            // Compra Bolsa Reforçada até o teto MaxStacks = 2
            var defBag = run.GetModifierDefinition("reinforced_bag");
            Assert.IsNotNull(defBag);
            Assert.IsTrue(run.CanApplyModifierStack("reinforced_bag"));
            Assert.IsTrue(run.TryPurchaseShopItem(state, new RunShopItem { Id = "bag1", Name = "Bag", Cost = 75 }));
            run.ApplyModifier(defBag);
            Assert.IsTrue(run.CanApplyModifierStack("reinforced_bag"));
            Assert.IsTrue(run.TryPurchaseShopItem(state, new RunShopItem { Id = "bag2", Name = "Bag", Cost = 75 }));
            run.ApplyModifier(defBag);
            Assert.IsFalse(run.CanApplyModifierStack("reinforced_bag"), "MaxStacks=2 deve bloquear 3a compra.");
            Assert.AreEqual(150, state.NightShopSpent);

            state.NightSupplySpent = 40;
            state.NightWagesSpent = 120;
            state.IsDebugTaintedRun = true;

            // Nova RUN limpa tudo sem duplicar modificadores
            run.ResetRun(state, 99, 3, RunDifficulty.Normal, startingBudget: 500, initialReputation: 100);
            Assert.AreEqual(500, state.RestaurantBudget);
            Assert.AreEqual(0, state.RunTotalRevenue);
            Assert.AreEqual(0, state.NightSupplySpent);
            Assert.AreEqual(0, state.NightShopSpent);
            Assert.AreEqual(0, state.NightWagesSpent);
            Assert.IsFalse(state.IsDebugTaintedRun);
            Assert.AreEqual(0, run.ActiveModifiers.Count);
            Assert.AreEqual(0, run.RecalculateEffectiveStats().ExtraBackpackCapacity);
            Assert.IsTrue(run.CanApplyModifierStack("reinforced_bag"));
        }
    }
}

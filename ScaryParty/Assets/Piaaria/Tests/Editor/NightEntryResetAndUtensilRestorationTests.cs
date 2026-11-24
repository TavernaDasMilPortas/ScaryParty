using System.Collections.Generic;
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Tests
{
    [TestFixture]
    public class NightEntryResetAndUtensilRestorationTests
    {
        [Test]
        public void CanonicalUtensils_RestoreAcrossHandWorldBenchEmployeeMissing_WithoutDuplication()
        {
            var state = new PizzeriaState { ToolRackStationId = 3 };

            var tHand = new ToolItemState(new ToolItemId(1), 1, ToolCapability.Grate, LocationRef.InStation(3, 0));
            var tWorld = new ToolItemState(new ToolItemId(2), 2, ToolCapability.Slice, LocationRef.InStation(3, 1));
            var tBench = new ToolItemState(new ToolItemId(3), 1, ToolCapability.Grate, LocationRef.InStation(3, 2));
            var tEmployee = new ToolItemState(new ToolItemId(4), 2, ToolCapability.Slice, LocationRef.InStation(3, 3));
            var tMissing = new ToolItemState(new ToolItemId(5), 2, ToolCapability.Slice, LocationRef.InStation(3, 4));

            var tools = new[] { tHand, tWorld, tBench, tEmployee, tMissing };
            for (int i = 0; i < tools.Length; i++)
            {
                state.Tools[tools[i].Id] = tools[i];
                NightCycleService.RegisterCanonicalToolOrigin(state, tools[i], 3, i);
            }

            // 1) Segurado na mão do jogador com lease ativo
            tHand.Location = LocationRef.InHand(0UL, HandSlotIndex.Left);
            Assert.IsTrue(tHand.TryAcquireLease(0UL, 10.0));

            // 2) Largado no chão (World)
            tWorld.Location = LocationRef.InWorld(4f, 0f, -2f);

            // 3) Deixado em outra bancada (PrepStation id=7)
            tBench.Location = LocationRef.InStation(7, 1);

            // 4) Em uso por funcionário (actorKey >= 1_000_000) com lease ativo
            tEmployee.Location = LocationRef.InHand(1_000_002UL, HandSlotIndex.Right);
            Assert.IsTrue(tEmployee.TryAcquireLease(1_000_002UL, 10.0));

            // 5) Desaparecido / despawnado
            state.Tools.Remove(tMissing.Id);

            // 6) Injetar item órfão / duplicado sem origem canônica
            state.Tools[new ToolItemId(999)] = new ToolItemState(
                new ToolItemId(999), 2, ToolCapability.Slice, LocationRef.InStation(3, 0));

            NightCycleService.ClearTransientNightEntities(state, repairBackpacks: true);
            NightCycleService.RestoreCanonicalTools(state);

            // Deve haver exatamente os 5 utensílios canônicos, sem duplicação e sem órfãos
            Assert.AreEqual(5, state.Tools.Count);
            Assert.IsFalse(state.Tools.ContainsKey(new ToolItemId(999)));

            var seenSlots = new HashSet<int>();
            for (int i = 0; i < tools.Length; i++)
            {
                var id = tools[i].Id;
                Assert.IsTrue(state.Tools.TryGetValue(id, out var restored), $"Utensílio {id} ausente após restauração.");
                Assert.AreEqual(LocationType.StationSlot, restored.Location.Type);
                Assert.AreEqual(3, restored.Location.StationId);
                Assert.AreEqual(i, restored.Location.SlotIndex);
                Assert.AreEqual(0UL, restored.ReservedByWorkerId);
                Assert.IsTrue(seenSlots.Add(restored.Location.SlotIndex), "Colisão de slot no rack.");
            }
        }

        [Test]
        public void TryBeginNightEntry_IsIdempotentPerGenerationNightAndAttempt()
        {
            var state = new PizzeriaState { GenerationId = 1 };
            int attempt1 = NightCycleService.AllocateNightEntryAttemptId(state);

            Assert.IsTrue(NightCycleService.TryBeginNightEntry(state, 1, attempt1, out _));
            Assert.IsFalse(NightCycleService.TryBeginNightEntry(state, 1, attempt1, out _), "Callback duplicado não deve reexecutar reset.");

            // Retry na mesma noite aloca novo attemptId e deve executar
            int attempt2 = NightCycleService.AllocateNightEntryAttemptId(state);
            Assert.AreNotEqual(attempt1, attempt2);
            Assert.IsTrue(NightCycleService.TryBeginNightEntry(state, 1, attempt2, out _));

            // Entrada na Noite 2 também deve executar
            int attempt3 = NightCycleService.AllocateNightEntryAttemptId(state);
            Assert.IsTrue(NightCycleService.TryBeginNightEntry(state, 2, attempt3, out _));
        }

        [Test]
        public void NightEntryAndRetry_PreserveMoneyReputationUpgradesAndStock_WhileClearingTransients()
        {
            var state = new PizzeriaState
            {
                RestaurantBudget = 500,
                Reputation = 85,
                ToolRackStationId = 3
            };
            state.StorageSlots[(1, 1)] = new StorageSlotState(1, 1, 8, 20);
            state.UpgradeLevels[1] = 2;

            var tool = new ToolItemState(new ToolItemId(1), 1, ToolCapability.Grate, LocationRef.InStation(3, 0));
            state.Tools[tool.Id] = tool;
            NightCycleService.RegisterCanonicalToolOrigin(state, tool, 3, 0);

            NightCycleService.StartNight(state, 1);

            // Mutar durante a Noite 1: faturamento, reputação, estoque consumido, upgrade comprado e utensílio na mão
            state.RestaurantBudget = 680;
            state.Reputation = 78;
            state.NightEarnedRevenue = 90;
            state.StorageSlots[(1, 1)].Quantity = 5;
            state.UpgradeLevels[1] = 3;
            tool.Location = LocationRef.InHand(0UL, 0);
            state.Items[state.GenerateItemId()] = new ItemState(new ItemId(1), 1, ItemCategory.Dough, LocationRef.InHand(0UL, HandSlotIndex.Right));
            NightCycleService.EvaluateNightOutcome(state);
            Assert.AreEqual(NightPhase.Results, state.CurrentNightPhase);

            // Avançar para Noite 2 preserva dinheiro, reputação, upgrades e estoque atuais; limpa itens e restaura utensílio
            Assert.IsTrue(NightCycleService.AdvanceToNextNight(state));
            Assert.AreEqual(2, state.CurrentNightNumber);
            Assert.AreEqual(680, state.RestaurantBudget);
            Assert.AreEqual(78, state.Reputation);
            Assert.AreEqual(3, state.UpgradeLevels[1]);
            Assert.AreEqual(5, state.StorageSlots[(1, 1)].Quantity);
            Assert.AreEqual(0, state.Items.Count);
            Assert.AreEqual(LocationType.StationSlot, state.Tools[tool.Id].Location.Type);
            Assert.AreEqual(3, state.Tools[tool.Id].Location.StationId);
            Assert.AreEqual(0, state.Tools[tool.Id].Location.SlotIndex);

            // Simular derrota na Noite 2 com gasto de caixa/estoque e utensílio perdido -> Retry restaura snapshot da Noite 2
            state.RestaurantBudget = 400;
            state.StorageSlots[(1, 1)].Quantity = 1;
            state.UpgradeLevels[1] = 4;
            state.Tools.Remove(tool.Id);
            state.NightEarnedRevenue = 10;
            NightCycleService.EvaluateNightOutcome(state);
            Assert.AreEqual(NightPhase.Defeat, state.CurrentNightPhase);

            Assert.IsTrue(NightCycleService.RetryCurrentNight(state));
            Assert.AreEqual(2, state.CurrentNightNumber);
            Assert.AreEqual(680, state.RestaurantBudget);
            Assert.AreEqual(3, state.UpgradeLevels[1]);
            Assert.AreEqual(5, state.StorageSlots[(1, 1)].Quantity);
            Assert.AreEqual(1, state.Tools.Count);
            Assert.AreEqual(LocationType.StationSlot, state.Tools[tool.Id].Location.Type);
            Assert.AreEqual(0, state.Tools[tool.Id].Location.SlotIndex);
        }
    }
}

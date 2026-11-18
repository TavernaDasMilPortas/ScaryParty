using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Tests
{
    [TestFixture]
    public class P4_SupplyAndUiDomainTests
    {
        [Test]
        public void SimultaneousSupplyPurchases_WithoutEnoughBudgetForBoth_AllowsOnlyFirstAndPreservesState()
        {
            var state = new PizzeriaState { RestaurantBudget = 30 };
            var catalog = new DefinitionCatalog();
            catalog.RegisterSupplyItem(new SupplyItemData(1, 1, 5, 20, 18f, "Pacote Massa"));

            bool firstOk = NightCycleService.TryPlaceSupplyOrder(
                state, catalog, 1, 1, now: 10.0, maxPendingOrders: 3, etaMultiplier: 0.6f,
                out var firstOrder, out string firstErr);

            bool secondOk = NightCycleService.TryPlaceSupplyOrder(
                state, catalog, 1, 1, now: 10.0, maxPendingOrders: 3, etaMultiplier: 0.6f,
                out var secondOrder, out string secondErr);

            Assert.IsTrue(firstOk, firstErr);
            Assert.IsNotNull(firstOrder);
            Assert.AreEqual(10, state.RestaurantBudget);
            Assert.AreEqual(20, state.NightSupplySpent);
            Assert.AreEqual(SupplyOrderLifecycle.InTransit, firstOrder.State);
            Assert.That(firstOrder.ExpectedArrivalTime, Is.EqualTo(10.0 + 18.0 * 0.6).Within(0.05));

            Assert.IsFalse(secondOk);
            Assert.IsNull(secondOrder);
            StringAssert.Contains("Saldo insuficiente", secondErr);
            Assert.AreEqual(10, state.RestaurantBudget);
            Assert.AreEqual(1, state.SupplyOrders.Count);
        }

        [Test]
        public void SupplyOrders_RespectMaxPendingCap_AndUnloadIntoStorageOnTick()
        {
            var state = new PizzeriaState { RestaurantBudget = 500 };
            state.StorageSlots[(2, 1)] = new StorageSlotState(2, 1, 2, 20);
            var catalog = new DefinitionCatalog();
            catalog.RegisterSupplyItem(new SupplyItemData(1, 1, 5, 10, 10f, "Pacote Massa"));

            Assert.IsTrue(NightCycleService.TryPlaceSupplyOrder(state, catalog, 1, 1, 0.0, maxPendingOrders: 2, 1f, out var o1, out _));
            Assert.IsTrue(NightCycleService.TryPlaceSupplyOrder(state, catalog, 1, 1, 1.0, maxPendingOrders: 2, 1f, out var o2, out _));
            Assert.IsFalse(NightCycleService.TryPlaceSupplyOrder(state, catalog, 1, 1, 2.0, maxPendingOrders: 2, 1f, out _, out string capErr));
            StringAssert.Contains("Limite de encomendas", capErr);

            Assert.IsTrue(NightCycleService.TickSupplies(state, 12.0));
            Assert.AreEqual(SupplyOrderLifecycle.Unloaded, o1.State);
            Assert.AreEqual(SupplyOrderLifecycle.Unloaded, o2.State);
            Assert.AreEqual(12, state.StorageSlots[(2, 1)].Quantity);
        }

        [Test]
        public void ShopUpgrades_RespectMaxStacks_AndTieVoteResolvesDeterministically()
        {
            var run = new RunProgressionService(seed: 4242, maxNights: 3, difficulty: RunDifficulty.Normal);
            var bagDef = run.GetModifierDefinition("reinforced_bag");
            Assert.IsNotNull(bagDef);
            Assert.AreEqual(2, bagDef.MaxStacks);

            Assert.IsTrue(run.CanApplyModifierStack("reinforced_bag"));
            run.ApplyModifier(bagDef);
            Assert.IsTrue(run.CanApplyModifierStack("reinforced_bag"));
            run.ApplyModifier(bagDef);
            Assert.IsFalse(run.CanApplyModifierStack("reinforced_bag"));

            var opts = run.OpenChoiceVote(2, 2);
            Assert.AreEqual(2, opts.Count);
            run.CastVote(1UL, opts[0].Id);
            run.CastVote(2UL, opts[1].Id);
            var winnerA = run.ResolveVoteServer();

            var run2 = new RunProgressionService(seed: 4242, maxNights: 3, difficulty: RunDifficulty.Normal);
            var bagDef2 = run2.GetModifierDefinition("reinforced_bag");
            run2.ApplyModifier(bagDef2);
            run2.ApplyModifier(bagDef2);
            var opts2 = run2.OpenChoiceVote(2, 2);
            run2.CastVote(1UL, opts2[0].Id);
            run2.CastVote(2UL, opts2[1].Id);
            var winnerB = run2.ResolveVoteServer();

            Assert.IsNotNull(winnerA);
            Assert.IsNotNull(winnerB);
            Assert.AreEqual(winnerA.Id, winnerB.Id);
        }
    }
}

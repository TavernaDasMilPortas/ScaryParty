using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Tests
{
    [TestFixture]
    public class PizzeriaDomainTests
    {
        private PizzeriaState _state;
        private DefinitionCatalog _catalog;
        private SimulatedClock _clock;
        private TransferService _transferService;
        private ProcessingService _processingService;
        private RecipeEvaluator _recipeEvaluator;
        private DeliveryEvaluator _deliveryEvaluator;
        private UpgradeService _upgradeService;

        [SetUp]
        public void Setup()
        {
            _state = new PizzeriaState();
            _catalog = new DefinitionCatalog();
            _clock = new SimulatedClock { CurrentTime = 100.0 };

            // Setup Catalog definitions
            // Ingredients
            _catalog.RegisterIngredient(new IngredientData(1, "Massa", ItemCategory.Dough, true, 2));
            _catalog.RegisterIngredient(new IngredientData(2, "Massa Aberta", ItemCategory.Dough));
            _catalog.RegisterIngredient(new IngredientData(3, "Molho", ItemCategory.Sauce));
            _catalog.RegisterIngredient(new IngredientData(4, "Queijo Inteiro", ItemCategory.Cheese, true, 5));
            _catalog.RegisterIngredient(new IngredientData(5, "Queijo Ralado", ItemCategory.Cheese));
            _catalog.RegisterIngredient(new IngredientData(6, "Calabresa Inteira", ItemCategory.Topping, true, 7));
            _catalog.RegisterIngredient(new IngredientData(7, "Calabresa Fatiada", ItemCategory.Topping));
            _catalog.RegisterIngredient(new IngredientData(8, "Cogumelo Inteiro", ItemCategory.Topping, true, 9));
            _catalog.RegisterIngredient(new IngredientData(9, "Cogumelo Fatiado", ItemCategory.Topping));

            // Tools
            _catalog.RegisterTool(new ToolData(1, "Ralador", ToolCapability.Grate, 1));
            _catalog.RegisterTool(new ToolData(2, "Faca", ToolCapability.Slice, 1));

            // Processes
            _catalog.RegisterProcess(new ProcessData(1, "Abrir Massa", 1, ProcessingStage.Raw, 2, ProcessingStage.Prepared, ToolCapability.None, 3.0f));
            _catalog.RegisterProcess(new ProcessData(2, "Ralar Queijo", 4, ProcessingStage.Raw, 5, ProcessingStage.Prepared, ToolCapability.Grate, 3.0f));
            _catalog.RegisterProcess(new ProcessData(3, "Fatiar Calabresa", 6, ProcessingStage.Raw, 7, ProcessingStage.Prepared, ToolCapability.Slice, 2.0f));

            // Stations
            _catalog.RegisterStation(new StationData(1, "Preparo A", 2, new[] { 1, 2, 3 }));
            // Station B with override of 2s for Grate
            _catalog.RegisterStation(new StationData(2, "Preparo B", 2, new[] { 2 }, new System.Collections.Generic.Dictionary<int, float> { { 2, 2.0f } }));

            // Recipes
            // Mussarela: Massa Aberta (2), Molho (3), Queijo Ralado (5) -> $15
            _catalog.RegisterRecipe(new RecipeData(1, "Mussarela", 2, new[]
            {
                new IngredientEntry(3, ProcessingStage.Raw, 1),
                new IngredientEntry(5, ProcessingStage.Prepared, 1)
            }, 15, 15f));

            // Calabresa: Massa Aberta (2), Molho (3), Queijo Ralado (5), Calabresa Fatiada (7) -> $20
            _catalog.RegisterRecipe(new RecipeData(2, "Calabresa", 2, new[]
            {
                new IngredientEntry(3, ProcessingStage.Raw, 1),
                new IngredientEntry(5, ProcessingStage.Prepared, 1),
                new IngredientEntry(7, ProcessingStage.Prepared, 1)
            }, 20, 15f));

            // Services
            _transferService = new TransferService(_state, _catalog);
            _processingService = new ProcessingService();
            _recipeEvaluator = new RecipeEvaluator();
            _deliveryEvaluator = new DeliveryEvaluator();
            _upgradeService = new UpgradeService();
        }

        [Test]
        public void ToolLease_WorkerAcquiresAndReleasesTool_CannotBeStolenWhileActive()
        {
            // Setup tool in hand of worker 101
            var toolId = _state.GenerateToolId();
            var tool = new ToolItemState(toolId, 1, ToolCapability.Grate, LocationRef.InHand(101, HandSlotIndex.Left));
            _state.Tools[toolId] = tool;

            // Setup cheese in station slot (1, 0)
            var cheeseId = _state.GenerateItemId();
            var cheese = new ItemState(cheeseId, 4, ItemCategory.Cheese, LocationRef.InStation(1, 0));
            _state.Items[cheeseId] = cheese;

            var slot = new StationSlotId(1, 0);

            // Worker 101 starts work
            var startRes = _processingService.StartWork(slot, 2, 101, _state, _catalog, _clock);
            Assert.IsTrue(startRes.Success, startRes.Message);
            Assert.IsTrue(tool.IsReserved(_clock.Now));
            Assert.AreEqual(101, tool.ReservedByWorkerId);

            // Another worker (202) tries to start work on the same tool -> ToolBusy
            var toolLeaseRes = tool.TryAcquireLease(202, _clock.Now, 0.75);
            Assert.IsFalse(toolLeaseRes, "Worker 202 should not acquire lease while 101 is working");

            // Advance clock slightly (0.5s) and complete work
            _clock.Advance(3.0);
            var compRes = _processingService.CompleteWork(slot, _state, _catalog);
            Assert.IsTrue(compRes.Success, compRes.Message);

            // Cheese should now be grated (defId 5, Prepared)
            Assert.AreEqual(5, cheese.DefinitionId);
            Assert.AreEqual(ProcessingStage.Prepared, cheese.PrepStage);

            // Tool lease should now be released
            Assert.IsFalse(tool.IsReserved(_clock.Now));
        }

        [Test]
        public void PermissiveAssembly_AllowsRawIngredientsAndAnyCombinationWithoutExceptions()
        {
            var pizzaId = _state.GenerateItemId();
            var pizza = new ItemState(pizzaId, 2, ItemCategory.Dough, LocationRef.InStation(3, 0));
            _state.Items[pizzaId] = pizza;

            // Add raw cheese (not grated) - should succeed physically!
            bool add1 = pizza.AddIngredient(4, ProcessingStage.Raw, 1);
            Assert.IsTrue(add1);

            // Add sauce
            bool add2 = pizza.AddIngredient(3, ProcessingStage.Raw, 1);
            Assert.IsTrue(add2);

            // Pizza should NOT match Mussarela exact (because cheese is Raw, not Prepared)
            var recipe = _catalog.Recipes[1];
            bool matches = _recipeEvaluator.MatchesRecipeExact(pizza, recipe);
            Assert.IsFalse(matches, "Raw cheese pizza must not match Mussarela exact");
        }

        [Test]
        public void DeliveryEvaluator_MultiPizzaOrder_EvaluatesBatchWithProportionalPayout()
        {
            var order = new OrderState(new OrderId(1), OrderSourceType.Phone, 4, CustomerId.None, 100.0, 400.0);
            order.AddLine(new OrderLineId(1), 1, 1); // 1x Mussarela ($15)
            order.AddLine(new OrderLineId(2), 2, 1); // 1x Calabresa ($20)
            // Total potential = $35

            // Create delivered items: 1 perfect Calabresa (Baked), 1 burned Mussarela (Burned)
            var p1 = new ItemState(new ItemId(1), 2, ItemCategory.Pizza, LocationRef.InHand(1, HandSlotIndex.Right))
            {
                CookingStage = CookingStage.Baked
            };
            p1.AddIngredient(3, ProcessingStage.Raw, 1);
            p1.AddIngredient(5, ProcessingStage.Prepared, 1);
            p1.AddIngredient(7, ProcessingStage.Prepared, 1); // Calabresa exact

            var p2 = new ItemState(new ItemId(2), 2, ItemCategory.Pizza, LocationRef.InHand(1, HandSlotIndex.Left))
            {
                CookingStage = CookingStage.Burned
            };
            p2.AddIngredient(3, ProcessingStage.Raw, 1);
            p2.AddIngredient(5, ProcessingStage.Prepared, 1); // Mussarela exact, but burned!

            var delivered = new[] { p1, p2 };

            var eval = _deliveryEvaluator.EvaluateDelivery(order, delivered, _catalog, burnedPayoutFactor: 0f, restaurantBaseBonus: 10);

            // Expected: Calabresa earns $20, Mussarela burned earns $0 -> Personal = $20
            Assert.AreEqual(20, eval.PersonalEarned);
            Assert.AreEqual(35, eval.TotalPotential);
            Assert.AreEqual(1, eval.CorrectBakedCount);
            Assert.AreEqual(1, eval.BurnedCount);
            Assert.AreEqual(OrderLifecycle.Completed, order.Lifecycle);
            // Restaurant reward: 10 * (20/35) = ~6
            Assert.AreEqual(6, eval.RestaurantEarned);
        }

        [Test]
        public void UpgradeService_FasterOvenAndExtraSlots_AppliesFormulaCorrectly()
        {
            // FasterOven: -20% PercentAdd
            _catalog.RegisterUpgrade(new UpgradeData(1, "FasterOven", UpgradeScope.Oven, "cookDuration", UpgradeOperation.PercentAdd, -0.20f));
            // ExtraOvenSlot: +1 FlatAdd
            _catalog.RegisterUpgrade(new UpgradeData(2, "ExtraOvenSlot", UpgradeScope.Oven, "slotCount", UpgradeOperation.FlatAdd, 1.0f));

            _state.UpgradeLevels[1] = 1; // Level 1 FasterOven
            _state.UpgradeLevels[2] = 1; // Level 1 ExtraSlot

            float effectiveDuration = _upgradeService.ComputeEffectiveValue(15f, UpgradeScope.Oven, "cookDuration", _state, _catalog);
            // 15 * (1 - 0.20) = 12.0s
            Assert.AreEqual(12.0f, effectiveDuration, 0.01f);

            float effectiveSlots = _upgradeService.ComputeEffectiveValue(2f, UpgradeScope.Oven, "slotCount", _state, _catalog, isIntegerStat: true);
            // 2 + 1 = 3 slots
            Assert.AreEqual(3.0f, effectiveSlots);
        }

        [Test]
        public void TransferService_SingleLocationInvariant_BlocksDoubleOccupancy()
        {
            var handRight = LocationRef.InHand(99, HandSlotIndex.Right);
            var item1 = new ItemState(new ItemId(1), 1, ItemCategory.Dough, handRight);
            _state.Items[item1.Id] = item1;

            var item2 = new ItemState(new ItemId(2), 3, ItemCategory.Sauce, LocationRef.InWorld(0, 0, 0));
            _state.Items[item2.Id] = item2;

            // Attempt to move item2 to handRight (which is already occupied by item1)
            var res = _transferService.TransferItem(item2.Id, handRight);
            Assert.IsFalse(res.Success);
            Assert.AreEqual(CommandError.SlotFull, res.Error);
            Assert.AreEqual(LocationRef.InWorld(0, 0, 0), item2.Location, "item2 must not change location on failure");
        }
    }
}

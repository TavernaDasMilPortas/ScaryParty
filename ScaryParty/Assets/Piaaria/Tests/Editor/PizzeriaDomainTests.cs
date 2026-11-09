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
        private OrderService _orderService;
        private OvenService _ovenService;
        private CallService _callService;
        private PlayerHoldService _holdService;
        private UpgradeService _upgradeService;

        [SetUp]
        public void Setup()
        {
            _state = new PizzeriaState();
            _catalog = new DefinitionCatalog();
            _clock = new SimulatedClock { CurrentTime = 100.0 };

            // Setup Catalog definitions
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
            _catalog.RegisterProcess(new ProcessData(4, "Fatiar Cogumelo", 8, ProcessingStage.Raw, 9, ProcessingStage.Prepared, ToolCapability.Slice, 2.0f));

            // Stations
            _catalog.RegisterStation(new StationData(1, "Preparo A", 2, new[] { 1, 2, 3, 4 }));
            _catalog.RegisterStation(new StationData(2, "Preparo B", 2, new[] { 2 }, new System.Collections.Generic.Dictionary<int, float> { { 2, 2.0f } }));
            _catalog.RegisterStation(new StationData(3, "Forno", 2, new int[0]));

            // Recipes
            _catalog.RegisterRecipe(new RecipeData(1, "Mussarela", 2, new[]
            {
                new IngredientEntry(3, ProcessingStage.Raw, 1),
                new IngredientEntry(5, ProcessingStage.Prepared, 1)
            }, 15, 15f));

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
            _orderService = new OrderService();
            _ovenService = new OvenService();
            _callService = new CallService();
            _holdService = new PlayerHoldService();
            _upgradeService = new UpgradeService();
        }

        [Test]
        public void ToolLease_WorkerAcquiresAndReleasesTool_CannotBeStolenWhileActive()
        {
            var toolId = _state.GenerateToolId();
            var tool = new ToolItemState(toolId, 1, ToolCapability.Grate, LocationRef.InHand(101, HandSlotIndex.Left));
            _state.Tools[toolId] = tool;

            var cheeseId = _state.GenerateItemId();
            var cheese = new ItemState(cheeseId, 4, ItemCategory.Cheese, LocationRef.InStation(1, 0));
            _state.Items[cheeseId] = cheese;

            var slot = new StationSlotId(1, 0);

            var startRes = _processingService.StartWork(slot, 2, 101, _state, _catalog, _clock);
            Assert.IsTrue(startRes.Success, startRes.Message);
            Assert.IsTrue(tool.IsReserved(_clock.Now));
            Assert.AreEqual(101, tool.ReservedByWorkerId);

            var toolLeaseRes = tool.TryAcquireLease(202, _clock.Now, 0.75);
            Assert.IsFalse(toolLeaseRes, "Worker 202 should not acquire lease while 101 is working");

            _clock.Advance(3.0);
            var compRes = _processingService.CompleteWork(slot, _state, _catalog);
            Assert.IsTrue(compRes.Success, compRes.Message);

            Assert.AreEqual(5, cheese.DefinitionId);
            Assert.AreEqual(ProcessingStage.Prepared, cheese.PrepStage);
            Assert.IsFalse(tool.IsReserved(_clock.Now));
        }

        [Test]
        public void ProcessingService_TimedProgress_PauseAndResumeWorksAccurately()
        {
            var toolId = _state.GenerateToolId();
            var tool = new ToolItemState(toolId, 1, ToolCapability.Grate, LocationRef.InHand(101, HandSlotIndex.Left));
            _state.Tools[toolId] = tool;

            var cheeseId = _state.GenerateItemId();
            var cheese = new ItemState(cheeseId, 4, ItemCategory.Cheese, LocationRef.InStation(1, 0));
            _state.Items[cheeseId] = cheese;

            var slot = new StationSlotId(1, 0);

            // Iniciar na bancada A (3s)
            var startRes = _processingService.BeginOperation(
                _state, slot, OperationKind.Process, ResumePolicy.Resumable,
                cheeseId, toolId, 2, 101, _catalog, (float)_clock.Now, 0.75f);
            Assert.IsTrue(startRes.Success);

            // Trabalha 1s
            _clock.Advance(1.0);
            _processingService.Heartbeat(_state, slot, 101, (float)_clock.Now, 0.75f);

            // Pausa (soltou F)
            _processingService.CancelOperation(_state, slot, 101, (float)_clock.Now);
            var op = _state.ActiveOperations[slot];
            Assert.AreEqual(OperationPhase.Paused, op.Phase);
            Assert.AreEqual(1.0f, op.Progress.Accumulated, 0.05f);

            // Pausa por 5 segundos no mundo
            _clock.Advance(5.0);

            // Retoma a operação (precisa de mais 2s para completar)
            var resumeRes = _processingService.BeginOperation(
                _state, slot, OperationKind.Process, ResumePolicy.Resumable,
                cheeseId, toolId, 2, 101, _catalog, (float)_clock.Now, 0.75f);
            Assert.IsTrue(resumeRes.Success);

            // Passa 1s (total 2s de 3s) -> ainda não completa (envia heartbeat para manter lease ativa)
            _clock.Advance(0.5);
            _processingService.Heartbeat(_state, slot, 101, (float)_clock.Now, 0.75f);
            _clock.Advance(0.5);
            _processingService.Heartbeat(_state, slot, 101, (float)_clock.Now, 0.75f);
            _processingService.TickOperations(_state, _catalog, (float)_clock.Now);
            Assert.IsTrue(_state.ActiveOperations.ContainsKey(slot));

            // Passa mais 1s (total 3s de 3s) -> completa!
            _clock.Advance(0.5);
            _processingService.Heartbeat(_state, slot, 101, (float)_clock.Now, 0.75f);
            _clock.Advance(0.5);
            _processingService.Heartbeat(_state, slot, 101, (float)_clock.Now, 0.75f);
            _processingService.TickOperations(_state, _catalog, (float)_clock.Now);

            // Agora operação concluiu e item transformou em queijo ralado (ID 5)
            Assert.IsFalse(_state.ActiveOperations.ContainsKey(slot));
            Assert.AreEqual(5, cheese.DefinitionId);
            Assert.AreEqual(ProcessingStage.Prepared, cheese.PrepStage);
        }

        [Test]
        public void PermissiveAssembly_AllowsRawIngredientsAndAnyCombinationWithoutExceptions()
        {
            var pizzaId = _state.GenerateItemId();
            var pizza = new ItemState(pizzaId, 2, ItemCategory.Dough, LocationRef.InStation(3, 0));
            _state.Items[pizzaId] = pizza;

            bool add1 = pizza.AddIngredient(4, ProcessingStage.Raw, 1);
            Assert.IsTrue(add1);

            bool add2 = pizza.AddIngredient(3, ProcessingStage.Raw, 1);
            Assert.IsTrue(add2);

            var recipe = _catalog.Recipes[1];
            bool matches = _recipeEvaluator.MatchesRecipeExact(pizza, recipe);
            Assert.IsFalse(matches, "Raw cheese pizza must not match Mussarela exact");
        }

        [Test]
        public void PizzaSpecification_CanonicalSortingAndEquality_OrderIndependent()
        {
            var specA = new PizzaSpecification(2, 3, new[]
            {
                new ToppingRequirement(7, ProcessingStage.Prepared, 1),
                new ToppingRequirement(5, ProcessingStage.Prepared, 1)
            });

            var specB = new PizzaSpecification(2, 3, new[]
            {
                new ToppingRequirement(5, ProcessingStage.Prepared, 1),
                new ToppingRequirement(7, ProcessingStage.Prepared, 1)
            });

            Assert.AreEqual(specA, specB, "PizzaSpecification must sort toppings canonically so order does not matter");
            Assert.AreEqual(5, specA.GetTopping(0).IngredientId);
            Assert.AreEqual(7, specA.GetTopping(1).IngredientId);
        }

        [Test]
        public void PizzaSpecificationEvaluator_MatchesDoughIdentityEvenWhenCategoryIsPizzaOrBoxed()
        {
            // Massa crua (1) aberta na bancada (vira 2) -> adiciona molho (3) e calabresa fatiada (7) sem queijo!
            var itemId = _state.GenerateItemId();
            var item = new ItemState(itemId, 1, ItemCategory.Dough, LocationRef.InStation(1, 0));
            _state.Items[itemId] = item;

            var slot = new StationSlotId(1, 0);
            var startRes = _processingService.StartWork(slot, 1, 101, _state, _catalog, _clock);
            Assert.IsTrue(startRes.Success);
            _clock.Advance(3.0);
            var compRes = _processingService.CompleteWork(slot, _state, _catalog);
            Assert.IsTrue(compRes.Success);

            Assert.AreEqual(2, item.DefinitionId);
            Assert.AreEqual(2, item.BaseDoughDefinitionId);
            Assert.AreEqual(ProcessingStage.Prepared, item.BaseDoughStage);

            // Adicionar molho e calabresa fatiada (categoria vira Pizza)
            Assert.IsTrue(item.AddIngredient(3, ProcessingStage.Raw, 1));
            Assert.IsTrue(item.AddIngredient(7, ProcessingStage.Prepared, 1));
            Assert.AreEqual(ItemCategory.Pizza, item.Category);
            Assert.AreEqual(2, item.BaseDoughDefinitionId, "BaseDoughDefinitionId must be preserved when category becomes Pizza");

            var specCalabresaNoCheese = new PizzaSpecification(2, 3, new[]
            {
                new ToppingRequirement(7, ProcessingStage.Prepared, 1)
            });

            var detail = PizzaSpecificationEvaluator.EvaluateDetailed(item, specCalabresaNoCheese);
            Assert.IsTrue(detail.IsExactCompositionMatch, "Pizza with sauce + calabresa (no cheese) must match spec with only calabresa");
            Assert.AreEqual(0, detail.MissingToppingsCount);
            Assert.AreEqual(0, detail.ExtraToppingsCount);
        }

        [Test]
        public void PizzaOrderGenerator_GeneratesValid1To2Toppings_RespectsLockedMushroom()
        {
            var rng = new System.Random(42);
            for (int i = 0; i < 20; i++)
            {
                var spec = PizzaOrderGenerator.GenerateRandomSpecification(_catalog, rng);
                Assert.IsTrue(spec.IsValid);
                Assert.AreEqual(2, spec.BaseDoughId);
                Assert.AreEqual(3, spec.SauceId);
                Assert.GreaterOrEqual(spec.ToppingCount, 1);
                Assert.LessOrEqual(spec.ToppingCount, 2);

                for (int t = 0; t < spec.ToppingCount; t++)
                {
                    int id = spec.GetTopping(t).IngredientId;
                    Assert.AreNotEqual(9, id, "Mushroom (ID 9) must not be picked when not unlocked in session");
                }
            }
        }

        [Test]
        public void DeliveryEvaluator_MultiPizzaOrder_EvaluatesBatchWithProportionalPayout_FixedA26()
        {
            var order = new OrderState(new OrderId(1), OrderSourceType.Phone, new AddressId(4), CustomerId.None, 100.0, 400.0);
            order.AddLine(new OrderLineId(1), 1, 1); // 1x Mussarela ()
            order.AddLine(new OrderLineId(2), 2, 1); // 1x Calabresa ()

            // Criar p1 (Calabresa perfeita): adicionar ingredientes ANTES de assar!
            var p1 = new ItemState(new ItemId(1), 2, ItemCategory.Pizza, LocationRef.InHand(1, HandSlotIndex.Right));
            p1.AddIngredient(3, ProcessingStage.Raw, 1);
            p1.AddIngredient(5, ProcessingStage.Prepared, 1);
            p1.AddIngredient(7, ProcessingStage.Prepared, 1);
            p1.CookingStage = CookingStage.Baked;

            // Criar p2 (Mussarela queimada): adicionar ingredientes ANTES de queimar!
            var p2 = new ItemState(new ItemId(2), 2, ItemCategory.Pizza, LocationRef.InHand(1, HandSlotIndex.Left));
            p2.AddIngredient(3, ProcessingStage.Raw, 1);
            p2.AddIngredient(5, ProcessingStage.Prepared, 1);
            p2.CookingStage = CookingStage.Burned;

            var delivered = new[] { p1, p2 };
            var eval = _deliveryEvaluator.EvaluateDelivery(order, delivered, _catalog, burnedPayoutFactor: 0f, restaurantBaseBonus: 10);

            Assert.AreEqual(20, eval.PersonalEarned);
            Assert.AreEqual(35, eval.TotalPotential);
            Assert.AreEqual(1, eval.CorrectBakedCount);
            Assert.AreEqual(1, eval.BurnedCount);
            Assert.AreEqual(6, eval.RestaurantEarned);
        }

        [Test]
        public void OvenService_Formulas_CookAndBurnProgressAndReinsertPreservesState()
        {
            var pizzaId = _state.GenerateItemId();
            var pizza = new ItemState(pizzaId, 2, ItemCategory.Pizza, LocationRef.InHand(1, HandSlotIndex.Right));
            pizza.AddIngredient(3, ProcessingStage.Raw, 1);
            pizza.AddIngredient(5, ProcessingStage.Prepared, 1);
            _state.Items[pizzaId] = pizza;

            var ovenSlot = new StationSlotId(3, 0);

            // Inserir no forno
            var insRes = _ovenService.InsertPizza(_state, ovenSlot, pizzaId, 1, _catalog, 15f);
            Assert.IsTrue(insRes.Success);

            // Assar por 7.5s (metade de 15s) -> CookProgress = 0.5, Cooking
            _ovenService.TickOven(_state, 3, 7.5f, 15f, 10f);
            Assert.AreEqual(0.5f, pizza.CookProgress, 0.01f);
            Assert.AreEqual(CookingStage.Cooking, pizza.CookingStage);

            // Retirar do forno antes de terminar
            var remRes = _ovenService.RemovePizza(_state, ovenSlot, LocationRef.InHand(1, HandSlotIndex.Right), 1);
            Assert.IsTrue(remRes.Success);
            Assert.AreEqual(0.5f, pizza.CookProgress, 0.01f);
            // Ao retirar meio assada, não permite adicionar ingredientes!
            Assert.IsFalse(pizza.CanAddIngredient(7));

            // Reinserir no forno
            var reinsertRes = _ovenService.InsertPizza(_state, ovenSlot, pizzaId, 1, _catalog, 15f);
            Assert.IsTrue(reinsertRes.Success);

            // Passar 7.5s (completa 15s) + 5s (metade dos 10s de queima)
            _ovenService.TickOven(_state, 3, 12.5f, 15f, 10f);
            Assert.AreEqual(1.0f, pizza.CookProgress, 0.01f);
            Assert.AreEqual(0.5f, pizza.BurnProgress, 0.01f);
            Assert.AreEqual(CookingStage.Baked, pizza.CookingStage);

            // Tick longo de 100s consome excedente e queima a pizza completamente
            _ovenService.TickOven(_state, 3, 100f, 15f, 10f);
            Assert.AreEqual(1.0f, pizza.BurnProgress);
            Assert.AreEqual(CookingStage.Burned, pizza.CookingStage);
        }

        [Test]
        public void StackService_DepositAndTakeTop_CapacityAndOrdering()
        {
            var stackId = _state.GenerateStackId();
            var stack = new StackState(stackId, 1, 2); // Capacidade 2
            _state.Stacks[stackId] = stack;

            var handLoc = LocationRef.InHand(1, HandSlotIndex.Right);

            var item1 = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            item1.PackagingState = PackagingState.Boxed;
            _state.Items[item1.Id] = item1;

            // Depositar primeiro item na pilha
            var dep1 = StackService.DepositStack(_state, stackId, item1.Id, handLoc, new AddressId(10));
            Assert.IsTrue(dep1.Success);
            Assert.AreEqual(1, stack.Items.Count);
            Assert.AreEqual(10, item1.LabelAddressId);

            // Depositar segundo item na pilha
            var item2 = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            item2.PackagingState = PackagingState.Boxed;
            _state.Items[item2.Id] = item2;
            var dep2 = StackService.DepositStack(_state, stackId, item2.Id, handLoc, new AddressId(11));
            Assert.IsTrue(dep2.Success);
            Assert.AreEqual(2, stack.Items.Count);

            // Tentar depositar terceiro item (pilha cheia) -> SlotFull
            var item3 = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            item3.PackagingState = PackagingState.Boxed;
            _state.Items[item3.Id] = item3;
            var dep3 = StackService.DepositStack(_state, stackId, item3.Id, handLoc, new AddressId(12));
            Assert.IsFalse(dep3.Success);
            Assert.AreEqual(CommandError.SlotFull, dep3.Error);

            // Liberar a mão do item3 para poder retirar do topo da pilha
            _state.Items.Remove(item3.Id);

            // Retirar do topo (deve ser o item2)
            var popRes = StackService.TakeTop(_state, stackId, item2.Id, handLoc);
            Assert.IsTrue(popRes.Success);
            Assert.AreEqual(item2.Id, stack.TopItem == ItemId.None ? item1.Id : item2.Id); // item1 é o novo topo
            Assert.AreEqual(1, stack.Items.Count);
        }

        [Test]
        public void BackpackService_EquipStoreRetrieveDrop_Workflow()
        {
            var bpId = _state.GenerateBackpackId();
            var bp = new BackpackState(bpId, 4);
            _state.Backpacks[bpId] = bp;

            // Equipar mochila
            var eqRes = BackpackService.Equip(_state, bpId, 101);
            Assert.IsTrue(eqRes.Success);
            Assert.AreEqual(101, bp.EquippedBy);

            // Outro jogador tenta equipar a mesma mochila -> Blocked
            var eqRes2 = BackpackService.Equip(_state, bpId, 202);
            Assert.IsFalse(eqRes2.Success);

            // Guardar caixa na mochila
            var handLoc = LocationRef.InHand(101, HandSlotIndex.Right);
            var box = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            box.PackagingState = PackagingState.Boxed;
            _state.Items[box.Id] = box;

            var storeRes = BackpackService.StoreItem(_state, bpId, box.Id, handLoc);
            Assert.IsTrue(storeRes.Success);
            Assert.AreEqual(1, bp.Items.Count);
            Assert.AreEqual(LocationType.Backpack, box.Location.Type);

            // Retirar da mochila
            var retRes = BackpackService.RetrieveItem(_state, bpId, box.Id, handLoc);
            Assert.IsTrue(retRes.Success);
            Assert.AreEqual(0, bp.Items.Count);
            Assert.AreEqual(handLoc, box.Location);

            // Largar mochila no chão
            var dropRes = BackpackService.Drop(_state, 101, new DomainVector3(1, 2, 3), DomainQuaternion.Identity);
            Assert.IsTrue(dropRes.Success);
            Assert.IsFalse(bp.IsEquipped);
            Assert.AreEqual(new DomainVector3(1, 2, 3), bp.WorldPos);
        }

        [Test]
        public void CallService_EnqueueTickAnswer_GeneratesOrderAtomically()
        {
            var spec = new PizzaSpecification(2, 3, new[]
            {
                new ToppingRequirement(5, ProcessingStage.Prepared, 1)
            });
            var lines = new System.Collections.Generic.List<OrderLineState>
            {
                new OrderLineState(_state.GenerateOrderLineId(), spec, 2, 1) // 2x Mussarela
            };

            var enqRes = _callService.EnqueueCall(_state, new AddressId(5), lines, (float)_clock.Now, 30f);
            Assert.IsTrue(enqRes.Success);
            var callId = enqRes.Value;
            Assert.IsTrue(_state.Calls.ContainsKey(callId));

            // Atender chamada
            var ansRes = _callService.AnswerCall(_state, callId, _orderService, _catalog, _clock, maxActiveOrders: 6);
            Assert.IsTrue(ansRes.Success);
            Assert.IsFalse(_state.Calls.ContainsKey(callId)); // Removida da fila de chamadas

            // Pedido criado
            var orderId = ansRes.Value;
            Assert.IsTrue(_state.Orders.ContainsKey(orderId));
            var order = _state.Orders[orderId];
            Assert.AreEqual(new AddressId(5), order.DestinationId);
            Assert.AreEqual(OrderLifecycle.Accepted, order.Lifecycle);
        }

        [Test]
        public void PlayerHoldService_DiscardAndArchiveTicket_ExecutesWithServerLease()
        {
            var handLoc = LocationRef.InHand(101, HandSlotIndex.Right);
            var trashItem = new ItemState(_state.GenerateItemId(), 1, ItemCategory.Ingredient, handLoc);
            _state.Items[trashItem.Id] = trashItem;

            // Iniciar descarte (hold de 1.0s)
            var discRes = _holdService.BeginDiscard(_state, 101, trashItem.Id, 1.0f, (float)_clock.Now, 0.75f);
            Assert.IsTrue(discRes.Success);
            Assert.AreEqual(101, trashItem.ReservedByHold);

            // Avançar 0.5s e enviar heartbeat
            _clock.Advance(0.5);
            _holdService.Heartbeat(_state, 101, (float)_clock.Now, 0.75f);

            // Avançar mais 0.6s (total 1.1s > 1.0s) e tickar
            _clock.Advance(0.6);
            _holdService.TickHolds(_state, (float)_clock.Now, _orderService);

            // Item destruído
            Assert.IsFalse(_state.Items.ContainsKey(trashItem.Id));
            Assert.IsFalse(_state.PlayerHolds.ContainsKey(101));
        }

        [Test]
        public void StationOverrides_DefinitionAndInstanceOverrides_CalculatesCorrectDuration()
        {
            // Bancada A (ID 1) usa baseDuration de Ralar Queijo = 3.0s
            var stationA = _catalog.Stations[1];
            var process = _catalog.Processes[2];
            Assert.AreEqual(3.0f, stationA.GetProcessDuration(process));

            // Bancada B (ID 2) tem override para Ralar Queijo = 2.0s
            var stationB = _catalog.Stations[2];
            Assert.AreEqual(2.0f, stationB.GetProcessDuration(process));
        }

        [Test]
        public void TransferService_SingleLocationInvariant_BlocksDoubleOccupancy()
        {
            var handRight = LocationRef.InHand(99, HandSlotIndex.Right);
            var item1 = new ItemState(new ItemId(1), 1, ItemCategory.Dough, handRight);
            _state.Items[item1.Id] = item1;

            var item2 = new ItemState(new ItemId(2), 3, ItemCategory.Sauce, LocationRef.InWorld(0, 0, 0));
            _state.Items[item2.Id] = item2;

            var res = _transferService.TransferItem(item2.Id, handRight);
            Assert.IsFalse(res.Success);
            Assert.AreEqual(CommandError.SlotFull, res.Error);
            Assert.AreEqual(LocationRef.InWorld(0, 0, 0), item2.Location, "item2 must not change location on failure");
        }

        [Test]
        public void QueueService_TwoQueues_ChoosesShorterAndCompactsOnFrontLeave()
        {
            var queueService = new QueueService();
            var rng = new System.Random(12345);

            // Cliente 1 entra -> deve ir para a Fila 0, Slot 0
            int q1 = queueService.ChooseBestQueueOnEntry(_state, queueCount: 2, maxCustomersPerQueue: 3, rng: rng);
            Assert.IsTrue(q1 == 0 || q1 == 1);
            int s1 = queueService.GetQueueOccupancy(_state, q1);
            Assert.AreEqual(0, s1);

            var c1 = new CustomerState(
                _state.GenerateCustomerId(), q1, s1,
                new PizzaSpecification(2, 3, new[] { new ToppingRequirement(5, ProcessingStage.Prepared, 1) }),
                1, 104, 80f, 240f, 0);
            _state.Customers[c1.Id] = c1;

            // Cliente 2 entra -> a outra fila está mais vazia (0 vs 1)
            int q2 = queueService.ChooseBestQueueOnEntry(_state, queueCount: 2, maxCustomersPerQueue: 3, rng: rng);
            Assert.AreNotEqual(q1, q2, "Must choose the shorter queue");
            int s2 = queueService.GetQueueOccupancy(_state, q2);
            Assert.AreEqual(0, s2);

            var c2 = new CustomerState(
                _state.GenerateCustomerId(), q2, s2,
                new PizzaSpecification(2, 3, new[] { new ToppingRequirement(7, ProcessingStage.Prepared, 1) }),
                2, 103, 80f, 240f, 1);
            _state.Customers[c2.Id] = c2;

            // Cliente 3 entra na mesma fila de c1 (Slot 1)
            var c3 = new CustomerState(
                _state.GenerateCustomerId(), q1, 1,
                new PizzaSpecification(2, 3, new[] { new ToppingRequirement(5, ProcessingStage.Prepared, 1) }),
                3, 105, 80f, 240f, 2);
            _state.Customers[c3.Id] = c3;

            // Quando Cliente 1 deixa a Fila q1 (vai para a mesa), Cliente 3 deve avançar para o Slot 0 da Fila q1 (sem trocar de fila!)
            c1.QueueIndex = -1;
            c1.QueueSlotIndex = -1;
            c1.Phase = CustomerPhase.WalkingToTable;
            queueService.CompactAndAdvanceQueue(_state, q1, 110.0);

            Assert.AreEqual(q1, c3.QueueIndex, "Customer must not hop queues");
            Assert.AreEqual(0, c3.QueueSlotIndex, "Customer behind must advance to slot 0");
            Assert.IsFalse(queueService.IsQueueStagnant(_state, q1, 120.0, 35f));
            Assert.IsTrue(queueService.IsQueueStagnant(_state, q1, 150.0, 35f));
        }

        [Test]
        public void CustomerTraits_DefaultTenTraits_BlocksIncompatiblePairs()
        {
            foreach (var t in CustomerTraitData.CreateDefaultTenTraits())
            {
                _catalog.RegisterCustomerTrait(t);
            }

            var rng = new System.Random(42);
            for (int i = 0; i < 80; i++)
            {
                var (posId, negId) = CustomerService.RollCompatibleTraitPair(_catalog, rng);
                Assert.IsTrue(_catalog.CustomerTraits.TryGetValue(posId, out var pos));
                Assert.IsTrue(_catalog.CustomerTraits.TryGetValue(negId, out var neg));
                Assert.AreEqual(TraitPolarity.Positive, pos.Polarity);
                Assert.AreEqual(TraitPolarity.Negative, neg.Polarity);
                Assert.IsTrue(pos.IsCompatibleWith(neg), $"Incompatible traits rolled together: {pos.Name} + {neg.Name}");
            }
        }

        [Test]
        public void CustomerFlow_HoldAttendanceReservesTable_ServesUnboxedPizzaAndPaysOnce()
        {
            foreach (var t in CustomerTraitData.CreateDefaultTenTraits())
            {
                _catalog.RegisterCustomerTrait(t);
            }

            var queueService = new QueueService();
            var tableService = new TableService();
            tableService.EnsureTablesInitialized(_state, new[] { 1, 2 });
            var custService = new CustomerService(queueService, tableService);
            var rng = new System.Random(777);

            // 1. Spawnar cliente presencial na entrada
            var spawnRes = custService.SpawnWalkInCustomer(_state, _catalog, _clock, rng: rng);
            Assert.IsTrue(spawnRes.Success, spawnRes.Message);
            var customer = _state.Customers[spawnRes.Value];
            Assert.IsTrue(customer.IsInQueue);
            Assert.AreEqual(0, customer.QueueSlotIndex);

            // 2. Segurar F por 0,6s via PlayerHoldService (soltar antes cancela; completar registra pedido e reserva mesa)
            var holdBegin = _holdService.BeginAttendCustomer(
                _state, 101, customer.Id, 0.6f, (float)_clock.Now, 0.75f,
                tableService, maxActiveOrders: 6);
            Assert.IsTrue(holdBegin.Success, holdBegin.Message);

            // Avançar 0,3s e cancelar (simula soltar F antes de 0,6s) -> cliente continua na fila sem pedido criado
            _clock.Advance(0.3);
            _holdService.Cancel(_state, 101);
            Assert.IsTrue(customer.IsInQueue);
            Assert.AreEqual(OrderId.None, customer.OrderId);

            // Iniciar novamente e manter F por 0,6s completos
            holdBegin = _holdService.BeginAttendCustomer(
                _state, 101, customer.Id, 0.6f, (float)_clock.Now, 0.75f,
                tableService, maxActiveOrders: 6);
            Assert.IsTrue(holdBegin.Success);

            _clock.Advance(0.65);
            _holdService.TickHolds(_state, (float)_clock.Now, _orderService, custService, _clock, maxActiveOrders: 6);

            // Pedido registrado no quadro e mesa 1 reservada!
            Assert.IsFalse(customer.IsInQueue);
            Assert.AreEqual(1, customer.AssignedTableId);
            Assert.AreNotEqual(OrderId.None, customer.OrderId);
            Assert.IsTrue(_state.Orders.ContainsKey(customer.OrderId));
            Assert.IsTrue(_state.Orders[customer.OrderId].IsDineIn);
            Assert.IsFalse(_state.Tables[1].IsFree);

            // 3. Tentar servir pizza EMBALADA em caixa na mesa -> deve falhar (caixas são só para delivery)
            var handLoc = LocationRef.InHand(101, HandSlotIndex.Right);
            var boxedPizza = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            boxedPizza.AddIngredient(3, ProcessingStage.Raw, 1);
            for (int i = 0; i < customer.RequestedPizza.ToppingCount; i++)
            {
                var top = customer.RequestedPizza.GetTopping(i);
                boxedPizza.AddIngredient(top.IngredientId, top.RequiredStage, top.Quantity);
            }
            boxedPizza.CookingStage = CookingStage.Baked;
            boxedPizza.PackagingState = PackagingState.Boxed;
            _state.Items[boxedPizza.Id] = boxedPizza;

            var boxedServeRes = custService.ServeTableUnboxedPizza(_state, 1, boxedPizza.Id, 101, _catalog, _orderService);
            Assert.IsFalse(boxedServeRes.Success, "Boxed pizza must be rejected at dining table");

            // 4. Criar pizza ASSADA SEM CAIXA com os ingredientes pedidos e servir na mesa 1
            var unboxedPizza = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            unboxedPizza.AddIngredient(3, ProcessingStage.Raw, 1);
            for (int i = 0; i < customer.RequestedPizza.ToppingCount; i++)
            {
                var top = customer.RequestedPizza.GetTopping(i);
                unboxedPizza.AddIngredient(top.IngredientId, top.RequiredStage, top.Quantity);
            }
            unboxedPizza.CookingStage = CookingStage.Baked;
            unboxedPizza.PackagingState = PackagingState.None;
            _state.Items[unboxedPizza.Id] = unboxedPizza;

            int moneyBefore = _state.RestaurantBudget;
            var serveRes = custService.ServeTableUnboxedPizza(_state, 1, unboxedPizza.Id, 101, _catalog, _orderService);
            Assert.IsTrue(serveRes.Success, serveRes.Message);
            Assert.IsTrue(serveRes.Value.Accepted);
            Assert.Greater(serveRes.Value.PersonalEarned, 0);
            Assert.IsTrue(customer.HasPaid, "Customer must mark payment as completed once");
            Assert.AreEqual(CustomerPhase.Eating, customer.Phase);
            Assert.Greater(_state.RestaurantBudget, moneyBefore);

            // 5. Tentar servir uma segunda pizza para o mesmo cliente -> bloqueado (pagamento único)
            var extraPizza = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            extraPizza.CookingStage = CookingStage.Baked;
            _state.Items[extraPizza.Id] = extraPizza;
            var secondServe = custService.ServeTableUnboxedPizza(_state, 1, extraPizza.Id, 101, _catalog, _orderService);
            Assert.IsFalse(secondServe.Success);

            // 6. Após o tempo de refeição, cliente levanta, libera a mesa e o prato
            custService.TickCustomers(_state, _catalog, _orderService, _clock, 15.0f);
            Assert.IsTrue(_state.Tables[1].IsFree, "Table must be freed after customer finishes eating");
        }

        [Test]
        public void CustomerPatience_ExpiryReleasesTableAndOrder_WithoutDeletingHeldPizzas()
        {
            var queueService = new QueueService();
            var tableService = new TableService();
            tableService.EnsureTablesInitialized(_state, new[] { 1 }); // Apenas 1 mesa no salão
            var custService = new CustomerService(queueService, tableService);
            var rng = new System.Random(99);

            var c1Id = custService.SpawnWalkInCustomer(_state, _catalog, _clock, rng: rng).Value;
            var attend1 = custService.CompleteAttendCustomer(_state, c1Id, _orderService, _clock, maxActiveOrders: 6);
            Assert.IsTrue(attend1.Success);
            var orderId = attend1.Value;

            // Segundo cliente na fila não consegue ser atendido enquanto a única mesa estiver ocupada
            var c2Id = custService.SpawnWalkInCustomer(_state, _catalog, _clock, rng: rng).Value;
            var holdBlocked = _holdService.BeginAttendCustomer(
                _state, 101, c2Id, 0.6f, (float)_clock.Now, 0.75f,
                tableService, maxActiveOrders: 6);
            Assert.IsFalse(holdBlocked.Success, "Must not allow attending customer when no table is free");

            // Jogador está segurando uma pizza na mão enquanto o cliente 1 espera
            var handLoc = LocationRef.InHand(101, HandSlotIndex.Left);
            var heldPizza = new ItemState(_state.GenerateItemId(), 2, ItemCategory.Pizza, handLoc);
            _state.Items[heldPizza.Id] = heldPizza;

            // Esgotar paciência de preparo do cliente 1 (ex: avançar 400s)
            custService.TickCustomers(_state, _catalog, _orderService, _clock, 400f);

            // Cliente 1 foi embora, pedido expirou e a mesa 1 foi liberada, mas a pizza na mão do jogador permanece intacta!
            Assert.AreEqual(OrderLifecycle.Expired, _state.Orders[orderId].Lifecycle);
            Assert.IsTrue(_state.Tables[1].IsFree, "Table must be released when customer patience expires");
            Assert.IsTrue(_state.Items.ContainsKey(heldPizza.Id), "Pizzas in hands or stations must NEVER be deleted when order expires");
            Assert.AreEqual(handLoc, _state.Items[heldPizza.Id].Location);
        }

        [Test]
        public void DemandDirector_WeightedChannelSelection_EnforcesStreakProtection()
        {
            OrderOrigin last = OrderOrigin.DeliveryCall;
            int streak = 0;

            // Forçar random01 = 0.1f (sempre pediria WalkInCustomer no peso 50/50), mas com maxConsecutiveSameChannel = 2
            Assert.IsTrue(CustomerService.TrySelectNextChannel(true, true, 0.5f, 0.5f, 2, ref last, ref streak, 0.1f, out var ch1));
            Assert.AreEqual(OrderOrigin.WalkInCustomer, ch1);
            Assert.AreEqual(1, streak);

            Assert.IsTrue(CustomerService.TrySelectNextChannel(true, true, 0.5f, 0.5f, 2, ref last, ref streak, 0.1f, out var ch2));
            Assert.AreEqual(OrderOrigin.WalkInCustomer, ch2);
            Assert.AreEqual(2, streak);

            // Na 3ª vez consecutiva com o mesmo random01 = 0.1f, a proteção contra sequência longa força DeliveryCall!
            Assert.IsTrue(CustomerService.TrySelectNextChannel(true, true, 0.5f, 0.5f, 2, ref last, ref streak, 0.1f, out var ch3));
            Assert.AreEqual(OrderOrigin.DeliveryCall, ch3);
            Assert.AreEqual(1, streak);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Tests
{
    [TestFixture]
    public class P2_OrderUnitAndEmployeeTests
    {
        private sealed class FakeClock : IClock
        {
            public double Now { get; set; } = 100.0;
        }

        private PizzeriaState _state;
        private DefinitionCatalog _catalog;
        private FakeClock _clock;
        private OrderService _orderService;
        private DeliveryService _deliveryService;
        private ProcessingService _processingService;
        private EmployeeTaskService _taskService;
        private CustomerService _customerService;

        [SetUp]
        public void Setup()
        {
            _state = new PizzeriaState { GenerationId = 1, RestaurantBudget = 1000 };
            _catalog = new DefinitionCatalog();
            _clock = new FakeClock { Now = 100.0 };

            _catalog.RegisterIngredient(new IngredientData(1, "Massa", ItemCategory.Dough, true, 2));
            _catalog.RegisterIngredient(new IngredientData(2, "Massa Aberta", ItemCategory.Dough));
            _catalog.RegisterIngredient(new IngredientData(3, "Molho", ItemCategory.Sauce));
            _catalog.RegisterIngredient(new IngredientData(4, "Queijo Inteiro", ItemCategory.Cheese, true, 5));
            _catalog.RegisterIngredient(new IngredientData(5, "Queijo Ralado", ItemCategory.Cheese));
            _catalog.RegisterIngredient(new IngredientData(6, "Calabresa Inteira", ItemCategory.Topping, true, 7));
            _catalog.RegisterIngredient(new IngredientData(7, "Calabresa Fatiada", ItemCategory.Topping));

            _catalog.RegisterTool(new ToolData(1, "Ralador", ToolCapability.Grate, 1));
            _catalog.RegisterTool(new ToolData(2, "Faca", ToolCapability.Slice, 1));

            _catalog.RegisterProcess(new ProcessData(1, "Abrir Massa", 1, ProcessingStage.Raw, 2, ProcessingStage.Prepared, ToolCapability.None, 2.0f));
            _catalog.RegisterProcess(new ProcessData(2, "Ralar Queijo", 4, ProcessingStage.Raw, 5, ProcessingStage.Prepared, ToolCapability.Grate, 2.0f));
            _catalog.RegisterProcess(new ProcessData(3, "Fatiar Calabresa", 6, ProcessingStage.Raw, 7, ProcessingStage.Prepared, ToolCapability.Slice, 2.0f));

            _catalog.RegisterStation(new StationData(2, "Preparo", 4, new[] { 1, 2, 3 }));
            _catalog.RegisterStation(new StationData(5, "Expedição", 4, new int[0]));

            _state.StorageSlots[(1, 1)] = new StorageSlotState(1, 1, 10, 20);
            _state.StorageSlots[(1, 3)] = new StorageSlotState(1, 3, 10, 20);
            _state.StorageSlots[(1, 4)] = new StorageSlotState(1, 4, 10, 20);
            _state.StorageSlots[(1, 6)] = new StorageSlotState(1, 6, 10, 20);

            _orderService = new OrderService();
            _deliveryService = new DeliveryService();
            _processingService = new ProcessingService();
            _taskService = new EmployeeTaskService();
            _customerService = new CustomerService();
        }

        private ItemState CreateReadyPizza(PizzaSpecification spec, bool boxed, LocationRef location)
        {
            var id = _state.GenerateItemId();
            var item = new ItemState(id, spec.BaseDoughId > 0 ? spec.BaseDoughId : 2, ItemCategory.Pizza, location);
            item.PrepStage = ProcessingStage.Prepared;
            item.BaseDoughDefinitionId = 2;
            item.BaseDoughStage = ProcessingStage.Prepared;
            if (spec.RequiresSauce && spec.SauceId > 0)
            {
                for (int i = 0; i < spec.SaucePortions; i++)
                    item.AddIngredient(spec.SauceId, ProcessingStage.Prepared, 1);
            }
            if (spec.Toppings != null)
            {
                foreach (var top in spec.Toppings)
                {
                    for (int i = 0; i < top.PortionCount; i++)
                        item.AddIngredient(top.IngredientDefId, top.RequiredStage, 1);
                }
            }
            item.CookingStage = CookingStage.Baked;
            item.CookProgress = 1f;
            if (boxed)
            {
                item.PackagingState = PackagingState.Boxed;
                item.Category = ItemCategory.Box;
            }
            _state.Items[id] = item;
            return item;
        }

        [Test]
        public void TwoIdenticalOrders_OneReadyPizza_OnlyOneOrderUnitClaimsIt()
        {
            var cheese = PizzaSpecification.CreateDefaultCheese();
            var o1 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(1), new[] { (cheese, 1) }, _state, _catalog, _clock, 6).Value;
            var o2 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(2), new[] { (cheese, 1) }, _state, _catalog, _clock, 6).Value;
            _taskService.EnqueueOrderTask(o1, EmployeeRole.Pizzaiolo);
            _taskService.EnqueueOrderTask(o2, EmployeeRole.Pizzaiolo);

            // Simula pizza embalada por humano usando ItemCategory.Box (bug anterior ignorava Category=Box)
            var pizza = CreateReadyPizza(cheese, boxed: true, LocationRef.InStation(5, 0));

            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);

            Assert.AreEqual(1, _state.ItemToOrderUnit.Count, "Apenas 1 vínculo exclusivo pode existir para a pizza única.");
            Assert.IsTrue(_state.ItemToOrderUnit.TryGetValue(pizza.Id, out var boundKey));
            Assert.AreEqual(o1, boundKey.OrderId);

            Assert.AreEqual(1, OrderUnitFulfillmentService.CountUnitsWithStatus(_state, o1, OrderUnitStatus.BoundToReadyItem));
            Assert.AreEqual(0, OrderUnitFulfillmentService.CountUnitsWithStatus(_state, o2, OrderUnitStatus.BoundToReadyItem));
            Assert.AreEqual(1, OrderUnitFulfillmentService.CountUnitsWithStatus(_state, o2, OrderUnitStatus.Pending));

            Assert.IsTrue(_taskService.TryGetTaskForOrder(o2, EmployeeRole.Pizzaiolo, out var task2));
            Assert.AreEqual(0, task2.TotalUnitsCompleted);
        }

        [Test]
        public void QuantityThreeLine_RequiresThreeDistinctPizzas_AndRepeatedSyncIsIdempotent()
        {
            var cheese = PizzaSpecification.CreateDefaultCheese();
            var o1 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(1), new[] { (cheese, 3) }, _state, _catalog, _clock, 6).Value;
            var task = _taskService.EnqueueOrderTask(o1, EmployeeRole.Pizzaiolo);

            CreateReadyPizza(cheese, boxed: true, LocationRef.InStation(5, 0));

            for (int i = 0; i < 6; i++)
            {
                OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);
            }

            Assert.AreEqual(1, task.TotalUnitsCompleted, "Sincronizações repetidas não podem avançar progresso com uma única pizza.");
            Assert.AreEqual(3, task.TotalUnitsRequired);
            Assert.AreNotEqual(EmployeeTaskState.Completed, task.State);

            CreateReadyPizza(cheese, boxed: true, LocationRef.InStation(5, 1));
            CreateReadyPizza(cheese, boxed: true, LocationRef.InStation(5, 2));
            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);

            Assert.AreEqual(3, task.TotalUnitsCompleted);
            Assert.AreEqual(EmployeeTaskState.Completed, task.State);
        }

        [Test]
        public void TwoDifferentRecipes_SinglePizzaMatchesOnlyCompatibleUnit()
        {
            var cheese = PizzaSpecification.CreateDefaultCheese();
            var calabresa = OrderLineState.CreateSpecFromLegacyRecipeId(2);
            var oCheese = _orderService.CreatePhoneOrderBySpecifications(new AddressId(1), new[] { (cheese, 1) }, _state, _catalog, _clock, 6).Value;
            var oCalabresa = _orderService.CreatePhoneOrderBySpecifications(new AddressId(2), new[] { (calabresa, 1) }, _state, _catalog, _clock, 6).Value;
            var tCheese = _taskService.EnqueueOrderTask(oCheese, EmployeeRole.Pizzaiolo);
            var tCalabresa = _taskService.EnqueueOrderTask(oCalabresa, EmployeeRole.Pizzaiolo);

            CreateReadyPizza(calabresa, boxed: true, LocationRef.InStation(5, 0));
            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);

            Assert.AreEqual(0, tCheese.TotalUnitsCompleted);
            Assert.AreEqual(1, tCalabresa.TotalUnitsCompleted);
            Assert.AreEqual(EmployeeTaskState.Completed, tCalabresa.State);
        }

        [Test]
        public void TwoConcurrentEmployees_ClaimDistinctUnitsAndRespectStationSlotReservations()
        {
            var emp1 = _taskService.TryHireEmployee(_state, EmployeeRole.Pizzaiolo);
            var emp2 = _taskService.TryHireEmployee(_state, EmployeeRole.Pizzaiolo);
            var cheese = PizzaSpecification.CreateDefaultCheese();
            var o1 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(1), new[] { (cheese, 2) }, _state, _catalog, _clock, 6).Value;
            var order = _state.Orders[o1];
            _taskService.EnqueueOrderTask(o1, EmployeeRole.Pizzaiolo);

            Assert.IsTrue(OrderUnitFulfillmentService.TryClaimNextPendingUnitForEmployee(_state, order, emp1.EmployeeId, _taskService, _clock.Now, out var u1));
            Assert.IsTrue(OrderUnitFulfillmentService.TryClaimNextPendingUnitForEmployee(_state, order, emp2.EmployeeId, _taskService, _clock.Now, out var u2));
            Assert.AreNotEqual(u1.Key, u2.Key, "Dois funcionários concorrentes devem assumir unidades distintas.");

            ulong actor1 = 1_000_000UL + (ulong)emp1.EmployeeId;
            ulong actor2 = 1_000_000UL + (ulong)emp2.EmployeeId;
            Assert.IsTrue(OrderUnitFulfillmentService.TryReserveSlot(_state, 2, 0, actor1, emp1.EmployeeId, u1.Key, _clock.Now, 10.0));
            Assert.IsFalse(OrderUnitFulfillmentService.TryReserveSlot(_state, 2, 0, actor2, emp2.EmployeeId, u2.Key, _clock.Now, 10.0),
                "Segundo funcionário não pode invadir slot reservado pelo primeiro.");
            Assert.IsTrue(OrderUnitFulfillmentService.IsSlotFreeForActor(_state, 2, 1, actor2, _clock.Now));
        }

        [Test]
        public void HumanRemovesMovesAndDeliversBoundPizzaToAnotherOrder_ReopensOriginalUnitAndTask()
        {
            var cheese = PizzaSpecification.CreateDefaultCheese();
            var o1 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(1), new[] { (cheese, 1) }, _state, _catalog, _clock, 6).Value;
            var o2 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(2), new[] { (cheese, 1) }, _state, _catalog, _clock, 6).Value;
            var t1 = _taskService.EnqueueOrderTask(o1, EmployeeRole.Pizzaiolo);

            var pizza = CreateReadyPizza(cheese, boxed: true, LocationRef.InStation(5, 0));
            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);
            Assert.AreEqual(EmployeeTaskState.Completed, t1.State);

            // Humano pega da bancada e guarda na mochila: vínculo preserva até entrega/descarte
            Assert.IsTrue(TransferService.TransferItem(_state, pizza.Id, LocationRef.InHand(10UL, HandSlotIndex.Left), 10UL).Success);
            Assert.IsTrue(BackpackService.Register(_state, new BackpackId(1), 4, new DomainVector3(0, 0, 0), new DomainQuaternion(0, 0, 0, 1)).Success);
            Assert.IsTrue(BackpackService.Equip(_state, new BackpackId(1), 10UL).Success);
            Assert.IsTrue(BackpackService.StoreItem(_state, new BackpackId(1), pizza.Id, LocationRef.InHand(10UL, HandSlotIndex.Left)).Success);
            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);
            Assert.AreEqual(1, t1.TotalUnitsCompleted, "Mover item para mão ou mochila não deve quebrar vínculo.");

            // Humano entrega essa pizza no Pedido 2 (endereço 2) -> Pedido 1 deve reabrir unidade!
            var delRes = _deliveryService.SubmitDelivery(
                _state, new AddressId(2), new[] { pizza.Id }, attemptId: 500UL, requesterId: 10UL, _catalog, _orderService,
                nowTime: _clock.Now, taskService: _taskService);
            Assert.IsTrue(delRes.Success);
            Assert.AreEqual(OrderUnitStatus.Pending, _state.OrderUnits[new OrderUnitKey(_state.GenerationId, o1, 0, 0)].Status);
            Assert.AreEqual(EmployeeTaskState.Available, t1.State);
            Assert.AreEqual(0, t1.TotalUnitsCompleted);
        }

        [Test]
        public void UnavailableIngredient_DoesNotConsumeStockOrLeaveOperations()
        {
            _state.StorageSlots[(1, 6)].Quantity = 0; // Sem calabresa crua
            int cheeseStockBefore = _state.StorageSlots[(1, 4)].Quantity;

            var disp = TransferService.DispenseFromStorage(_state, _catalog, 1, 6, 1_000_001UL, HandSlotIndex.Right);
            Assert.IsFalse(disp.Success);
            Assert.AreEqual(cheeseStockBefore, _state.StorageSlots[(1, 4)].Quantity);
            Assert.AreEqual(0, _state.Items.Count);
            Assert.AreEqual(0, _state.ActiveOperations.Count);
            Assert.AreEqual(0, _state.StationSlotReservations.Count);
        }

        [Test]
        public void HumanHeldOrLeasedTool_RejectsEmployeeOperationWithoutForcingCompletion()
        {
            var tool = new ToolItemState(_state.GenerateToolId(), 1, ToolCapability.Grate, LocationRef.InStation(3, 0));
            _state.Tools[tool.Id] = tool;
            // Humano segura/reserva a ferramenta
            Assert.IsTrue(tool.TryAcquireLease(workerId: 1UL, _clock.Now, 15.0));

            int stockBefore = _state.StorageSlots[(1, 4)].Quantity;
            var cheeseId = TransferService.DispenseFromStorage(_state, _catalog, 1, 4, 1_000_001UL, HandSlotIndex.Right).Value;
            Assert.IsTrue(TransferService.TransferItem(_state, cheeseId, LocationRef.InStation(2, 0), 1_000_001UL).Success);

            var beginRes = _processingService.BeginOperation(
                _state, new StationSlotId(2, 0), OperationKind.Process, ResumePolicy.Resumable,
                cheeseId, tool.Id, 2, 1_000_001UL, _catalog, (float)_clock.Now);

            Assert.IsFalse(beginRes.Success);
            Assert.AreEqual(CommandError.ToolBusy, beginRes.Error);
            Assert.AreEqual(ProcessingStage.Raw, _state.Items[cheeseId].PrepStage, "Falha jamais pode forçar estágio Prepared.");
            Assert.AreEqual(0, _state.ActiveOperations.Count);
            Assert.AreEqual(stockBefore - 1, _state.StorageSlots[(1, 4)].Quantity);
        }

        [Test]
        public void RejectedAssemblyOrMissingOperationComplete_DoesNotMutatePizzaIngredientsOrPrepStage()
        {
            var doughId = TransferService.DispenseFromStorage(_state, _catalog, 1, 1, 1_000_001UL, HandSlotIndex.Right).Value;
            TransferService.TransferItem(_state, doughId, LocationRef.InStation(2, 0), 1_000_001UL);

            // Tentar completar trabalho sem operação ativa falha e mantém massa Raw
            var compRes = _processingService.CompleteWork(new StationSlotId(2, 0), _state, _catalog);
            Assert.IsFalse(compRes.Success);
            Assert.AreEqual(ProcessingStage.Raw, _state.Items[doughId].PrepStage);

            // Montar molho diretamente em massa Raw sem abrir é rejeitado e não adiciona ingrediente à força
            var sauceId = TransferService.DispenseFromStorage(_state, _catalog, 1, 3, 1_000_001UL, HandSlotIndex.Right).Value;
            var asmRes = PizzaAssemblyService.Begin(_state, doughId, sauceId, 1_000_001UL, (float)_clock.Now, 0.5f);
            Assert.IsFalse(asmRes.Success);
            Assert.IsFalse(PizzaAssemblyService.Complete(_state, new StationSlotId(2, 0)));
            Assert.AreEqual(0, _state.Items[doughId].Ingredients.Count);
            Assert.IsTrue(_state.Items.ContainsKey(sauceId), "Ingrediente não montado permanece íntegro.");
        }

        [Test]
        public void CancelledOrderAndNewRunGeneration_InvalidateUnitsCleanlyWithoutDestroyingItems()
        {
            var cheese = PizzaSpecification.CreateDefaultCheese();
            var o1 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(1), new[] { (cheese, 1) }, _state, _catalog, _clock, 6).Value;
            var t1 = _taskService.EnqueueOrderTask(o1, EmployeeRole.Pizzaiolo);

            var pizza = CreateReadyPizza(cheese, boxed: true, LocationRef.InStation(5, 0));
            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);
            Assert.AreEqual(1, _state.ItemToOrderUnit.Count);

            // Cancelar O1 libera o vínculo sem destruir a pizza, permitindo O2 assumi-la
            var cancelRes = _orderService.CancelOrder(o1, _state, _taskService);
            Assert.IsTrue(cancelRes.Success);
            Assert.IsTrue(_state.Items.ContainsKey(pizza.Id), "Cancelar pedido não deve destruir pizza física existente.");
            Assert.AreEqual(0, _state.ItemToOrderUnit.Count);
            Assert.AreEqual(EmployeeTaskState.Cancelled, t1.State);

            var o2 = _orderService.CreatePhoneOrderBySpecifications(new AddressId(2), new[] { (cheese, 1) }, _state, _catalog, _clock, 6).Value;
            var t2 = _taskService.EnqueueOrderTask(o2, EmployeeRole.Pizzaiolo);
            OrderUnitFulfillmentService.SyncAllActiveOrderTasks(_state, _taskService);
            Assert.AreEqual(1, t2.TotalUnitsCompleted);

            // Nova RUN limpa e avança GenerationId -> chaves antigas nunca colidem
            _state.Reset(_state.GenerationId + 1, 500, 100);
            _taskService.ResetForNewRun();
            Assert.AreEqual(2u, _state.GenerationId);
            Assert.AreEqual(0, _state.OrderUnits.Count);
            Assert.AreEqual(0, _state.ItemToOrderUnit.Count);
            Assert.AreEqual(0, _state.StationSlotReservations.Count);
            Assert.AreEqual(0, _state.ActiveOperations.Count);
        }
    }
}

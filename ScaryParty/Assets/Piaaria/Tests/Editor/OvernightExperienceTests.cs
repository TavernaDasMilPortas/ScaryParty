using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Tests
{
    [TestFixture]
    public class OvernightExperienceTests
    {
        [Test]
        public void NightCycle_VictoryAndOperationalDefeat_UseOnlyNightEarnedRevenueAndRestoreSnapshotOnRetry()
        {
            var state = new PizzeriaState
            {
                RestaurantBudget = 500
            };
            state.StorageSlots[(1, 1)] = new StorageSlotState(1, 1, 10, 20);
            state.UpgradeLevels[1] = 2;
            Assert.IsTrue(BackpackService.Register(
                state,
                new BackpackId(1),
                4,
                new DomainVector3(0f, 0f, 0f),
                new DomainQuaternion(0f, 0f, 0f, 1f)).Success);

            NightCycleService.StartNight(state, 1);

            Assert.AreEqual(1, state.CurrentNightNumber);
            Assert.AreEqual(0, state.NightEarnedRevenue);
            Assert.AreEqual(60, state.NightRevenueGoal);

            // Simula mutações durante a noite: saldo alto preexistente, mas receita da noite insuficiente (< 60)
            state.RestaurantBudget = 999;
            state.NightEarnedRevenue = 50;
            state.StorageSlots[(1, 1)].Quantity = 2;
            state.UpgradeLevels[1] = 3;
            BackpackService.ApplyBackpackDamage(state, new BackpackId(1), 100, new DomainVector3(1f, 0f, 1f), out _);
            Assert.IsTrue(state.Backpacks[new BackpackId(1)].IsTorn);

            NightCycleService.EvaluateNightOutcome(state);
            Assert.AreEqual(NightPhase.Defeat, state.CurrentNightPhase);
            Assert.AreEqual(NightDefeatReason.RevenueBelowGoal, state.NightDefeatReason);

            // Retry restaura snapshot da entrada da noite (saldo, estoque, upgrades e integridade de mochila)
            Assert.IsTrue(NightCycleService.RetryCurrentNight(state));
            Assert.AreEqual(500, state.RestaurantBudget);
            Assert.AreEqual(10, state.StorageSlots[(1, 1)].Quantity);
            Assert.AreEqual(2, state.UpgradeLevels[1]);
            Assert.AreEqual(100, state.Backpacks[new BackpackId(1)].Integrity);
            Assert.IsFalse(state.Backpacks[new BackpackId(1)].IsTorn);
            Assert.AreEqual(0, state.NightEarnedRevenue);
            Assert.AreEqual(1, state.CurrentNightNumber);

            // Vitória ao atingir a meta apenas com receita da noite atual
            state.NightEarnedRevenue = 60;
            NightCycleService.EvaluateNightOutcome(state);
            Assert.AreEqual(NightPhase.Results, state.CurrentNightPhase);
            Assert.AreEqual(NightDefeatReason.None, state.NightDefeatReason);

            Assert.IsTrue(NightCycleService.AdvanceToNextNight(state));
            Assert.AreEqual(2, state.CurrentNightNumber);
            Assert.AreEqual(110, state.NightRevenueGoal);
        }

        [Test]
        public void OrderTaskBoard_ClaimToggleAndStages_TransitionThroughPrepReadyAndDelivery()
        {
            var state = new PizzeriaState();
            Assert.IsTrue(BackpackService.Register(
                state,
                new BackpackId(1),
                4,
                new DomainVector3(0f, 0f, 0f),
                new DomainQuaternion(0f, 0f, 0f, 1f)).Success);

            var orderId = new OrderId(10);
            var order = new OrderState(orderId, OrderSourceType.Phone, new AddressId(5), CustomerId.None, 0.0, 180.0);
            state.Orders[orderId] = order;

            Assert.AreEqual(OrderTaskBoardStage.New, NightCycleService.ComputeOrderTaskStage(state, order));

            // Host (clientId == 0) assume a comanda
            var claimOk = NightCycleService.ToggleOrderClaim(state, orderId, 0UL);
            Assert.IsTrue(claimOk.Success);
            Assert.AreEqual(0UL, order.ClaimedByClientId);
            Assert.AreEqual(OrderTaskBoardStage.InPrep, order.TaskStage);

            // Outro cliente não pode roubar comanda já assumida
            var stealFail = NightCycleService.ToggleOrderClaim(state, orderId, 1UL);
            Assert.IsFalse(stealFail.Success);

            // Caixa embalada na bancada com endereço da comanda -> Ready
            var boxId = state.GenerateItemId();
            var box = new ItemState(boxId, 2, ItemCategory.Pizza, LocationRef.InStation(1, 0))
            {
                CookingStage = CookingStage.Baked,
                PackagingState = PackagingState.Boxed,
                LabelAddress = new AddressId(5)
            };
            state.Items[boxId] = box;
            Assert.AreEqual(OrderTaskBoardStage.Ready, NightCycleService.ComputeOrderTaskStage(state, order));

            // Caixa transferida para mochila -> OutForDelivery
            box.Location = new LocationRef(LocationType.Backpack, 1);
            Assert.AreEqual(OrderTaskBoardStage.OutForDelivery, NightCycleService.ComputeOrderTaskStage(state, order));

            // Entrega concluída -> Completed
            order.Lifecycle = OrderLifecycle.Delivered;
            Assert.AreEqual(OrderTaskBoardStage.Completed, NightCycleService.ComputeOrderTaskStage(state, order));
        }

        [Test]
        public void BackpackService_DamageRuptureDropsAtMostOneLastBox_BlocksStoreAllowsRetrieveAndRepairs()
        {
            var state = new PizzeriaState();
            var bpId = new BackpackId(1);
            Assert.IsTrue(BackpackService.Register(
                state,
                bpId,
                4,
                new DomainVector3(0f, 0f, 0f),
                new DomainQuaternion(0f, 0f, 0f, 1f)).Success);
            Assert.IsTrue(BackpackService.Equip(state, bpId, 0UL).Success);

            var handLeft = LocationRef.InHand(0UL, HandSlotIndex.Left);
            var handRight = LocationRef.InHand(0UL, HandSlotIndex.Right);

            var item1 = new ItemState(new ItemId(101), 2, ItemCategory.Pizza, handLeft)
            {
                CookingStage = CookingStage.Baked,
                PackagingState = PackagingState.Boxed,
                LabelAddress = new AddressId(1)
            };
            var item2 = new ItemState(new ItemId(102), 2, ItemCategory.Pizza, handRight)
            {
                CookingStage = CookingStage.Baked,
                PackagingState = PackagingState.Boxed,
                LabelAddress = new AddressId(2)
            };
            state.Items[item1.Id] = item1;
            state.Items[item2.Id] = item2;

            Assert.IsTrue(BackpackService.StoreItem(state, bpId, item1.Id, handLeft).Success);
            Assert.IsTrue(BackpackService.StoreItem(state, bpId, item2.Id, handRight).Success);
            Assert.AreEqual(2, state.Backpacks[bpId].Items.Count);

            // Desgaste parcial não rasga nem derruba caixa
            bool droppedPartial = BackpackService.ApplyBackpackDamage(state, bpId, 60, new DomainVector3(2f, 0f, 3f), out var dropped1);
            Assert.IsFalse(droppedPartial);
            Assert.IsFalse(dropped1.IsValid);
            Assert.AreEqual(40, state.Backpacks[bpId].Integrity);
            Assert.IsFalse(state.Backpacks[bpId].IsTorn);

            // Ruptura derruba no máximo 1 caixa (a última inserida) no mundo
            bool droppedRupture = BackpackService.ApplyBackpackDamage(state, bpId, 50, new DomainVector3(4f, 0f, 5f), out var dropped2);
            Assert.IsTrue(droppedRupture);
            Assert.AreEqual(new ItemId(102), dropped2);
            Assert.AreEqual(0, state.Backpacks[bpId].Integrity);
            Assert.IsTrue(state.Backpacks[bpId].IsTorn);
            Assert.AreEqual(LocationType.World, state.Items[item2.Id].Location.Type);
            Assert.AreEqual(1, state.Backpacks[bpId].Items.Count);

            // Dano adicional já rasgada não derruba segunda caixa
            bool droppedExtra = BackpackService.ApplyBackpackDamage(state, bpId, 30, new DomainVector3(6f, 0f, 7f), out var dropped3);
            Assert.IsFalse(droppedExtra);
            Assert.IsFalse(dropped3.IsValid);
            Assert.AreEqual(1, state.Backpacks[bpId].Items.Count);

            // Guardar caixa nova enquanto rasgada é bloqueado
            var item3 = new ItemState(new ItemId(103), 2, ItemCategory.Pizza, handLeft)
            {
                CookingStage = CookingStage.Baked,
                PackagingState = PackagingState.Boxed,
                LabelAddress = new AddressId(3)
            };
            state.Items[item3.Id] = item3;
            Assert.IsFalse(BackpackService.StoreItem(state, bpId, item3.Id, handLeft).Success);

            // Retirar caixa restante enquanto rasgada continua permitido (usando mão direita livre)
            Assert.IsTrue(BackpackService.RetrieveItem(state, bpId, item1.Id, handRight).Success);
            Assert.AreEqual(0, state.Backpacks[bpId].Items.Count);

            // Reparo restaura 100% e reabilita armazenamento
            Assert.IsTrue(BackpackService.RepairBackpack(state, bpId).Success);
            Assert.AreEqual(100, state.Backpacks[bpId].Integrity);
            Assert.IsFalse(state.Backpacks[bpId].IsTorn);
            Assert.IsTrue(BackpackService.StoreItem(state, bpId, item3.Id, handLeft).Success);
            Assert.AreEqual(1, state.Backpacks[bpId].Items.Count);
        }

        [Test]
        public void TransferService_EnforceRequesterOwnership_AllowsHostClientZeroAndBlocksOtherClient()
        {
            var state = new PizzeriaState();
            var hostHand = LocationRef.InHand(0UL, HandSlotIndex.Left);
            var item = new ItemState(state.GenerateItemId(), 2, ItemCategory.Dough, hostHand);
            state.Items[item.Id] = item;

            // Cliente diferente tentando mover item da mão do Host deve falhar
            var otherAttempt = TransferService.TransferItem(
                state,
                item.Id,
                LocationRef.InStation(1, 0),
                requesterId: 1UL,
                bypassReservation: false,
                enforceRequesterOwnership: true);
            Assert.IsFalse(otherAttempt.Success);
            Assert.AreEqual(CommandError.NotOwner, otherAttempt.Error);

            // Host legítimo (clientId == 0) consegue mover seu próprio item com enforceRequesterOwnership ativo
            var hostAttempt = TransferService.TransferItem(
                state,
                item.Id,
                LocationRef.InStation(1, 0),
                requesterId: 0UL,
                bypassReservation: false,
                enforceRequesterOwnership: true);
            Assert.IsTrue(hostAttempt.Success);
            Assert.AreEqual(LocationType.StationSlot, state.Items[item.Id].Location.Type);
        }

        [Test]
        public void ClearTransientNightEntities_ReturnsToolsToConfiguredToolRackWithoutSlotCollision()
        {
            var state = new PizzeriaState
            {
                ToolRackStationId = 15
            };

            var toolInHandId = state.GenerateToolId();
            var toolInHand = new ToolItemState(toolInHandId, 1, ToolCapability.Grate, LocationRef.InHand(0UL, HandSlotIndex.Left));
            toolInHand.TryAcquireLease(101, 10.0, 2.0);
            state.Tools[toolInHandId] = toolInHand;

            var toolOnRackId = state.GenerateToolId();
            var toolOnRack = new ToolItemState(toolOnRackId, 2, ToolCapability.Slice, LocationRef.InStation(15, 0));
            state.Tools[toolOnRackId] = toolOnRack;

            NightCycleService.ClearTransientNightEntities(state, repairBackpacks: true);

            Assert.AreEqual(LocationRef.InStation(15, 0), state.Tools[toolOnRackId].Location);
            Assert.AreEqual(LocationRef.InStation(15, 1), state.Tools[toolInHandId].Location);
            Assert.IsFalse(state.Tools[toolInHandId].IsReserved(10.0));
        }

        [Test]
        public void NightCycle_ClockZero_SettlesOpenOrdersOnceWithoutClosingGraceAndPreservesSupplies()
        {
            var state = new PizzeriaState
            {
                RestaurantBudget = 100,
                Reputation = 100
            };

            NightCycleService.StartNight(state, 1, new NightGoalParameters(1, 10f, 50, 0f, 0f));
            var openOrder = new OrderState(state.GenerateOrderId(), OrderSourceType.Phone, new AddressId(1), CustomerId.None, 0.0, 60.0);
            state.Orders[openOrder.Id] = openOrder;

            // Suprimento pago pendente não deve desaparecer ao avançar de noite
            var supplyId = state.GenerateSupplyOrderId();
            state.SupplyOrders[supplyId] = new SupplyOrderState(supplyId, 1, 1, 5, 25, 0.0, 20.0);

            NightCycleService.RecordNightRevenue(state, 50);
            NightCycleService.TickNight(state, 10f);

            Assert.AreEqual(OrderLifecycle.Expired, openOrder.Lifecycle);
            Assert.AreEqual(1, state.NightOrdersFailed);
            Assert.AreEqual(90, state.Reputation);
            Assert.AreEqual(NightPhase.Results, state.CurrentNightPhase);

            Assert.IsTrue(NightCycleService.AdvanceToNextNight(state));
            Assert.IsTrue(state.SupplyOrders.ContainsKey(supplyId));
            Assert.AreEqual(150, state.RestaurantBudget);
            Assert.AreEqual(50, state.RunTotalRevenue);
        }

        [Test]
        public void SpeciesHostilityAndZombieConversion_EnforceCatalogOffenseCapResetAndIdempotency()
        {
            var hostility = new SpeciesHostilityService { MaxHostilityLevel = 100, PersistAcrossNights = false };
            Assert.IsTrue(hostility.TryRegisterIngredientOffense(NpcSpecies.Vampire, 10)); // Alho
            Assert.IsFalse(hostility.TryRegisterIngredientOffense(NpcSpecies.Werewolf, 10));
            Assert.AreEqual(35, hostility.GetHostility(NpcSpecies.Vampire));

            hostility.RegisterOffense(NpcSpecies.Vampire, 200);
            Assert.AreEqual(100, hostility.GetHostility(NpcSpecies.Vampire));

            hostility.OnNightEnded();
            Assert.AreEqual(0, hostility.GetHostility(NpcSpecies.Vampire));

            var zombie = new ZombieConversionService { MaxInfectedCap = 2, ConversionChance = 1f, ConversionCooldownSeconds = 1.0 };
            var rng = new System.Random(42);
            Assert.IsTrue(zombie.TryConvertTarget(101UL, 0, 10.0, rng));
            Assert.IsFalse(zombie.TryConvertTarget(101UL, 1, 15.0, rng)); // Idempotente: não converte 2x
            Assert.IsFalse(zombie.TryConvertTarget(102UL, 1, 10.5, rng)); // Cooldown
            Assert.IsTrue(zombie.TryConvertTarget(102UL, 1, 12.0, rng));
            Assert.IsFalse(zombie.TryConvertTarget(103UL, 2, 20.0, rng)); // Teto atingido
        }

        [Test]
        public void RunProgression_ThreeNightsVotesModifiersShopAndCleanReset()
        {
            var state = new PizzeriaState { RestaurantBudget = 100 };
            var run = new RunProgressionService(seed: 777, maxNights: 3, difficulty: RunDifficulty.Normal);

            // T52: Escala congelada na noite mesmo se alguém desconectar
            run.FreezeNightStartParameters(state, 2);
            int night1Goal = state.NightRevenueGoal;
            Assert.AreEqual(run.ComputeTabulatedGoal(1, 2), night1Goal);

            // T53 & T54: Votação com empate, desconexão e prontidão coletiva
            var options = run.OpenChoiceVote(1, 2);
            Assert.AreEqual(2, options.Count);
            Assert.IsTrue(run.CastVote(0UL, options[0].Id));
            Assert.IsTrue(run.CastVote(1UL, options[1].Id));
            run.SetPlayerReady(0UL, true);
            run.SetPlayerReady(1UL, false);
            Assert.IsFalse(run.CanStartNight(new[] { 0UL, 1UL }));

            run.OnParticipantDisconnected(1UL);
            var winner = run.ResolveVoteServer();
            Assert.IsNotNull(winner);
            Assert.IsTrue(run.CanStartNight(new[] { 0UL }));

            // T55: Modificador temporário expira, permanente persiste sem duplicar
            run.ApplyModifier(new RunModifierDefinition { Id = "blood_moon", Duration = RunModifierDuration.OneNight, Value = 0.5f });
            run.ApplyModifier(new RunModifierDefinition { Id = "oven_timer", Duration = RunModifierDuration.Permanent, Value = 1f });
            Assert.IsTrue(run.RecalculateEffectiveStats().OvenAutoShutoffEnabled);
            Assert.Greater(run.RecalculateEffectiveStats().VampireSpawnMultiplier, 1f);

            // T56: Compra única atômica com saldo para 1 unidade
            var item = new RunShopItem { Id = "espresso_machine", Name = "Café", Cost = 80, IsUnique = true };
            Assert.IsTrue(run.TryPurchaseShopItem(state, item));
            Assert.AreEqual(20, state.RestaurantBudget);
            Assert.IsFalse(run.TryPurchaseShopItem(state, item));

            // T59: Avançar N1 -> N2 -> N3 e encerrar RUN na 3ª noite
            state.CurrentNightPhase = NightPhase.Results;
            Assert.IsTrue(run.TryCompleteNightAndAdvance(state));
            Assert.AreEqual(2, state.CurrentNightNumber);
            Assert.IsFalse(run.ActiveModifiers.ContainsKey("blood_moon"));
            Assert.IsTrue(run.ActiveModifiers.ContainsKey("oven_timer"));

            state.CurrentNightPhase = NightPhase.Results;
            Assert.IsTrue(run.TryCompleteNightAndAdvance(state));
            Assert.AreEqual(3, state.CurrentNightNumber);

            state.CurrentNightPhase = NightPhase.Results;
            Assert.IsFalse(run.TryCompleteNightAndAdvance(state));
            Assert.IsTrue(run.IsRunCompleted);
            Assert.IsTrue(run.IsRunVictory);

            // Nova RUN limpa toda persistência
            run.ResetRun(state, seed: 888);
            Assert.IsFalse(run.IsRunCompleted);
            Assert.AreEqual(0, run.ActiveModifiers.Count);
            Assert.AreEqual(1, state.CurrentNightNumber);
        }

        [Test]
        public void EmployeeTaskService_ExclusiveReservationsHumanTakeoverAndNightPersistence()
        {
            var state = new PizzeriaState { RestaurantBudget = 400 };
            var service = new EmployeeTaskService();

            var cashier = service.TryHireEmployee(state, EmployeeRole.Cashier);
            var pizzaiolo = service.TryHireEmployee(state, EmployeeRole.Pizzaiolo);
            Assert.IsNotNull(cashier);
            Assert.IsNotNull(pizzaiolo);
            Assert.AreEqual(100, state.RestaurantBudget); // 400 - 180 - 120

            var task = service.EnqueueOrderTask(new OrderId(50), EmployeeRole.Pizzaiolo);
            Assert.IsTrue(service.TryReserveTask(task.TaskId, (ulong)pizzaiolo.EmployeeId, isHuman: false, now: 10.0));
            // Humano não rouba por TryReserveTask enquanto lease ativa; usa HumanTakeoverTask
            Assert.IsFalse(service.TryReserveTask(task.TaskId, 0UL, isHuman: true, now: 12.0));
            Assert.IsTrue(service.HumanTakeoverTask(task.TaskId, 0UL, now: 12.0));
            Assert.IsTrue(task.IsReservedByHuman);

            // Liberação por cancelamento/desconexão torna tarefa disponível novamente
            service.ReleaseActorTasks(0UL, isHuman: true);
            Assert.AreEqual(EmployeeTaskState.Available, task.State);

            // Persistência entre noites mantém funcionários contratados e limpa apenas na nova RUN
            pizzaiolo.IsAlive = false;
            service.OnNightEnded();
            Assert.AreEqual(2, service.Employees.Count);
            Assert.IsTrue(pizzaiolo.IsAlive);

            service.ResetForNewRun();
            Assert.AreEqual(0, service.Employees.Count);
        }

        [Test]
        public void R1_LifeStateCleanupReleasesAttendedCustomersClaimedOrdersAndBackpack()
        {
            var state = new PizzeriaState();
            var catalog = new DefinitionCatalog();
            var clock = new FixedClock(100.0);
            var customerService = new CustomerService(new QueueService(), new TableService());
            var orderService = new OrderService();

            var cRes = customerService.SpawnWalkInCustomer(state, catalog, clock, rng: new System.Random(1));
            Assert.IsTrue(cRes.Success);
            var cust = state.Customers[cRes.Value];
            cust.AttendedByPlayerId = 7UL;

            var oRes = orderService.CreatePhoneOrder(new AddressId(1), new[] { (1, 1) }, state, catalog, clock, 6);
            Assert.IsTrue(oRes.Success);
            state.Orders[oRes.Value].ClaimedByClientId = 7UL;

            var bpId = state.GenerateBackpackId();
            Assert.IsTrue(BackpackService.Register(state, bpId, 6, new DomainVector3(0f, 0f, 0f), new DomainQuaternion(0f, 0f, 0f, 1f)).Success);
            Assert.IsTrue(BackpackService.Equip(state, bpId, 7UL).Success);
            var bp = state.Backpacks[bpId];

            customerService.ReleaseCustomersAttendedByPlayer(state, 7UL);
            orderService.ReleaseOrdersClaimedByPlayer(state, 7UL);
            BackpackService.ReleaseOnDisconnect(state, 7UL, new DomainVector3(2, 0, 3));

            Assert.AreEqual(0UL, cust.AttendedByPlayerId);
            Assert.AreEqual(OrderState.UnclaimedClientId, state.Orders[oRes.Value].ClaimedByClientId);
            Assert.IsFalse(bp.IsEquipped);
            Assert.AreEqual(BackpackState.NoOwner, bp.EquippedBy);
        }

        [Test]
        public void R3_MultiPizzaMissingCountsWrongDamagedBagHalvesCapacityAndSuppliesTick()
        {
            var state = new PizzeriaState { RestaurantBudget = 100 };
            NightCycleService.StartNight(state, 1);

            var catalog = new DefinitionCatalog();
            var orderService = new OrderService();
            var clock = new FixedClock(50.0);
            var oRes = orderService.CreatePhoneOrder(new AddressId(2), new[] { (1, 3) }, state, catalog, clock, 6, deliveryDuration: 120.0);
            Assert.IsTrue(oRes.Success);

            // Cria 2 pizzas corretas (faltando 1 de 3)
            var deliveredIds = new System.Collections.Generic.List<ItemId>();
            for (int i = 0; i < 2; i++)
            {
                var pid = state.GenerateItemId();
                var pizza = new ItemState(pid, 100, ItemCategory.Pizza, LocationRef.InHand(0UL, HandSlotIndex.Left));
                pizza.CookingStage = CookingStage.Baked;
                pizza.PackagingState = PackagingState.Boxed;
                pizza.LabelAddress = new AddressId(2);
                state.Items[pid] = pizza;
                deliveredIds.Add(pid);
            }

            var delivery = new DeliveryService();
            var dRes = delivery.SubmitDelivery(state, new AddressId(2), deliveredIds, attemptId: 1001, requesterId: 0UL, catalog, orderService, 0f, 0f, 15, nowTime: 60.0);
            Assert.IsTrue(dRes.Success);
            Assert.AreEqual(2, dRes.Value.CorrectBakedCount);
            Assert.AreEqual(1, dRes.Value.WrongOrIncompleteCount);
            Assert.AreEqual(dRes.Value.RestaurantEarned, state.NightEarnedRevenue);
            Assert.AreEqual(90, state.Reputation); // -10 por entrega incompleta

            // T39: mochila danificada reduz capacidade efetiva pela metade
            var bpId = state.GenerateBackpackId();
            Assert.IsTrue(BackpackService.Register(state, bpId, 4, new DomainVector3(0, 0, 0), new DomainQuaternion(0, 0, 0, 1)).Success);
            var bp = state.Backpacks[bpId];
            bp.Integrity = 50;
            Assert.IsTrue(bp.IsDamaged);
            Assert.AreEqual(2, bp.EffectiveCapacity);

            // T18: piso mínimo de ingredientes + TickSupplies
            state.StorageSlots[(1, 1)] = new StorageSlotState(1, 1, 0, 20);
            NightCycleService.EnsureMinimumIngredientFloor(state);
            Assert.GreaterOrEqual(state.StorageSlots[(1, 1)].Quantity, 2);

            var supId = state.GenerateSupplyOrderId();
            state.SupplyOrders[supId] = new SupplyOrderState(supId, 1, 1, 5, 25, 60.0, 90.0);
            Assert.IsFalse(NightCycleService.TickSupplies(state, 80.0));
            Assert.IsTrue(NightCycleService.TickSupplies(state, 95.0));
            Assert.GreaterOrEqual(state.StorageSlots[(1, 1)].Quantity, 7);
        }

        [Test]
        public void R4_OvenTimerPreventsBurnAndOvenBurnsWithoutModifier()
        {
            var state = new PizzeriaState();
            var catalog = new DefinitionCatalog();
            var clock = new FixedClock(0.0);
            var processing = new ProcessingService();
            var ovenService = new OvenService();

            var pizzaId = state.GenerateItemId();
            var pizza = new ItemState(pizzaId, 100, ItemCategory.Pizza, LocationRef.InHand(0UL, HandSlotIndex.Left));
            state.Items[pizzaId] = pizza;

            var ovenSlot = new StationSlotId(1, 0);
            Assert.IsTrue(ovenService.InsertPizza(state, ovenSlot, pizzaId, 0UL, catalog, defaultBakeDuration: 5f).Success);

            clock.Now = 10.0; // 5s bake + 5s past bake (> grace 2s)
            processing.TickOven(ovenSlot, state, burnGraceDuration: 2f, clock, ovenAutoShutoff: true);
            Assert.AreEqual(CookingStage.Baked, pizza.CookingStage);
            Assert.AreEqual(0f, pizza.BurnProgress);

            // Sem o modificador oven_timer, queima após a tolerância
            processing.TickOven(ovenSlot, state, burnGraceDuration: 2f, clock, ovenAutoShutoff: false);
            Assert.AreEqual(CookingStage.Burned, pizza.CookingStage);
        }

        [Test]
        public void R5_SpeciesHostilityGarlicOffenseAndZombieConversionIdempotency()
        {
            var hostility = new SpeciesHostilityService();
            Assert.AreEqual(0, hostility.GetHostility(NpcSpecies.Vampire));

            // Ingrediente comum não ofende vampiro
            Assert.IsFalse(hostility.TryRegisterIngredientOffense(NpcSpecies.Vampire, 1));
            Assert.AreEqual(0, hostility.GetHostility(NpcSpecies.Vampire));

            // Alho (id 10) ofende vampiro e aumenta hostilidade
            Assert.IsTrue(hostility.TryRegisterIngredientOffense(NpcSpecies.Vampire, 10));
            Assert.Greater(hostility.GetHostility(NpcSpecies.Vampire), 0);

            // Reset no fim da noite (PersistAcrossNights == false por padrão)
            hostility.OnNightEnded();
            Assert.AreEqual(0, hostility.GetHostility(NpcSpecies.Vampire));

            // Conversão de zumbi: única por targetId e respeita teto
            var zSvc = new ZombieConversionService { MaxInfectedCap = 2, ConversionChance = 1.0f, ConversionCooldownSeconds = 0.0 };
            var rng = new System.Random(42);
            Assert.IsTrue(zSvc.TryConvertTarget(101UL, activeInfectedCount: 0, now: 1.0, rng: rng));
            Assert.IsFalse(zSvc.TryConvertTarget(101UL, activeInfectedCount: 1, now: 2.0, rng: rng));
            Assert.IsFalse(zSvc.TryConvertTarget(102UL, activeInfectedCount: 2, now: 3.0, rng: rng));
        }

        [Test]
        public void R6_EmployeeActorIdIsolationAndBlockedTaskPreservation()
        {
            var state = new PizzeriaState { RestaurantBudget = 1000 };
            var svc = new EmployeeTaskService();
            var emp1 = svc.TryHireEmployee(state, EmployeeRole.Pizzaiolo);
            var emp2 = svc.TryHireEmployee(state, EmployeeRole.Pizzaiolo);
            Assert.IsNotNull(emp1);
            Assert.IsNotNull(emp2);

            var task = svc.EnqueueOrderTask(new OrderId(77), EmployeeRole.Pizzaiolo);
            Assert.IsTrue(svc.TryReserveTask(task.TaskId, (ulong)emp1.EmployeeId, isHuman: false, now: 10.0, leaseSeconds: 20.0));

            // Humano com ClientId == emp1.EmployeeId (1) NÃO pode colidir em TryReserveTask
            Assert.IsFalse(svc.TryReserveTask(task.TaskId, 1UL, isHuman: true, now: 11.0, leaseSeconds: 20.0));

            // Tarefa bloqueada permanece reservada a emp1 durante lease, impedindo emp2 de roubar
            svc.MarkTaskBlocked(task.TaskId, EmployeeBlockReason.NoDough);
            Assert.AreEqual(EmployeeTaskState.Blocked, task.State);
            Assert.IsFalse(svc.TryReserveTask(task.TaskId, (ulong)emp2.EmployeeId, isHuman: false, now: 12.0, leaseSeconds: 20.0));

            // Takeover explícito por humano assume sem duplicar e libera emp1
            Assert.IsTrue(svc.HumanTakeoverTask(task.TaskId, 1UL, now: 13.0));
            Assert.IsTrue(task.IsReservedByHuman);
            Assert.AreEqual(-1, emp1.AssignedTaskId);
        }
    }

    internal sealed class FixedClock : ScaryParty.Pizzeria.Domain.Ports.IClock
    {
        public FixedClock(double now) => Now = now;
        public double Now { get; set; }
    }
}

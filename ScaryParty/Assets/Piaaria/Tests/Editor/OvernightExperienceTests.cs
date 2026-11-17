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
    }
}

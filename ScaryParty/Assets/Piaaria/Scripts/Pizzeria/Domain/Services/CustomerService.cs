using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    /// <summary>
    /// Serviço de domínio responsável pelo ciclo de vida dos clientes presenciais (NPCs):
    /// - Sorteio de 1 traço positivo + 1 traço negativo compatíveis entre si
    /// - Entrada em uma das 2 filas (escolhendo a menor fila e sem trocar continuamente)
    /// - Atendimento no balcão (segurar F por 0,6 s) reservando mesa livre e publicando pedido de 1 pizza no quadro
    /// - Decaimento de paciência na fila (desde a entrada, com multiplicador de estagnação) e na mesa (incluindo caminhada até a mesa)
    /// - Expiração de pedido quando a paciência acaba (liberando a mesa sem destruir pizzas nas mãos/bancadas)
    /// - Serviço de mesa (pizza sem caixa, pagamento único ao aceitar, tempo comendo e saída)
    /// </summary>
    public class CustomerService
    {
        private static readonly Random SharedRng = new Random();
        private readonly QueueService _queueService;
        private readonly TableService _tableService;
        private readonly CustomerSatisfactionEvaluator _satisfactionEvaluator;

        public QueueService QueueService => _queueService;
        public TableService TableService => _tableService;
        public CustomerSatisfactionEvaluator SatisfactionEvaluator => _satisfactionEvaluator;

        public CustomerService(
            QueueService queueService = null,
            TableService tableService = null,
            CustomerSatisfactionEvaluator satisfactionEvaluator = null)
        {
            _queueService = queueService ?? new QueueService();
            _tableService = tableService ?? new TableService();
            _satisfactionEvaluator = satisfactionEvaluator ?? new CustomerSatisfactionEvaluator();
        }

        public int CountActiveDineInCustomers(PizzeriaState state)
        {
            if (state == null) return 0;
            int count = 0;
            foreach (var cust in state.Customers.Values)
            {
                if (cust.Phase != CustomerPhase.LeavingSatisfied &&
                    cust.Phase != CustomerPhase.LeavingAngry &&
                    cust.Phase != CustomerPhase.Despawned)
                {
                    count++;
                }
            }
            return count;
        }

        public static bool TrySelectNextChannel(
            bool canSpawnDineIn,
            bool canSpawnDelivery,
            float dineInWeight,
            float deliveryWeight,
            int maxConsecutiveSameChannel,
            ref OrderOrigin lastChannel,
            ref int consecutiveCount,
            float random01,
            out OrderOrigin selectedChannel)
        {
            selectedChannel = OrderOrigin.DeliveryCall;

            if (!canSpawnDineIn && !canSpawnDelivery)
                return false;

            if (canSpawnDineIn && !canSpawnDelivery)
            {
                selectedChannel = OrderOrigin.WalkInCustomer;
                RecordChannelSelection(selectedChannel, ref lastChannel, ref consecutiveCount);
                return true;
            }

            if (!canSpawnDineIn && canSpawnDelivery)
            {
                selectedChannel = OrderOrigin.DeliveryCall;
                RecordChannelSelection(selectedChannel, ref lastChannel, ref consecutiveCount);
                return true;
            }

            int streakLimit = Math.Max(1, maxConsecutiveSameChannel);
            if (consecutiveCount >= streakLimit)
            {
                selectedChannel = lastChannel == OrderOrigin.WalkInCustomer
                    ? OrderOrigin.DeliveryCall
                    : OrderOrigin.WalkInCustomer;
                RecordChannelSelection(selectedChannel, ref lastChannel, ref consecutiveCount);
                return true;
            }

            float wDineIn = Math.Max(0f, dineInWeight);
            float wDelivery = Math.Max(0f, deliveryWeight);
            float total = wDineIn + wDelivery;
            if (total <= 0.0001f)
            {
                wDineIn = 0.5f;
                wDelivery = 0.5f;
                total = 1.0f;
            }

            float clamped = random01 < 0f ? 0f : (random01 > 1f ? 1f : random01);
            float threshold = wDineIn / total;
            selectedChannel = (clamped < threshold)
                ? OrderOrigin.WalkInCustomer
                : OrderOrigin.DeliveryCall;

            RecordChannelSelection(selectedChannel, ref lastChannel, ref consecutiveCount);
            return true;
        }

        private static void RecordChannelSelection(OrderOrigin chosen, ref OrderOrigin lastChannel, ref int consecutiveCount)
        {
            if (consecutiveCount == 0 || lastChannel != chosen)
            {
                lastChannel = chosen;
                consecutiveCount = 1;
            }
            else
            {
                consecutiveCount++;
            }
        }

        public static (int positiveTraitId, int negativeTraitId) RollCompatibleTraitPair(DefinitionCatalog catalog, Random rng = null)
        {
            var r = rng ?? SharedRng;
            var positives = new List<CustomerTraitData>();
            var negatives = new List<CustomerTraitData>();

            if (catalog != null && catalog.CustomerTraits != null && catalog.CustomerTraits.Count > 0)
            {
                foreach (var tr in catalog.CustomerTraits.Values)
                {
                    if (tr.Polarity == TraitPolarity.Positive) positives.Add(tr);
                    else negatives.Add(tr);
                }
            }

            if (positives.Count == 0 || negatives.Count == 0)
            {
                var defaults = CustomerTraitData.CreateDefaultTenTraits();
                positives.Clear();
                negatives.Clear();
                foreach (var tr in defaults)
                {
                    if (tr.Polarity == TraitPolarity.Positive) positives.Add(tr);
                    else negatives.Add(tr);
                }
            }

            int posIdx = r.Next(0, positives.Count);
            var chosenPos = positives[posIdx];

            var compatibleNegs = new List<CustomerTraitData>();
            for (int i = 0; i < negatives.Count; i++)
            {
                if (chosenPos.IsCompatibleWith(negatives[i]))
                {
                    compatibleNegs.Add(negatives[i]);
                }
            }

            if (compatibleNegs.Count == 0)
            {
                compatibleNegs.AddRange(negatives);
            }

            int negIdx = r.Next(0, compatibleNegs.Count);
            var chosenNeg = compatibleNegs[negIdx];

            return (chosenPos.Id, chosenNeg.Id);
        }

        public CommandResult<CustomerId> SpawnWalkInCustomer(
            PizzeriaState state,
            DefinitionCatalog catalog,
            IClock clock,
            int queueCount = 2,
            int maxCustomersPerQueue = 3,
            int maxActiveDineInCustomers = 6,
            float queuePatienceMin = 60f,
            float queuePatienceMax = 100f,
            float foodPatienceMin = 180f,
            float foodPatienceMax = 300f,
            Random rng = null)
        {
            if (state == null)
                return CommandResult<CustomerId>.Fail(CommandError.InvalidState, "Estado nulo.");

            if (CountActiveDineInCustomers(state) >= maxActiveDineInCustomers)
                return CommandResult<CustomerId>.Fail(CommandError.SlotFull, "Limite de clientes presenciais ativos atingido.");

            var r = rng ?? SharedRng;
            int chosenQueue = _queueService.ChooseBestQueueOnEntry(state, queueCount, maxCustomersPerQueue, r);
            if (chosenQueue < 0)
                return CommandResult<CustomerId>.Fail(CommandError.SlotFull, "Ambas as filas estão cheias.");

            int slotIndex = _queueService.GetQueueOccupancy(state, chosenQueue);
            var (posTraitId, negTraitId) = RollCompatibleTraitPair(catalog, r);

            CustomerTraitData posTrait = null;
            CustomerTraitData negTrait = null;
            if (catalog != null)
            {
                catalog.CustomerTraits.TryGetValue(posTraitId, out posTrait);
                catalog.CustomerTraits.TryGetValue(negTraitId, out negTrait);
            }

            float qMult = (posTrait?.QueuePatienceMultiplier ?? 1f) * (negTrait?.QueuePatienceMultiplier ?? 1f);
            float fMult = (posTrait?.FoodPatienceMultiplier ?? 1f) * (negTrait?.FoodPatienceMultiplier ?? 1f);

            float rawQ = queuePatienceMin + (float)r.NextDouble() * Math.Max(1f, queuePatienceMax - queuePatienceMin);
            float rawF = foodPatienceMin + (float)r.NextDouble() * Math.Max(1f, foodPatienceMax - foodPatienceMin);

            var spec = PizzaOrderGenerator.GenerateRandomSpecification(catalog, r);
            var customerId = state.GenerateCustomerId();
            int colorIdx = r.Next(0, 8);

            var customer = new CustomerState(
                customerId,
                chosenQueue,
                slotIndex,
                spec,
                posTraitId,
                negTraitId,
                rawQ * qMult,
                rawF * fMult,
                colorIdx);

            state.Customers[customerId] = customer;
            if (!state.QueueLastAdvanceTime.ContainsKey(chosenQueue))
            {
                state.QueueLastAdvanceTime[chosenQueue] = clock?.Now ?? 0.0;
            }

            state.IncrementRevision();
            return CommandResult<CustomerId>.Ok(customerId);
        }

        /// <summary>
        /// Conclui o atendimento de um cliente na frente da fila:
        /// - Verifica se o cliente está no slot 0 da fila
        /// - Reserva atomicamente uma mesa livre
        /// - Cria o pedido presencial no quadro de comandas (OrderService.CreateDineInOrder)
        /// - Avança os demais clientes da fila
        /// </summary>
        public CommandResult<OrderId> CompleteAttendCustomer(
            PizzeriaState state,
            CustomerId customerId,
            OrderService orderService,
            IClock clock,
            int maxActiveOrders = 6)
        {
            if (state == null || orderService == null)
                return CommandResult<OrderId>.Fail(CommandError.InvalidState, "Estado ou OrderService nulo.");

            if (!state.Customers.TryGetValue(customerId, out var customer))
                return CommandResult<OrderId>.Fail(CommandError.ItemNotFound, "Cliente não encontrado.");

            if (!customer.IsInQueue || customer.QueueSlotIndex != 0)
                return CommandResult<OrderId>.Fail(CommandError.InvalidOperation, "Apenas o primeiro cliente da fila pode ser atendido.");

            _tableService.EnsureDefaultTablesIfEmpty(state, 4);

            var freeTable = _tableService.FindFirstFreeTable(state);
            if (freeTable == null)
            {
                return CommandResult<OrderId>.Fail(CommandError.SlotFull, "Não há mesa livre! Aguarde liberar uma mesa.");
            }

            var lines = new List<(PizzaSpecification spec, int quantity)>
            {
                (customer.RequestedPizza, 1)
            };

            var orderRes = orderService.CreateDineInOrder(
                customerId,
                freeTable.TableId,
                lines,
                state,
                null,
                clock,
                maxActiveOrders,
                customer.FoodPatienceMax);

            if (!orderRes.Success)
            {
                return orderRes;
            }

            int prevQueue = customer.QueueIndex;
            freeTable.Reserve(customerId, orderRes.Value);

            customer.AssignedTableId = freeTable.TableId;
            customer.OrderId = orderRes.Value;
            customer.QueueIndex = -1;
            customer.QueueSlotIndex = -1;
            customer.AttendedByPlayerId = 0;
            customer.Phase = CustomerPhase.WalkingToTable;
            customer.Revision++;

            _queueService.CompactAndAdvanceQueue(state, prevQueue, clock?.Now ?? 0.0);
            state.IncrementRevision();

            return orderRes;
        }

        /// <summary>
        /// Serve uma pizza SEM CAIXA diretamente na mesa de um cliente que está aguardando comida.
        /// Pizzas embaladas em caixa são rejeitadas no serviço de mesa (delivery exige caixa, mesa exige pizza sem caixa).
        /// </summary>
        public CommandResult<CustomerSatisfactionResult> ServeTableUnboxedPizza(
            PizzeriaState state,
            int tableId,
            ItemId pizzaItemId,
            ulong servingPlayerId,
            DefinitionCatalog catalog,
            OrderService orderService,
            float burnedPayoutFactor = 0f,
            float unbakedPayoutFactor = 0f,
            int restaurantBaseBonus = 10,
            float eatingDuration = 12f)
        {
            if (state == null)
                return CommandResult<CustomerSatisfactionResult>.Fail(CommandError.InvalidState, "Estado nulo.");

            if (!state.Tables.TryGetValue(tableId, out var table) || table.IsFree)
                return CommandResult<CustomerSatisfactionResult>.Fail(CommandError.InvalidOperation, "Não há cliente aguardando nesta mesa.");

            if (!state.Customers.TryGetValue(table.ReservedByCustomerId, out var customer))
                return CommandResult<CustomerSatisfactionResult>.Fail(CommandError.ItemNotFound, "Cliente da mesa não encontrado.");

            if (!customer.IsWaitingForFood || customer.HasPaid)
                return CommandResult<CustomerSatisfactionResult>.Fail(CommandError.InvalidOperation, "Este cliente não está aguardando pizza no momento.");

            if (!state.Items.TryGetValue(pizzaItemId, out var item))
                return CommandResult<CustomerSatisfactionResult>.Fail(CommandError.ItemNotFound, "Pizza não encontrada.");

            if (item.Location.Type != LocationType.Hand || item.Location.HolderId != servingPlayerId)
                return CommandResult<CustomerSatisfactionResult>.Fail(CommandError.NotOwner, "Segure a pizza na mão para servir a mesa.");

            if (item.IsBoxed)
            {
                return CommandResult<CustomerSatisfactionResult>.Fail(
                    CommandError.InvalidOperation,
                    "Clientes na mesa recebem a pizza diretamente no prato (sem caixa)! Use caixas apenas para Delivery.");
            }

            if (item.Category != ItemCategory.Pizza && item.Category != ItemCategory.PizzaBase)
            {
                return CommandResult<CustomerSatisfactionResult>.Fail(
                    CommandError.InvalidOperation,
                    "Apenas uma pizza montada pode ser servida na mesa.");
            }

            if (item.CookingStage == CookingStage.Uncooked || item.CookingStage == CookingStage.Cooking)
            {
                return CommandResult<CustomerSatisfactionResult>.Fail(
                    CommandError.InvalidOperation,
                    "Asse a pizza no forno antes de servir ao cliente na mesa!");
            }

            // Avaliar satisfação e pagamento único
            var eval = _satisfactionEvaluator.EvaluateDineInService(
                customer,
                item,
                catalog,
                burnedPayoutFactor,
                unbakedPayoutFactor,
                restaurantBaseBonus);

            // Mover pizza para o prato da mesa (visível enquanto o cliente come)
            item.Location = new LocationRef(LocationType.TablePlate, (ulong)tableId, 0);
            item.Revision++;
            table.ServedPizzaItemId = item.Id;
            table.Revision++;

            // Pagamento único ao aceitar a pizza
            customer.HasPaid = true;
            customer.Phase = CustomerPhase.Eating;
            customer.EatingRemainingSeconds = Math.Max(3f, eatingDuration);
            customer.Revision++;

            state.RestaurantBudget += eval.RestaurantEarned;

            // Concluir e arquivar o pedido correspondente no quadro
            if (customer.OrderId.IsValid && state.Orders.TryGetValue(customer.OrderId, out var order))
            {
                for (int i = 0; i < order.Lines.Count; i++)
                {
                    order.Lines[i].DeliveredQuantity = order.Lines[i].Quantity;
                }
                order.Lifecycle = OrderLifecycle.Delivered;
                order.IsArchivedFromBoard = true;
                order.Revision++;
            }

            state.IncrementRevision();
            return CommandResult<CustomerSatisfactionResult>.Ok(eval);
        }

        /// <summary>
        /// Atualiza todos os clientes presenciais no servidor:
        /// - Paciência de fila (para todos na fila, com multiplicador se a fila estiver estagnada)
        /// - Paciência de preparo (começa quando o pedido é aceito no balcão; caminhar até a mesa conta nesse tempo)
        /// - Tempo comendo na mesa -> libera mesa e prato ao levantar e sair
        /// - Expiração por paciência esgotada -> cliente vai embora irritado, pedido expira e libera mesa (sem apagar pizzas nas mãos/bancadas)
        /// </summary>
        public bool TickCustomers(
            PizzeriaState state,
            DefinitionCatalog catalog,
            OrderService orderService,
            IClock clock,
            float dt,
            float stagnationThresholdSeconds = 35f,
            float stagnationDecayMultiplier = 1.35f)
        {
            if (state == null || dt <= 0f) return false;

            bool dirty = false;
            double now = clock?.Now ?? 0.0;
            var toRemove = new List<CustomerId>();

            foreach (var kvp in state.Customers)
            {
                var cust = kvp.Value;

                if (cust.IsInQueue)
                {
                    // Se está sendo atendido ativamente neste instante, ainda conta mas não expira no meio do hold se faltar >0
                    float decayRate = 1f;
                    if (_queueService.IsQueueStagnant(state, cust.QueueIndex, now, stagnationThresholdSeconds))
                    {
                        CustomerTraitData posTrait = null;
                        CustomerTraitData negTrait = null;
                        if (catalog != null)
                        {
                            catalog.CustomerTraits.TryGetValue(cust.PositiveTraitId, out posTrait);
                            catalog.CustomerTraits.TryGetValue(cust.NegativeTraitId, out negTrait);
                        }
                        float stagSens = (posTrait?.StagnationSensitivityMultiplier ?? 1f) * (negTrait?.StagnationSensitivityMultiplier ?? 1f);
                        decayRate = stagnationDecayMultiplier * stagSens;
                    }

                    cust.QueuePatienceRemaining = Math.Max(0f, cust.QueuePatienceRemaining - dt * decayRate);
                    if (cust.QueuePatienceRemaining <= 0f)
                    {
                        int qIdx = cust.QueueIndex;
                        cust.QueueIndex = -1;
                        cust.QueueSlotIndex = -1;
                        cust.AttendedByPlayerId = 0;
                        cust.Phase = CustomerPhase.LeavingAngry;
                        cust.LeavingRemainingSeconds = 5f;
                        cust.Revision++;
                        _queueService.CompactAndAdvanceQueue(state, qIdx, now);
                        dirty = true;
                    }
                }
                else if (cust.IsWaitingForFood)
                {
                    cust.FoodPatienceRemaining = Math.Max(0f, cust.FoodPatienceRemaining - dt);

                    // Sincronizar se o pedido expirou ou foi cancelado
                    bool orderExpired = false;
                    if (cust.OrderId.IsValid && state.Orders.TryGetValue(cust.OrderId, out var order))
                    {
                        if (order.Lifecycle == OrderLifecycle.Expired || order.Lifecycle == OrderLifecycle.Cancelled)
                        {
                            orderExpired = true;
                        }
                    }

                    if (cust.FoodPatienceRemaining <= 0f || orderExpired)
                    {
                        // Expirar pedido sem apagar pizzas que continuam nas mãos ou bancadas
                        if (cust.OrderId.IsValid && state.Orders.TryGetValue(cust.OrderId, out var activeOrder))
                        {
                            if (activeOrder.Lifecycle == OrderLifecycle.Active)
                            {
                                activeOrder.Lifecycle = OrderLifecycle.Expired;
                                activeOrder.IsArchivedFromBoard = true;
                                activeOrder.Revision++;
                            }
                        }

                        if (cust.AssignedTableId > 0)
                        {
                            _tableService.ReleaseTable(state, cust.AssignedTableId);
                            cust.AssignedTableId = -1;
                        }

                        cust.Phase = CustomerPhase.LeavingAngry;
                        cust.LeavingRemainingSeconds = 6f;
                        cust.Revision++;
                        dirty = true;
                    }
                }
                else if (cust.Phase == CustomerPhase.Eating)
                {
                    cust.EatingRemainingSeconds -= dt;
                    if (cust.EatingRemainingSeconds <= 0f)
                    {
                        if (cust.AssignedTableId > 0)
                        {
                            if (state.Tables.TryGetValue(cust.AssignedTableId, out var tbl) && tbl.ServedPizzaItemId.IsValid)
                            {
                                state.Items.Remove(tbl.ServedPizzaItemId);
                            }
                            _tableService.ReleaseTable(state, cust.AssignedTableId);
                            cust.AssignedTableId = -1;
                        }

                        cust.Phase = CustomerPhase.LeavingSatisfied;
                        cust.LeavingRemainingSeconds = 6f;
                        cust.Revision++;
                        dirty = true;
                    }
                }
                else if (cust.Phase == CustomerPhase.LeavingSatisfied || cust.Phase == CustomerPhase.LeavingAngry)
                {
                    cust.LeavingRemainingSeconds -= dt;
                    if (cust.LeavingRemainingSeconds <= 0f)
                    {
                        cust.Phase = CustomerPhase.Despawned;
                        toRemove.Add(cust.Id);
                        dirty = true;
                    }
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                state.Customers.Remove(toRemove[i]);
            }

            if (dirty)
            {
                state.IncrementRevision();
            }

            return dirty;
        }
    }
}

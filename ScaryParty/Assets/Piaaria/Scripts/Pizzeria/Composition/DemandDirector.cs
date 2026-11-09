using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Composition
{
    /// <summary>
    /// Diretor de demanda mista (presencial vs delivery) na Fase 6:
    /// - Geração ponderada 50% presencial / 50% delivery (configurável em PizzeriaConfig);
    /// - Proteção contra sequências longas de um mesmo canal (maxConsecutiveSameChannel = 2);
    /// - Respeita limites separados de filas/mesas presenciais e chamadas/pedidos de delivery;
    /// - Se um canal estiver lotado mas o outro tiver capacidade, redireciona para o canal disponível.
    /// </summary>
    public class DemandDirector : NetworkBehaviour
    {
        public static DemandDirector Instance { get; private set; }

        private float _timer = 0f;
        private float _nextSpawnInterval = 6f;
        private bool _initialized = false;
        private OrderOrigin _lastChannel = OrderOrigin.DeliveryCall;
        private int _consecutiveSameChannelCount = 0;
        private PizzaOrderGenerator _orderGenerator = new PizzaOrderGenerator();

        public OrderOrigin LastChannel => _lastChannel;
        public int ConsecutiveSameChannelCount => _consecutiveSameChannelCount;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        /// <summary>
        /// Seleciona de forma determinística/testável o próximo canal (WalkInCustomer vs DeliveryCall)
        /// respeitando os pesos configurados, a disponibilidade de cada canal e o limite maxConsecutiveSameChannel.
        /// </summary>
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

            // Ambos os canais estão disponíveis: verificar proteção contra sequência longa
            int streakLimit = Mathf.Max(1, maxConsecutiveSameChannel);
            if (consecutiveCount >= streakLimit)
            {
                selectedChannel = lastChannel == OrderOrigin.WalkInCustomer
                    ? OrderOrigin.DeliveryCall
                    : OrderOrigin.WalkInCustomer;
                RecordChannelSelection(selectedChannel, ref lastChannel, ref consecutiveCount);
                return true;
            }

            float wDineIn = Mathf.Max(0f, dineInWeight);
            float wDelivery = Mathf.Max(0f, deliveryWeight);
            float total = wDineIn + wDelivery;
            if (total <= 0.0001f)
            {
                wDineIn = 0.5f;
                wDelivery = 0.5f;
                total = 1.0f;
            }

            float threshold = wDineIn / total;
            selectedChannel = (Mathf.Clamp01(random01) < threshold)
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

        public void TickDemand(PizzeriaRoot root, float deltaTime)
        {
            if (root == null || root.DomainState == null || root.Config == null) return;

            if (!_initialized)
            {
                _initialized = true;
                _timer = 0f;
                _nextSpawnInterval = 5f;
            }

            _timer += deltaTime;
            if (_timer < _nextSpawnInterval) return;

            _timer = 0f;
            var cfg = root.Config;
            _nextSpawnInterval = Random.Range(
                Mathf.Max(4f, cfg.minOrderInterval * 0.75f),
                Mathf.Max(8f, cfg.maxOrderInterval * 0.85f));

            TriggerNextDemandEvent(root);
        }

        public bool TriggerNextDemandEvent(PizzeriaRoot root)
        {
            if (root == null || root.DomainState == null || root.Config == null || root.Catalog == null)
                return false;

            var state = root.DomainState;
            var cfg = root.Config;

            bool canDineIn = CanSpawnDineIn(root);
            bool canDelivery = CanSpawnDelivery(root);

            if (!TrySelectNextChannel(
                canDineIn,
                canDelivery,
                cfg.dineInChannelWeight,
                cfg.deliveryChannelWeight,
                cfg.maxConsecutiveSameChannel,
                ref _lastChannel,
                ref _consecutiveSameChannelCount,
                Random.value,
                out var selected))
            {
                return false;
            }

            if (selected == OrderOrigin.WalkInCustomer)
            {
                return SpawnWalkInCustomerDemand(root);
            }
            else
            {
                return SpawnDeliveryCallDemand(root);
            }
        }

        public bool CanSpawnDineIn(PizzeriaRoot root)
        {
            var state = root.DomainState;
            var cfg = root.Config;
            if (state == null || cfg == null || root.CustomerService == null || root.CustomerService.QueueService == null)
                return false;

            if (root.CustomerService.CountActiveDineInCustomers(state) >= Mathf.Max(1, cfg.maxActiveDineInCustomers))
                return false;

            int bestQueue = root.CustomerService.QueueService.ChooseBestQueueOnEntry(
                state,
                Mathf.Max(1, cfg.queueCount),
                Mathf.Max(1, cfg.maxCustomersPerQueue));
            return bestQueue >= 0;
        }

        public bool CanSpawnDelivery(PizzeriaRoot root)
        {
            var state = root.DomainState;
            var cfg = root.Config;
            if (state == null || cfg == null || root.CallService == null)
                return false;

            if (state.Calls.Count >= Mathf.Max(1, cfg.maxPendingCalls))
                return false;

            int activeOrders = 0;
            foreach (var o in state.Orders.Values)
            {
                if (o.Lifecycle == OrderLifecycle.Active)
                    activeOrders++;
            }

            return activeOrders < Mathf.Max(1, cfg.maxActiveOrders);
        }

        public bool SpawnWalkInCustomerDemand(PizzeriaRoot root)
        {
            var cfg = root.Config;
            var res = root.CustomerService.SpawnWalkInCustomer(
                root.DomainState,
                root.Catalog,
                root.Clock,
                Mathf.Max(1, cfg.queueCount),
                Mathf.Max(1, cfg.maxCustomersPerQueue),
                Mathf.Max(1, cfg.maxActiveDineInCustomers),
                cfg.customerQueuePatienceMin,
                cfg.customerQueuePatienceMax,
                cfg.customerFoodPatienceMin,
                cfg.customerFoodPatienceMax);
            if (res.Success && root.DomainState.Customers.TryGetValue(res.Value, out var cust))
            {
                string summary = PizzaOrderFormatter.FormatShortTitle(cust.RequestedPizza, root.Catalog, false);
                Debug.Log($"[DemandDirector] 🚶 Cliente presencial #{cust.Id.Value} entrou na Fila {cust.QueueIndex + 1} (Slot {cust.QueueSlotIndex + 1}) desejando {summary}!");
                ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return true;
            }
            return false;
        }

        public bool SpawnDeliveryCallDemand(PizzeriaRoot root)
        {
            if (_orderGenerator == null) _orderGenerator = new PizzaOrderGenerator();

            var availableAddresses = new List<AddressId>();
            if (root.AddressRegistry != null && root.AddressRegistry.GetAllIds().Count > 0)
            {
                foreach (var addr in root.AddressRegistry.GetAllIds())
                {
                    bool isOccupied = false;
                    foreach (var o in root.DomainState.Orders.Values)
                    {
                        if (!o.IsDineIn && o.DestinationId == addr && o.Lifecycle == OrderLifecycle.Active)
                        {
                            isOccupied = true;
                            break;
                        }
                    }
                    if (!isOccupied)
                    {
                        foreach (var c in root.DomainState.Calls.Values)
                        {
                            if (c.AddressId == addr)
                            {
                                isOccupied = true;
                                break;
                            }
                        }
                    }

                    if (!isOccupied) availableAddresses.Add(addr);
                }
            }

            AddressId destId = availableAddresses.Count > 0
                ? availableAddresses[Random.Range(0, availableAddresses.Count)]
                : new AddressId(Random.Range(0, 8));

            var lines = new List<OrderLineState>();
            var spec = _orderGenerator.GenerateSpecification(root.Catalog, root.DomainState, root.UpgradeService);
            lines.Add(new OrderLineState(root.DomainState.GenerateOrderLineId(), spec, 1));

            float now = (float)(root.Clock != null ? root.Clock.Now : Time.time);
            float expire = root.Config != null && root.Config.callExpireSeconds > 0f ? root.Config.callExpireSeconds : 45f;
            var res = root.CallService.EnqueueCall(root.DomainState, destId, lines, now, expire);
            if (res.Success)
            {
                string summary = PizzaOrderFormatter.FormatShortTitle(spec, root.Catalog, true);
                Debug.Log($"[DemandDirector] 📞 Chamada de Delivery #{res.Value.Value} ({summary}) para {AddressFormat.Display(destId.Value)}!");
                ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                return true;
            }
            return false;
        }
    }
}

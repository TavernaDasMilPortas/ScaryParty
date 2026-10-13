using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Domain.Models;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Composition
{
    public class Night1Bootstrapper : NetworkBehaviour
    {
        private float _timer = 0f;
        private bool _isStarted = false;
        private float _nextOrderTime = 5f; // First order after 5 seconds

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (PizzeriaRoot.Instance == null)
            {
                Debug.LogWarning("[Night1Bootstrapper] PizzeriaRoot.Instance is null on OnNetworkSpawn. Will try again in Update.");
                return;
            }

            PizzeriaRoot.Instance.OnPizzeriaInitialized += OnPizzeriaInitialized;
            // In case it was already initialized
            if (PizzeriaRoot.Instance.DomainState != null)
            {
                OnPizzeriaInitialized();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (PizzeriaRoot.Instance != null)
            {
                PizzeriaRoot.Instance.OnPizzeriaInitialized -= OnPizzeriaInitialized;
            }
        }

        private void OnPizzeriaInitialized()
        {
            _isStarted = true;
            Debug.Log("[Night1Bootstrapper] Pizzeria Initialized. Starting Night 1 logic...");
        }

        private void Update()
        {
            if (!IsServer) return;

            // Late-bind if OnNetworkSpawn fired too early
            if (!_isStarted && PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.DomainState != null)
            {
                PizzeriaRoot.Instance.OnPizzeriaInitialized -= OnPizzeriaInitialized;
                PizzeriaRoot.Instance.OnPizzeriaInitialized += OnPizzeriaInitialized;
                OnPizzeriaInitialized();
            }

            if (!_isStarted) return;

            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null || root.Config == null) return;

            var state = root.DomainState;
            var config = root.Config;

            // Count active orders
            int activeOrders = 0;
            foreach (var order in state.Orders.Values)
            {
                if (order.Lifecycle == OrderLifecycle.Accepted)
                    activeOrders++;
            }

            if (activeOrders < config.maxActiveOrders)
            {
                _timer += Time.deltaTime;
                if (_timer >= _nextOrderTime)
                {
                    GenerateAutoOrder();
                    _timer = 0f;
                    _nextOrderTime = Random.Range(config.minOrderInterval, config.maxOrderInterval);
                }
            }
        }

        private void GenerateAutoOrder()
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.Catalog == null || root.Catalog.Recipes.Count == 0 || root.DomainState == null) return;

            // Seleciona endereço do AddressRegistry não ocupado por pedidos ou chamadas
            var availableAddresses = new List<AddressId>();
            if (root.AddressRegistry != null && root.AddressRegistry.GetAllIds().Count > 0)
            {
                foreach (var addr in root.AddressRegistry.GetAllIds())
                {
                    bool isOccupied = false;
                    foreach (var o in root.DomainState.Orders.Values)
                    {
                        if (o.DestinationId == addr && o.Lifecycle == OrderLifecycle.Active)
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

            // Gera 1-2 receitas aleatórias
            var lines = new List<OrderLineState>();
            int numRecipes = Random.Range(1, 3);
            var recipeKeys = new List<int>(root.Catalog.Recipes.Keys);

            for (int i = 0; i < numRecipes; i++)
            {
                int rId = recipeKeys[Random.Range(0, recipeKeys.Count)];
                lines.Add(new OrderLineState(rId, 1));
            }

            float now = (float)(root.Clock != null ? root.Clock.Now : Time.time);
            var res = root.CallService.EnqueueCall(root.DomainState, destId, lines, now, 45f);
            if (res.Success)
            {
                Debug.Log($"[Night1Bootstrapper] 📞 Chamada telefônica agendada #{res.Value.Value} para o endereço {AddressFormat.Display(destId.Value)}!");
                var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;
                if (cmd != null) cmd.CommitAndReplicate();
            }
        }
    }
}

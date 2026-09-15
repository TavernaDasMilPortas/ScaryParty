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
            if (root == null || root.Catalog == null || root.Catalog.Recipes.Count == 0) return;

            // Generate 1-2 random recipes
            var recipes = new List<(int recipeId, int qty)>();
            int numRecipes = Random.Range(1, 3);
            
            var recipeKeys = new List<int>(root.Catalog.Recipes.Keys);
            for (int i = 0; i < numRecipes; i++)
            {
                int rId = recipeKeys[Random.Range(0, recipeKeys.Count)];
                recipes.Add((rId, 1));
            }

            int destId = Random.Range(1, 9);

            var res = root.OrderService.CreatePhoneOrder(destId, recipes, root.DomainState, root.Catalog, root.Clock, root.Config.deliveryTimeLimit);
            if (res.Success)
            {
                Debug.Log($"[Night1Bootstrapper] Pedido automático #{res.Value.Value} gerado para o destino #{destId}.");
                var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;
                if (cmd != null) cmd.CommitAndReplicate();
            }
        }
    }
}

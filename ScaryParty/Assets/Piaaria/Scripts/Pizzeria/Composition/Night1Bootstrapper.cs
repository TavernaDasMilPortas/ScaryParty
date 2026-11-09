using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Composition
{
    public class Night1Bootstrapper : NetworkBehaviour
    {
        private bool _isStarted = false;
        private PizzaOrderGenerator _orderGenerator;
        private DemandDirector _demandDirector;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            _orderGenerator = new PizzaOrderGenerator();
            EnsureDemandDirector();

            if (PizzeriaRoot.Instance == null)
            {
                Debug.LogWarning("[Night1Bootstrapper] PizzeriaRoot.Instance is null on OnNetworkSpawn. Will try again in Update.");
                return;
            }

            PizzeriaRoot.Instance.OnPizzeriaInitialized += OnPizzeriaInitialized;
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

        private void EnsureDemandDirector()
        {
            if (_demandDirector != null) return;
            _demandDirector = DemandDirector.Instance != null
                ? DemandDirector.Instance
                : GetComponent<DemandDirector>();
            if (_demandDirector == null)
            {
                _demandDirector = gameObject.AddComponent<DemandDirector>();
            }
        }

        private void OnPizzeriaInitialized()
        {
            _isStarted = true;
            if (_orderGenerator == null) _orderGenerator = new PizzaOrderGenerator();
            EnsureDemandDirector();
            Debug.Log("[Night1Bootstrapper] Pizzeria Initialized. Starting Night 1 mixed demand (50% Presencial / 50% Delivery)...");
        }

        private void Update()
        {
            if (!IsServer) return;

            if (!_isStarted && PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.DomainState != null)
            {
                PizzeriaRoot.Instance.OnPizzeriaInitialized -= OnPizzeriaInitialized;
                PizzeriaRoot.Instance.OnPizzeriaInitialized += OnPizzeriaInitialized;
                OnPizzeriaInitialized();
            }

            if (!_isStarted) return;

            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null || root.Config == null) return;

            EnsureDemandDirector();
            if (_demandDirector != null)
            {
                _demandDirector.TickDemand(root, Time.deltaTime);
            }
        }

        /// <summary>
        /// Aciona imediatamente o próximo evento de demanda (presencial ou delivery) via DemandDirector.
        /// </summary>
        public void GenerateAutoOrder()
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.Catalog == null || root.DomainState == null) return;

            EnsureDemandDirector();
            if (_demandDirector != null)
            {
                _demandDirector.TriggerNextDemandEvent(root);
            }
        }
    }
}

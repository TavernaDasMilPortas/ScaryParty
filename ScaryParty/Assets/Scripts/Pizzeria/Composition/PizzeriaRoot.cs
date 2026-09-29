using System;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Ports;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Composition
{
    public class PizzeriaRoot : NetworkBehaviour
    {
        public static PizzeriaRoot Instance { get; private set; }

        [Header("Configuração de Design")]
        [SerializeField] private PizzeriaConfig _config;
        public PizzeriaConfig Config => _config;

        [Header("Âncoras de Logística e Spawns")]
        public Transform[] playerSpawnAnchors;
        public Transform receivingAnchor;
        public Transform recoveryAnchor;
        public Transform entranceAnchor;
        public Transform toolRackAnchor;

        // Domínio autoritativo no Servidor
        public PizzeriaState DomainState { get; private set; }
        public DefinitionCatalog Catalog { get; private set; }
        public IClock Clock { get; private set; }

        // Serviços de Domínio
        public TransferService TransferService { get; private set; }
        public ProcessingService ProcessingService { get; private set; }
        public RecipeEvaluator RecipeEvaluator { get; private set; }
        public DeliveryEvaluator DeliveryEvaluator { get; private set; }
        public OrderService OrderService { get; private set; }
        public UpgradeService UpgradeService { get; private set; }

        public event Action OnPizzeriaInitialized;

        private void Awake()
        {
            Instance = this;
            Clock = new NetworkServerClock();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (_config == null)
            {
                Debug.LogError("[PizzeriaRoot] PizzeriaConfig não atribuído no inspector!");
                return;
            }

            Catalog = _config.BuildCatalog();

            if (IsServer)
            {
                InitializeServerDomain();
                ReplicateInitialState();
            }

            OnPizzeriaInitialized?.Invoke();
        }

        private void ReplicateInitialState()
        {
            var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;
            if (cmd != null)
            {
                cmd.CommitAndReplicate();
                Debug.Log("[PizzeriaRoot] Estado inicial replicado para clientes.");
            }
            else
            {
                // CommandHandler pode não estar pronto ainda — agendar para o próximo frame
                StartCoroutine(ReplicateInitialStateDeferred());
            }
        }

        private System.Collections.IEnumerator ReplicateInitialStateDeferred()
        {
            yield return null; // Esperar um frame
            var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;
            if (cmd != null)
            {
                cmd.CommitAndReplicate();
                Debug.Log("[PizzeriaRoot] Estado inicial replicado (deferred) para clientes.");
            }
            else
            {
                Debug.LogError("[PizzeriaRoot] PizzeriaCommandHandler.Instance não encontrado após defer!");
            }
        }

        private void InitializeServerDomain()
        {
            DomainState = new PizzeriaState();
            DomainState.RestaurantBudget = _config.startingBudget;

            // Inicializar Serviços
            TransferService = new TransferService(DomainState, Catalog);
            ProcessingService = new ProcessingService();
            RecipeEvaluator = new RecipeEvaluator();
            DeliveryEvaluator = new DeliveryEvaluator();
            OrderService = new OrderService();
            UpgradeService = new UpgradeService();

            // Inicializar Estoque nos StorageSlots
            // Geladeira (ID 1)
            InitStorageSlot(1, 3, _config.initialSauceStock, _config.fridgeCapacityPerIngredient);     // Molho
            InitStorageSlot(1, 4, _config.initialCheeseStock, _config.fridgeCapacityPerIngredient);    // Queijo Inteiro
            InitStorageSlot(1, 6, _config.initialCalabresaStock, _config.fridgeCapacityPerIngredient); // Calabresa Inteira
            InitStorageSlot(1, 8, _config.initialMushroomStock, _config.fridgeCapacityPerIngredient);  // Cogumelo Inteiro

            // Armário (ID 2)
            InitStorageSlot(2, 1, _config.initialDoughStock, _config.cupboardCapacityPerIngredient);    // Massa

            // Inicializar Utensílios Compartilhados no ToolRack (ID 3)
            foreach (var kvp in Catalog.Tools)
            {
                var toolData = kvp.Value;
                for (int i = 0; i < toolData.InitialCount; i++)
                {
                    var toolId = DomainState.GenerateToolId();
                    var toolState = new ToolItemState(toolId, toolData.Id, toolData.Capabilities, LocationRef.InStation(3 /* ToolRack */, i));
                    DomainState.Tools[toolId] = toolState;
                }
            }

            Debug.Log($"[PizzeriaRoot] Servidor inicializou Domínio com sucesso. Budget: R${DomainState.RestaurantBudget}, {DomainState.Tools.Count} utensílios.");
        }

        private void InitStorageSlot(int storageId, int ingredientId, int initialStock, int maxCap)
        {
            var key = (storageId, ingredientId);
            DomainState.StorageSlots[key] = new StorageSlotState(storageId, ingredientId, initialStock, maxCap);
        }

        private void Update()
        {
            if (IsServer && DomainState != null)
            {
                bool dirty = false;

                // Tick Active Operations (like Oven and Manual work)
                var keys = new System.Collections.Generic.List<ScaryParty.Pizzeria.Domain.Types.StationSlotId>(DomainState.ActiveOperations.Keys);
                foreach (var slot in keys)
                {
                    if (DomainState.ActiveOperations.TryGetValue(slot, out var op))
                    {
                        float oldProgress = op.AccumulatedProgress;
                        
                        // Se for um forno (processo autônomo sem workerId)
                        if (op.WorkerId == OperationState.ServerWorker)
                        {
                            float oldCookProgress = 0f;
                            float oldBurnProgress = 0f;
                            if (DomainState.Items.TryGetValue(op.InputItemId, out var pizzaItem))
                            {
                                oldCookProgress = pizzaItem.CookProgress;
                                oldBurnProgress = pizzaItem.BurnProgress;
                            }

                            ProcessingService.TickOven(slot, DomainState, _config.ovenBurnGraceDuration, Clock);
                            
                            if (DomainState.Items.TryGetValue(op.InputItemId, out var updatedPizza))
                            {
                                if (Mathf.Abs(updatedPizza.CookProgress - oldCookProgress) > 0.005f ||
                                    Mathf.Abs(updatedPizza.BurnProgress - oldBurnProgress) > 0.005f)
                                {
                                    dirty = true;
                                }
                            }
                            else
                            {
                                dirty = true; // Operation completed or removed
                            }
                        }
                        else
                        {
                            // Para trabalho manual: auto-completar se atingir 1.0
                            if (op.ComputeCurrentProgress(Clock.Now) >= 1f)
                            {
                                ProcessingService.CompleteWork(slot, DomainState, Catalog);
                                dirty = true;
                            }
                        }
                    }
                }

                if (dirty)
                {
                    ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                }
            }
        }

        public Vector3 GetPlayerSpawnPosition(int index)
        {
            if (playerSpawnAnchors != null && playerSpawnAnchors.Length > 0)
            {
                int safeIdx = Mathf.Clamp(index, 0, playerSpawnAnchors.Length - 1);
                if (playerSpawnAnchors[safeIdx] != null)
                    return playerSpawnAnchors[safeIdx].position;
            }
            return transform.position + Vector3.up * 0.1f;
        }

        public Quaternion GetPlayerSpawnRotation(int index)
        {
            if (playerSpawnAnchors != null && playerSpawnAnchors.Length > 0)
            {
                int safeIdx = Mathf.Clamp(index, 0, playerSpawnAnchors.Length - 1);
                if (playerSpawnAnchors[safeIdx] != null)
                    return playerSpawnAnchors[safeIdx].rotation;
            }
            return transform.rotation;
        }
    }

    public class NetworkServerClock : IClock
    {
        public double Now
        {
            get
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    return NetworkManager.Singleton.ServerTime.Time;
                }
                return Time.timeAsDouble;
            }
        }
    }
}

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
        public OvenService OvenService { get; private set; }
        public CallService CallService { get; private set; }
        public PlayerHoldService PlayerHoldService { get; private set; }
        public AddressRegistry AddressRegistry { get; } = new AddressRegistry();

        public event Action OnPizzeriaInitialized;

        private void Awake()
        {
            // Se este objeto faz parte de um __City__ estático pré-existente sendo descartado em Play Mode, ignorar.
            if (Application.isPlaying && transform.root != null && transform.root.name == "__City__")
            {
                var gen = FindObjectOfType<CityGenerator>();
                if (gen != null && gen.CityData == null)
                {
                    return;
                }
            }

            Instance = this;
            Clock = new NetworkServerClock();
            EnsureAnchorsResolved();
        }

        public void EnsureAnchorsResolved()
        {
            var bindings = GetComponent<PizzeriaAuthoringBindings>();

            if (playerSpawnAnchors == null || playerSpawnAnchors.Length == 0)
            {
                var list = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < 4; i++)
                {
                    Transform t = transform.Find($"Anchors/PlayerSpawn_{i}") 
                               ?? transform.Find($"PlayerSpawn_{i}")
                               ?? FindDeepChildByName(transform, $"PlayerSpawn_{i}");
                    if (t != null) list.Add(t);
                }
                if (list.Count > 0) playerSpawnAnchors = list.ToArray();
            }

            if (entranceAnchor == null)
                entranceAnchor = transform.Find("Anchors/EntranceAnchor") ?? transform.Find("EntranceAnchor") ?? FindDeepChildByName(transform, "EntranceAnchor");

            if (receivingAnchor == null)
            {
                if (bindings != null && bindings.primaryReceivingStation != null)
                {
                    receivingAnchor = bindings.primaryReceivingStation.slotAnchors != null && bindings.primaryReceivingStation.slotAnchors.Length > 0
                        ? bindings.primaryReceivingStation.slotAnchors[0]
                        : bindings.primaryReceivingStation.transform;
                }
                if (receivingAnchor == null)
                    receivingAnchor = transform.Find("Anchors/ReceivingAnchor") ?? transform.Find("ReceivingAnchor") ?? FindDeepChildByName(transform, "ReceivingAnchor");
            }

            if (recoveryAnchor == null)
                recoveryAnchor = transform.Find("Anchors/RecoveryAnchor") ?? transform.Find("RecoveryAnchor") ?? FindDeepChildByName(transform, "RecoveryAnchor");

            if (toolRackAnchor == null)
            {
                if (bindings != null && bindings.primaryToolRackStation != null)
                    toolRackAnchor = bindings.primaryToolRackStation.transform;

                if (toolRackAnchor == null)
                {
                    var rackComp = GetComponentInChildren<ScaryParty.Pizzeria.Stations.ToolRackStation>(true);
                    if (rackComp != null) toolRackAnchor = rackComp.transform;
                }

                if (toolRackAnchor == null)
                    toolRackAnchor = transform.Find("Stations/Station_ToolRack") ?? transform.Find("Station_ToolRack");
            }
        }

        private static Transform FindDeepChildByName(Transform parent, string targetName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == targetName) return child;
                var found = FindDeepChildByName(child, targetName);
                if (found != null) return found;
            }
            return null;
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

            EnsureAnchorsResolved();
            Catalog = _config.BuildCatalog();

            RebuildAddressRegistry();

            if (IsServer)
            {
                InitializeServerDomain();
                ReplicateInitialState();
            }

            OnPizzeriaInitialized?.Invoke();
        }

        public void RebuildAddressRegistry()
        {
            AddressRegistry.Clear();
            var deliveryPoints = FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < deliveryPoints.Length; i++)
            {
                var dp = deliveryPoints[i];
                var pos = dp.transform.position;
                AddressRegistry.Register(new AddressId(dp.AddressId), new DomainVector3(pos.x, pos.y, pos.z));
            }

            int hash = AddressRegistry.ComputeHash();
            if (IsServer)
            {
                var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
                if (netState != null)
                {
                    netState.AddressRegistryHash.Value = hash;
                }
            }

            Debug.Log($"[PizzeriaRoot] AddressRegistry populado com {deliveryPoints.Length} endereços. Hash: {hash}");
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
            OvenService = new OvenService();
            CallService = new CallService();
            PlayerHoldService = new PlayerHoldService();

            var bindings = GetComponent<PizzeriaAuthoringBindings>();

            // Inicializar Estoque nos StorageSlots
            if (_config.initialStorageSlots != null && _config.initialStorageSlots.Count > 0)
            {
                foreach (var slotCfg in _config.initialStorageSlots)
                {
                    if (slotCfg == null) continue;
                    InitStorageSlot(slotCfg.storageId, slotCfg.EffectiveIngredientId, slotCfg.initialStock, slotCfg.capacity);
                }
            }
            else
            {
                int fridgeId = bindings != null ? bindings.ResolvePrimaryFridgeId(1) : 1;
                int pantryId = bindings != null ? bindings.ResolvePrimaryPantryId(2) : 2;

                // Geladeira (ID padrão 1)
                InitStorageSlot(fridgeId, 3, _config.initialSauceStock, _config.fridgeCapacityPerIngredient);     // Molho
                InitStorageSlot(fridgeId, 4, _config.initialCheeseStock, _config.fridgeCapacityPerIngredient);    // Queijo Inteiro
                InitStorageSlot(fridgeId, 6, _config.initialCalabresaStock, _config.fridgeCapacityPerIngredient); // Calabresa Inteira
                InitStorageSlot(fridgeId, 8, _config.initialMushroomStock, _config.fridgeCapacityPerIngredient);  // Cogumelo Inteiro

                // Armário/Dispensa (ID padrão 2)
                InitStorageSlot(pantryId, 1, _config.initialDoughStock, _config.cupboardCapacityPerIngredient);    // Massa
            }

            // Inicializar Utensílios Compartilhados no ToolRack com índice contínuo (A12)
            int toolRackStationId = bindings != null ? bindings.ResolvePrimaryToolRackId(3) : 3;
            if (toolRackStationId == 3 && toolRackAnchor != null)
            {
                var rackStation = toolRackAnchor.GetComponent<ScaryParty.Pizzeria.Stations.StationView>();
                if (rackStation != null && rackStation.stationId > 0)
                    toolRackStationId = rackStation.stationId;
            }

            int globalToolSlotIndex = 0;
            foreach (var kvp in Catalog.Tools)
            {
                var toolData = kvp.Value;
                for (int i = 0; i < toolData.InitialCount; i++)
                {
                    var toolId = DomainState.GenerateToolId();
                    var toolState = new ToolItemState(toolId, toolData.Id, toolData.Capabilities, LocationRef.InStation(toolRackStationId, globalToolSlotIndex++));
                    DomainState.Tools[toolId] = toolState;
                }
            }

            // Inicializar Pilhas de Caixas configuradas no prefab
            var stackStations = GetComponentsInChildren<ScaryParty.Pizzeria.Stations.StackStation>(true);
            for (int i = 0; i < stackStations.Length; i++)
            {
                var stk = stackStations[i];
                if (stk == null) continue;
                var stkId = new StackId(stk.stackId > 0 ? stk.stackId : (ulong)(i + 1));
                if (!DomainState.Stacks.ContainsKey(stkId))
                {
                    int cap = stk.capacity > 0 ? stk.capacity : 6;
                    DomainState.Stacks[stkId] = new StackState(stkId, stk.stationId, cap);
                }
            }

            // Inicializar Mochilas Térmicas dos Entregadores no Domínio (ID = PlayerId + 1)
            int bpCap = _config != null && _config.backpackInitialSlots > 0 ? _config.backpackInitialSlots : 3;
            for (ulong p = 0; p < 4; p++)
            {
                var bpId = new BackpackId(p + 1);
                var bp = new BackpackState(bpId, bpCap);
                bp.EquippedBy = p;
                DomainState.Backpacks[bpId] = bp;
            }

            Debug.Log($"[PizzeriaRoot] Servidor inicializou Domínio com sucesso. Budget: R${DomainState.RestaurantBudget}, {DomainState.Tools.Count} utensílios, {DomainState.Backpacks.Count} mochilas.");
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

                // Tick de Pedidos (A15), Chamadas e Holds do Jogador
                OrderService?.TickOrders(DomainState, Clock);
                CallService?.TickCalls(DomainState, (float)Clock.Now);
                PlayerHoldService?.TickHolds(DomainState, (float)Clock.Now, OrderService);
            }
        }

        public Vector3 GetPlayerSpawnPosition(int index)
        {
            EnsureAnchorsResolved();
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
            EnsureAnchorsResolved();
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

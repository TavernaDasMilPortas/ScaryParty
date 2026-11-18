using System;
using System.Collections.Generic;
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
        public CustomerService CustomerService { get; private set; }
        public RunProgressionService RunProgressionService { get; private set; }
        public EmployeeTaskService EmployeeTaskService { get; private set; }
        public SpeciesHostilityService SpeciesHostilityService { get; private set; }
        public ZombieConversionService ZombieConversionService { get; private set; }
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

        public override void OnNetworkDespawn()
        {
            Presentation.EmployeePopulationDirector.ClearAll();
            ScaryParty.Combat.Monsters.MonsterPopulationDirector.Instance?.ClearOnSessionEnd();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            Presentation.EmployeePopulationDirector.ClearAll();
            ScaryParty.Combat.Monsters.MonsterPopulationDirector.Instance?.ClearOnSessionEnd();
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
            SyncCatalogWithSceneStations();

            RebuildAddressRegistry();

            if (IsServer)
            {
                InitializeServerDomain();
                ReplicateInitialState();
            }

            // Diretor de monstros instalado tanto no servidor quanto nos clientes remotos (P1)
            ScaryParty.Combat.Monsters.MonsterPopulationDirector.EnsureOn(this);

            // Garantir que o visualizador de clientes presenciais e HUD de paciência existam
            if (GetComponent<ScaryParty.Pizzeria.Presentation.CustomerSpawner>() == null)
                gameObject.AddComponent<ScaryParty.Pizzeria.Presentation.CustomerSpawner>();

            if (GetComponent<ScaryParty.Pizzeria.Presentation.CustomerPatienceHudPresenter>() == null)
                gameObject.AddComponent<ScaryParty.Pizzeria.Presentation.CustomerPatienceHudPresenter>();

            // Garante controlador de iluminacao interna por zonas (A6) e elimina Point shadows no atlas URP
            if (GetComponent<ScaryParty.Lighting.PizzeriaLightingController>() == null)
            {
                gameObject.AddComponent<ScaryParty.Lighting.PizzeriaLightingController>();
            }

            EnsureExperiencePresenters();

            // Aplica estilo cel-shading ativo no cliente e servidor preservando hash de NetworkObject
            ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                gameObject,
                ScaryParty.VisualStyles.VisualSemanticRole.BuildingFacade,
                ScaryParty.VisualStyles.VisualCategoryIds.Architecture,
                "pizzeria_modular");

            OnPizzeriaInitialized?.Invoke();
        }

        /// <summary>
        /// Garante que o catálogo de domínio (Processos e Estações) esteja sincronizado com os componentes
        /// reais presentes na cena/prefab (ex: PrepStation aceitando todos os processos 1..4 e SlotCount correto).
        /// </summary>
        private void SyncCatalogWithSceneStations()
        {
            if (Catalog == null) return;

            // Garantir que os 4 processos essenciais de preparo existam no catálogo em runtime
            if (!Catalog.Processes.ContainsKey(1))
                Catalog.RegisterProcess(new ProcessData(1, "Abrir Massa", 1, ProcessingStage.Raw, 2, ProcessingStage.Prepared, ToolCapability.None, 3f));
            if (!Catalog.Processes.ContainsKey(2))
                Catalog.RegisterProcess(new ProcessData(2, "Ralar Queijo", 4, ProcessingStage.Raw, 5, ProcessingStage.Prepared, ToolCapability.Grate, 2f));
            if (!Catalog.Processes.ContainsKey(3))
                Catalog.RegisterProcess(new ProcessData(3, "Fatiar Calabresa", 6, ProcessingStage.Raw, 7, ProcessingStage.Prepared, ToolCapability.Slice, 2f));
            if (!Catalog.Processes.ContainsKey(4))
                Catalog.RegisterProcess(new ProcessData(4, "Fatiar Cogumelo", 8, ProcessingStage.Raw, 9, ProcessingStage.Prepared, ToolCapability.Slice, 2f));

            int[] allPrepProcesses = new int[] { 1, 2, 3, 4 };
            var stations = GetComponentsInChildren<ScaryParty.Pizzeria.Stations.StationView>(true);
            for (int i = 0; i < stations.Length; i++)
            {
                var st = stations[i];
                if (st == null || st.stationId <= 0) continue;

                int anchorCount = 0;
                if (st.slotAnchors != null)
                {
                    for (int s = 0; s < st.slotAnchors.Length; s++)
                        if (st.slotAnchors[s] != null) anchorCount++;
                }
                if (anchorCount <= 0) anchorCount = 1;

                bool isPrep = st is ScaryParty.Pizzeria.Stations.PrepStation;
                if (Catalog.Stations.TryGetValue(st.stationId, out var existing))
                {
                    int slotCount = Mathf.Max(existing.SlotCount, anchorCount);
                    var accepted = new List<int>(existing.AcceptedProcessIds);
                    if (isPrep)
                    {
                        for (int p = 0; p < allPrepProcesses.Length; p++)
                        {
                            if (!accepted.Contains(allPrepProcesses[p]))
                                accepted.Add(allPrepProcesses[p]);
                        }
                    }
                    var overrides = existing.DurationOverrides != null
                        ? new Dictionary<int, float>(existing.DurationOverrides)
                        : null;

                    Catalog.RegisterStation(new StationData(
                        existing.Id,
                        existing.Name,
                        slotCount,
                        accepted,
                        overrides));
                }
                else
                {
                    var accepted = isPrep ? allPrepProcesses : Array.Empty<int>();
                    Catalog.RegisterStation(new StationData(
                        st.stationId,
                        st.stationName,
                        anchorCount,
                        accepted));
                }
            }
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
            CustomerService = new CustomerService();
            int runSeed = (_config != null && _config.runSeedOverride != 0)
                ? _config.runSeedOverride
                : (System.Environment.TickCount ^ GetInstanceID());
            int maxNights = _config != null ? Mathf.Clamp(_config.runMaxNights, 1, 7) : 3;
            var diff = _config != null ? _config.runDifficulty : RunDifficulty.Normal;
            int initialNight = (_config != null && _config.enableTutorialNight0ByDefault) ? 0 : 1;

            RunProgressionService = new RunProgressionService(runSeed, maxNights: maxNights, difficulty: diff);
            EmployeeTaskService = new EmployeeTaskService();
            SpeciesHostilityService = new SpeciesHostilityService();
            ZombieConversionService = new ZombieConversionService();

            DomainState.RestaurantBudget = _config != null ? _config.startingBudget : 500;
            DomainState.Reputation = Mathf.Clamp(_config != null ? _config.initialReputation : 100, 0, 100);
            RebuildSceneDomainStateForNewRunServer();

            NightCycleService.StartNight(DomainState, initialNight, _config.ResolveNightParameters(initialNight));
            if (initialNight == 0)
            {
                SeedPredictableTutorialOrderServer();
            }
            ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.Instance?.SetNightContext(
                DomainState.CurrentNightNumber,
                DomainState.NightRevenueGoal);

            Debug.Log($"[PizzeriaRoot] Servidor inicializou Domínio com sucesso. Budget: R${DomainState.RestaurantBudget}, Meta Noite {DomainState.CurrentNightNumber}: R${DomainState.NightRevenueGoal}, {DomainState.Tools.Count} utensílios, {DomainState.Backpacks.Count} mochilas, {DomainState.Tables.Count} mesas.");
        }

        /// <summary>
        /// Reconstrói apenas os registros derivados das definições de cena/prefab (mesas, estoques iniciais,
        /// utensílios, pilhas e mochilas) sem duplicar referências, reutilizado na inicialização e ao reiniciar RUN.
        /// </summary>
        public void RebuildSceneDomainStateForNewRunServer()
        {
            if (DomainState == null) return;

            DomainState.Tables.Clear();
            var tableStations = GetComponentsInChildren<ScaryParty.Pizzeria.Stations.DiningTableStation>(true);
            if (tableStations != null && tableStations.Length > 0)
            {
                var tableIds = new List<int>();
                for (int i = 0; i < tableStations.Length; i++)
                {
                    if (tableStations[i] != null && tableStations[i].tableId > 0)
                        tableIds.Add(tableStations[i].tableId);
                }
                CustomerService.TableService.EnsureTablesInitialized(DomainState, tableIds);
            }
            CustomerService.TableService.EnsureDefaultTablesIfEmpty(DomainState, 4);

            var bindings = GetComponent<PizzeriaAuthoringBindings>();

            DomainState.StorageSlots.Clear();
            if (_config != null && _config.initialStorageSlots != null && _config.initialStorageSlots.Count > 0)
            {
                foreach (var slotCfg in _config.initialStorageSlots)
                {
                    if (slotCfg == null) continue;
                    InitStorageSlot(slotCfg.storageId, slotCfg.EffectiveIngredientId, slotCfg.initialStock, slotCfg.capacity);
                }
            }
            else if (_config != null)
            {
                int fridgeId = bindings != null ? bindings.ResolvePrimaryFridgeId(1) : 1;
                int pantryId = bindings != null ? bindings.ResolvePrimaryPantryId(2) : 2;

                InitStorageSlot(fridgeId, 3, _config.initialSauceStock, _config.fridgeCapacityPerIngredient);
                InitStorageSlot(fridgeId, 4, _config.initialCheeseStock, _config.fridgeCapacityPerIngredient);
                InitStorageSlot(fridgeId, 6, _config.initialCalabresaStock, _config.fridgeCapacityPerIngredient);
                InitStorageSlot(fridgeId, 8, _config.initialMushroomStock, _config.fridgeCapacityPerIngredient);
                InitStorageSlot(pantryId, 1, _config.initialDoughStock, _config.cupboardCapacityPerIngredient);
            }

            int toolRackStationId = bindings != null ? bindings.ResolvePrimaryToolRackId(3) : 3;
            if (toolRackStationId == 3 && toolRackAnchor != null)
            {
                var rackStation = toolRackAnchor.GetComponent<ScaryParty.Pizzeria.Stations.StationView>();
                if (rackStation != null && rackStation.stationId > 0)
                    toolRackStationId = rackStation.stationId;
            }
            DomainState.ToolRackStationId = toolRackStationId;

            DomainState.Tools.Clear();
            int globalToolSlotIndex = 0;
            if (Catalog != null)
            {
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
            }

            DomainState.Stacks.Clear();
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

            DomainState.Backpacks.Clear();
            int bpCap = _config != null && _config.backpackInitialSlots > 0 ? _config.backpackInitialSlots : 3;
            var bags = GetComponentsInChildren<DeliveryBagAuthoring>(true);
            foreach (var bag in bags)
            {
                if (bag.BackpackId == 0 || bag.BagRenderer == null || bag.InteractionCollider == null)
                {
                    Debug.LogError($"[PizzeriaRoot] Bag binding incomplete: {bag.name}");
                    continue;
                }
                var pos = bag.transform.position;
                var rot = bag.transform.rotation;
                var result = BackpackService.Register(DomainState, new BackpackId(bag.BackpackId), bpCap,
                    new DomainVector3(pos.x, pos.y, pos.z), new DomainQuaternion(rot.x, rot.y, rot.z, rot.w));
                if (!result.Success) Debug.LogError($"[PizzeriaRoot] {bag.name}: {result.Message}");
            }
        }

        public void ReinitializeForNewRunServer(int newSeed, int maxNights, RunDifficulty difficulty, int startNight = 1)
        {
            if (!IsServer || DomainState == null) return;

            Presentation.EmployeePopulationDirector.ClearAll();
            EmployeeTaskService?.ResetForNewRun();
            SpeciesHostilityService?.ResetForNewRun();
            ZombieConversionService?.Reset();
            ScaryParty.Combat.Monsters.MonsterPopulationDirector.Instance?.ClearNightMonstersServer();

            int startBudget = _config != null ? _config.startingBudget : 500;
            int startRep = _config != null ? _config.initialReputation : 100;
            RunProgressionService?.ResetRun(DomainState, newSeed, maxNights, difficulty, startBudget, startRep);

            RebuildSceneDomainStateForNewRunServer();

            var nightParams = _config != null ? _config.ResolveNightParameters(startNight) : (NightGoalParameters?)null;
            NightCycleService.StartNight(DomainState, startNight, nightParams);
            if (startNight == 0)
            {
                SeedPredictableTutorialOrderServer();
            }

            int humanCount = Unity.Netcode.NetworkManager.Singleton != null
                ? Mathf.Max(1, Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count)
                : 1;
            RunProgressionService?.FreezeNightStartParameters(DomainState, humanCount);

            Presentation.PizzeriaSceneLoadingCoordinator.Instance?.SetNightContext(
                DomainState.CurrentNightNumber,
                DomainState.NightRevenueGoal);
        }

        private void SeedPredictableTutorialOrderServer()
        {
            if (DomainState == null || CustomerService == null) return;
            var res = CustomerService.SpawnWalkInCustomer(DomainState, Catalog, Clock, rng: new System.Random(1001));
            if (res.Success && DomainState.Customers.TryGetValue(res.Value, out var cust))
            {
                cust.RequestedPizza = PizzaSpecification.CreateDefaultCheese(15, 12f);
                cust.QueuePatienceMax = 600f;
                cust.QueuePatienceRemaining = 600f;
                cust.FoodPatienceMax = 600f;
                cust.FoodPatienceRemaining = 600f;
                cust.Revision++;
            }
        }

        private float _nightSyncAccumulator = 0f;

        private void EnsureExperiencePresenters()
        {
            if (ScaryParty.Pizzeria.Presentation.NightResultsPresenter.Instance == null)
                ScaryParty.Pizzeria.Presentation.NightResultsPresenter.EnsureInstance();

            if (ScaryParty.Pizzeria.Presentation.ContextualTutorialPresenter.Instance == null)
                ScaryParty.Pizzeria.Presentation.ContextualTutorialPresenter.EnsureInstance();

            if (ScaryParty.UI.Piaria.Presenters.PauseEscapeMenuPresenter.Instance == null)
                ScaryParty.UI.Piaria.Presenters.PauseEscapeMenuPresenter.EnsureInstance();

            if (ScaryParty.Pizzeria.Presentation.GameplayIntroCutsceneFlow.Instance == null)
                ScaryParty.Pizzeria.Presentation.GameplayIntroCutsceneFlow.EnsureInstance();

            PizzeriaParkingLotBuilder.EnsureBuilt(this);
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
                        if (op.Phase != OperationPhase.Running) continue;

                        // Autonomous jobs finish without player heartbeats; manual jobs must release on timeout.
                        if (op.WorkerId != OperationState.ServerWorker && Clock.Now > op.Progress.LeaseExpiry &&
                            op.ComputeCurrentProgress(Clock.Now) < 1f)
                        {
                            if (op.Kind == OperationKind.Assemble)
                                PizzaAssemblyService.Cancel(DomainState, slot);
                            else
                                ProcessingService.CancelOperation(DomainState, slot, op.WorkerId, (float)Clock.Now);
                            dirty = true;
                            continue;
                        }
                        
                        if (op.Kind == OperationKind.Assemble)
                        {
                            if (op.ComputeCurrentProgress(Clock.Now) >= 1f)
                            {
                                Debug.Log($"[PizzeriaRoot] Concluindo operacao Assemble no slot {slot}!");
                                bool completed = PizzaAssemblyService.Complete(DomainState, slot);
                                if (completed)
                                    Debug.Log($"[PizzeriaRoot] PizzaAssemblyService.Complete executado com sucesso no slot {slot}.");
                                else
                                    Debug.LogWarning($"[PizzeriaRoot] PizzaAssemblyService.Complete falhou ou retornou false no slot {slot}.");
                                dirty |= completed;
                            }
                        }
                        // Se for um forno (processo autônomo sem workerId)
                        else if (op.WorkerId == OperationState.ServerWorker && op.ProcessId == 0)
                        {
                            float oldCookProgress = 0f;
                            float oldBurnProgress = 0f;
                            if (DomainState.Items.TryGetValue(op.InputItemId, out var pizzaItem))
                            {
                                oldCookProgress = pizzaItem.CookProgress;
                                oldBurnProgress = pizzaItem.BurnProgress;
                            }

                            var runStats = RunProgressionService != null ? RunProgressionService.RecalculateEffectiveStats() : null;
                            ProcessingService.TickOven(slot, DomainState, _config.ovenBurnGraceDuration, Clock, runStats != null && runStats.OvenAutoShutoffEnabled);
                            
                            if (DomainState.Items.TryGetValue(op.InputItemId, out var updatedPizza))
                            {
                                if (Mathf.Abs(updatedPizza.CookProgress - oldCookProgress) > 0.04f ||
                                    Mathf.Abs(updatedPizza.BurnProgress - oldBurnProgress) > 0.04f)
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
                                Debug.Log($"[PizzeriaRoot] Concluindo trabalho de preparo no slot {slot} (Processo: {op.ProcessId})!");
                                var compRes = ProcessingService.CompleteWork(slot, DomainState, Catalog);
                                if (compRes.Success)
                                {
                                    Debug.Log($"[PizzeriaRoot] ProcessingService.CompleteWork concluido com sucesso para o slot {slot}!");
                                    dirty = true;
                                }
                                else
                                {
                                    Debug.LogWarning($"[PizzeriaRoot] ProcessingService.CompleteWork falhou no slot {slot}: {compRes.Error} - {compRes.Message}");
                                }
                            }
                        }
                    }
                }

                // Tick de Pedidos (A15), Chamadas, Suprimentos, Holds do Jogador e Clientes Presenciais
                OrderService?.TickOrders(DomainState, Clock);
                CallService?.TickCalls(DomainState, (float)Clock.Now);
                SpeciesHostilityService?.Tick(Time.deltaTime);
                Presentation.EmployeePopulationDirector.TickAndProjectServer(this, Network.PizzeriaNetworkState.Instance, Time.deltaTime);
                dirty |= NightCycleService.TickSupplies(DomainState, Clock.Now);

                int prevHoldCount = DomainState.PlayerHolds.Count;
                PlayerHoldService?.TickHolds(
                    DomainState,
                    (float)Clock.Now,
                    OrderService,
                    CustomerService,
                    Clock,
                    _config != null ? _config.maxActiveOrders : 6);
                if (DomainState.PlayerHolds.Count != prevHoldCount)
                    dirty = true;

                if (CustomerService != null)
                {
                    bool custDirty = CustomerService.TickCustomers(
                        DomainState,
                        Catalog,
                        OrderService,
                        Clock,
                        Time.deltaTime,
                        _config != null ? _config.queueStagnationThresholdSeconds : 35f,
                        _config != null ? _config.queueStagnationDecayMultiplier : 1.35f);
                    dirty |= custDirty;
                }

                if (!ScaryParty.Cutscenes.CutscenePlaybackService.IsNightTimerPaused)
                {
                    var nightParams = _config != null ? _config.ResolveNightParameters(DomainState.CurrentNightNumber) : (NightGoalParameters?)null;
                    if (NightCycleService.TickNight(DomainState, Time.deltaTime, nightParams))
                    {
                        dirty = true;
                    }
                }

                _nightSyncAccumulator += Time.deltaTime;
                if (_nightSyncAccumulator >= 1.0f)
                {
                    _nightSyncAccumulator = 0f;
                    var phase = DomainState.CurrentNightPhase;
                    if (phase == NightPhase.Preparing || phase == NightPhase.Active || phase == NightPhase.Closing)
                    {
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.CommitAndReplicate();
                }
            }
            else if (IsSpawned && !IsServer)
            {
                Presentation.EmployeePopulationDirector.EnsureClientSubscribedAndTick(Network.PizzeriaNetworkState.Instance);
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

        public Bounds GetPizzeriaInteriorBounds()
        {
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                return box.bounds;
            }
            Vector3 center = entranceAnchor != null ? entranceAnchor.position - transform.forward * 6f : transform.position;
            Bounds bounds = new Bounds(center, new Vector3(22f, 8f, 22f));
            if (recoveryAnchor != null) bounds.Encapsulate(recoveryAnchor.position);
            if (receivingAnchor != null) bounds.Encapsulate(receivingAnchor.position);
            bounds.Expand(new Vector3(2f, 4f, 2f));
            return bounds;
        }

        public bool IsWorldPositionInsidePizzeria(Vector3 worldPos)
        {
            Bounds b = GetPizzeriaInteriorBounds();
            return b.Contains(worldPos);
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

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
#if UNITY_EDITOR
        public PlaytestCheckpointStore CheckpointStore { get; } = new PlaytestCheckpointStore();
#endif

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

            var extAnchors = GetComponent<PizzeriaExteriorAnchors>();
            if (extAnchors != null)
            {
                if ((playerSpawnAnchors == null || playerSpawnAnchors.Length == 0) && extAnchors.playerSpawns != null)
                {
                    var valid = new System.Collections.Generic.List<Transform>();
                    for (int i = 0; i < extAnchors.playerSpawns.Length; i++)
                    {
                        if (extAnchors.playerSpawns[i] != null) valid.Add(extAnchors.playerSpawns[i]);
                    }
                    if (valid.Count > 0) playerSpawnAnchors = valid.ToArray();
                }
                if (recoveryAnchor == null && extAnchors.playerRecovery != null)
                {
                    recoveryAnchor = extAnchors.playerRecovery;
                }
            }

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

        public Authoring.PizzeriaRunPreset ActivePresetOverride { get; private set; }

        public void SetActiveRunPresetServer(Authoring.PizzeriaRunPreset preset)
        {
            ActivePresetOverride = preset;
            if (DomainState != null)
            {
                var effPreset = GetEffectiveRunPreset();
                DomainState.ActivePresetId = effPreset != null ? effPreset.presetId : "Default";
                DomainState.AllowDebugPanel = effPreset == null || effPreset.allowDebugPanel;
            }
        }

        public Authoring.PizzeriaRunPreset GetEffectiveRunPreset()
        {
            if (ActivePresetOverride != null) return ActivePresetOverride;
            var launch = Network.SessionLaunchConfig.Current;
            if (launch.HasHostSelection && !string.IsNullOrEmpty(launch.PresetId))
            {
                var fromConfig = FindPresetById(launch.PresetId);
                if (fromConfig != null) return fromConfig;
            }
            return _config != null ? _config.defaultPreset : null;
        }

        public Authoring.PizzeriaRunPreset FindPresetById(string presetId)
        {
            if (string.IsNullOrEmpty(presetId)) return null;
            if (_config != null)
            {
                if (_config.defaultPreset != null && string.Equals(_config.defaultPreset.presetId, presetId, System.StringComparison.OrdinalIgnoreCase))
                    return _config.defaultPreset;
                if (_config.availablePresets != null)
                {
                    for (int i = 0; i < _config.availablePresets.Count; i++)
                    {
                        var p = _config.availablePresets[i];
                        if (p != null && string.Equals(p.presetId, presetId, System.StringComparison.OrdinalIgnoreCase))
                            return p;
                    }
                }
            }

            var manifest = Authoring.PlaytestReferenceManifest.LoadDefault();
            if (manifest != null && manifest.runPresets != null)
            {
                for (int i = 0; i < manifest.runPresets.Count; i++)
                {
                    if (manifest.runPresets[i] is Authoring.PizzeriaRunPreset p &&
                        string.Equals(p.presetId, presetId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        public static int CountConnectedHumanClientsServer()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.ConnectedClientsIds != null && nm.ConnectedClientsIds.Count > 0)
            {
                return Mathf.Clamp(nm.ConnectedClientsIds.Count, 1, 4);
            }
            return 1;
        }

        /// <summary>
        /// Caminho único e autoritativo para resolver parâmetros e iniciar/reiniciar qualquer noite (P3).
        /// </summary>
        public ResolvedNightParameters ResolveAndApplyNightStartParametersServer(int nightNumber, bool isRetry = false)
        {
            var preset = GetEffectiveRunPreset();
            var diff = RunProgressionService != null
                ? RunProgressionService.Difficulty
                : (preset != null ? preset.difficulty : (_config != null ? _config.runDifficulty : RunDifficulty.Normal));
            int humanCount = CountConnectedHumanClientsServer();
            float modMult = RunProgressionService != null
                ? RunProgressionService.RecalculateEffectiveStats().NightGoalMultiplier
                : 1.0f;

            var resolved = _config != null
                ? _config.ResolveEffectiveNightParameters(nightNumber, humanCount, diff, preset, modMult)
                : NightParameterResolver.ResolveEffectiveParameters(
                    nightNumber, humanCount, diff, preset != null ? preset.ToDomainData() : null, null, 600f, 15, modMult);

            if (DomainState != null)
            {
                DomainState.AllowDebugPanel = preset == null || preset.allowDebugPanel;
                if (isRetry)
                {
                    NightCycleService.RetryCurrentNight(DomainState, resolved.ToNightGoalParameters());
                    NightCycleService.StartNight(DomainState, resolved);
                }
                else
                {
                    NightCycleService.StartNight(DomainState, resolved);
                }
                RunProgressionService?.FreezeResolvedNightParameters(DomainState, resolved);
#if UNITY_EDITOR
                if (!isRetry)
                {
                    CheckpointStore.Capture(PlaytestCheckpointKind.NightStart, DomainState, RunProgressionService, EmployeeTaskService, Clock != null ? Clock.Now : Time.timeAsDouble);
                }
#endif
            }

            Presentation.PizzeriaSceneLoadingCoordinator.Instance?.SetNightContext(
                DomainState != null ? DomainState.CurrentNightNumber : resolved.NightNumber,
                DomainState != null ? DomainState.NightRevenueGoal : resolved.FinalGoal);

            return resolved;
        }

        public void ResolveSessionRunSettings(out int seed, out int maxNights, out RunDifficulty difficulty, out int startNight)
        {
            var preset = GetEffectiveRunPreset();
            var launch = Network.SessionLaunchConfig.Current;
            bool useLaunch = ActivePresetOverride == null && launch.HasHostSelection;

            seed = (useLaunch && launch.SeedOverride != 0)
                ? launch.SeedOverride
                : ((_config != null && _config.runSeedOverride != 0)
                    ? _config.runSeedOverride
                    : (System.Environment.TickCount ^ GetInstanceID()));

            maxNights = useLaunch
                ? Mathf.Clamp(launch.MaxNights, 1, 7)
                : (preset != null
                    ? Mathf.Clamp(preset.totalNights, 1, 7)
                    : (_config != null ? Mathf.Clamp(_config.runMaxNights, 1, 7) : 3));

            difficulty = useLaunch
                ? launch.Difficulty
                : (preset != null
                    ? preset.difficulty
                    : (_config != null ? _config.runDifficulty : RunDifficulty.Normal));

            bool includeNight0 = useLaunch
                ? launch.IncludeNight0Tutorial
                : ((preset != null && preset.includeNight0Tutorial) ||
                   (_config != null && _config.enableTutorialNight0ByDefault));
            startNight = includeNight0 ? 0 : 1;
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

            var preset = GetEffectiveRunPreset();
            ResolveSessionRunSettings(out int runSeed, out int maxNights, out var diff, out int initialNight);

            string balancingDiagnostic = string.Empty;
            var balancingSnapshot = _config != null
                ? _config.ResolveBalancingSnapshot(out balancingDiagnostic, preset != null ? preset.balancingCatalogOverride : null)
                : null;
            if (!string.IsNullOrEmpty(balancingDiagnostic))
            {
                Debug.LogWarning($"[PizzeriaRoot] {balancingDiagnostic} Usando snapshot de dominio explicito de contingencia.");
            }
            if (balancingSnapshot == null)
            {
                balancingSnapshot = RunBalancingSnapshot.CreateForDomainUnitTests();
            }

            RunProgressionService = new RunProgressionService(runSeed, maxNights: maxNights, difficulty: diff, snapshot: balancingSnapshot);
            EmployeeTaskService = new EmployeeTaskService(balancingSnapshot);
            SpeciesHostilityService = new SpeciesHostilityService();
            ZombieConversionService = new ZombieConversionService();

            DomainState.RestaurantBudget = preset != null ? preset.startingBudget : (_config != null ? _config.startingBudget : 500);
            DomainState.Reputation = Mathf.Clamp(preset != null ? preset.startingReputation : (_config != null ? _config.initialReputation : 100), 0, 100);
            DomainState.AllowDebugPanel = preset == null || preset.allowDebugPanel;
            RebuildSceneDomainStateForNewRunServer();

            if (initialNight == 0)
            {
                ResolveAndApplyNightStartParametersServer(initialNight, isRetry: false);
                SeedPredictableTutorialOrderServer();
            }
            else
            {
                OpenPreparationBeforeNightServer(initialNight);
            }

            Debug.Log($"[PizzeriaRoot] Servidor inicializou Domínio com sucesso. Preset: {DomainState.ActivePresetId}, HumanosCongelados: {DomainState.FrozenHumanCount}, Budget: R${DomainState.RestaurantBudget}, NoiteAlvo {DomainState.TargetNightNumber}, Meta: R${DomainState.NightRevenueGoal} (Fonte={(GoalResolutionSource)DomainState.ResolvedGoalSource}).");
        }

        public void OpenPreparationBeforeNightServer(int targetNight)
        {
            if (!IsServer || DomainState == null) return;
            int resolvedTarget = Mathf.Max(1, targetNight);

            var preset = GetEffectiveRunPreset();
            var diff = RunProgressionService != null
                ? RunProgressionService.Difficulty
                : (preset != null ? preset.difficulty : (_config != null ? _config.runDifficulty : RunDifficulty.Normal));
            int humanCount = CountConnectedHumanClientsServer();
            float modMult = RunProgressionService != null
                ? RunProgressionService.RecalculateEffectiveStats().NightGoalMultiplier
                : 1.0f;

            var preview = _config != null
                ? _config.ResolveEffectiveNightParameters(resolvedTarget, humanCount, diff, preset, modMult)
                : NightParameterResolver.ResolveEffectiveParameters(
                    resolvedTarget, humanCount, diff, preset != null ? preset.ToDomainData() : null, null, 600f, 60, modMult);

            DomainState.LastCompletedNightRevenueGoal = resolvedTarget > 1 ? DomainState.NightRevenueGoal : 0;
            DomainState.CurrentNightPhase = NightPhase.PreparingNextNight;
            DomainState.CurrentNightNumber = Mathf.Max(0, resolvedTarget - 1);
            DomainState.TargetNightNumber = resolvedTarget;
            DomainState.AllowDebugPanel = preset == null || preset.allowDebugPanel;
            DomainState.FrozenHumanCount = preview.FrozenHumanCount;
            DomainState.ResolvedBaseGoal = preview.BaseGoal;
            DomainState.ResolvedHumanScaleFactor = preview.HumanScaleFactor;
            DomainState.ResolvedModifierMultiplier = preview.ModifierGoalMultiplier;
            DomainState.ResolvedGoalSource = (byte)preview.GoalSource;
            DomainState.ActivePresetId = preview.PresetId;
            DomainState.NightRevenueGoal = preview.FinalGoal;
            DomainState.NightReputationMinGoal = preview.ReputationMinGoal;
            DomainState.NightPizzasMinGoal = preview.PizzasMinGoal;
            DomainState.IsNonLethalTutorialNight = preview.NonLethalDefeat;
            DomainState.NightDurationSeconds = preview.DurationSeconds;
            DomainState.NightRemainingSeconds = preview.DurationSeconds;
            DomainState.NightPrepRemainingSeconds = preview.PrepSeconds;
            DomainState.NightGraceRemainingSeconds = preview.ClosingGraceSeconds;
            DomainState.UnlimitedShiftTime = preview.UnlimitedShiftTime;
            DomainState.IsForcedDebugOutcome = false;

            RunProgressionService?.OpenTypedBallots(resolvedTarget, 3, 3, respectCatalogSchedule: true);
            int stageCount = RunProgressionService?.CurrentVoteStages?.Count ?? 0;
            var stageState = RunProgressionService != null ? RunProgressionService.IntermissionStage : Domain.Services.IntermissionStage.None;
            Debug.Log($"[PizzeriaRoot] Preparação aberta: CurrentNight={DomainState.CurrentNightNumber}, TargetNight={DomainState.TargetNightNumber}, Phase={DomainState.CurrentNightPhase}, IntermissionStage={stageState}, VoteStages={stageCount}, Preset={DomainState.ActivePresetId}, UnlimitedShift={DomainState.UnlimitedShiftTime}.");
#if UNITY_EDITOR
            if (resolvedTarget == 1)
            {
                CheckpointStore.Capture(PlaytestCheckpointKind.PrePreparation, DomainState, RunProgressionService, EmployeeTaskService, Clock != null ? Clock.Now : Time.timeAsDouble);
            }
#endif
            DomainState.IncrementRevision();
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
                        int slotIndex = globalToolSlotIndex++;
                        var toolState = new ToolItemState(toolId, toolData.Id, toolData.Capabilities, LocationRef.InStation(toolRackStationId, slotIndex));
                        DomainState.Tools[toolId] = toolState;
                        NightCycleService.RegisterCanonicalToolOrigin(DomainState, toolState, toolRackStationId, slotIndex);
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
            var bpPreset = GetEffectiveRunPreset();
            int bpCap = bpPreset != null && bpPreset.backpackInitialSlots > 0
                ? bpPreset.backpackInitialSlots
                : (_config != null && _config.backpackInitialSlots > 0 ? _config.backpackInitialSlots : 2);
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
            ReinitializeForNewRunWithPolicyServer(newSeed, maxNights, difficulty, startNight, openPreparation: true);
        }

        public void ReinitializeForNewRunWithPolicyServer(int newSeed, int maxNights, RunDifficulty difficulty, int startNight, bool openPreparation)
        {
            if (!IsServer || DomainState == null) return;

            Presentation.EmployeePopulationDirector.ClearAll();
            EmployeeTaskService?.ResetForNewRun();
            SpeciesHostilityService?.ResetForNewRun();
            ZombieConversionService?.Reset();
            ScaryParty.Combat.Monsters.MonsterPopulationDirector.Instance?.ClearNightMonstersServer();
            DemandDirector.Instance?.ResetForNewRun();

            var preset = GetEffectiveRunPreset();
            int resolvedMaxNights = maxNights > 0 ? Mathf.Clamp(maxNights, 1, 7) : (preset != null ? preset.totalNights : 3);
            int startBudget = preset != null ? preset.startingBudget : (_config != null ? _config.startingBudget : 500);
            int startRep = preset != null ? preset.startingReputation : (_config != null ? _config.initialReputation : 100);
            RunProgressionService?.ResetRun(DomainState, newSeed, resolvedMaxNights, difficulty, startBudget, startRep);

            RebuildSceneDomainStateForNewRunServer();

            if (startNight == 0)
            {
                ResolveAndApplyNightStartParametersServer(startNight, isRetry: false);
                SeedPredictableTutorialOrderServer();
            }
            else if (openPreparation)
            {
                OpenPreparationBeforeNightServer(startNight);
            }
            else
            {
                ResolveAndApplyNightStartParametersServer(startNight, isRetry: false);
            }
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

            if (ScaryParty.UI.Piaria.Presenters.OperationsHubPresenter.Instance == null)
                ScaryParty.UI.Piaria.Presenters.OperationsHubPresenter.EnsureInstance();

            if (ScaryParty.UI.Piaria.Presenters.LifeAndSpectatorOverlayPresenter.Instance == null)
                ScaryParty.UI.Piaria.Presenters.LifeAndSpectatorOverlayPresenter.EnsureInstance();

            if (ScaryParty.UI.Piaria.Core.NightIntermissionFeelBridge.Instance == null)
                ScaryParty.UI.Piaria.Core.NightIntermissionFeelBridge.EnsureInstance();

            if (ScaryParty.UI.Piaria.Presenters.NightIntermissionPresenter.Instance == null)
                ScaryParty.UI.Piaria.Presenters.NightIntermissionPresenter.EnsureInstance();

#if UNITY_EDITOR
            if (Presentation.PlaytestDebugController.Instance == null &&
                FindFirstObjectByType<Presentation.PlaytestDebugController>(FindObjectsInactive.Include) == null)
            {
                var debugGo = new GameObject("[Debug] Central de Playtest");
                debugGo.AddComponent<Presentation.PlaytestDebugController>();
            }
#endif

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
                            CookingStage oldStage = CookingStage.Cooking;
                            if (DomainState.Items.TryGetValue(op.InputItemId, out var pizzaItem))
                            {
                                oldCookProgress = pizzaItem.CookProgress;
                                oldBurnProgress = pizzaItem.BurnProgress;
                                oldStage = pizzaItem.CookingStage;
                            }

                            var runStats = RunProgressionService != null ? RunProgressionService.RecalculateEffectiveStats() : null;
                            ProcessingService.TickOven(slot, DomainState, _config.ovenBurnGraceDuration, Clock, runStats != null && runStats.OvenAutoShutoffEnabled);
                            
                            if (DomainState.Items.TryGetValue(op.InputItemId, out var updatedPizza))
                            {
                                if (updatedPizza.CookingStage != oldStage ||
                                    Mathf.Abs(updatedPizza.CookProgress - oldCookProgress) > 0.04f ||
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

                // Funcionários só atuam durante preparação inicial/expediente ativo
                if (DomainState.CurrentNightPhase == NightPhase.Preparing || DomainState.CurrentNightPhase == NightPhase.Active)
                {
                    Presentation.EmployeePopulationDirector.TickAndProjectServer(this, Network.PizzeriaNetworkState.Instance, Time.deltaTime);
                }

                // Tick de Pedidos (A15), Chamadas, Suprimentos, Holds do Jogador e Clientes Presenciais somente durante expediente ativo e despausado
                if (DomainState.CurrentNightPhase == NightPhase.Active)
                {
                    bool shiftPaused = ScaryParty.Cutscenes.CutscenePlaybackService.IsNightTimerPaused || DomainState.IsShiftClockPaused;
                    if (shiftPaused)
                    {
                        NightCycleService.RebaseRelativeDeadlines(DomainState, Time.deltaTime);
                    }
                    else
                    {
                        OrderService?.TickOrders(DomainState, Clock, EmployeeTaskService);
                        CallService?.TickCalls(DomainState, (float)Clock.Now);
                        SpeciesHostilityService?.Tick(Time.deltaTime);
                        dirty |= NightCycleService.TickSupplies(DomainState, Clock.Now);

                        if (CustomerService != null)
                        {
                            bool custDirty = CustomerService.TickCustomers(
                                DomainState,
                                Catalog,
                                OrderService,
                                Clock,
                                Time.deltaTime,
                                _config != null ? _config.queueStagnationThresholdSeconds : 35f,
                                _config != null ? _config.queueStagnationDecayMultiplier : 1.35f,
                                EmployeeTaskService);
                            dirty |= custDirty;
                        }
                    }

                    int prevHoldCount = DomainState.PlayerHolds.Count;
                    PlayerHoldService?.TickHolds(
                        DomainState,
                        (float)Clock.Now,
                        OrderService,
                        CustomerService,
                        Clock,
                        _config != null ? _config.maxActiveOrders : 6,
                        EmployeeTaskService);
                    if (DomainState.PlayerHolds.Count != prevHoldCount)
                        dirty = true;
                }

                if (!ScaryParty.Cutscenes.CutscenePlaybackService.IsNightTimerPaused)
                {
                    var closingDiff = RunProgressionService != null ? RunProgressionService.Difficulty : (_config != null ? _config.runDifficulty : RunDifficulty.Normal);
                    var nightParams = _config != null
                        ? _config.ResolveEffectiveNightParameters(
                            DomainState.CurrentNightNumber,
                            Mathf.Max(1, DomainState.FrozenHumanCount),
                            closingDiff,
                            GetEffectiveRunPreset(),
                            DomainState.ResolvedModifierMultiplier > 0.01f ? DomainState.ResolvedModifierMultiplier : 1f).ToNightGoalParameters()
                        : (NightGoalParameters?)null;
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

        private const int MaxEmployeeSpawnSlots = 12;
        private readonly SpawnReservationService _spawnReservations = new SpawnReservationService();

        public SpawnReservationService SpawnReservations => _spawnReservations;

        public void ClearSpawnReservations()
        {
            _spawnReservations.ClearAll();
        }

        public void ReleaseEmployeeSpawnReservation(int employeeId)
        {
            _spawnReservations.ReleaseByOwner(employeeId, SpawnOwnerKind.Employee);
        }

        public void ReleasePlayerSpawnReservation(int clientId)
        {
            _spawnReservations.ReleaseByOwner(clientId, SpawnOwnerKind.Player);
        }

        public bool TryResolveSpawn(in SpawnRequest request, int maxSlots, int preferredStartSlot, out SpawnReservation reservation, out SpawnFailure failure)
        {
            var navMgr = ScaryParty.Navigation.Core.NavigationWorldManager.Instance;
            if (navMgr != null && !navMgr.IsReady)
            {
                reservation = default;
                failure = new SpawnFailure(SpawnFailureReason.NavMeshNotReady, "NavigationWorldManager ainda não está pronto.");
                return false;
            }

            Vector3 resolvedPos = Vector3.zero;
            SpawnFailure lastCheckFailure = default;
            SpawnRequest capturedRequest = request;

            bool ok = _spawnReservations.TryReserve(
                request.OwnerId,
                request.OwnerKind,
                maxSlots,
                preferredStartSlot,
                slot =>
                {
                    Vector3 spread = ComputeSpawnSpreadOffset(slot, transform.right, transform.forward);
                    var slotReq = capturedRequest;
                    slotReq.PrimaryPosition = capturedRequest.PrimaryPosition + spread;
                    return SpawnResolver.TryResolveCandidate(in slotReq, SpawnResolver.ValidateUnityCandidate, out resolvedPos, out lastCheckFailure);
                },
                out reservation,
                out failure);

            if (!ok)
            {
                if (failure.Reason == SpawnFailureReason.CapsuleBlocked && lastCheckFailure.Reason != SpawnFailureReason.None)
                {
                    failure = lastCheckFailure;
                }
                return false;
            }

            if (resolvedPos == Vector3.zero)
            {
                // Já possuía reserva ativa; revalida posição atual
                if (!SpawnResolver.TryResolveCandidate(in capturedRequest, SpawnResolver.ValidateUnityCandidate, out resolvedPos, out failure))
                {
                    _spawnReservations.ReleaseByOwner(capturedRequest.OwnerId, capturedRequest.OwnerKind);
                    reservation = default;
                    return false;
                }
            }

            reservation.Position = resolvedPos;
            reservation.Rotation = request.PrimaryRotation;
            _spawnReservations.UpdateReservationPose(request.OwnerId, request.OwnerKind, resolvedPos, request.PrimaryRotation);
            return true;
        }

        public static Vector3 ComputeSpawnSpreadOffset(int slot, Vector3 right, Vector3 forward)
        {
            if (slot <= 0) return Vector3.zero;
            int col = slot % 3;
            int lateral = col == 1 ? 1 : (col == 2 ? -1 : 0);
            int row = slot / 3;
            return right * (lateral * 1.15f) + forward * (row * 1.15f);
        }

        private static Vector3 ResolveEmployeePathDestination(EmployeeRole role, Vector3 fallbackPos, Vector3? waitingAnchor)
        {
            if (role == EmployeeRole.Cashier)
            {
                var counters = Presentation.EmployeeStationCache.GetCounters();
                if (counters != null && counters.Length > 0 && counters[0] != null)
                    return counters[0].transform.position;
            }
            else
            {
                var prep = Presentation.EmployeeStationCache.GetPrep();
                if (prep != null)
                    return prep.transform.position;
            }

            if (waitingAnchor.HasValue)
                return waitingAnchor.Value;

            return fallbackPos;
        }

        public static Vector3 ComputeInteriorGridLocalPosition(int slotIndex, float floorSurfaceLocalY)
        {
            int safe = Mathf.Max(0, slotIndex);
            int col = safe % 4;
            int row = safe / 4;
            return new Vector3(4.5f + col * 1.45f, floorSurfaceLocalY + 0.05f, -2.5f + row * 1.45f);
        }

        public bool TryResolveInteriorPlayerSpawnPoseServer(ulong clientId, int playerIndex, out Vector3 spawnPos, out Quaternion spawnRot)
        {
            EnsureAnchorsResolved();
            spawnRot = GetPlayerSpawnRotation(playerIndex);

            var fp = GetComponent<PizzeriaFootprint>();
            float floorY = fp != null ? fp.floorSurfaceLocalY : 0f;
            Vector3? recoveryPos = recoveryAnchor != null && IsWorldPositionInsidePizzeria(recoveryAnchor.position)
                ? recoveryAnchor.position
                : (Vector3?)null;

            var candidates = new List<(Vector3 pos, Quaternion rot)>(12);
            if (playerSpawnAnchors != null)
            {
                for (int i = 0; i < playerSpawnAnchors.Length; i++)
                {
                    var t = playerSpawnAnchors[i];
                    if (t != null && IsWorldPositionInsidePizzeria(t.position))
                        candidates.Add((t.position, t.rotation));
                }
            }

            var ext = GetComponent<PizzeriaExteriorAnchors>();
            if (ext != null && ext.playerSpawns != null)
            {
                for (int i = 0; i < ext.playerSpawns.Length; i++)
                {
                    var t = ext.playerSpawns[i];
                    if (t != null && IsWorldPositionInsidePizzeria(t.position))
                        candidates.Add((t.position, t.rotation));
                }
            }

            for (int g = 0; g < 8; g++)
            {
                Vector3 worldPt = transform.TransformPoint(ComputeInteriorGridLocalPosition(g, floorY));
                if (fp == null || IsWorldPositionInsidePizzeria(worldPt))
                {
                    candidates.Add((worldPt, transform.rotation));
                }
            }

            if (candidates.Count == 0)
            {
                candidates.Add((transform.TransformPoint(ComputeInteriorGridLocalPosition(playerIndex, floorY)), transform.rotation));
            }

            int ownerId = unchecked((int)clientId);
            var usedPositions = new List<Vector3>(8);
            _spawnReservations.CollectPositions(SpawnOwnerKind.Player, usedPositions, ownerId);

            bool IsSeparatedFromOtherPlayers(Vector3 p)
            {
                for (int i = 0; i < usedPositions.Count; i++)
                {
                    if ((usedPositions[i] - p).sqrMagnitude < 0.72f)
                        return false;
                }
                return true;
            }

            Vector3 resolvedPos = Vector3.zero;
            Quaternion resolvedRot = spawnRot;

            bool reserved = _spawnReservations.TryReserve(
                ownerId,
                SpawnOwnerKind.Player,
                16,
                Mathf.Max(0, playerIndex),
                slot =>
                {
                    var c = candidates[slot % candidates.Count];
                    var req = new SpawnRequest
                    {
                        OwnerId = ownerId,
                        OwnerKind = SpawnOwnerKind.Player,
                        PrimaryPosition = c.pos,
                        PrimaryRotation = c.rot,
                        RecoveryPosition = recoveryPos,
                        ExpectedFloorY = c.pos.y,
                        MaxAnchorDistance = 6f,
                        MaxSlopeDegrees = 35f,
                        CapsuleRadius = 0.34f,
                        CapsuleHeight = 1.8f,
                        CollisionMask = Physics.DefaultRaycastLayers
                    };

                    if (SpawnResolver.TryResolveCandidate(in req, SpawnResolver.ValidateUnityCandidate, out var validPos, out _) &&
                        (fp == null || IsWorldPositionInsidePizzeria(validPos)) &&
                        IsSeparatedFromOtherPlayers(validPos))
                    {
                        resolvedPos = validPos;
                        resolvedRot = c.rot;
                        return true;
                    }
                    return false;
                },
                out var reservation,
                out _);

            if (reserved)
            {
                if (resolvedPos == Vector3.zero)
                {
                    resolvedPos = reservation.Position != Vector3.zero
                        ? reservation.Position
                        : candidates[Mathf.Max(0, playerIndex) % candidates.Count].pos;
                    resolvedRot = reservation.Rotation != default ? reservation.Rotation : spawnRot;
                }
                _spawnReservations.UpdateReservationPose(ownerId, SpawnOwnerKind.Player, resolvedPos, resolvedRot);
                spawnPos = resolvedPos;
                spawnRot = resolvedRot;
                return true;
            }

            // Fallback seguro se NavMesh ainda não estiver pronto ou física indisponível (mantém separação no interior)
            for (int offset = 0; offset < candidates.Count; offset++)
            {
                var c = candidates[(Mathf.Max(0, playerIndex) + offset) % candidates.Count];
                if (IsSeparatedFromOtherPlayers(c.pos))
                {
                    spawnPos = c.pos;
                    spawnRot = c.rot;
                    return true;
                }
            }

            spawnPos = transform.TransformPoint(ComputeInteriorGridLocalPosition(playerIndex, floorY));
            spawnRot = transform.rotation;
            return true;
        }

        public Vector3 GetPlayerSpawnPosition(int index)
        {
            EnsureAnchorsResolved();
            Vector3 rawPos;
            Quaternion rawRot = GetPlayerSpawnRotation(index);
            Vector3? recoveryPos = recoveryAnchor != null ? recoveryAnchor.position : (Vector3?)null;
            var ext = GetComponent<PizzeriaExteriorAnchors>();
            if (ext != null && ext.playerRecovery != null) recoveryPos = ext.playerRecovery.position;

            if (ext != null && ext.playerSpawns != null && ext.playerSpawns.Length > 0)
            {
                int safeIdx = Mathf.Clamp(index, 0, ext.playerSpawns.Length - 1);
                if (ext.playerSpawns[safeIdx] != null)
                {
                    rawPos = ext.playerSpawns[safeIdx].position;
                    var req = new SpawnRequest
                    {
                        OwnerId = index,
                        OwnerKind = SpawnOwnerKind.Player,
                        PrimaryPosition = rawPos,
                        PrimaryRotation = rawRot,
                        RecoveryPosition = recoveryPos,
                        ExpectedFloorY = rawPos.y,
                        MaxAnchorDistance = 6f,
                        MaxSlopeDegrees = 35f,
                        CapsuleRadius = 0.34f,
                        CapsuleHeight = 1.8f,
                        CollisionMask = Physics.DefaultRaycastLayers
                    };
                    if (SpawnResolver.TryResolveCandidate(in req, SpawnResolver.ValidateUnityCandidate, out Vector3 safeExt, out _))
                        return safeExt;
                    return rawPos;
                }
            }
            if (playerSpawnAnchors != null && playerSpawnAnchors.Length > 0)
            {
                int safeIdx = Mathf.Clamp(index, 0, playerSpawnAnchors.Length - 1);
                if (playerSpawnAnchors[safeIdx] != null)
                {
                    rawPos = playerSpawnAnchors[safeIdx].position;
                    var req = new SpawnRequest
                    {
                        OwnerId = index,
                        OwnerKind = SpawnOwnerKind.Player,
                        PrimaryPosition = rawPos,
                        PrimaryRotation = rawRot,
                        RecoveryPosition = recoveryPos,
                        ExpectedFloorY = rawPos.y,
                        MaxAnchorDistance = 6f,
                        MaxSlopeDegrees = 35f,
                        CapsuleRadius = 0.34f,
                        CapsuleHeight = 1.8f,
                        CollisionMask = Physics.DefaultRaycastLayers
                    };
                    if (SpawnResolver.TryResolveCandidate(in req, SpawnResolver.ValidateUnityCandidate, out Vector3 safeAnchor, out _))
                        return safeAnchor;
                    return rawPos;
                }
            }
            var fp = GetComponent<PizzeriaFootprint>();
            Vector3 fallbackLocal = new Vector3(4.5f + (index % 4) * 1.4f, fp != null ? fp.floorSurfaceLocalY + 0.05f : 0.05f, -2.5f);
            rawPos = transform.TransformPoint(fallbackLocal);
            var fallbackReq = new SpawnRequest
            {
                OwnerId = index,
                OwnerKind = SpawnOwnerKind.Player,
                PrimaryPosition = rawPos,
                PrimaryRotation = rawRot,
                RecoveryPosition = recoveryPos,
                ExpectedFloorY = rawPos.y,
                MaxAnchorDistance = 6f,
                MaxSlopeDegrees = 35f,
                CapsuleRadius = 0.34f,
                CapsuleHeight = 1.8f,
                CollisionMask = Physics.DefaultRaycastLayers
            };
            return SpawnResolver.TryResolveCandidate(in fallbackReq, SpawnResolver.ValidateUnityCandidate, out Vector3 safeFallback, out _) ? safeFallback : rawPos;
        }

        public Quaternion GetPlayerSpawnRotation(int index)
        {
            EnsureAnchorsResolved();
            var ext = GetComponent<PizzeriaExteriorAnchors>();
            if (ext != null && ext.playerSpawns != null && ext.playerSpawns.Length > 0)
            {
                int safeIdx = Mathf.Clamp(index, 0, ext.playerSpawns.Length - 1);
                if (ext.playerSpawns[safeIdx] != null)
                    return ext.playerSpawns[safeIdx].rotation;
            }
            if (playerSpawnAnchors != null && playerSpawnAnchors.Length > 0)
            {
                int safeIdx = Mathf.Clamp(index, 0, playerSpawnAnchors.Length - 1);
                if (playerSpawnAnchors[safeIdx] != null)
                    return playerSpawnAnchors[safeIdx].rotation;
            }
            return transform.rotation;
        }

        public bool GetEmployeeSpawnPose(EmployeeRole role, int employeeId, out Vector3 spawnPos, out Quaternion spawnRot)
        {
            return TryGetEmployeeSpawnPose(role, employeeId, out spawnPos, out spawnRot, out _);
        }

        public bool TryGetEmployeeSpawnPose(EmployeeRole role, int employeeId, out Vector3 spawnPos, out Quaternion spawnRot, out SpawnFailure failure)
        {
            spawnPos = Vector3.zero;
            spawnRot = transform.rotation;

            EnsureAnchorsResolved();
            var ext = GetComponent<PizzeriaExteriorAnchors>();
            Transform preferred = null;
            if (ext != null)
            {
                preferred = role == EmployeeRole.Cashier
                    ? (ext.employeeSpawnCashier != null ? ext.employeeSpawnCashier : ext.employeeArrival)
                    : (ext.employeeSpawnCook != null ? ext.employeeSpawnCook : ext.employeeArrival);
            }

            var fp = GetComponent<PizzeriaFootprint>();
            float floorY = fp != null ? fp.floorSurfaceLocalY : -1.6f;
            Vector3 localPos = role == EmployeeRole.Cashier
                ? new Vector3(6.0f, floorY + 0.05f, -1.5f)
                : new Vector3(20.0f, floorY + 0.05f, -18.0f);
            Vector3 insideFallbackPos = transform.TransformPoint(localPos);

            Vector3 basePos;
            if (preferred != null)
            {
                basePos = preferred.position;
                spawnRot = preferred.rotation;
            }
            else
            {
                basePos = insideFallbackPos;
                spawnRot = transform.rotation;
            }

            Vector3? recovery = ext != null && ext.employeeRecovery != null ? ext.employeeRecovery.position : (Vector3?)null;
            Vector3? waiting = ext != null && ext.employeeWaiting != null ? ext.employeeWaiting.position : (Vector3?)null;
            Vector3 pathDestination = ResolveEmployeePathDestination(role, basePos, waiting);
            Vector3[] altPositions = preferred != null
                ? (waiting.HasValue ? new[] { waiting.Value, insideFallbackPos } : new[] { insideFallbackPos })
                : (waiting.HasValue ? new[] { waiting.Value } : null);

            var req = new SpawnRequest
            {
                OwnerId = employeeId,
                OwnerKind = SpawnOwnerKind.Employee,
                PrimaryPosition = basePos,
                PrimaryRotation = spawnRot,
                AlternativePositions = altPositions,
                RecoveryPosition = recovery,
                PathDestination = pathDestination,
                ExpectedFloorY = basePos.y,
                MaxAnchorDistance = 8f,
                MaxSlopeDegrees = 35f,
                CapsuleRadius = 0.34f,
                CapsuleHeight = 1.8f,
                CollisionMask = Physics.DefaultRaycastLayers
            };

            if (!TryResolveSpawn(in req, MaxEmployeeSpawnSlots, Mathf.Abs(employeeId) % 4, out SpawnReservation res, out failure))
            {
                return false;
            }

            spawnPos = res.Position;
            spawnRot = res.Rotation;
            return true;
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
            var footprint = GetComponent<PizzeriaFootprint>();
            if (footprint != null)
            {
                return footprint.IsWorldPositionInsideInterior(worldPos);
            }
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

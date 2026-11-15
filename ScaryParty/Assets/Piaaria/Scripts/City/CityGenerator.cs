using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

/// <summary>
/// Main orchestrator for procedural city generation.
/// </summary>
public class CityGenerator : NetworkBehaviour
{
    [Header("Configuration")]
    public CityConfig config;
    public GameObject pizzeriaPrefab;

    [Header("Materials â€” Streets")]
    public Material streetMaterial;
    public Material sidewalkMaterial;
    public Material intersectionMaterial;

    [Header("Materials â€” Buildings")]
    public Material[] buildingMaterials;

    [Header("Materials - Road Markings & Crosswalks")]
    public Material roadMarkingYellowMaterial;
    public Material roadMarkingWhiteMaterial;
    public Material crosswalkMaterial;

    [SerializeField, HideInInspector]
    private CityData _cityData;

    public CityData CityData 
    { 
        get => _cityData; 
        private set => _cityData = value; 
    }

    public int CurrentSeed { get; private set; }

    /// <summary>
    /// Seed persistida via rede. Qualquer cliente que entrar na sessÃ£o (inclusive late-joiners)
    /// lerÃ¡ este valor no OnNetworkSpawn e gerarÃ¡ a cidade localmente com a mesma seed.
    /// </summary>
    private NetworkVariable<int> _networkSeed = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event Action OnCityGenerated;
    public event Action OnCityCleared;

    // The list is authoritative; the revision is a synchronization barrier for late joins/rebuilds.
    private NetworkList<int> _blockedEdges;
    private readonly NetworkVariable<int> _closureRevision = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private int _appliedClosureRevision = -1;
    private int _appliedSeed;
    private bool _awaitingSnapshot;
    private int _snapshotRevision = -1;
    private int _snapshotSeed;
    private int[] _snapshotEdges;
    public bool ClosuresSynchronized => _pathfinder != null && _pathfinder.IsReady &&
        _pathfinder.RoutesReady && (!IsSpawned || (IsServer && _networkSeed.Value == CurrentSeed) ||
            (!_awaitingSnapshot && _appliedSeed == _networkSeed.Value &&
            _appliedClosureRevision == _closureRevision.Value));
    public int ClosureRevision => _closureRevision.Value;
    public event Action OnClosuresChanged;

    public void SetRoadBlocked(int edge, bool blocked)
    {
        if (!IsSpawned || !IsServer || _pathfinder == null || !_pathfinder.IsReady ||
            edge < 0 || edge >= _pathfinder.Graph.edges.Count) return;
        if (_blockedEdges.Contains(edge) == blocked) return;
        if (blocked) _blockedEdges.Add(edge);
        else _blockedEdges.Remove(edge);
        _closureRevision.Value++;
        ApplyRoadClosures();


    }

    private void OnBlockedEdgesChanged(NetworkListEvent<int> change)
    {
        if (IsServer) ApplyRoadClosures();
        else
        {
            _awaitingSnapshot = true;
            _pathfinder?.SetRoutesReady(false);
            RequestClosures();
        }
    }

    private void OnClosureRevisionChanged(int previous, int current)
    {
        if (IsServer) return;
        _awaitingSnapshot = true;
        _pathfinder?.SetRoutesReady(false);
        RequestClosures();
    }

    private void ApplyRoadClosures()
    {
        if (_pathfinder == null || !_pathfinder.IsReady || CityData == null) return;
        if (!IsServer && (_awaitingSnapshot || _snapshotEdges == null ||
            _snapshotSeed != _networkSeed.Value || _snapshotRevision != _closureRevision.Value)) return;
        var blocked = new HashSet<int>();
        if (IsServer)
        {
            for (int i = 0; i < _blockedEdges.Count; i++) blocked.Add(_blockedEdges[i]);
        }
        else blocked.UnionWith(_snapshotEdges);
        for (int i = 0; i < _pathfinder.Graph.edges.Count; i++)
        {
            if (blocked.Contains(i)) _pathfinder.BlockEdge(i);
            else _pathfinder.UnblockEdge(i);
        }
        _appliedSeed = CurrentSeed;
        _appliedClosureRevision = _closureRevision.Value;
        _pathfinder.SetRoutesReady(true);
        if (IsSpawned && !IsServer) _eventManager?.SynchronizeVisuals(blocked);
        OnClosuresChanged?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestClosuresServerRpc(int seed, ServerRpcParams rpcParams = default)
    {
        if (seed != CurrentSeed || CityData == null) return;
        var target = new ClientRpcParams { Send = new ClientRpcSendParams {
            TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } };
        var edges = new int[_blockedEdges.Count];
        for (int i = 0; i < edges.Length; i++) edges[i] = _blockedEdges[i];
        ReceiveClosuresClientRpc(seed, _closureRevision.Value, edges, target);
    }

    [ClientRpc]
    private void ReceiveClosuresClientRpc(int seed, int revision, int[] edges, ClientRpcParams rpcParams = default)
    {
        if (IsServer || !IsSpawned || seed != _networkSeed.Value ||
            revision != _closureRevision.Value) return;
        _snapshotSeed = seed;
        _snapshotRevision = revision;
        _snapshotEdges = edges;
        _awaitingSnapshot = false;
        ApplyRoadClosures();


    }

    private void RequestClosures()
    {
        if (IsServer || !IsSpawned || CityData == null || CurrentSeed != _networkSeed.Value) return;
        _awaitingSnapshot = true;
        _pathfinder?.SetRoutesReady(false);
        RequestClosuresServerRpc(CurrentSeed);
    }

    private StreetGridGenerator _streetGridGen;
    private BlockFiller _blockFiller;
    private IntersectionBuilder _intersectionBuilder;
    private DeliveryPointPlacer _deliveryPointPlacer;
    private CityGraphPathfinder _pathfinder;
    private EnemySpawnPointGenerator _enemySpawnGen;
    private TrafficManager _trafficManager;
    private CityEventManager _eventManager;

    private GameObject _cityRoot;
    private GameObject _streetsRoot;
    private GameObject _blocksRoot;
    private GameObject _intersectionsRoot;
    private GameObject _deliveryPointsRoot;
    private GameObject _trafficRoot;
    private GameObject _eventsRoot;
    private GameObject _markingsRoot;
    private GameObject _crosswalksRoot;
    private GameObject _pedestriansRoot;
    private ScaryParty.Navigation.Core.NavigationWorldManager _navWorldManager;
    private CityPopulationDirector _populationDirector;

    public override void OnNetworkSpawn()
    {
        _blockedEdges.OnListChanged += OnBlockedEdgesChanged;
        _closureRevision.OnValueChanged += OnClosureRevisionChanged;
        if (IsServer)
        {
            if (config == null) return;

            // Gera uma seed e persiste na NetworkVariable ANTES de gerar localmente.
            // Isso garante que qualquer cliente que entrar depois (late-join) consiga ler
            // a seed e gerar a cidade localmente sem precisar do ClientRpc.
            int useSeed = config.seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : config.seed;
            _networkSeed.Value = useSeed;
            _blockedEdges.Clear();
            _closureRevision.Value++;

            DestroyStaticCity();
            GenerateLocally(useSeed);
            // ClientRpc Ã© um fast-path para clientes jÃ¡ conectados no momento do spawn.
            ClientGenerateCityClientRpc(useSeed);
        }
        else
        {
            // Registra listener ANTES de verificar o valor atual.
            // Ordem importa: se o server ainda nÃ£o setou a seed, o OnValueChanged vai disparar quando setar.
            _networkSeed.OnValueChanged += OnNetworkSeedChanged;

            DestroyStaticCity();

            // Late-join guard: se a seed jÃ¡ foi setada pelo servidor antes de entrarmos,
            // o OnValueChanged NÃƒO vai disparar (o valor nÃ£o vai mudar para nÃ³s).
            // Por isso verificamos o valor atual agora e geramos se necessÃ¡rio.
            if (_networkSeed.Value != 0)
            {
                Debug.Log($"[CityGenerator] Late-join detectado â€” gerando cidade com seed {_networkSeed.Value}");
                GenerateLocally(_networkSeed.Value);
            }
            else
            {
                Debug.Log("[CityGenerator] Cliente conectou antes do servidor gerar â€” aguardando OnValueChanged.");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        _blockedEdges.OnListChanged -= OnBlockedEdgesChanged;
        _closureRevision.OnValueChanged -= OnClosureRevisionChanged;
        _awaitingSnapshot = true;
        _snapshotEdges = null;
        _appliedClosureRevision = -1;
        _pathfinder?.SetRoutesReady(false);
        if (!IsServer)
        {
            _networkSeed.OnValueChanged -= OnNetworkSeedChanged;
        }
    }

    /// <summary>
    /// Chamado no cliente quando o servidor seta a seed pela primeira vez.
    /// SÃ³ Ã© relevante para clientes que estavam conectados antes do servidor gerar.
    /// Clientes que entraram depois (late-join) jÃ¡ leram o valor diretamente no OnNetworkSpawn.
    /// </summary>
    private void OnNetworkSeedChanged(int previousValue, int newValue)
    {
        if (IsServer) return;
        _awaitingSnapshot = true;
        _pathfinder?.SetRoutesReady(false);
        if (newValue == 0) { ClearLocalCity(); return; }

        if (CityData != null && CurrentSeed == newValue) return;

        Debug.Log($"[CityGenerator] Seed recebida via NetworkVariable ({newValue}) â€” gerando cidade.");
        DestroyStaticCity();
        ClearLocalCity();
        GenerateLocally(newValue);
    }

    private void OnEnable()
    {
        if (config != null) config.OnConfigChanged += HandleConfigChanged;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.afterAssemblyReload += ReSubscribeAfterReload;
#endif
    }

    private void OnDisable()
    {
        if (config != null) config.OnConfigChanged -= HandleConfigChanged;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.afterAssemblyReload -= ReSubscribeAfterReload;
#endif
    }

#if UNITY_EDITOR
    private void ReSubscribeAfterReload()
    {
        if (config != null) 
        {
            config.OnConfigChanged -= HandleConfigChanged;
            config.OnConfigChanged += HandleConfigChanged;
        }
    }
#endif

    private void HandleConfigChanged()
    {
        if (config != null && config.gerarEmTempoReal && !Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= GenerateCity;
            UnityEditor.EditorApplication.delayCall += GenerateCity;
#endif
        }
    }

    private void Awake()
    {
        _blockedEdges = new NetworkList<int>();
        if (Application.isPlaying) DestroyStaticCity();
    }

    private void DestroyStaticCity()
    {
        GameObject staticCity = GameObject.Find("__City__");
        if (staticCity != null)
        {
            staticCity.SetActive(false);
            Destroy(staticCity);
        }

        var oldResiduals = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in oldResiduals)
        {
            if (t != null && (t.name.StartsWith("BlockGround") || t.name.StartsWith("SolidCore")))
            {
                Destroy(t.gameObject);
            }
        }
    }

    [ContextMenu("Generate City")]
    public void GenerateCity()
    {
        if (config == null || (IsSpawned && !IsServer)) return;
        ClearLocalCity();

        // Sempre gerar uma nova seed no Quick Rebuild
        config.seed = UnityEngine.Random.Range(1, int.MaxValue);
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(config);
        #endif
        
        int useSeed = config.seed;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                _blockedEdges.Clear();
                _networkSeed.Value = useSeed;
                _closureRevision.Value++;
                GenerateLocally(useSeed);
                ClientGenerateCityClientRpc(useSeed);
            }
        }
        else
        {
            GenerateLocally(useSeed);
        }
    }

    [ContextMenu("Clear City")]
    public void ClearCity()
    {
        if (IsSpawned && !IsServer) return;
        ClearLocalCity();
        if (IsSpawned)
        {
            _blockedEdges.Clear();
            _networkSeed.Value = 0;
            _closureRevision.Value++;
            ClearCityClientRpc();
        }
    }

    [ClientRpc]
    private void ClearCityClientRpc()
    {
        if (!IsServer) ClearLocalCity();
    }

    private void ClearLocalCity()
    {
        _awaitingSnapshot = true;
        _snapshotEdges = null;
        _appliedClosureRevision = -1;
        _appliedSeed = 0;
        _pathfinder?.SetRoutesReady(false);
        _pathfinder = null;
        OnClosuresChanged?.Invoke();
        if (_cityRoot != null)
        {
            _cityRoot.SetActive(false);
            if (Application.isPlaying) Destroy(_cityRoot);
            else DestroyImmediate(_cityRoot);
        }

        GameObject oldCity = _cityRoot == null ? GameObject.Find("__City__") : null;
        if (oldCity != null)
        {
            oldCity.SetActive(false);
            if (Application.isPlaying) Destroy(oldCity);
            else DestroyImmediate(oldCity);
        }

        // Remove qualquer peca antiga residual que tenha ficado na cena (BlockGround ou SolidCore)
        var oldResiduals = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in oldResiduals)
        {
            if (t != null && (t.name.StartsWith("BlockGround") || t.name.StartsWith("SolidCore")))
            {
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        }

        CityData = null;
        CurrentSeed = 0;
        CityBuildingRegistry.Instance?.Clear();
        _populationDirector?.ClearPopulation();
        _populationDirector = null;
        _navWorldManager = null;
        _cityRoot = _streetsRoot = _blocksRoot = _intersectionsRoot = _deliveryPointsRoot = _trafficRoot = _eventsRoot = _markingsRoot = _crosswalksRoot = _pedestriansRoot = null;
        _trafficManager = null;
        _eventManager = null;

        OnCityCleared?.Invoke();
    }

    [ContextMenu("Regenerate City")]
    public void RegenerateCity()
    {
        if (IsSpawned && !IsServer) return;
        GenerateCity();
    }

    public CityGraphPathfinder GetPathfinder() => _pathfinder;
    public TrafficManager GetTrafficManager() => _trafficManager;
    public CityEventManager GetEventManager() => _eventManager;

    [ClientRpc]
    private void ClientGenerateCityClientRpc(int seed)
    {
        if (IsServer) return;

        if (seed != _networkSeed.Value || (CityData != null && CurrentSeed == seed)) return;
        ClearLocalCity();
        GenerateLocally(seed);
    }

    private void GenerateLocally(int seed)
    {
        if (CityData != null) ClearLocalCity();
        _awaitingSnapshot = IsSpawned && !IsServer;
        _snapshotEdges = null;
        _appliedClosureRevision = -1;
        CurrentSeed = seed;
        System.Random rng = new System.Random(seed);

        CreateHierarchy();
        EnsureSubGenerators();

        CityData = ScriptableObject.CreateInstance<CityData>();
        CityData.Initialize(config.gridWidth, config.gridHeight, config.maxStreetBranchLength, config.maxStreetBranchLength, config.streetWidth);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Piaaria/ScriptableObjects"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Piaaria", "ScriptableObjects");

            string assetPath = "Assets/Piaaria/ScriptableObjects/GeneratedCityData.asset";
            CityData existingData = UnityEditor.AssetDatabase.LoadAssetAtPath<CityData>(assetPath);
            if (existingData != null) UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            
            UnityEditor.AssetDatabase.CreateAsset(CityData, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            _cityData = CityData;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        Material resolvedStreetMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
            streetMaterial, ScaryParty.VisualStyles.VisualSemanticRole.Street, ScaryParty.VisualStyles.VisualCategoryIds.Streets);
        Material resolvedSidewalkMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
            sidewalkMaterial, ScaryParty.VisualStyles.VisualSemanticRole.Sidewalk, ScaryParty.VisualStyles.VisualCategoryIds.Streets);
        Material resolvedIntersectionMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
            intersectionMaterial, ScaryParty.VisualStyles.VisualSemanticRole.Intersection, ScaryParty.VisualStyles.VisualCategoryIds.Streets);
        Material resolvedYellowMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
            roadMarkingYellowMaterial, ScaryParty.VisualStyles.VisualSemanticRole.RoadYellow, ScaryParty.VisualStyles.VisualCategoryIds.Streets);
        Material resolvedWhiteMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
            roadMarkingWhiteMaterial, ScaryParty.VisualStyles.VisualSemanticRole.RoadWhite, ScaryParty.VisualStyles.VisualCategoryIds.Streets);
        Material resolvedCrosswalkMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
            crosswalkMaterial, ScaryParty.VisualStyles.VisualSemanticRole.Crosswalk, ScaryParty.VisualStyles.VisualCategoryIds.Streets);

        StreetGraph graph = _streetGridGen.Generate(
            config, rng, _streetsRoot.transform,
            resolvedStreetMat, resolvedSidewalkMat, buildingMaterials, out List<Vector3[]> blockPolygons
        );
        CityData.streetGraph = graph;

        GenerateBlocks(rng, blockPolygons, out ScaryParty.City.PlacementResult placementResult);

        if (placementResult != null && placementResult.success)
        {
            InstantiatePizzeriaFromPlacement(placementResult);
        }
        else if (config.pizzariaInsideBlock)
        {
            Debug.LogWarning($"[CityGenerator] Falha na implantaÃ§Ã£o geomÃ©trica da pizzaria: {placementResult?.failureReason}. Tentando fallback perimetral.");
            PlacePizzariaInBlock(rng);
        }
        else
        {
            CityData.pizzariaPosition = Vector3.zero;
            GeneratePizzariaLegacy();
        }

        _intersectionBuilder.BuildIntersections(graph, config, rng, _intersectionsRoot.transform, resolvedIntersectionMat);

        if (_markingsRoot != null)
        {
            RoadMarkingGenerator.GenerateMarkings(graph, config, _markingsRoot.transform, resolvedYellowMat, resolvedWhiteMat);
        }
        if (_crosswalksRoot != null)
        {
            CrosswalkGenerator.GenerateCrosswalks(graph, config, _crosswalksRoot.transform, resolvedCrosswalkMat, out _);
        }

        CityBuilding[] allBuildings = _blocksRoot.GetComponentsInChildren<CityBuilding>();
        List<DeliveryPoint> points = _deliveryPointPlacer.PlaceDeliveryPoints(CityData.blocks.ToArray(), allBuildings, config, rng, _deliveryPointsRoot.transform);
        CityData.DeliveryPointCount = points.Count;

        _pathfinder.Initialize(graph);
        _pathfinder.MaxAccessDistance = Mathf.Max(config.streetWidth + config.sidewalkWidth, 20f);
        if (IsSpawned && !IsServer) _pathfinder.SetRoutesReady(false);
        ApplyRoadClosures();



        _navWorldManager = _cityRoot.GetComponent<ScaryParty.Navigation.Core.NavigationWorldManager>() ?? _cityRoot.AddComponent<ScaryParty.Navigation.Core.NavigationWorldManager>();
        _navWorldManager.BuildCityNavMesh();

        MinimapRouteManager routeManager = FindObjectOfType<MinimapRouteManager>();
        if (routeManager != null) routeManager.Initialize(_pathfinder);

        _enemySpawnGen.GenerateSpawnPoints(CityData, config, rng);

        if (Application.isPlaying) InitializeRuntimeSystems(seed, graph);

#if UNITY_EDITOR
        if (!Application.isPlaying && CityData != null)
        {
            UnityEditor.EditorUtility.SetDirty(CityData);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        TeleportPlayersToSpawn();
        OnCityGenerated?.Invoke();
        RequestClosures();
    }

    private void InitializeRuntimeSystems(int seed, StreetGraph graph)
    {
        if (config.enablePedestrians)
        {
            _populationDirector = _cityRoot.GetComponent<CityPopulationDirector>() ?? _cityRoot.AddComponent<CityPopulationDirector>();
            _populationDirector.Initialize(config, seed + 3000, _pedestriansRoot.transform);
        }

        if (config.maxTrafficVehicles > 0)
        {
            _trafficManager.Initialize(_pathfinder, graph, seed + 1000, config.maxTrafficVehicles, config.trafficBaseSpeed);
        }
        _eventManager.Initialize(graph, _trafficManager, config, seed + 2000, _eventsRoot.transform, this);
        if (IsSpawned && !IsServer && ClosuresSynchronized && _snapshotEdges != null)
            _eventManager.SynchronizeVisuals(new HashSet<int>(_snapshotEdges));
    }

    private void TeleportPlayersToSpawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            System.Array.Sort(allPlayers, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

            for (int i = 0; i < allPlayers.Length; i++)
            {
                var ps = allPlayers[i];
                if (ps == null) continue;

                Vector3 targetPos = Vector3.zero;
                Quaternion targetRot = Quaternion.identity;
                bool foundPos = false;

                if (ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null)
                {
                    targetPos = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnPosition(i);
                    targetRot = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnRotation(i);
                    foundPos = true;
                }
                else
                {
                    GameObject spawnPoint = GameObject.Find($"NetworkSpawnPoint_{i % 4}")
                        ?? GameObject.Find("NetworkSpawnPoint_0")
                        ?? GameObject.Find("NetworkSpawnPoint");

                    if (spawnPoint != null)
                    {
                        targetPos = spawnPoint.transform.position;
                        targetRot = spawnPoint.transform.rotation;
                        foundPos = true;
                    }
                }

                if (foundPos)
                {
                    if (ps.IsOwner)
                    {
                        ps.PerformTeleport(targetPos, targetRot);
                    }
                    else if (IsServer)
                    {
                        ps.ForceTeleportClientRpc(targetPos, targetRot);
                    }
                }
            }

            // Fallback caso LocalClient.PlayerObject ainda nao conste em allPlayers
            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var po = NetworkManager.Singleton.LocalClient.PlayerObject;
                var ps = po.GetComponent<PlayerState>();
                if (ps != null && ps.IsOwner)
                {
                    ps.RequestTeleportToSpawn();
                }
            }
        }
    }

    private void InstantiatePizzeriaFromPlacement(ScaryParty.City.PlacementResult placement)
    {
        string blockParentName = $"Block_{placement.blockIndex}";
        Transform blockTransform = _blocksRoot.transform.Find(blockParentName);
        Transform parentTransform = blockTransform != null ? blockTransform : _blocksRoot.transform;

        if (pizzeriaPrefab != null)
        {
            var pObj = ScaryParty.Pizzeria.Integration.CityPizzeriaPlacementAdapter.SpawnPizzeria(
                pizzeriaPrefab, placement.position, placement.rotation, parentTransform, CityData, config.sidewalkHeight, 7.5f, placement.entranceDirectionWorld);

            if (pObj != null)
            {
                var pRoot = pObj.GetComponent<ScaryParty.Pizzeria.Composition.PizzeriaRoot>();
                for (int i = 0; i < 4; i++)
                {
                    GameObject sp = new GameObject($"NetworkSpawnPoint_{i}");
                    sp.transform.SetParent(pObj.transform);
                    if (pRoot != null)
                    {
                        sp.transform.position = pRoot.GetPlayerSpawnPosition(i);
                        sp.transform.rotation = pRoot.GetPlayerSpawnRotation(i);
                    }
                    else
                    {
                        sp.transform.position = placement.position + Vector3.up * 0.1f;
                        sp.transform.rotation = placement.rotation;
                    }
                }

                GameObject spLegacy = new GameObject("NetworkSpawnPoint");
                spLegacy.transform.SetParent(pObj.transform);
                if (pRoot != null)
                {
                    spLegacy.transform.position = pRoot.GetPlayerSpawnPosition(0);
                    spLegacy.transform.rotation = pRoot.GetPlayerSpawnRotation(0);
                }
                else
                {
                    spLegacy.transform.position = placement.position + Vector3.up * 0.1f;
                    spLegacy.transform.rotation = placement.rotation;
                }
            }
            return;
        }

        SpawnPrimitivePizzeria(placement.position, placement.rotation, placement.entranceDirectionWorld, parentTransform, blockTransform);
    }

    private void SpawnPrimitivePizzeria(Vector3 pizzariaPos, Quaternion pizzariaRot, Vector3 entranceDir, Transform parentTransform, Transform blockTransform)
    {
        Vector3 pizzariaScale = new Vector3(12f, 6f, 12f);
        float actualGroundLevel = 0.02f + (config != null ? config.sidewalkHeight : 0.15f);
        pizzariaPos.y = actualGroundLevel + (pizzariaScale.y * 0.5f);
        CityData.pizzariaPosition = pizzariaPos;
        CityData.pizzariaRotation = pizzariaRot;

        GameObject pizzariaBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pizzariaBuilding.name = "The_Pizzaria_Building";
        pizzariaBuilding.transform.position = pizzariaPos;
        pizzariaBuilding.transform.rotation = pizzariaRot;
        pizzariaBuilding.transform.localScale = pizzariaScale;
        pizzariaBuilding.transform.SetParent(parentTransform);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.8f, 0.1f, 0.1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
        pizzariaBuilding.GetComponent<Renderer>().sharedMaterial = mat;

        float offsetToSidewalk = (pizzariaScale.z * 0.5f) + 1.5f;
        Vector3 bancadaPos = pizzariaPos + entranceDir * offsetToSidewalk;
        bancadaPos.y = actualGroundLevel + 0.5f;

        GameObject bancada = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bancada.name = "Bancada_Pizzas";
        bancada.transform.position = bancadaPos;
        bancada.transform.rotation = pizzariaRot;
        bancada.transform.localScale = new Vector3(4f, 1f, 1.5f);
        bancada.transform.SetParent(blockTransform != null ? blockTransform : _blocksRoot.transform);

        Material bancadaMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        bancadaMat.color = new Color(0.4f, 0.2f, 0.1f);
        if (bancadaMat.HasProperty("_BaseColor")) bancadaMat.SetColor("_BaseColor", bancadaMat.color);
        bancada.GetComponent<Renderer>().sharedMaterial = bancadaMat;

        CityData.bancadaPosition = bancadaPos + new Vector3(0, 0.5f, 0);

        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Pizzaria_Sign";
        sign.transform.SetParent(pizzariaBuilding.transform);
        sign.transform.localPosition = new Vector3(0, 0.6f, 0.5f);
        sign.transform.localScale = new Vector3(0.8f, 0.2f, 0.1f);
        Material signMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        signMat.color = Color.yellow;
        if (signMat.HasProperty("_BaseColor")) signMat.SetColor("_BaseColor", signMat.color);
        sign.GetComponent<Renderer>().sharedMaterial = signMat;
        sign.GetComponent<Collider>().enabled = false;
    }

    private void PlacePizzariaInBlock(System.Random rng)
    {
        int bestBlock = FindBestPizzariaBlock();
        if (bestBlock < 0)
        {
            CityData.pizzariaPosition = Vector3.zero;
            GeneratePizzariaLegacy();
            return;
        }

        BlockInfo pBlock = CityData.blocks[bestBlock];
        CityData.pizzariaBlockIndex = bestBlock;
        pBlock.hasPizzaria = true;
        CityData.blocks[bestBlock] = pBlock; 

        string blockParentName = $"Block_{bestBlock}";
        Transform blockTransform = _blocksRoot.transform.Find(blockParentName);

        Vector3 pizzariaPos = pBlock.worldCenter;
        Vector3 entranceDir = Vector3.forward; 
        Quaternion pizzariaRot = Quaternion.identity;
        Vector3 pizzariaScale = new Vector3(12f, 6f, 12f);

        // Footprint do prefab, baseado no objeto "Floor" dele (fallback 12x12).
        GetPizzeriaFootprint(out Vector2 floorSize, out Vector2 floorCenter, out Vector2 frontLocal);
        float frontHalfDepth = Mathf.Abs(frontLocal.x) * floorSize.x * 0.5f + Mathf.Abs(frontLocal.y) * floorSize.y * 0.5f;
        float frontDistance = frontHalfDepth + Vector2.Dot(floorCenter, frontLocal) + 1.5f; // pivÃ´ -> bancada

        // â”€â”€ Pick the building closest to the block edge (= closest to the street) â”€â”€
        if (blockTransform != null)
        {
            Vector3 blockCenter = pBlock.worldCenter;
            CityBuilding bestBuilding = null;
            float bestEdgeDist = float.MaxValue;

            foreach (Transform child in blockTransform)
            {
                CityBuilding building = child.GetComponent<CityBuilding>();
                if (building == null) continue;

                // Score: distance from block center â†’ farther = closer to street edge.
                // We also need the building to be big enough to be replaced by the
                // pizzeria (12 m wide). Accept buildings â‰¥ 6 m wide.
                Vector3 scale = child.localScale;
                float footprint = Mathf.Max(scale.x, scale.z);
                if (footprint < 6f) continue;

                // Direction from center to building (outward toward street)
                Vector3 toBuilding = child.position - blockCenter;
                toBuilding.y = 0;
                float distFromCenter = toBuilding.magnitude;

                // Higher distFromCenter = closer to street. Pick the farthest out.
                if (distFromCenter > 0 && (bestBuilding == null || distFromCenter > -bestEdgeDist))
                {
                    bestEdgeDist = -distFromCenter; // negate so "best" = most negative
                    bestBuilding = building;
                }
            }

            // Fallback: if nothing was found with footprint â‰¥ 6, just pick the largest
            if (bestBuilding == null)
            {
                float largestVolume = 0f;
                foreach (Transform child in blockTransform)
                {
                    CityBuilding building = child.GetComponent<CityBuilding>();
                    if (building == null) continue;
                    Vector3 s = child.localScale;
                    float vol = s.x * s.y * s.z;
                    if (vol > largestVolume)
                    {
                        largestVolume = vol;
                        bestBuilding = building;
                    }
                }
            }

            if (bestBuilding != null)
            {
                // â”€â”€ Calculate street-snapped position â”€â”€
                Vector3 buildingPos = bestBuilding.transform.position;

                // Direction from block center outward (toward the street)
                entranceDir = (buildingPos - blockCenter);
                entranceDir.y = 0;
                if (entranceDir.sqrMagnitude < 0.01f) entranceDir = Vector3.forward;
                entranceDir.Normalize();

                // Snap to the nearest cardinal axis so the 12 Ã— 12 box aligns cleanly
                if (Mathf.Abs(entranceDir.x) > Mathf.Abs(entranceDir.z))
                    entranceDir = new Vector3(Mathf.Sign(entranceDir.x), 0, 0);
                else
                    entranceDir = new Vector3(0, 0, Mathf.Sign(entranceDir.z));

                // RotaÃ§Ã£o: a frente do prefab (direÃ§Ã£o do EntranceAnchor, frontLocal) deve
                // apontar para a rua (entranceDir).
                Vector3 frontLocal3 = new Vector3(frontLocal.x, 0f, frontLocal.y);
                pizzariaRot = Quaternion.LookRotation(entranceDir, Vector3.up)
                              * Quaternion.Inverse(Quaternion.LookRotation(frontLocal3, Vector3.up));

                // Use world-space bounds so rotation of the old building doesn't matter.
                Renderer rend = bestBuilding.GetComponent<Renderer>();
                Bounds bnd = rend != null ? rend.bounds : new Bounds(buildingPos, bestBuilding.transform.localScale);

                // Farthest extent of the old building in entranceDir = street edge
                float dotCenter = Vector3.Dot(bnd.center, entranceDir);
                float dotExtent = Mathf.Abs(bnd.extents.x * entranceDir.x)
                                + Mathf.Abs(bnd.extents.z * entranceDir.z);
                float streetEdgeDot = dotCenter + dotExtent;
                Vector3 streetEdge = entranceDir * streetEdgeDot;
                streetEdge.y = buildingPos.y;
                // Keep the lateral coordinate from the building
                if (Mathf.Abs(entranceDir.x) > 0.5f)
                    streetEdge.z = buildingPos.z;
                else
                    streetEdge.x = buildingPos.x;

                // A borda da frente do Floor (centro + frontLocal*halfDepth) fica rente Ã  calÃ§ada.
                Vector3 frontEdgeLocal = new Vector3(
                    floorCenter.x + frontLocal.x * frontHalfDepth, 0f,
                    floorCenter.y + frontLocal.y * frontHalfDepth);
                pizzariaPos = streetEdge - (pizzariaRot * frontEdgeLocal);

                if (Application.isPlaying) Destroy(bestBuilding.gameObject);
                else DestroyImmediate(bestBuilding.gameObject);

                // â”€â”€ Carve out space for the Pizzeria (tamanho real do Floor) â”€â”€
                Vector3 floorWorldCenter = pizzariaPos + pizzariaRot * new Vector3(floorCenter.x, 0f, floorCenter.y);
                Vector3 rotatedSize = pizzariaRot * new Vector3(floorSize.x, 10f, floorSize.y);
                Bounds pizzeriaBounds = new Bounds(floorWorldCenter,
                    new Vector3(Mathf.Abs(rotatedSize.x), 10f, Mathf.Abs(rotatedSize.z)));
                pizzeriaBounds.Expand(0.5f); // Leave a small gap to neighbors

                CityBuilding[] remainingBuildings = blockTransform.GetComponentsInChildren<CityBuilding>();
                foreach (var b in remainingBuildings)
                {
                    if (b == null) continue;
                    Renderer bRend = b.GetComponent<Renderer>();
                    Bounds bBounds = bRend != null ? bRend.bounds : new Bounds(b.transform.position, b.transform.localScale);
                    
                    if (pizzeriaBounds.Intersects(bBounds))
                    {
                        if (Application.isPlaying) Destroy(b.gameObject);
                        else DestroyImmediate(b.gameObject);
                    }
                }
            }
        }

        Transform parentTransform = blockTransform != null ? blockTransform : _blocksRoot.transform;

        if (pizzeriaPrefab != null)
        {
            var pObj = ScaryParty.Pizzeria.Integration.CityPizzeriaPlacementAdapter.SpawnPizzeria(
                pizzeriaPrefab, pizzariaPos, pizzariaRot, parentTransform, CityData, config.sidewalkHeight, frontDistance, entranceDir);

            if (pObj != null)
            {
                var pRoot = pObj.GetComponent<ScaryParty.Pizzeria.Composition.PizzeriaRoot>();
                for (int i = 0; i < 4; i++)
                {
                    GameObject sp = new GameObject($"NetworkSpawnPoint_{i}");
                    sp.transform.SetParent(pObj.transform);
                    if (pRoot != null)
                    {
                        sp.transform.position = pRoot.GetPlayerSpawnPosition(i);
                        sp.transform.rotation = pRoot.GetPlayerSpawnRotation(i);
                    }
                    else
                    {
                        sp.transform.position = pizzariaPos + Vector3.up * 0.1f;
                        sp.transform.rotation = pizzariaRot;
                    }
                }

                GameObject spLegacy = new GameObject("NetworkSpawnPoint");
                spLegacy.transform.SetParent(pObj.transform);
                if (pRoot != null)
                {
                    spLegacy.transform.position = pRoot.GetPlayerSpawnPosition(0);
                    spLegacy.transform.rotation = pRoot.GetPlayerSpawnRotation(0);
                }
                else
                {
                    spLegacy.transform.position = pizzariaPos + Vector3.up * 0.1f;
                    spLegacy.transform.rotation = pizzariaRot;
                }
            }
            return;
        }

        float actualGroundLevel = 0.02f + (config != null ? config.sidewalkHeight : 0.15f);
        pizzariaPos.y = actualGroundLevel + (pizzariaScale.y * 0.5f);
        CityData.pizzariaPosition = pizzariaPos;

        GameObject pizzariaBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pizzariaBuilding.name = "The_Pizzaria_Building";
        pizzariaBuilding.transform.position = pizzariaPos;
        pizzariaBuilding.transform.rotation = pizzariaRot;
        pizzariaBuilding.transform.localScale = pizzariaScale;
        pizzariaBuilding.transform.SetParent(parentTransform);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.8f, 0.1f, 0.1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
        pizzariaBuilding.GetComponent<Renderer>().sharedMaterial = mat;

        // Bancada outside the pizzaria, on the sidewalk!
        float offsetToSidewalk = (pizzariaScale.z * 0.5f) + 1.5f;
        Vector3 bancadaPos = pizzariaPos + entranceDir * offsetToSidewalk;
        bancadaPos.y = actualGroundLevel + 0.5f;
        
        GameObject bancada = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bancada.name = "Bancada_Pizzas";
        bancada.transform.position = bancadaPos;
        bancada.transform.rotation = pizzariaRot;
        bancada.transform.localScale = new Vector3(4f, 1f, 1.5f);
        bancada.transform.SetParent(blockTransform != null ? blockTransform : _blocksRoot.transform);

        Material bancadaMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        bancadaMat.color = new Color(0.4f, 0.2f, 0.1f);
        if (bancadaMat.HasProperty("_BaseColor")) bancadaMat.SetColor("_BaseColor", bancadaMat.color);
        bancada.GetComponent<Renderer>().sharedMaterial = bancadaMat;

        CityData.bancadaPosition = bancadaPos + new Vector3(0, 0.5f, 0);

        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Pizzaria_Sign";
        sign.transform.SetParent(pizzariaBuilding.transform);
        sign.transform.localPosition = new Vector3(0, 0.6f, 0.5f);
        sign.transform.localScale = new Vector3(0.8f, 0.2f, 0.1f);
        Material signMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        signMat.color = Color.yellow;
        if (signMat.HasProperty("_BaseColor")) signMat.SetColor("_BaseColor", signMat.color);
        sign.GetComponent<Renderer>().sharedMaterial = signMat;
        sign.GetComponent<Collider>().enabled = false;

        Vector3 rightDir = Vector3.Cross(Vector3.up, -entranceDir).normalized;
        float spacing = 2.0f;
        float spawnStreetOffset = Mathf.Max(config.streetWidth, 8f);

        for (int i = 0; i < 4; i++)
        {
            GameObject spawnPoint = new GameObject($"NetworkSpawnPoint_{i}");
            spawnPoint.transform.SetParent(pizzariaBuilding.transform);
            
            float offsetAmount = (i - 1.5f) * spacing;
            Vector3 spawnPos = bancadaPos + entranceDir * spawnStreetOffset + rightDir * offsetAmount + Vector3.up * 0.1f;
            
            spawnPoint.transform.position = spawnPos;
            spawnPoint.transform.rotation = Quaternion.LookRotation(-entranceDir);
        }
    }

    /// <summary>
    /// LÃª as dimensÃµes da pizzaria. Prioriza o componente PizzeriaFootprint com geometria explÃ­cita;
    /// se ausente, faz fallback para o objeto "Floor" do prefab. Fallback geral: 12x12 centrado.
    /// </summary>
    private void GetPizzeriaFootprint(out Vector2 size, out Vector2 center, out Vector2 frontLocal)
    {
        size = new Vector2(12f, 12f);
        center = Vector2.zero;
        frontLocal = new Vector2(0f, -1f); // fallback: frente em -Z
        if (pizzeriaPrefab == null) return;

        // Se houver PizzeriaFootprint explÃ­cito no prefab, usar metadados de alta fidelidade
        var footprint = pizzeriaPrefab.GetComponent<ScaryParty.Pizzeria.Composition.PizzeriaFootprint>();
        if (footprint != null)
        {
            size = footprint.footprintSize;
            center = footprint.footprintCenter;
            frontLocal = footprint.entranceDirectionLocal;
            return;
        }

        Transform floor = null;
        foreach (Transform t in pizzeriaPrefab.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Floor") { floor = t; break; }
        }
        if (floor == null) return;

        MeshFilter mf = floor.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        // Matriz do Floor relativa Ã  raiz do prefab
        Matrix4x4 m = pizzeriaPrefab.transform.worldToLocalMatrix * floor.localToWorldMatrix;
        Bounds mb = mf.sharedMesh.bounds;
        Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, 0, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = m.MultiplyPoint3x4(corner);
            min.x = Mathf.Min(min.x, p.x); max.x = Mathf.Max(max.x, p.x);
            min.z = Mathf.Min(min.z, p.z); max.z = Mathf.Max(max.z, p.z);
        }
        size = new Vector2(max.x - min.x, max.z - min.z);
        center = new Vector2((max.x + min.x) * 0.5f, (max.z + min.z) * 0.5f);

        // Frente = direÃ§Ã£o do centro do Floor atÃ© o "EntranceAnchor" (snap cardinal).
        foreach (Transform t in pizzeriaPrefab.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "EntranceAnchor") continue;
            Vector3 a = pizzeriaPrefab.transform.worldToLocalMatrix.MultiplyPoint3x4(t.position);
            Vector2 d = new Vector2(a.x, a.z) - center;
            if (d.sqrMagnitude > 0.0001f)
            {
                frontLocal = Mathf.Abs(d.x) > Mathf.Abs(d.y)
                    ? new Vector2(Mathf.Sign(d.x), 0f)
                    : new Vector2(0f, Mathf.Sign(d.y));
            }
            break;
        }
    }

    private int FindBestPizzariaBlock()
    {
        if (CityData == null || CityData.blocks == null || CityData.blocks.Count == 0) return -1;

        GetPizzeriaFootprint(out Vector2 reqSize, out _, out _);
        float minRequiredDim = Mathf.Min(reqSize.x, reqSize.y);
        float requiredArea = (reqSize.x * reqSize.y) * 0.65f;

        Vector3 gridCenter = Vector3.zero; // Origem orgÃ¢nica Ã© 0,0,0
        int bestIndex = -1;
        float bestScore = float.MaxValue;

        for (int i = 0; i < CityData.blocks.Count; i++)
        {
            BlockInfo block = CityData.blocks[i];
            float distToCenter = Vector3.Distance(block.worldCenter, gridCenter);

            float zoneBonus = 0f;
            switch (block.zoneType)
            {
                case ZoneType.Commercial: zoneBonus = 0f; break;
                case ZoneType.Residential: zoneBonus = 20f; break;
                case ZoneType.Industrial: zoneBonus = 50f; break;
                case ZoneType.MonsterZone: zoneBonus = 100f; break;
            }

            // Penalizar quadras menores do que o footprint necessÃ¡rio para o prÃ©dio da pizzaria
            float sizePenalty = 0f;
            float maxBlockDim = Mathf.Max(block.size.x, block.size.z);
            if (maxBlockDim < minRequiredDim || (block.area > 0 && block.area < requiredArea))
            {
                sizePenalty = 500f;
            }

            float score = distToCenter + zoneBonus + sizePenalty;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private void GeneratePizzariaLegacy()
    {
        Vector3 pos = CityData.pizzariaPosition;
        Transform blocksRoot = _blocksRoot.transform;

        if (pizzeriaPrefab != null)
        {
            ScaryParty.Pizzeria.Integration.CityPizzeriaPlacementAdapter.SpawnPizzeria(
                pizzeriaPrefab, pos, Quaternion.identity, blocksRoot, CityData, config.sidewalkHeight);
            return;
        }

        GameObject pizzariaBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pizzariaBuilding.name = "The_Pizzaria_Building";
        pizzariaBuilding.transform.position = pos + new Vector3(0, 3f, 0);
        pizzariaBuilding.transform.localScale = new Vector3(15f, 6f, 15f);
        pizzariaBuilding.transform.SetParent(blocksRoot);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.8f, 0.1f, 0.1f);
        pizzariaBuilding.GetComponent<Renderer>().sharedMaterial = mat;

        Vector3 bancadaPos = pos + new Vector3(0, config.sidewalkHeight + 0.5f, -8.5f);
        GameObject bancada = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bancada.name = "Bancada_Pizzas";
        bancada.transform.position = bancadaPos;
        bancada.transform.localScale = new Vector3(3f, 1f, 1.5f);
        bancada.transform.SetParent(blocksRoot);

        Material bancadaMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        bancadaMat.color = new Color(0.4f, 0.2f, 0.1f);
        bancada.GetComponent<Renderer>().sharedMaterial = bancadaMat;

        CityData.bancadaPosition = bancadaPos + new Vector3(0, 0.5f, 0);

        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Pizzaria_Sign";
        sign.transform.SetParent(pizzariaBuilding.transform);
        sign.transform.localPosition = new Vector3(0, 0.6f, 0.5f);
        sign.transform.localScale = new Vector3(0.8f, 0.2f, 0.1f);
        Material signMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        signMat.color = Color.yellow;
        sign.GetComponent<Renderer>().sharedMaterial = signMat;
        sign.GetComponent<Collider>().enabled = false;

        float legacyStreetOffset = Mathf.Max(config.streetWidth, 8f);
        float spacing = 2.0f;

        for (int i = 0; i < 4; i++)
        {
            GameObject spawnPoint = new GameObject($"NetworkSpawnPoint_{i}");
            spawnPoint.transform.SetParent(pizzariaBuilding.transform);
            float offsetAmount = (i - 1.5f) * spacing;
            spawnPoint.transform.position = pos + new Vector3(offsetAmount, 0.1f, -(8.5f + legacyStreetOffset));
            spawnPoint.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
    }

    private void CreateHierarchy()
    {
        _cityRoot = new GameObject("__City__");
        _streetsRoot = new GameObject("Streets");
        _blocksRoot = new GameObject("Blocks");
        _intersectionsRoot = new GameObject("Intersections");
        _deliveryPointsRoot = new GameObject("DeliveryPoints");
        _trafficRoot = new GameObject("Traffic");
        _eventsRoot = new GameObject("Events");
        _markingsRoot = new GameObject("Markings");
        _crosswalksRoot = new GameObject("Crosswalks");
        _pedestriansRoot = new GameObject("Pedestrians");

        _markingsRoot.transform.SetParent(_cityRoot.transform);
        _crosswalksRoot.transform.SetParent(_cityRoot.transform);
        _pedestriansRoot.transform.SetParent(_cityRoot.transform);

        _streetsRoot.transform.SetParent(_cityRoot.transform);
        _blocksRoot.transform.SetParent(_cityRoot.transform);
        _intersectionsRoot.transform.SetParent(_cityRoot.transform);
        _deliveryPointsRoot.transform.SetParent(_cityRoot.transform);
        _trafficRoot.transform.SetParent(_cityRoot.transform);
        _eventsRoot.transform.SetParent(_cityRoot.transform);
    }

    private void EnsureSubGenerators()
    {
        _streetGridGen = _cityRoot.AddComponent<StreetGridGenerator>();
        _blockFiller = _cityRoot.AddComponent<BlockFiller>();
        _intersectionBuilder = _cityRoot.AddComponent<IntersectionBuilder>();
        _deliveryPointPlacer = _cityRoot.AddComponent<DeliveryPointPlacer>();
        _pathfinder = _cityRoot.AddComponent<CityGraphPathfinder>();
        _enemySpawnGen = _cityRoot.AddComponent<EnemySpawnPointGenerator>();
        _trafficManager = _cityRoot.AddComponent<TrafficManager>();
        _eventManager = _cityRoot.AddComponent<CityEventManager>();
    }

    private Vector3[] CleanPolygon(Vector3[] poly)
    {
        if (poly == null || poly.Length < 3) return poly;
        
        List<Vector3> distanceCleaned = new List<Vector3>();
        distanceCleaned.Add(poly[0]);
        for (int i = 1; i < poly.Length; i++)
        {
            // Apenas remove arestas microscÃ³picas (< 1 metro) que quebram o cÃ¡lculo da normal.
            // NÃ£o remove segmentos curtos vÃ¡lidos (zig-zags).
            if (Vector3.Distance(distanceCleaned[distanceCleaned.Count - 1], poly[i]) > 1f)
            {
                distanceCleaned.Add(poly[i]);
            }
        }
        
        if (distanceCleaned.Count > 2 && Vector3.Distance(distanceCleaned[distanceCleaned.Count - 1], distanceCleaned[0]) <= 1f)
        {
            distanceCleaned.RemoveAt(distanceCleaned.Count - 1);
        }

        if (distanceCleaned.Count < 3) return distanceCleaned.ToArray();
        
        List<Vector3> finalCleaned = new List<Vector3>();
        int n = distanceCleaned.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = distanceCleaned[(i - 1 + n) % n];
            Vector3 curr = distanceCleaned[i];
            Vector3 next = distanceCleaned[(i + 1) % n];

            Vector3 dir1 = (curr - prev).normalized;
            Vector3 dir2 = (next - curr).normalized;

            float angle = Vector3.Angle(dir1, dir2);
            // TolerÃ¢ncia de 5 graus:
            // - Ã‰ o suficiente para fundir T-intersections ligeiramente tortos (que criavam buracos no meio da rua).
            // - Preserva curvas reais (que normalmente tÃªm Ã¢ngulos de 10 a 15 graus por segmento).
            if (angle > 5f) 
            {
                finalCleaned.Add(curr);
            }
        }
        
        return finalCleaned.ToArray();
    }

    private void GenerateBlocks(System.Random rng, List<Vector3[]> blockPolygons)
    {
        GenerateBlocks(rng, blockPolygons, out _);
    }

    private void GenerateBlocks(System.Random rng, List<Vector3[]> blockPolygons, out ScaryParty.City.PlacementResult placementResult)
    {
        placementResult = null;
        CityGenLogger.StartLog();
        CityData.blocks = new List<BlockInfo>();

        // PASS 1: Generate all BlockInfo geometry
        foreach (Vector3[] rawPoly in blockPolygons)
        {
            Vector3[] poly = PolygonUtils.CleanPolygon(rawPoly, 0.05f, 0.5f);
            if (poly.Length < 3) continue;

            // blockPolygons from UnifiedRoadMesher already match the exact sidewalk inner boundary
            Vector3[] insetPoly = poly;
            
            float insetArea = CalculatePolygonArea(insetPoly);
            if (Mathf.Abs(insetArea) < 100f) continue;
            
            float area = CalculatePolygonArea(insetPoly);
            float minViableArea = (config.minBuildingWidth + config.blockCornerMargin * 2f) 
                                * (config.minBuildingDepth * 2f + config.buildingGap);
            minViableArea = Mathf.Max(minViableArea, 50f);
            if (area < minViableArea) continue;

            Vector3 centerPos = CalculatePolygonCentroid(insetPoly);
            Vector3 size = CalculatePolygonExtents(insetPoly);
            
            float minDimension = Mathf.Min(size.x, size.z);
            if (minDimension < config.minBuildingDepth * 2f) continue;

            float perimeter = 0f;
            for (int i = 0; i < insetPoly.Length; i++) {
                perimeter += Vector3.Distance(insetPoly[i], insetPoly[(i + 1) % insetPoly.Length]);
            }
            if (perimeter > 0f) {
                float thinness = (4f * Mathf.PI * area) / (perimeter * perimeter);
                if (thinness < 0.1f) continue;
            }

            float maxAbsDist = Mathf.Max(Mathf.Abs(centerPos.x), Mathf.Abs(centerPos.z));
            float chunkSize = config.maxStreetBranchLength;
            
            ZoneType zone;
            if (maxAbsDist <= chunkSize * 0.5f) {
                zone = ZoneType.Commercial;
            } else if (maxAbsDist <= chunkSize * 1.5f) {
                zone = ZoneType.Residential;
            } else if (maxAbsDist <= chunkSize * 2.5f) {
                zone = ZoneType.Industrial;
            } else {
                zone = ZoneType.MonsterZone;
            }

            BlockInfo block = new BlockInfo
            {
                worldCenter = centerPos,
                size = size,
                zoneType = zone,
                deliveryPointIndices = new List<int>(),
                polygon = insetPoly,
                area = area,
                hasPizzaria = false
            };

            CityData.blocks.Add(block);
        }

        // Identify and reserve Pizzeria block before filling buildings
        if (config.pizzariaInsideBlock && CityData.blocks.Count > 0)
        {
            placementResult = ScaryParty.City.PizzeriaPlacementSolver.Solve(CityData.blocks, pizzeriaPrefab, config, rng);
            if (placementResult != null && placementResult.success)
            {
                BlockInfo pBlock = CityData.blocks[placementResult.blockIndex];
                pBlock.hasPizzaria = true;
                CityData.blocks[placementResult.blockIndex] = pBlock;
                CityData.pizzariaBlockIndex = placementResult.blockIndex;
                CityData.pizzariaPosition = placementResult.position;
                CityData.pizzariaRotation = placementResult.rotation;
                CityData.bancadaPosition = placementResult.bancadaPosition;
                CityData.hasPizzariaReserved = true;
                Debug.Log($"[CityGenerator] âœ… Pizzaria reservada com sucesso na quadra {placementResult.blockIndex}!");
            }
            else
            {
                // Fallback legado de Ã­ndice se o solver nÃ£o encontrar quadra
                int bestBlock = FindBestPizzariaBlock();
                if (bestBlock >= 0)
                {
                    BlockInfo pBlock = CityData.blocks[bestBlock];
                    pBlock.hasPizzaria = true;
                    CityData.blocks[bestBlock] = pBlock;
                    CityData.pizzariaBlockIndex = bestBlock;
                }
            }
        }

        // PASS 2: Fill Blocks (respeitando reservas da pizzaria)
        for (int i = 0; i < CityData.blocks.Count; i++)
        {
            BlockInfo block = CityData.blocks[i];
            CityGenLogger.StartBlock(i, block.area, block.polygon.Length);
            var reserved = (placementResult != null && placementResult.success && i == placementResult.blockIndex)
                ? placementResult.reservedOBBs
                : null;
            _blockFiller.FillBlock(block, config, rng, _blocksRoot.transform, buildingMaterials, i, reserved, sidewalkMaterial);
        }
        
        CityGenLogger.SaveLog();
    }

    private Vector3[] InsetPolygon(Vector3[] poly, float inset)
    {
        return PolygonUtils.InsetPolygonSafe(poly, inset);
    }

    private Vector3 CalculatePolygonCentroid(Vector3[] polygon)
    {
        Vector3 center = Vector3.zero;
        if (polygon == null || polygon.Length == 0) return center;
        foreach (var p in polygon) center += p;
        return center / polygon.Length;
    }

    private float CalculatePolygonArea(Vector3[] polygon)
    {
        if (polygon == null || polygon.Length < 3) return 0f;
        float area = 0f;
        int n = polygon.Length;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += polygon[i].x * polygon[j].z;
            area -= polygon[j].x * polygon[i].z;
        }
        return Mathf.Abs(area) * 0.5f;
    }

    private Vector3 CalculatePolygonExtents(Vector3[] polygon)
    {
        if (polygon == null || polygon.Length == 0) return Vector3.zero;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var p in polygon)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }
        return new Vector3(maxX - minX, 0f, maxZ - minZ);
    }

    private void OnDrawGizmosSelected()
    {
        if (CityData == null || config == null || CityData.blocks == null) return;
        
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        foreach (var b in CityData.blocks)
        {
            Gizmos.DrawWireCube(b.worldCenter, b.size);
        }
    }
}

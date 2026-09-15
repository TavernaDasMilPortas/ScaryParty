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

    [Header("Materials — Streets")]
    public Material streetMaterial;
    public Material sidewalkMaterial;
    public Material intersectionMaterial;

    [Header("Materials — Buildings")]
    public Material[] buildingMaterials;

    [SerializeField, HideInInspector]
    private CityData _cityData;

    public CityData CityData 
    { 
        get => _cityData; 
        private set => _cityData = value; 
    }

    public int CurrentSeed { get; private set; }

    /// <summary>
    /// Seed persistida via rede. Qualquer cliente que entrar na sessão (inclusive late-joiners)
    /// lerá este valor no OnNetworkSpawn e gerará a cidade localmente com a mesma seed.
    /// </summary>
    private NetworkVariable<int> _networkSeed = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event Action OnCityGenerated;
    public event Action OnCityCleared;

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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (config == null) return;

            // Gera uma seed e persiste na NetworkVariable ANTES de gerar localmente.
            // Isso garante que qualquer cliente que entrar depois (late-join) consiga ler
            // a seed e gerar a cidade localmente sem precisar do ClientRpc.
            int useSeed = config.seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : config.seed;
            _networkSeed.Value = useSeed;

            DestroyStaticCity();
            GenerateLocally(useSeed);
            // ClientRpc é um fast-path para clientes já conectados no momento do spawn.
            ClientGenerateCityClientRpc(useSeed);
        }
        else
        {
            // Registra listener ANTES de verificar o valor atual.
            // Ordem importa: se o server ainda não setou a seed, o OnValueChanged vai disparar quando setar.
            _networkSeed.OnValueChanged += OnNetworkSeedChanged;

            DestroyStaticCity();

            // Late-join guard: se a seed já foi setada pelo servidor antes de entrarmos,
            // o OnValueChanged NÃO vai disparar (o valor não vai mudar para nós).
            // Por isso verificamos o valor atual agora e geramos se necessário.
            if (_networkSeed.Value != 0)
            {
                Debug.Log($"[CityGenerator] Late-join detectado — gerando cidade com seed {_networkSeed.Value}");
                GenerateLocally(_networkSeed.Value);
            }
            else
            {
                Debug.Log("[CityGenerator] Cliente conectou antes do servidor gerar — aguardando OnValueChanged.");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            _networkSeed.OnValueChanged -= OnNetworkSeedChanged;
        }
    }

    /// <summary>
    /// Chamado no cliente quando o servidor seta a seed pela primeira vez.
    /// Só é relevante para clientes que estavam conectados antes do servidor gerar.
    /// Clientes que entraram depois (late-join) já leram o valor diretamente no OnNetworkSpawn.
    /// </summary>
    private void OnNetworkSeedChanged(int previousValue, int newValue)
    {
        if (IsServer) return;
        if (newValue == 0) return;

        // Evita gerar duas vezes (o ClientRpc também pode ser recebido logo depois)
        if (CityData != null)
        {
            Debug.Log("[CityGenerator] OnNetworkSeedChanged: cidade já gerada via late-join, ignorando.");
            return;
        }

        Debug.Log($"[CityGenerator] Seed recebida via NetworkVariable ({newValue}) — gerando cidade.");
        DestroyStaticCity();
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

    private void DestroyStaticCity()
    {
        GameObject staticCity = GameObject.Find("__City__");
        if (staticCity != null) Destroy(staticCity);
    }

    [ContextMenu("Generate City")]
    public void GenerateCity()
    {
        if (config == null) return;
        ClearCity();

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
        if (_cityRoot != null)
        {
            if (Application.isPlaying) Destroy(_cityRoot);
            else DestroyImmediate(_cityRoot);
        }

        GameObject oldCity = GameObject.Find("__City__");
        if (oldCity != null)
        {
            if (Application.isPlaying) Destroy(oldCity);
            else DestroyImmediate(oldCity);
        }

        CityData = null;
        _cityRoot = _streetsRoot = _blocksRoot = _intersectionsRoot = _deliveryPointsRoot = _trafficRoot = _eventsRoot = null;
        _trafficManager = null;
        _eventManager = null;

        OnCityCleared?.Invoke();
    }

    [ContextMenu("Regenerate City")]
    public void RegenerateCity()
    {
        ClearCity();
        GenerateCity();
    }

    public CityGraphPathfinder GetPathfinder() => _pathfinder;
    public TrafficManager GetTrafficManager() => _trafficManager;
    public CityEventManager GetEventManager() => _eventManager;

    [ClientRpc]
    private void ClientGenerateCityClientRpc(int seed)
    {
        if (IsServer) return;

        // Evita gerar duas vezes se o cliente já gerou via NetworkVariable (late-join)
        if (CityData != null)
        {
            Debug.Log("[CityGenerator] ClientRpc recebido mas cidade já existe (late-join). Ignorando.");
            return;
        }

        Debug.Log($"[CityGenerator] ClientRpc recebido — gerando cidade com seed {seed}.");
        GenerateLocally(seed);
    }

    private void GenerateLocally(int seed)
    {
        CurrentSeed = seed;
        System.Random rng = new System.Random(seed);

        CreateHierarchy();
        EnsureSubGenerators();

        CityData = ScriptableObject.CreateInstance<CityData>();
        CityData.Initialize(config.gridWidth, config.gridHeight, config.maxStreetBranchLength, config.maxStreetBranchLength, config.streetWidth);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

            string assetPath = "Assets/ScriptableObjects/GeneratedCityData.asset";
            CityData existingData = UnityEditor.AssetDatabase.LoadAssetAtPath<CityData>(assetPath);
            if (existingData != null) UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            
            UnityEditor.AssetDatabase.CreateAsset(CityData, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            _cityData = CityData;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        StreetGraph graph = _streetGridGen.Generate(
            config, rng, _streetsRoot.transform,
            streetMaterial, sidewalkMaterial, buildingMaterials, out List<Vector3[]> blockPolygons
        );
        CityData.streetGraph = graph;

        GenerateBlocks(rng, blockPolygons);

        _intersectionBuilder.BuildIntersections(graph, config, rng, _intersectionsRoot.transform, intersectionMaterial);

        CityBuilding[] allBuildings = _blocksRoot.GetComponentsInChildren<CityBuilding>();
        List<DeliveryPoint> points = _deliveryPointPlacer.PlaceDeliveryPoints(CityData.blocks.ToArray(), allBuildings, config, rng, _deliveryPointsRoot.transform);
        CityData.DeliveryPointCount = points.Count;

        _pathfinder.Initialize(graph);

        MinimapRouteManager routeManager = FindObjectOfType<MinimapRouteManager>();
        if (routeManager != null) routeManager.Initialize(_pathfinder);

        if (config.pizzariaInsideBlock) PlacePizzariaInBlock(rng);
        else
        {
            CityData.pizzariaPosition = Vector3.zero;
            GeneratePizzariaLegacy();
        }

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
    }

    private void InitializeRuntimeSystems(int seed, StreetGraph graph)
    {
        if (config.maxTrafficVehicles > 0)
        {
            _trafficManager.Initialize(_pathfinder, graph, seed + 1000, config.maxTrafficVehicles, config.trafficBaseSpeed);
        }
        _eventManager.Initialize(_pathfinder, graph, _trafficManager, config, seed + 2000, _eventsRoot.transform);
    }

    private void TeleportPlayersToSpawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var po = NetworkManager.Singleton.LocalClient.PlayerObject;
                var cc = po.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                int playerIndex = 0;
                var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
                System.Array.Sort(allPlayers, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
                
                for (int i = 0; i < allPlayers.Length; i++)
                {
                    if (allPlayers[i].OwnerClientId == NetworkManager.Singleton.LocalClientId)
                    {
                        playerIndex = i;
                        break;
                    }
                }
                
                GameObject spawnPoint = GameObject.Find($"NetworkSpawnPoint_{playerIndex % 4}");
                if (spawnPoint == null) spawnPoint = GameObject.Find("NetworkSpawnPoint_0");
                
                if (spawnPoint != null)
                {
                    po.transform.position = spawnPoint.transform.position;
                    po.transform.rotation = spawnPoint.transform.rotation;
                }
                
                if (cc != null) cc.enabled = true;
            }
        }
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

        // ── Pick the building closest to the block edge (= closest to the street) ──
        if (blockTransform != null)
        {
            Vector3 blockCenter = pBlock.worldCenter;
            CityBuilding bestBuilding = null;
            float bestEdgeDist = float.MaxValue;

            foreach (Transform child in blockTransform)
            {
                CityBuilding building = child.GetComponent<CityBuilding>();
                if (building == null) continue;

                // Score: distance from block center → farther = closer to street edge.
                // We also need the building to be big enough to be replaced by the
                // pizzeria (12 m wide). Accept buildings ≥ 6 m wide.
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

            // Fallback: if nothing was found with footprint ≥ 6, just pick the largest
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
                // ── Calculate street-snapped position ──
                Vector3 buildingPos = bestBuilding.transform.position;

                // Direction from block center outward (toward the street)
                entranceDir = (buildingPos - blockCenter);
                entranceDir.y = 0;
                if (entranceDir.sqrMagnitude < 0.01f) entranceDir = Vector3.forward;
                entranceDir.Normalize();

                // Snap to the nearest cardinal axis so the 12 × 12 box aligns cleanly
                if (Mathf.Abs(entranceDir.x) > Mathf.Abs(entranceDir.z))
                    entranceDir = new Vector3(Mathf.Sign(entranceDir.x), 0, 0);
                else
                    entranceDir = new Vector3(0, 0, Mathf.Sign(entranceDir.z));

                // Rotation: the prefab door is at local Z = −6 (the −Z face).
                // LookRotation(-entranceDir) makes +Z point inward and −Z face the street.
                pizzariaRot = Quaternion.LookRotation(-entranceDir, Vector3.up);

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

                // Pizzeria center is 6 m inward from the street edge so the door
                // wall (local Z = −6) sits flush with the sidewalk.
                const float pizzeriaHalfDepth = 6f;
                pizzariaPos = streetEdge - entranceDir * pizzeriaHalfDepth;

                if (Application.isPlaying) Destroy(bestBuilding.gameObject);
                else DestroyImmediate(bestBuilding.gameObject);

                // ── Carve out space for the 12x12 Pizzeria ──
                Bounds pizzeriaBounds = new Bounds(pizzariaPos, new Vector3(12f, 10f, 12f));
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
            ScaryParty.Pizzeria.Integration.CityPizzeriaPlacementAdapter.SpawnPizzeria(
                pizzeriaPrefab, pizzariaPos, pizzariaRot, parentTransform, CityData, config.sidewalkHeight);
            return;
        }

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
        bancadaPos.y = config.sidewalkHeight + 0.5f;
        
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

    private int FindBestPizzariaBlock()
    {
        if (CityData == null || CityData.blocks == null || CityData.blocks.Count == 0) return -1;

        Vector3 gridCenter = Vector3.zero; // Origem orgânica é 0,0,0
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

            float score = distToCenter + zoneBonus;
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
            // Apenas remove arestas microscópicas (< 1 metro) que quebram o cálculo da normal.
            // Não remove segmentos curtos válidos (zig-zags).
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
            // Tolerância de 5 graus:
            // - É o suficiente para fundir T-intersections ligeiramente tortos (que criavam buracos no meio da rua).
            // - Preserva curvas reais (que normalmente têm ângulos de 10 a 15 graus por segmento).
            if (angle > 5f) 
            {
                finalCleaned.Add(curr);
            }
        }
        
        return finalCleaned.ToArray();
    }

    private void GenerateBlocks(System.Random rng, List<Vector3[]> blockPolygons)
    {
        CityGenLogger.StartLog();
        CityData.blocks = new List<BlockInfo>();

        // PASS 1: Generate all BlockInfo geometry
        foreach (Vector3[] rawPoly in blockPolygons)
        {
            Vector3[] poly = CleanPolygon(rawPoly);
            if (poly.Length < 3) continue;

            float totalInset = (config.streetWidth * 0.5f) + config.sidewalkWidth;
            Vector3[] insetPoly = InsetPolygon(poly, totalInset);
            
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

        // Identify Pizzeria block before filling
        if (config.pizzariaInsideBlock && CityData.blocks.Count > 0)
        {
            int bestBlock = FindBestPizzariaBlock();
            if (bestBlock >= 0)
            {
                BlockInfo pBlock = CityData.blocks[bestBlock];
                pBlock.hasPizzaria = true;
                CityData.blocks[bestBlock] = pBlock;
                CityData.pizzariaBlockIndex = bestBlock;
            }
        }

        // PASS 2: Fill Blocks
        for (int i = 0; i < CityData.blocks.Count; i++)
        {
            BlockInfo block = CityData.blocks[i];
            CityGenLogger.StartBlock(i, block.area, block.polygon.Length);
            _blockFiller.FillBlock(block, config, rng, _blocksRoot.transform, buildingMaterials, i);
        }
        
        CityGenLogger.SaveLog();
    }

    private Vector3[] InsetPolygon(Vector3[] poly, float inset)
    {
        int n = poly.Length;
        if (n < 3) return poly;

        // 1. Determine winding order (CW vs CCW) using signed area
        float signedArea = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            signedArea += poly[i].x * poly[j].z - poly[j].x * poly[i].z;
        }
        // sign > 0 means CCW in XZ plane; we want inward normals
        float windingSign = Mathf.Sign(signedArea);

        // 2. Compute inward normal for each edge
        Vector3[] edgeNormals = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 edgeDir = (poly[j] - poly[i]);
            edgeDir.y = 0;
            edgeDir.Normalize();
            // Inward normal depends on winding direction
            // For CCW (signedArea > 0): inward = (-edgeDir.z, 0, edgeDir.x)
            // For CW  (signedArea < 0): inward = (edgeDir.z, 0, -edgeDir.x)
            edgeNormals[i] = new Vector3(-edgeDir.z * windingSign, 0, edgeDir.x * windingSign);
        }

        // 3. Compute miter vector for each vertex
        Vector3[] insetPoly = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int prevEdge = (i - 1 + n) % n;
            int currEdge = i;

            Vector3 n1 = edgeNormals[prevEdge];
            Vector3 n2 = edgeNormals[currEdge];
            Vector3 miter = (n1 + n2);
            miter.y = 0;

            float miterSqr = miter.sqrMagnitude;
            if (miterSqr < 0.001f)
            {
                // Edges are nearly parallel, just offset by normal
                insetPoly[i] = poly[i] + n1 * inset;
            }
            else
            {
                // Miter length = inset / dot(miter_normalized, normal)
                miter.Normalize();
                float dot = Vector3.Dot(miter, n1);
                if (Mathf.Abs(dot) < 0.1f) dot = 0.1f * Mathf.Sign(dot); // Clamp for very acute angles
                float miterLength = inset / dot;
                // Cap miter length to avoid spikes on very acute angles
                miterLength = Mathf.Min(miterLength, inset * 3f);
                insetPoly[i] = poly[i] + miter * miterLength;
            }
        }

        return insetPoly;
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

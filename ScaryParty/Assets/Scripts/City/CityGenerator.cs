using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

/// <summary>
/// Main orchestrator for procedural city generation.
/// Place on a GameObject in the scene. Coordinates all sub-generators.
/// In networked sessions, the host generates seed and clients generate locally.
/// Requires a NetworkObject component on the same GameObject.
/// </summary>
public class CityGenerator : NetworkBehaviour
{
    [Header("Configuration")]
    [Tooltip("The city configuration asset")]
    public CityConfig config;

    [Header("Materials — Streets")]
    [Tooltip("Material for road surfaces")]
    public Material streetMaterial;

    [Tooltip("Material for sidewalks")]
    public Material sidewalkMaterial;

    [Tooltip("Material for intersections")]
    public Material intersectionMaterial;

    [Header("Materials — Buildings")]
    [Tooltip("Materials used for buildings (selected randomly per building)")]
    public Material[] buildingMaterials;

    // ─────────────────────────────────────────────────────────────────────
    // Runtime State
    // ─────────────────────────────────────────────────────────────────────

    [SerializeField, HideInInspector]
    private CityData _cityData;

    /// <summary>
    /// The generated city data. Null if no city has been generated.
    /// </summary>
    public CityData CityData 
    { 
        get => _cityData; 
        private set => _cityData = value; 
    }

    /// <summary>
    /// The seed used for the current generation.
    /// </summary>
    public int CurrentSeed { get; private set; }

    // Events
    public event Action OnCityGenerated;
    public event Action OnCityCleared;

    // Sub-generators (created at runtime)
    private StreetGridGenerator _streetGridGen;
    private BlockFiller _blockFiller;
    private IntersectionBuilder _intersectionBuilder;
    private DeliveryPointPlacer _deliveryPointPlacer;
    private CityGraphPathfinder _pathfinder;

    // Hierarchy roots
    private GameObject _cityRoot;
    private GameObject _streetsRoot;
    private GameObject _blocksRoot;
    private GameObject _intersectionsRoot;
    private GameObject _deliveryPointsRoot;

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (config == null)
            {
                Debug.LogError("[CityGenerator] OnNetworkSpawn: Missing CityConfig! Assign one in the Inspector.");
                return;
            }
            int useSeed = config.seed;
            if (useSeed == 0) useSeed = UnityEngine.Random.Range(1, int.MaxValue);
            
            // Destroy the editor-baked static city before generating fresh
            DestroyStaticCity();

            GenerateLocally(useSeed);
            ClientGenerateCityClientRpc(useSeed);
        }
        else
        {
            // Clients wait for the RPC from server — destroy their static city too
            DestroyStaticCity();
        }
    }

    /// <summary>
    /// Finds and destroys the editor-baked static city objects in the scene,
    /// so the runtime-generated city can take over cleanly.
    /// </summary>
    private void DestroyStaticCity()
    {
        // The editor-baked city is stored under "__City__" if generated via ContextMenu
        GameObject staticCity = GameObject.Find("__City__");
        if (staticCity != null)
        {
            Debug.Log("[CityGenerator] Destroying editor-baked static city before runtime generation.");
            Destroy(staticCity);
        }
    }

    [ContextMenu("Generate City")]
    public void GenerateCity()
    {
        if (config == null)
        {
            Debug.LogError("[CityGenerator] Missing CityConfig! Assign one in the Inspector.");
            return;
        }

        ClearCity();

        int useSeed = config.seed;
        if (useSeed == 0)
        {
            useSeed = UnityEngine.Random.Range(1, int.MaxValue);
        }

        // Networking: if in a network session, use RPCs
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
            // Editor / Offline generation
            GenerateLocally(useSeed);
        }
    }

    [ContextMenu("Clear City")]
    public void ClearCity()
    {
        if (_cityRoot != null)
        {
            if (Application.isPlaying)
                Destroy(_cityRoot);
            else
                DestroyImmediate(_cityRoot);
        }

        // Catch-all for orphaned city roots
        GameObject oldCity = GameObject.Find("__City__");
        if (oldCity != null)
        {
            if (Application.isPlaying)
                Destroy(oldCity);
            else
                DestroyImmediate(oldCity);
        }

        CityData = null;
        _cityRoot = null;
        _streetsRoot = null;
        _blocksRoot = null;
        _intersectionsRoot = null;
        _deliveryPointsRoot = null;

        Debug.Log("[CityGenerator] City cleared.");
        OnCityCleared?.Invoke();
    }

    [ContextMenu("Regenerate City")]
    public void RegenerateCity()
    {
        ClearCity();
        GenerateCity();
    }

    /// <summary>
    /// Returns the pathfinder for GPS route calculations.
    /// </summary>
    public CityGraphPathfinder GetPathfinder()
    {
        return _pathfinder;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Network RPCs
    // ─────────────────────────────────────────────────────────────────────

    [ClientRpc]
    private void ClientGenerateCityClientRpc(int seed)
    {
        if (IsServer) return; // Server already generated
        GenerateLocally(seed);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Generation Pipeline
    // ─────────────────────────────────────────────────────────────────────

    private void GenerateLocally(int seed)
    {
        CurrentSeed = seed;
        Debug.Log($"[CityGenerator] Starting generation with seed {seed} | Grid: {config.gridWidth}x{config.gridHeight}");
        System.Random rng = new System.Random(seed);

        // 0. Create hierarchy
        CreateHierarchy();
        EnsureSubGenerators();

        // 1. Initialize CityData
        CityData = ScriptableObject.CreateInstance<CityData>();
        CityData.Initialize(config.gridWidth, config.gridHeight, config.blockWidth, config.blockDepth, config.streetWidth);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

            string assetPath = "Assets/ScriptableObjects/GeneratedCityData.asset";
            // Check if it already exists to overwrite, otherwise create new
            CityData existingData = UnityEditor.AssetDatabase.LoadAssetAtPath<CityData>(assetPath);
            if (existingData != null)
            {
                // We overwrite properties or just create a new one. Creating new is easier if we overwrite the asset
                UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            }
            UnityEditor.AssetDatabase.CreateAsset(CityData, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            _cityData = CityData; // link the serialized field
            
            UnityEditor.EditorUtility.SetDirty(this); // CRITICAL: Save the reference in the scene!
        }
#endif

        // 2. Generate Street Grid
        Debug.Log("[CityGenerator] Phase 1/4: Generating street grid...");
        StreetGraph graph = _streetGridGen.Generate(
            config.gridWidth + 1, // +1 because intersections = blocks + 1
            config.gridHeight + 1,
            config.blockWidth,
            config.blockDepth,
            config.streetWidth,
            config.sidewalkWidth,
            config.streetRemovalChance,
            rng,
            _streetsRoot.transform,
            streetMaterial,
            sidewalkMaterial
        );
        CityData.streetGraph = graph;
        Debug.Log($"[CityGenerator] Street grid done: {graph.nodes.Count} nodes, {graph.edges.Count} edges");

        // 3. Generate Blocks (zone assignment + building fill)
        Debug.Log("[CityGenerator] Phase 2/4: Generating blocks...");
        GenerateBlocks(rng);
        Debug.Log($"[CityGenerator] Blocks done: {CityData.BlockCount} blocks");

        // 4. Generate Intersections
        Debug.Log("[CityGenerator] Phase 3/4: Generating intersections...");
        _intersectionBuilder.BuildIntersections(
            graph,
            config,
            rng,
            _intersectionsRoot.transform,
            intersectionMaterial
        );
        Debug.Log("[CityGenerator] Intersections done.");

        // 5. Place Delivery Points
        Debug.Log("[CityGenerator] Phase 4/4: Placing delivery points...");
        CityBuilding[] allBuildings = _blocksRoot.GetComponentsInChildren<CityBuilding>();
        List<DeliveryPoint> points = _deliveryPointPlacer.PlaceDeliveryPoints(
            CityData.blocks,
            allBuildings,
            config,
            rng,
            _deliveryPointsRoot.transform
        );
        CityData.DeliveryPointCount = points.Count;
        Debug.Log($"[CityGenerator] Delivery points done: {CityData.DeliveryPointCount} points");

        // 6. Initialize pathfinder
        _pathfinder.Initialize(graph);

        MinimapRouteManager routeManager = FindObjectOfType<MinimapRouteManager>();
        if (routeManager != null)
        {
            routeManager.Initialize(_pathfinder);
        }
        else
        {
            Debug.LogWarning("[CityGenerator] MinimapRouteManager not found in scene — routes will not be drawn. Add it via Scene Builder.");
        }

        // 7. Set pizzaria position (center of grid)
        CityData.pizzariaPosition = new Vector3(
            config.gridWidth * config.SpacingX / 2f,
            0f,
            config.gridHeight * config.SpacingZ / 2f
        );

        GeneratePizzaria();

#if UNITY_EDITOR
        if (!Application.isPlaying && CityData != null)
        {
            UnityEditor.EditorUtility.SetDirty(CityData);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        Debug.Log("[CityGenerator] ✅ City generation complete!");
        TeleportPlayersToSpawn();
        OnCityGenerated?.Invoke();
    }

    private void TeleportPlayersToSpawn()
    {
        GameObject spawnPoint = GameObject.Find("NetworkSpawnPoint");
        if (spawnPoint == null) return;
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                // Only teleport local player to avoid NetworkTransform fighting (if client authoritative)
                if (client.PlayerObject.IsOwner)
                {
                    var cc = client.PlayerObject.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    
                    client.PlayerObject.transform.position = spawnPoint.transform.position;
                    client.PlayerObject.transform.rotation = spawnPoint.transform.rotation;
                    
                    if (cc != null) cc.enabled = true;
                }
            }
        }
    }

    private void GeneratePizzaria()
    {
        Vector3 pos = CityData.pizzariaPosition;
        Transform blocksRoot = _blocksRoot.transform;

        // Create a visual Pizzaria building
        GameObject pizzariaBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pizzariaBuilding.name = "The_Pizzaria_Building";
        pizzariaBuilding.transform.position = pos + new Vector3(0, 3f, 0);
        pizzariaBuilding.transform.localScale = new Vector3(15f, 6f, 15f);
        pizzariaBuilding.transform.SetParent(blocksRoot);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.8f, 0.1f, 0.1f); // Red Pizzaria
        pizzariaBuilding.GetComponent<Renderer>().sharedMaterial = mat;

        // Create the Bancada (Workbench) outside the Pizzaria
        Vector3 bancadaPos = pos + new Vector3(0, 0.5f, -8.5f); // On the sidewalk in front
        GameObject bancada = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bancada.name = "Bancada_Pizzas";
        bancada.transform.position = bancadaPos;
        bancada.transform.localScale = new Vector3(3f, 1f, 1.5f);
        bancada.transform.SetParent(blocksRoot);

        Material bancadaMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        bancadaMat.color = new Color(0.4f, 0.2f, 0.1f); // Brown Wood
        bancada.GetComponent<Renderer>().sharedMaterial = bancadaMat;

        // Save position for Pizza Spawning (Top of the bancada)
        CityData.bancadaPosition = bancadaPos + new Vector3(0, 0.5f, 0);

        // Add a sign
        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Pizzaria_Sign";
        sign.transform.SetParent(pizzariaBuilding.transform);
        sign.transform.localPosition = new Vector3(0, 0.6f, 0.5f);
        sign.transform.localScale = new Vector3(0.8f, 0.2f, 0.1f);
        Material signMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        signMat.color = Color.yellow;
        sign.GetComponent<Renderer>().sharedMaterial = signMat;
        sign.GetComponent<Collider>().enabled = false;

        // Create Spawn Point marker for players
        GameObject spawnPoint = new GameObject("NetworkSpawnPoint");
        spawnPoint.transform.SetParent(pizzariaBuilding.transform);
        spawnPoint.transform.position = pos + new Vector3(0, 0.1f, -12f);
        // Face outwards from the pizzaria
        spawnPoint.transform.rotation = Quaternion.Euler(0, 180, 0);
    }

    private void CreateHierarchy()
    {
        _cityRoot = new GameObject("__City__");
        _streetsRoot = new GameObject("Streets");
        _blocksRoot = new GameObject("Blocks");
        _intersectionsRoot = new GameObject("Intersections");
        _deliveryPointsRoot = new GameObject("DeliveryPoints");

        _streetsRoot.transform.SetParent(_cityRoot.transform);
        _blocksRoot.transform.SetParent(_cityRoot.transform);
        _intersectionsRoot.transform.SetParent(_cityRoot.transform);
        _deliveryPointsRoot.transform.SetParent(_cityRoot.transform);
    }

    private void EnsureSubGenerators()
    {
        // Create sub-generators as components on the city root
        _streetGridGen = _cityRoot.AddComponent<StreetGridGenerator>();
        _blockFiller = _cityRoot.AddComponent<BlockFiller>();
        _intersectionBuilder = _cityRoot.AddComponent<IntersectionBuilder>();
        _deliveryPointPlacer = _cityRoot.AddComponent<DeliveryPointPlacer>();
        _pathfinder = _cityRoot.AddComponent<CityGraphPathfinder>();
    }

    private void GenerateBlocks(System.Random rng)
    {
        for (int x = 0; x < config.gridWidth; x++)
        {
            for (int y = 0; y < config.gridHeight; y++)
            {
                // Calculate block center position
                // Blocks sit between intersections, offset by half spacing + half street
                Vector3 centerPos = new Vector3(
                    x * config.SpacingX + config.SpacingX / 2f,
                    0f,
                    y * config.SpacingZ + config.SpacingZ / 2f
                );

                // Determine zone type based on weighted probabilities
                double rand = rng.NextDouble();
                ZoneType zone;
                if (rand < config.residentialProb)
                    zone = ZoneType.Residential;
                else if (rand < config.residentialProb + config.commercialProb)
                    zone = ZoneType.Commercial;
                else if (rand < config.residentialProb + config.commercialProb + config.industrialProb)
                    zone = ZoneType.Industrial;
                else
                    zone = ZoneType.MonsterZone;

                BlockInfo block = new BlockInfo
                {
                    gridX = x,
                    gridY = y,
                    worldCenter = centerPos,
                    size = new Vector3(config.blockWidth, 0f, config.blockDepth),
                    zoneType = zone,
                    deliveryPointIndices = new List<int>()
                };

                CityData.SetBlockAt(x, y, block);

                // Fill block with buildings
                _blockFiller.FillBlock(block, config, rng, _blocksRoot.transform, buildingMaterials);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (CityData == null || config == null) return;

        // Draw block zones
        for (int x = 0; x < config.gridWidth; x++)
        {
            for (int y = 0; y < config.gridHeight; y++)
            {
                BlockInfo block = CityData.GetBlockAt(x, y);

                switch (block.zoneType)
                {
                    case ZoneType.Residential: Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f); break;
                    case ZoneType.Commercial: Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.2f); break;
                    case ZoneType.Industrial: Gizmos.color = new Color(0.8f, 0.4f, 0.1f, 0.2f); break;
                    case ZoneType.MonsterZone: Gizmos.color = new Color(0.6f, 0.1f, 0.6f, 0.2f); break;
                }

                Gizmos.DrawCube(block.worldCenter + Vector3.up * 0.5f, new Vector3(config.blockWidth, 1f, config.blockDepth));
                Gizmos.DrawWireCube(block.worldCenter + Vector3.up * 0.5f, new Vector3(config.blockWidth, 1f, config.blockDepth));
            }
        }

        // Draw street nodes
        Gizmos.color = Color.cyan;
        foreach (var node in CityData.streetGraph.nodes)
        {
            Gizmos.DrawSphere(node.worldPosition, 1.5f);
        }

        // Draw street edges
        foreach (var edge in CityData.streetGraph.edges)
        {
            Gizmos.color = edge.isBlocked ? Color.red : Color.green;
            Vector3 a = CityData.streetGraph.nodes[edge.nodeA].worldPosition;
            Vector3 b = CityData.streetGraph.nodes[edge.nodeB].worldPosition;
            Gizmos.DrawLine(a + Vector3.up * 0.3f, b + Vector3.up * 0.3f);
        }
    }
}

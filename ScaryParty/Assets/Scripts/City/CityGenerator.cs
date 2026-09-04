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
            int useSeed = config.seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : config.seed;
            DestroyStaticCity();
            GenerateLocally(useSeed);
            ClientGenerateCityClientRpc(useSeed);
        }
        else
        {
            DestroyStaticCity();
        }
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
        GameObject spawnPoint = GameObject.Find("NetworkSpawnPoint");
        if (spawnPoint == null) return;
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
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

        if (blockTransform != null)
        {
            CityBuilding largestBuilding = null;
            float largestVolume = 0f;

            foreach (Transform child in blockTransform)
            {
                CityBuilding building = child.GetComponent<CityBuilding>();
                if (building == null) continue;

                Vector3 scale = child.localScale;
                float volume = scale.x * scale.y * scale.z;
                if (volume > largestVolume)
                {
                    largestVolume = volume;
                    largestBuilding = building;
                }
            }

            if (largestBuilding != null)
            {
                pizzariaPos = largestBuilding.transform.position;
                pizzariaRot = largestBuilding.transform.rotation;
                pizzariaScale = largestBuilding.transform.localScale;
                
                entranceDir = largestBuilding.transform.forward;
                entranceDir.y = 0;
                if (entranceDir.sqrMagnitude < 0.01f) entranceDir = Vector3.forward;

                if (Application.isPlaying) Destroy(largestBuilding.gameObject);
                else DestroyImmediate(largestBuilding.gameObject);
            }
        }

        CityData.pizzariaPosition = pizzariaPos;

        GameObject pizzariaBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pizzariaBuilding.name = "The_Pizzaria_Building";
        pizzariaBuilding.transform.position = pizzariaPos;
        pizzariaBuilding.transform.rotation = pizzariaRot;
        pizzariaBuilding.transform.localScale = pizzariaScale;
        pizzariaBuilding.transform.SetParent(blockTransform != null ? blockTransform : _blocksRoot.transform);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.8f, 0.1f, 0.1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
        pizzariaBuilding.GetComponent<Renderer>().sharedMaterial = mat;

        // Bancada outside the pizzaria, on the sidewalk!
        // Pizzaria scale Z is depth. So pizzariaScale.z * 0.5f is the edge of the building.
        // Add 1.5f so the counter sits exactly on the sidewalk just outside the building.
        float offsetToSidewalk = (pizzariaScale.z * 0.5f) + 1.5f;
        Vector3 bancadaPos = pizzariaPos + entranceDir * offsetToSidewalk + Vector3.up * 0.5f;
        
        GameObject bancada = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bancada.name = "Bancada_Pizzas";
        bancada.transform.position = bancadaPos;
        bancada.transform.rotation = pizzariaRot; // Align rotation with building
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
        sign.transform.localPosition = new Vector3(0, 0.6f, 0.5f); // Front of the building
        sign.transform.localScale = new Vector3(0.8f, 0.2f, 0.1f);
        Material signMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        signMat.color = Color.yellow;
        if (signMat.HasProperty("_BaseColor")) signMat.SetColor("_BaseColor", signMat.color);
        sign.GetComponent<Renderer>().sharedMaterial = signMat;
        sign.GetComponent<Collider>().enabled = false;

        GameObject spawnPoint = new GameObject("NetworkSpawnPoint");
        spawnPoint.transform.SetParent(pizzariaBuilding.transform);
        // Posicionar o player spawn atrás da bancada, ou na frente
        spawnPoint.transform.position = bancadaPos + entranceDir * 2f + Vector3.up * 0.1f;
        spawnPoint.transform.rotation = Quaternion.LookRotation(-entranceDir);
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

        GameObject pizzariaBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pizzariaBuilding.name = "The_Pizzaria_Building";
        pizzariaBuilding.transform.position = pos + new Vector3(0, 3f, 0);
        pizzariaBuilding.transform.localScale = new Vector3(15f, 6f, 15f);
        pizzariaBuilding.transform.SetParent(blocksRoot);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.8f, 0.1f, 0.1f);
        pizzariaBuilding.GetComponent<Renderer>().sharedMaterial = mat;

        Vector3 bancadaPos = pos + new Vector3(0, 0.5f, -8.5f);
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

        GameObject spawnPoint = new GameObject("NetworkSpawnPoint");
        spawnPoint.transform.SetParent(pizzariaBuilding.transform);
        spawnPoint.transform.position = pos + new Vector3(0, 0.1f, -12f);
        spawnPoint.transform.rotation = Quaternion.Euler(0, 180, 0);
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
        int blockIndex = 0;

        foreach (Vector3[] rawPoly in blockPolygons)
        {
            // BUG FIX: Remove degenerate collinear edges that cause severe miter math corruption
            Vector3[] poly = CleanPolygon(rawPoly);
            if (poly.Length < 3) continue;

            // Fix: Inset polygon by streetWidth * 0.5f PLUS sidewalkWidth
            float totalInset = (config.streetWidth * 0.5f) + config.sidewalkWidth;
            Vector3[] insetPoly = InsetPolygon(poly, totalInset);
            
            // BUG FIX: Quarteirões muito pequenos se auto-interceptam durante o Inset, criando geometrias inválidas.
            // Pulamos quarteirões cuja área útil ficou pequena demais!
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

            // Thinness filter: Area / Perimeter^2 check to reject long, narrow wedge blocks
            float perimeter = 0f;
            for (int i = 0; i < insetPoly.Length; i++) {
                perimeter += Vector3.Distance(insetPoly[i], insetPoly[(i + 1) % insetPoly.Length]);
            }
            if (perimeter > 0f) {
                float thinness = (4f * Mathf.PI * area) / (perimeter * perimeter);
                if (thinness < 0.1f) continue; // Reject extremely thin polygons (e.g. 1x20 aspect ratio)
            }

            // Zonas baseadas em "Chunks" quadrados (Chebyshev distance para não formar círculos)
            float maxAbsDist = Mathf.Max(Mathf.Abs(centerPos.x), Mathf.Abs(centerPos.z));
            float chunkSize = config.maxStreetBranchLength;
            
            ZoneType zone;
            if (maxAbsDist <= chunkSize * 0.5f) {
                zone = ZoneType.Commercial; // Chunk 1x1 (Centro Seguro)
            } else if (maxAbsDist <= chunkSize * 1.5f) {
                zone = ZoneType.Residential; // Chunks 3x3 (Bairros)
            } else if (maxAbsDist <= chunkSize * 2.5f) {
                zone = ZoneType.Industrial; // Chunks 5x5 (Zona Afastada)
            } else {
                zone = ZoneType.MonsterZone; // Bordas (Perigo Máximo)
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
            CityGenLogger.StartBlock(blockIndex, area, insetPoly.Length);
            _blockFiller.FillBlock(block, config, rng, _blocksRoot.transform, buildingMaterials, blockIndex);
            
            blockIndex++;
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

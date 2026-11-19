using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Manages NPC traffic vehicles that circulate through city streets.
/// Server-authoritative: the server controls all vehicle positions and syncs to clients.
/// </summary>
public class TrafficManager : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int _maxVehicles = 10;
    [SerializeField] private float _baseSpeed = 8f;
    [SerializeField] private float _spawnDelay = 2f; // seconds between spawning vehicles
    [SerializeField] private float _syncInterval = 0.25f;
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugOverlay = false;

    private CityGraphPathfinder _pathfinder;
    private CityGenerator _networkRelay;
    private StreetGraph _graph;
    private List<TrafficVehicle> _vehicles = new List<TrafficVehicle>();
    private GameObject _vehicleRoot;
    private float _spawnTimer;
    private float _syncTimer;
    private System.Random _rng;
    private bool _initialized;

    private int _nextVehicleId = 1;

    public struct VehicleState : INetworkSerializable
    {
        public int vehicleId;
        public Vector3 position;
        public Quaternion rotation;
        public Color color;
        public bool active;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref vehicleId);
            serializer.SerializeValue(ref position);

            short yawCentidegrees = serializer.IsWriter
                ? (short)Mathf.Clamp(Mathf.RoundToInt(Mathf.DeltaAngle(0f, rotation.eulerAngles.y) * 100f), -18000, 18000)
                : (short)0;
            serializer.SerializeValue(ref yawCentidegrees);
            if (serializer.IsReader)
            {
                rotation = Quaternion.Euler(0f, yawCentidegrees * 0.01f, 0f);
            }

            Color32 c32 = serializer.IsWriter ? (Color32)color : default;
            byte r = c32.r;
            byte g = c32.g;
            byte b = c32.b;
            byte a = c32.a;
            serializer.SerializeValue(ref r);
            serializer.SerializeValue(ref g);
            serializer.SerializeValue(ref b);
            serializer.SerializeValue(ref a);
            if (serializer.IsReader)
            {
                color = new Color32(r, g, b, a);
            }

            serializer.SerializeValue(ref active);
        }
    }

    public static readonly Vector3 VehicleDimensions = new Vector3(2.35f, 1.2f, 4.4f);

    private List<VehicleState> _vehicleStates = new List<VehicleState>();
    private readonly Dictionary<int, GameObject> _clientVisuals = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, Rigidbody> _clientProxyBodies = new Dictionary<int, Rigidbody>();

    public void SetNetworkRelay(CityGenerator relay)
    {
        _networkRelay = relay;
    }

    /// <summary>
    /// Initialize with the city's pathfinder and graph
    /// </summary>
    public void Initialize(CityGraphPathfinder pathfinder, StreetGraph graph, int seed, int maxVehicles, float baseSpeed)
    {
        _pathfinder = pathfinder;
        _graph = graph;
        _rng = new System.Random(seed);
        _maxVehicles = maxVehicles;
        _baseSpeed = baseSpeed;
        
        _vehicleRoot = new GameObject("__Traffic__");
        _vehicleRoot.transform.SetParent(transform);
        
        _initialized = true;
    }

    private bool IsActiveServerOrLocal
    {
        get
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true; // Rodando em modo local / offline / sem NGO ativo

            return NetworkManager.Singleton.IsServer;
        }
    }

    private bool IsActiveClient
    {
        get
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer;
        }
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f7Key.wasPressedThisFrame)
            _showDebugOverlay = !_showDebugOverlay;
#else
        if (Input.GetKeyDown(KeyCode.F7))
            _showDebugOverlay = !_showDebugOverlay;
#endif
        if (!_initialized) return;

        if (IsActiveClient)
        {
            ClientUpdate();
        }
    }

    private void FixedUpdate()
    {
        if (!_initialized) return;
        if (IsActiveServerOrLocal)
        {
            ServerFixedUpdate(Time.fixedDeltaTime);
        }
    }

    private void ServerFixedUpdate(float fixedDeltaTime)
    {
        // Spawning
        if (_vehicles.Count < _maxVehicles)
        {
            _spawnTimer += fixedDeltaTime;
            if (_spawnTimer >= _spawnDelay)
            {
                SpawnVehicle();
                _spawnTimer = 0f;
            }
        }

        // Physical Movement (FixedUpdate)
        for (int i = _vehicles.Count - 1; i >= 0; i--)
        {
            TrafficVehicle vehicle = _vehicles[i];
            if (vehicle == null || !vehicle.isActive) continue;

            bool reachedDestination = vehicle.UpdateMovement(fixedDeltaTime);
            if (reachedDestination)
            {
                AssignRandomPath(vehicle);
            }
        }

        // Sync to clients
        _syncTimer += fixedDeltaTime;
        if (_syncTimer >= _syncInterval)
        {
            SyncVehicles();
            _syncTimer = 0f;
        }
    }

    private void ClientUpdate()
    {
        for (int i = 0; i < _vehicleStates.Count; i++)
        {
            var st = _vehicleStates[i];
            if (!_clientVisuals.TryGetValue(st.vehicleId, out var visual) || visual == null)
                continue;

            if (st.active)
            {
                if (!visual.activeSelf)
                {
                    visual.transform.SetPositionAndRotation(st.position, st.rotation);
                    visual.SetActive(true);
                }
                else
                {
                    Vector3 nextPos = Vector3.Lerp(visual.transform.position, st.position, Time.deltaTime * 12f);
                    Quaternion nextRot = Quaternion.Slerp(visual.transform.rotation, st.rotation, Time.deltaTime * 12f);
                    if (_clientProxyBodies.TryGetValue(st.vehicleId, out var proxyRb) && proxyRb != null)
                    {
                        proxyRb.MovePosition(nextPos);
                        proxyRb.MoveRotation(nextRot);
                    }
                    else
                    {
                        visual.transform.SetPositionAndRotation(nextPos, nextRot);
                    }
                }
            }
            else if (visual.activeSelf)
            {
                visual.SetActive(false);
            }
        }
    }

    private void SpawnVehicle()
    {
        if (_graph == null || _graph.nodes == null || _graph.nodes.Count < 2) return;

        GameObject vehicleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vehicleObj.SetActive(false);
        vehicleObj.name = $"TrafficVehicle_{_nextVehicleId}";
        vehicleObj.transform.localScale = VehicleDimensions;
        vehicleObj.transform.SetParent(_vehicleRoot.transform);

        // Corpo físico sólido (não-trigger) para colisão real
        BoxCollider solidCol = vehicleObj.GetComponent<BoxCollider>();
        if (solidCol != null) solidCol.isTrigger = false;

        // Sensor de antecipação separado (trigger) para pedestres/buzina
        var sensorGo = new GameObject("AnticipationSensor");
        sensorGo.transform.SetParent(vehicleObj.transform, false);
        sensorGo.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        var sensorCol = sensorGo.AddComponent<BoxCollider>();
        sensorCol.isTrigger = true;
        sensorCol.size = new Vector3(1.15f, 1.05f, 1.35f);

        var rb = vehicleObj.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.mass = 1200f;
        rb.linearDamping = 1.5f;
        rb.angularDamping = 5.0f;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        TrafficVehicle vehicle = vehicleObj.AddComponent<TrafficVehicle>();
        vehicle.VehicleId = _nextVehicleId++;
        vehicle.speed = _baseSpeed * (0.8f + (float)_rng.NextDouble() * 0.4f);
        vehicle.vehicleColor = new Color((float)_rng.NextDouble(), (float)_rng.NextDouble(), (float)_rng.NextDouble());

        Renderer vehRend = vehicleObj.GetComponent<Renderer>();
        if (vehRend != null)
        {
            Material vehMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                vehRend.sharedMaterial,
                ScaryParty.VisualStyles.VisualSemanticRole.GenericOpaque,
                ScaryParty.VisualStyles.VisualCategoryIds.Props);
            if (vehMat != null) vehRend.sharedMaterial = vehMat;
            ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(vehRend, 0, vehicle.vehicleColor);
        }

        if (!AssignRandomPath(vehicle, isInitialSpawn: true) || vehicle.waypoints == null || vehicle.waypoints.Count < 2)
        {
            Destroy(vehicleObj);
            return;
        }

        Vector3 spawnPos = vehicle.waypoints[0];
        spawnPos.y += 0.65f;
        vehicleObj.transform.position = spawnPos;
        Vector3 initialFwd = vehicle.waypoints[1] - vehicle.waypoints[0];
        initialFwd.y = 0f;
        if (initialFwd.sqrMagnitude > 0.001f)
            vehicleObj.transform.rotation = Quaternion.LookRotation(initialFwd.normalized);

        vehicle.isActive = true;
        vehicleObj.SetActive(true);
        _vehicles.Add(vehicle);
    }

    private bool AssignRandomPath(TrafficVehicle vehicle, bool isInitialSpawn = false)
    {
        if (_graph == null || _graph.nodes == null || _graph.nodes.Count < 2) return false;

        int startIndex = (!isInitialSpawn && vehicle.CurrentEndNode >= 0 && vehicle.CurrentEndNode < _graph.nodes.Count)
            ? vehicle.CurrentEndNode
            : _rng.Next(0, _graph.nodes.Count);

        Vector3 startPos = (!isInitialSpawn && vehicle.CurrentEndNode >= 0)
            ? _graph.nodes[startIndex].worldPosition
            : _graph.nodes[startIndex].worldPosition;

        List<Vector3> path = null;
        int chosenEndIndex = -1;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            int endIndex = _rng.Next(0, _graph.nodes.Count);
            if (endIndex == startIndex) continue;

            if (_pathfinder != null)
            {
                var candidate = _pathfinder.FindPath(startPos, _graph.nodes[endIndex].worldPosition);
                if (candidate != null && candidate.Count >= 2)
                {
                    path = candidate;
                    chosenEndIndex = endIndex;
                    break;
                }
            }
        }

        // A10: nunca liga em linha reta cruzando quarteirões — fallback apenas para vizinho conectado no grafo viário
        if ((path == null || path.Count < 2) && startIndex >= 0 && startIndex < _graph.nodes.Count)
        {
            var edges = _graph.nodes[startIndex].connectedEdges;
            if (edges != null && edges.Count > 0)
            {
                for (int e = 0; e < edges.Count; e++)
                {
                    int edgeId = edges[e];
                    if (edgeId < 0 || edgeId >= _graph.edges.Count) continue;
                    var edge = _graph.edges[edgeId];
                    if (edge.isBlocked) continue;

                    int neighborId = edge.nodeA == startIndex ? edge.nodeB : edge.nodeA;
                    if (neighborId < 0 || neighborId >= _graph.nodes.Count) continue;

                    path = new List<Vector3> { _graph.nodes[startIndex].worldPosition, _graph.nodes[neighborId].worldPosition };
                    chosenEndIndex = neighborId;
                    vehicle.CurrentEdgeId = edgeId;
                    break;
                }
            }
        }

        if (path == null || path.Count < 2)
        {
            vehicle.isActive = false;
            return false;
        }

        vehicle.CurrentStartNode = startIndex;
        vehicle.CurrentEndNode = chosenEndIndex;
        if (vehicle.CurrentEdgeId < 0 && path.Count >= 2)
            vehicle.CurrentEdgeId = ResolveClosestEdgeId(path[0], path[1]);
        float streetWidth = (_networkRelay != null && _networkRelay.config != null && _networkRelay.config.streetWidth > 1f)
            ? _networkRelay.config.streetWidth
            : 6.0f;
        vehicle.waypoints = BuildLaneAndCornerWaypoints(path, streetWidth, VehicleDimensions.x);
        vehicle.currentWaypointIndex = 0;
        return true;
    }

    public static float ComputeSafeLaneOffset(float streetWidth, float vehicleWidth)
    {
        float halfStreet = Mathf.Max(2.0f, streetWidth * 0.5f);
        float halfVehicle = vehicleWidth * 0.5f;
        float maxSafeOffset = Mathf.Max(0f, halfStreet - halfVehicle - 0.20f);
        return Mathf.Min(halfStreet * 0.5f, maxSafeOffset);
    }

    public static bool IsEnvelopeContainedInCorridor(
        IReadOnlyList<Vector3> waypoints,
        IReadOnlyList<Vector3> centerPath,
        float streetWidth,
        float vehicleWidth)
    {
        if (waypoints == null || centerPath == null || centerPath.Count < 2) return false;
        float halfStreet = Mathf.Max(2.0f, streetWidth * 0.5f);
        float halfVehicle = vehicleWidth * 0.5f;
        float maxCenterOffset = Mathf.Max(0f, halfStreet - halfVehicle - 0.10f);

        for (int w = 0; w < waypoints.Count; w++)
        {
            Vector3 pt = waypoints[w];
            float minDistSq = float.MaxValue;
            for (int s = 0; s < centerPath.Count - 1; s++)
            {
                Vector3 a = centerPath[s];
                Vector3 b = centerPath[s + 1];
                Vector3 ab = b - a;
                ab.y = 0f;
                float lenSq = ab.sqrMagnitude;
                if (lenSq < 0.0001f) continue;
                Vector3 ap = pt - a;
                ap.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / lenSq);
                Vector3 proj = a + ab * t;
                Vector3 d = pt - proj;
                d.y = 0f;
                if (d.sqrMagnitude < minDistSq) minDistSq = d.sqrMagnitude;
            }
            if (Mathf.Sqrt(minDistSq) > maxCenterOffset + 0.02f)
                return false;
        }
        return true;
    }

    public static List<Vector3> BuildLaneAndCornerWaypoints(List<Vector3> centerPath, float streetWidth, float vehicleWidth)
    {
        if (centerPath == null || centerPath.Count < 2) return centerPath;

        float halfStreet = Mathf.Max(2.0f, streetWidth * 0.5f);
        float halfVehicle = vehicleWidth * 0.5f;
        // Margem de segurança de 0.20m da guia da calçada
        float maxSafeOffset = Mathf.Max(0f, halfStreet - halfVehicle - 0.20f);
        float laneOffset = ComputeSafeLaneOffset(streetWidth, vehicleWidth);
        float trimBase = Mathf.Clamp(halfStreet * 0.85f, 1.4f, 3.2f);

        var result = new List<Vector3>(centerPath.Count * 4);

        for (int i = 0; i < centerPath.Count - 1; i++)
        {
            Vector3 a = centerPath[i];
            Vector3 b = centerPath[i + 1];
            Vector3 dir = b - a;
            dir.y = 0f;
            float len = dir.magnitude;
            if (len < 0.05f) continue;
            dir /= len;

            Vector3 rightLane = Vector3.Cross(Vector3.up, dir).normalized * laneOffset;
            float trim = Mathf.Min(trimBase, len * 0.28f);
            Vector3 entry = a + dir * trim + rightLane;
            Vector3 exit = b - dir * trim + rightLane;

            if (result.Count > 0)
            {
                // Arco quadrático dentro do cruzamento usando o vértice da interseção deslocado pela faixa segura
                Vector3 prev = result[result.Count - 1];
                Vector3 prevToA = a - prev;
                prevToA.y = 0f;
                Vector3 control = a + (entry - a) * 0.45f + (prev - a) * 0.45f;
                control.y = prev.y;

                // Amostra 2 pontos intermediários na curva de Bézier quadrática e garante contenção na rua
                for (int s = 1; s <= 2; s++)
                {
                    float t = s / 3f;
                    float omt = 1f - t;
                    Vector3 arcPt = (omt * omt * prev) + (2f * omt * t * control) + (t * t * entry);
                    arcPt = ClampPointToStreetCorridor(arcPt, a, b, maxSafeOffset);
                    result.Add(arcPt);
                }
            }

            result.Add(entry);
            result.Add(exit);
        }

        if (result.Count < 2) return centerPath;
        return result;
    }

    private static Vector3 ClampPointToStreetCorridor(Vector3 pt, Vector3 segA, Vector3 segB, float maxLateralOffset)
    {
        Vector3 ab = segB - segA;
        ab.y = 0f;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.001f) return pt;

        Vector3 ap = pt - segA;
        ap.y = 0f;
        float proj = Mathf.Clamp01(Vector3.Dot(ap, ab) / lenSq);
        Vector3 closest = segA + ab * proj;
        closest.y = pt.y;
        Vector3 lateral = pt - closest;
        lateral.y = 0f;
        float latMag = lateral.magnitude;
        if (latMag > maxLateralOffset && latMag > 0.001f)
        {
            return closest + (lateral / latMag) * maxLateralOffset;
        }
        return pt;
    }

    private int ResolveClosestEdgeId(Vector3 a, Vector3 b)
    {
        if (_graph == null || _graph.edges == null || _graph.nodes == null) return -1;
        Vector3 mid = (a + b) * 0.5f;
        int bestEdge = -1;
        float bestDistSq = 16f;
        for (int i = 0; i < _graph.edges.Count; i++)
        {
            var e = _graph.edges[i];
            if (e.nodeA < 0 || e.nodeA >= _graph.nodes.Count || e.nodeB < 0 || e.nodeB >= _graph.nodes.Count) continue;
            Vector3 eMid = (_graph.nodes[e.nodeA].worldPosition + _graph.nodes[e.nodeB].worldPosition) * 0.5f;
            float dSq = (eMid - mid).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestEdge = e.id;
            }
        }
        return bestEdge;
    }

    public void DespawnAllVehicles()
    {
        foreach (var v in _vehicles)
        {
            if (v != null) Destroy(v.gameObject);
        }
        _vehicles.Clear();

        foreach (var kvp in _clientVisuals)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        _clientVisuals.Clear();
        _vehicleStates.Clear();
    }

    private void SyncVehicles()
    {
        _vehicleStates.Clear();
        foreach (var v in _vehicles)
        {
            if (v != null)
            {
                _vehicleStates.Add(new VehicleState
                {
                    vehicleId = v.VehicleId,
                    position = v.transform.position,
                    rotation = v.transform.rotation,
                    color = v.vehicleColor,
                    active = v.isActive
                });
            }
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            if (NetworkManager.Singleton.ConnectedClientsIds.Count <= 1) return;
            VehicleState[] statesArray = _vehicleStates.ToArray();
            if (IsSpawned)
            {
                SyncVehiclesClientRpc(statesArray);
            }
            else if (_networkRelay != null && _networkRelay.IsSpawned)
            {
                _networkRelay.BroadcastTrafficStates(statesArray);
            }
        }
    }

    public void ApplySyncedVehicleStatesFromRelay(VehicleState[] states)
    {
        ApplySyncedVehicleStates(states);
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SyncVehiclesClientRpc(VehicleState[] states)
    {
        if (IsServer) return;
        ApplySyncedVehicleStates(states);
    }

    private void ApplySyncedVehicleStates(VehicleState[] states)
    {

        _vehicleStates = new List<VehicleState>(states ?? System.Array.Empty<VehicleState>());
        var activeIds = new HashSet<int>();

        for (int i = 0; i < _vehicleStates.Count; i++)
        {
            var st = _vehicleStates[i];
            activeIds.Add(st.vehicleId);

            if (!_clientVisuals.TryGetValue(st.vehicleId, out var visual) || visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.SetActive(false);
                visual.name = $"TrafficVehicleProxy_{st.vehicleId}";
                visual.transform.localScale = VehicleDimensions;

                Renderer rend = visual.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material vehMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                        rend.sharedMaterial,
                        ScaryParty.VisualStyles.VisualSemanticRole.GenericOpaque,
                        ScaryParty.VisualStyles.VisualCategoryIds.Props);
                    if (vehMat != null) rend.sharedMaterial = vehMat;
                    ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, 0, st.color);
                }

                if (_vehicleRoot == null)
                {
                    _vehicleRoot = new GameObject("__TrafficVisuals__");
                    _vehicleRoot.transform.SetParent(transform);
                }
                visual.transform.SetParent(_vehicleRoot.transform);

                // R04: manter colisor sólido no cliente com dimensões idênticas ao servidor e Rigidbody cinemático (sem dano duplicado)
                BoxCollider proxyCol = visual.GetComponent<BoxCollider>();
                if (proxyCol != null) proxyCol.isTrigger = false;

                var proxyRb = visual.AddComponent<Rigidbody>();
                proxyRb.isKinematic = true;
                proxyRb.useGravity = false;
                proxyRb.interpolation = RigidbodyInterpolation.Interpolate;
                _clientProxyBodies[st.vehicleId] = proxyRb;

                // A13: definir pose inicial ANTES de ativar o objeto no cliente
                visual.transform.SetPositionAndRotation(st.position, st.rotation);
                visual.SetActive(st.active);
                _clientVisuals[st.vehicleId] = visual;
            }
        }

        // A13: remover visuais excedentes / removidos no servidor
        var toRemove = new List<int>();
        foreach (var kvp in _clientVisuals)
        {
            if (!activeIds.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            if (_clientVisuals.TryGetValue(toRemove[i], out var dead) && dead != null)
                Destroy(dead);
            _clientVisuals.Remove(toRemove[i]);
            _clientProxyBodies.Remove(toRemove[i]);
        }
    }

    /// <summary>
    /// Forces reroute for affected vehicles when an edge is blocked
    /// </summary>
    public void OnEdgeBlocked(int edgeId)
    {
        if (!IsActiveServerOrLocal) return;
        // Reroute logic - for simplicity, give all vehicles a new path
        foreach (var v in _vehicles)
        {
            if (v != null && v.isActive)
            {
                AssignRandomPath(v);
            }
        }
    }

    /// <summary>
    /// Called when an edge is unblocked
    /// </summary>
    public void OnEdgeUnblocked(int edgeId)
    {
        // Optional logic when unblocked
    }

    private void OnGUI()
    {
        if (!_showDebugOverlay || !_initialized) return;
        GUILayout.BeginArea(new Rect(12f, 12f, 440f, 360f), "Traffic Debug (F7)", GUI.skin.window);
        GUILayout.Label($"Mode: {(IsActiveServerOrLocal ? "Server/Local" : "Client")} | Vehicles: {_vehicles.Count}/{_maxVehicles} | States: {_vehicleStates.Count}");
        int shown = 0;
        for (int i = 0; i < _vehicles.Count && shown < 10; i++)
        {
            var v = _vehicles[i];
            if (v == null) continue;
            int wpCount = v.waypoints != null ? v.waypoints.Count : 0;
            GUILayout.Label($"#{v.VehicleId} | edge={v.CurrentEdgeId} | wp={v.currentWaypointIndex}/{wpCount} | spd={v.CurrentSpeed:F1}/{v.speed:F1} | stop={v.CurrentStopReason}");
            shown++;
        }
        GUILayout.EndArea();
    }
}

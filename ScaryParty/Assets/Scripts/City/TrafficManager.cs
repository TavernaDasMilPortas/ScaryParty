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
    [SerializeField] private float _syncInterval = 0.2f;
    
    private CityGraphPathfinder _pathfinder;
    private StreetGraph _graph;
    private List<TrafficVehicle> _vehicles = new List<TrafficVehicle>();
    private GameObject _vehicleRoot;
    private float _spawnTimer;
    private float _syncTimer;
    private System.Random _rng;
    private bool _initialized;

    public struct VehicleState : INetworkSerializable
    {
        public Vector3 position;
        public Quaternion rotation;
        public Color color;
        public bool active;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref color);
            serializer.SerializeValue(ref active);
        }
    }

    private List<VehicleState> _vehicleStates = new List<VehicleState>();
    private List<GameObject> _clientVisuals = new List<GameObject>();

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

    private void Update()
    {
        if (!_initialized) return;

        if (IsServer)
        {
            ServerUpdate();
        }
        else if (IsClient)
        {
            ClientUpdate();
        }
    }

    private void ServerUpdate()
    {
        // Spawning
        if (_vehicles.Count < _maxVehicles)
        {
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _spawnDelay)
            {
                SpawnVehicle();
                _spawnTimer = 0f;
            }
        }

        // Movement
        for (int i = _vehicles.Count - 1; i >= 0; i--)
        {
            TrafficVehicle vehicle = _vehicles[i];
            if (vehicle == null || !vehicle.isActive) continue;

            bool reachedDestination = vehicle.UpdateMovement(Time.deltaTime);
            if (reachedDestination)
            {
                // Give new random destination
                AssignRandomPath(vehicle);
            }
        }

        // Sync to clients
        _syncTimer += Time.deltaTime;
        if (_syncTimer >= _syncInterval)
        {
            SyncVehicles();
            _syncTimer = 0f;
        }
    }

    private void ClientUpdate()
    {
        // Lerp visuals
        for (int i = 0; i < _clientVisuals.Count && i < _vehicleStates.Count; i++)
        {
            if (_vehicleStates[i].active)
            {
                if (!_clientVisuals[i].activeSelf) _clientVisuals[i].SetActive(true);
                
                // Interpolate
                _clientVisuals[i].transform.position = Vector3.Lerp(_clientVisuals[i].transform.position, _vehicleStates[i].position, Time.deltaTime * 10f);
                _clientVisuals[i].transform.rotation = Quaternion.Slerp(_clientVisuals[i].transform.rotation, _vehicleStates[i].rotation, Time.deltaTime * 10f);
            }
            else
            {
                if (_clientVisuals[i].activeSelf) _clientVisuals[i].SetActive(false);
            }
        }
    }

    private void SpawnVehicle()
    {
        if (_graph == null || _graph.nodes == null || _graph.nodes.Count < 2) return;

        GameObject vehicleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vehicleObj.transform.localScale = new Vector3(2.5f, 1.2f, 4.5f);
        vehicleObj.transform.SetParent(_vehicleRoot.transform);
        
        // Keep collider as trigger so TrafficVehicle.CheckVehicleAhead() raycasts still detect other vehicles
        Collider col = vehicleObj.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        
        TrafficVehicle vehicle = vehicleObj.AddComponent<TrafficVehicle>();
        vehicle.speed = _baseSpeed * (0.8f + (float)_rng.NextDouble() * 0.4f);
        vehicle.vehicleColor = new Color((float)_rng.NextDouble(), (float)_rng.NextDouble(), (float)_rng.NextDouble());
        vehicle.isActive = true;

        AssignRandomPath(vehicle);

        if (vehicle.waypoints != null && vehicle.waypoints.Count > 0)
        {
            vehicle.transform.position = vehicle.waypoints[0];
            if (vehicle.waypoints.Count > 1)
            {
                vehicle.transform.rotation = Quaternion.LookRotation(vehicle.waypoints[1] - vehicle.waypoints[0]);
            }
        }

        _vehicles.Add(vehicle);
    }

    private void AssignRandomPath(TrafficVehicle vehicle)
    {
        int startIndex = _rng.Next(0, _graph.nodes.Count);
        int endIndex = _rng.Next(0, _graph.nodes.Count);
        
        while (startIndex == endIndex && _graph.nodes.Count > 1)
        {
            endIndex = _rng.Next(0, _graph.nodes.Count);
        }

        // Use the pathfinder for actual A* routing along streets
        Vector3 startPos = _graph.nodes[startIndex].worldPosition;
        Vector3 endPos = _graph.nodes[endIndex].worldPosition;

        List<Vector3> path = null;
        if (_pathfinder != null)
        {
            path = _pathfinder.FindPath(startPos, endPos);
        }

        // Fallback if pathfinder fails or returns empty
        if (path == null || path.Count < 2)
        {
            path = new List<Vector3>();
            path.Add(startPos);
            path.Add(endPos);
        }

        vehicle.waypoints = path;
        vehicle.currentWaypointIndex = 0;
    }

    public void DespawnAllVehicles()
    {
        foreach (var v in _vehicles)
        {
            if (v != null) Destroy(v.gameObject);
        }
        _vehicles.Clear();
        
        foreach (var v in _clientVisuals)
        {
            if (v != null) Destroy(v);
        }
        _clientVisuals.Clear();
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
                    position = v.transform.position,
                    rotation = v.transform.rotation,
                    color = v.vehicleColor,
                    active = v.isActive
                });
            }
        }

        VehicleState[] statesArray = _vehicleStates.ToArray();
        SyncVehiclesClientRpc(statesArray);
    }

    [ClientRpc]
    private void SyncVehiclesClientRpc(VehicleState[] states)
    {
        if (IsServer) return; // Server already knows the real positions

        _vehicleStates = new List<VehicleState>(states);

        // Ensure we have enough visual objects
        while (_clientVisuals.Count < _vehicleStates.Count)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.localScale = new Vector3(2.5f, 1.2f, 4.5f);
            
            // Apply color via MaterialPropertyBlock for optimization
            Renderer rend = visual.GetComponent<Renderer>();
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", _vehicleStates[_clientVisuals.Count].color);
            rend.SetPropertyBlock(mpb);

            if (_vehicleRoot == null)
            {
                _vehicleRoot = new GameObject("__TrafficVisuals__");
                _vehicleRoot.transform.SetParent(transform);
            }
            visual.transform.SetParent(_vehicleRoot.transform);
            
            // Disable collider on client
            Collider col = visual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _clientVisuals.Add(visual);
        }
    }

    /// <summary>
    /// Forces reroute for affected vehicles when an edge is blocked
    /// </summary>
    public void OnEdgeBlocked(int edgeId)
    {
        if (!IsServer) return;
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
}

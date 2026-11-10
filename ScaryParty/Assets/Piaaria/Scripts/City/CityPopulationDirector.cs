using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Navigation.Core;
using ScaryParty.Pizzeria.Presentation;

/// <summary>
/// Controls the living population of pedestrians in the city.
/// Calculates target population based on city size (formula: N = min(N_max, N_base + d * B)),
/// manages object pooling for character visuals, coordinates building portal transitions,
/// and integrates pedestrians directly into the pizzeria customer flow.
/// </summary>
public class CityPopulationDirector : MonoBehaviour
{
    public static CityPopulationDirector Instance { get; private set; }

    [Header("Configuration")]
    public CityConfig config;
    public GameObject characterVisualPrefab;

    [Header("Runtime State")]
    [SerializeField] private int _targetPopulation = 10;
    [SerializeField] private int _activePedestriansCount = 0;
    public int ActivePedestriansCount => _activePedestriansCount;

    private readonly List<PedestrianNpc> _activePedestrians = new List<PedestrianNpc>();
    private readonly Queue<PedestrianNpc> _pool = new Queue<PedestrianNpc>();
    private readonly List<VirtualOccupantEntry> _virtualOccupants = new List<VirtualOccupantEntry>();

    private System.Random _rng;
    private bool _isInitialized = false;
    private int _pedestrianIdCounter = 0;

    private struct VirtualOccupantEntry
    {
        public string NpcId;
        public CityBuilding CurrentBuilding;
        public float ExitTime;
        public float WalkSpeed;
        public int ColorIndex;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        EnsureVisualPrefabLoaded();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearPopulation();
    }

    private void EnsureVisualPrefabLoaded()
    {
        if (characterVisualPrefab != null) return;
        characterVisualPrefab = Resources.Load<GameObject>("CharacterVisual");
#if UNITY_EDITOR
        if (characterVisualPrefab == null)
        {
            characterVisualPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
        }
#endif
    }

    public void Initialize(CityConfig cityConfig, int seed, Transform parent)
    {
        config = cityConfig;
        _rng = new System.Random(seed);
        _isInitialized = true;

        if (NavigationWorldManager.Instance != null && NavigationWorldManager.Instance.IsReady)
        {
            SpawnInitialPopulation();
        }
        else if (NavigationWorldManager.Instance != null)
        {
            NavigationWorldManager.Instance.OnNavigationReady -= HandleNavigationReady;
            NavigationWorldManager.Instance.OnNavigationReady += HandleNavigationReady;
        }
    }

    private void HandleNavigationReady()
    {
        if (NavigationWorldManager.Instance != null)
        {
            NavigationWorldManager.Instance.OnNavigationReady -= HandleNavigationReady;
        }
        SpawnInitialPopulation();
    }

    public void SpawnInitialPopulation()
    {
        if (!_isInitialized || config == null || !config.enablePedestrians) return;

        var registry = CityBuildingRegistry.Instance;
        if (registry == null || registry.Count == 0) return;

        int buildingCount = registry.Count;
        // Formula: N = min(N_max, N_base + d * B)
        _targetPopulation = Mathf.Min(
            config.maxActivePedestrians,
            Mathf.RoundToInt(config.populationBase + config.populationDensity * buildingCount)
        );

        Debug.Log($"[CityPopulationDirector] Spawning initial population: {_targetPopulation} pedestrians for {buildingCount} buildings.");

        for (int i = 0; i < _targetPopulation; i++)
        {
            CityBuilding startBuilding = registry.GetRandomBuilding(_rng, pedestrianEligibleOnly: true);
            if (startBuilding == null) continue;

            SpawnPedestrianFromBuilding(startBuilding);
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;

        float now = Time.time;

        // Process virtual occupants inside buildings ready to emerge
        for (int i = _virtualOccupants.Count - 1; i >= 0; i--)
        {
            var entry = _virtualOccupants[i];
            if (now >= entry.ExitTime)
            {
                _virtualOccupants.RemoveAt(i);
                if (entry.CurrentBuilding != null)
                {
                    entry.CurrentBuilding.portal?.UnregisterOccupant(entry.NpcId);
                    EmergePedestrian(entry);
                }
            }
        }
    }

    private PedestrianNpc GetOrCreatePedestrian(string id, float speed, int colorIndex)
    {
        PedestrianNpc npc;
        if (_pool.Count > 0)
        {
            npc = _pool.Dequeue();
            npc.gameObject.SetActive(true);
        }
        else
        {
            GameObject npcGo = new GameObject($"Pedestrian_{id}");
            npcGo.transform.SetParent(transform, true);

            var agent = npcGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.speed = speed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.35f;
            agent.height = 1.65f;
            agent.radius = 0.35f;

            GameObject visualInst = null;
            if (characterVisualPrefab != null)
            {
                visualInst = Instantiate(characterVisualPrefab);
                visualInst.name = "CharacterVisual";
                visualInst.SetActive(true);
            }

            npc = npcGo.AddComponent<PedestrianNpc>();
            npc.Initialize(id, visualInst, speed, colorIndex);
            npc.OnEnteredBuilding += HandlePedestrianEnteredBuilding;
        }

        return npc;
    }

    private void SpawnPedestrianFromBuilding(CityBuilding building)
    {
        if (building == null) return;

        string id = $"ped_{++_pedestrianIdCounter}";
        float speed = 2.0f + (float)_rng.NextDouble() * 0.8f;
        int colorIdx = _rng.Next(0, 10);

        PedestrianNpc npc = GetOrCreatePedestrian(id, speed, colorIdx);
        _activePedestrians.Add(npc);
        _activePedestriansCount = _activePedestrians.Count;

        Vector3 spawnPos = building.sidewalkApproachAnchor != null ? building.sidewalkApproachAnchor.position : building.entrancePosition;
        Quaternion spawnRot = Quaternion.LookRotation(building.entranceDirection, Vector3.up);

        npc.SpawnAt(spawnPos, spawnRot);

        // Pick a destination building
        CityBuilding targetBuilding = CityBuildingRegistry.Instance?.GetRandomBuilding(_rng, excludeId: building.buildingId, pedestrianEligibleOnly: true);
        if (targetBuilding != null)
        {
            float dwell = 6f + (float)_rng.NextDouble() * 8f;
            npc.AssignTravelToBuilding(targetBuilding, dwell);
        }
    }

    private void EmergePedestrian(VirtualOccupantEntry entry)
    {
        PedestrianNpc npc = GetOrCreatePedestrian(entry.NpcId, entry.WalkSpeed, entry.ColorIndex);
        _activePedestrians.Add(npc);
        _activePedestriansCount = _activePedestrians.Count;

        Vector3 spawnPos = entry.CurrentBuilding.sidewalkApproachAnchor != null
            ? entry.CurrentBuilding.sidewalkApproachAnchor.position
            : entry.CurrentBuilding.entrancePosition;
        Quaternion spawnRot = Quaternion.LookRotation(entry.CurrentBuilding.entranceDirection, Vector3.up);

        npc.SpawnAt(spawnPos, spawnRot);

        CityBuilding target = CityBuildingRegistry.Instance?.GetRandomBuilding(_rng, excludeId: entry.CurrentBuilding.buildingId, pedestrianEligibleOnly: true);
        if (target != null)
        {
            float dwell = 6f + (float)_rng.NextDouble() * 8f;
            npc.AssignTravelToBuilding(target, dwell);
        }
    }

    private void HandlePedestrianEnteredBuilding(PedestrianNpc npc, CityBuilding building)
    {
        if (npc == null || building == null) return;

        _activePedestrians.Remove(npc);
        _activePedestriansCount = _activePedestrians.Count;

        VirtualOccupantEntry entry = new VirtualOccupantEntry
        {
            NpcId = npc.PedestrianId,
            CurrentBuilding = building,
            ExitTime = Time.time + (6f + (float)_rng.NextDouble() * 8f),
            WalkSpeed = npc.Motor != null && npc.Motor.Agent != null ? npc.Motor.Agent.speed : 2.2f,
            ColorIndex = _rng.Next(0, 10)
        };

        _virtualOccupants.Add(entry);

        npc.gameObject.SetActive(false);
        _pool.Enqueue(npc);
    }

    /// <summary>
    /// Recruits an eligible city pedestrian to visit the pizzeria as a dine-in customer.
    /// Preserves the character identity, visual model, and appearance.
    /// </summary>
    public bool TryRecruitPedestrianForPizzeria(Vector3 pizzeriaEntrance, out PedestrianNpc recruitedNpc)
    {
        recruitedNpc = null;
        if (_activePedestrians.Count == 0)
        {
            // If no active pedestrians are walking, spawn one from a nearby building
            var nearBldg = CityBuildingRegistry.Instance?.GetNearestBuilding(pizzeriaEntrance, b => b.canAdmitPedestrians);
            if (nearBldg != null)
            {
                SpawnPedestrianFromBuilding(nearBldg);
            }
        }

        // Find nearest traveling pedestrian not currently at the pizzeria
        float bestDistSqr = float.MaxValue;
        for (int i = 0; i < _activePedestrians.Count; i++)
        {
            var p = _activePedestrians[i];
            if (p == null || p.State == PedestrianState.VisitingPizzeria) continue;

            float d2 = (p.transform.position - pizzeriaEntrance).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                recruitedNpc = p;
            }
        }

        if (recruitedNpc != null)
        {
            recruitedNpc.OnReachedPizzeria -= HandlePedestrianReachedPizzeria;
            recruitedNpc.OnReachedPizzeria += HandlePedestrianReachedPizzeria;
            recruitedNpc.AssignVisitPizzeria(pizzeriaEntrance);
            return true;
        }

        return false;
    }

    private void HandlePedestrianReachedPizzeria(PedestrianNpc npc)
    {
        if (npc == null) return;
        npc.OnReachedPizzeria -= HandlePedestrianReachedPizzeria;
        _activePedestrians.Remove(npc);
        _activePedestriansCount = _activePedestrians.Count;
        npc.gameObject.SetActive(false);
        _pool.Enqueue(npc);
    }

    /// <summary>
    /// Returns a satisfied or leaving customer back to the city pedestrian population.
    /// The character continues walking away to a random building, respecting population limits.
    /// </summary>
    public void ReturnCustomerToCityPopulation(CustomerNpcView customerView)
    {
        if (customerView == null) return;

        int maxLimit = config != null ? config.maxActivePedestrians : 20;
        if (_activePedestrians.Count >= maxLimit)
        {
            Destroy(customerView.gameObject);
            return;
        }

        int colorIdx = customerView.Appearance != null ? customerView.CurrentState.VisualColorIndex : 0;
        GameObject visualObj = customerView.VisualController != null ? customerView.VisualController.gameObject : null;

        var agent = customerView.Agent;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
        }

        Destroy(customerView); // Remove customer view component, convert to PedestrianNpc

        string id = $"ped_{++_pedestrianIdCounter}";
        var npc = customerView.gameObject.AddComponent<PedestrianNpc>();
        npc.Initialize(id, visualObj, 2.2f, colorIdx);
        npc.OnEnteredBuilding -= HandlePedestrianEnteredBuilding;
        npc.OnEnteredBuilding += HandlePedestrianEnteredBuilding;

        _activePedestrians.Add(npc);
        _activePedestriansCount = _activePedestrians.Count;

        var targetBldg = CityBuildingRegistry.Instance?.GetRandomBuilding(_rng, pedestrianEligibleOnly: true);
        if (targetBldg != null)
        {
            npc.AssignTravelToBuilding(targetBldg, 8f);
        }
        else
        {
            _activePedestrians.Remove(npc);
            _activePedestriansCount = _activePedestrians.Count;
            Destroy(npc.gameObject);
        }
    }

    public void ClearPopulation()
    {
        for (int i = 0; i < _activePedestrians.Count; i++)
        {
            if (_activePedestrians[i] != null) Destroy(_activePedestrians[i].gameObject);
        }
        _activePedestrians.Clear();

        while (_pool.Count > 0)
        {
            var n = _pool.Dequeue();
            if (n != null) Destroy(n.gameObject);
        }

        _virtualOccupants.Clear();
        _activePedestriansCount = 0;
        _isInitialized = false;
    }
}

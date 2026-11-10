using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry of all buildings generated in the city.
/// Decouples building indexing from GameObject hierarchy or naming conventions.
/// Provides deterministic queries for pedestrian spawners, routines, and delivery systems.
/// </summary>
public class CityBuildingRegistry : MonoBehaviour
{
    public static CityBuildingRegistry Instance { get; private set; }

    private readonly Dictionary<string, CityBuilding> _buildingsById = new Dictionary<string, CityBuilding>(StringComparer.Ordinal);
    private readonly List<CityBuilding> _allBuildings = new List<CityBuilding>();
    private readonly List<CityBuilding> _pedestrianEligible = new List<CityBuilding>();
    private readonly List<CityBuilding> _deliveryEligible = new List<CityBuilding>();

    public IReadOnlyList<CityBuilding> AllBuildings => _allBuildings;
    public IReadOnlyList<CityBuilding> PedestrianEligibleBuildings => _pedestrianEligible;
    public IReadOnlyList<CityBuilding> DeliveryEligibleBuildings => _deliveryEligible;
    public int Count => _allBuildings.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Clear();
    }

    public static CityBuildingRegistry EnsureInstance()
    {
        if (Instance != null) return Instance;
        Instance = UnityEngine.Object.FindFirstObjectByType<CityBuildingRegistry>();
        if (Instance == null)
        {
            GameObject go = new GameObject("CityBuildingRegistry");
            Instance = go.AddComponent<CityBuildingRegistry>();
        }
        return Instance;
    }

    public void Register(CityBuilding building)
    {
        if (building == null) return;
        if (string.IsNullOrEmpty(building.buildingId))
        {
            building.buildingId = $"bldg_{building.GetInstanceID()}";
        }

        if (_buildingsById.ContainsKey(building.buildingId))
        {
            _buildingsById[building.buildingId] = building;
            return;
        }

        _buildingsById[building.buildingId] = building;
        _allBuildings.Add(building);

        if (building.canAdmitPedestrians)
        {
            _pedestrianEligible.Add(building);
        }

        if (building.canHaveDeliveryPoint)
        {
            _deliveryEligible.Add(building);
        }
    }

    public void Unregister(CityBuilding building)
    {
        if (building == null || string.IsNullOrEmpty(building.buildingId)) return;
        if (_buildingsById.Remove(building.buildingId))
        {
            _allBuildings.Remove(building);
            _pedestrianEligible.Remove(building);
            _deliveryEligible.Remove(building);
        }
    }

    public void Clear()
    {
        _buildingsById.Clear();
        _allBuildings.Clear();
        _pedestrianEligible.Clear();
        _deliveryEligible.Clear();
    }

    public CityBuilding GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _buildingsById.TryGetValue(id, out var b);
        return b;
    }

    public CityBuilding GetRandomBuilding(System.Random rng, ZoneType? zone = null, string excludeId = null, bool pedestrianEligibleOnly = true)
    {
        var list = pedestrianEligibleOnly ? _pedestrianEligible : _allBuildings;
        if (list.Count == 0) return null;

        List<CityBuilding> candidates = new List<CityBuilding>();
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b == null) continue;
            if (excludeId != null && b.buildingId == excludeId) continue;
            if (zone.HasValue && b.zone != zone.Value) continue;
            candidates.Add(b);
        }

        if (candidates.Count == 0)
        {
            // Relax zone constraint if none found
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null) continue;
                if (excludeId != null && b.buildingId == excludeId) continue;
                candidates.Add(b);
            }
        }

        if (candidates.Count == 0) return null;
        int idx = rng != null ? rng.Next(candidates.Count) : UnityEngine.Random.Range(0, candidates.Count);
        return candidates[idx];
    }

    public CityBuilding GetNearestBuilding(Vector3 position, Func<CityBuilding, bool> predicate = null)
    {
        CityBuilding best = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < _allBuildings.Count; i++)
        {
            var b = _allBuildings[i];
            if (b == null) continue;
            if (predicate != null && !predicate(b)) continue;

            float d2 = (b.transform.position - position).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                best = b;
            }
        }

        return best;
    }
}

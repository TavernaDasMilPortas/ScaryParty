using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Manages random city events that block streets and create gameplay obstacles.
/// Server-authoritative with RPC sync.
/// </summary>
public class CityEventManager : NetworkBehaviour
{
    private float _minInterval = 10f;
    private float _maxInterval = 30f;
    private float _blockadeDuration = 20f;
    
    private List<ActiveEvent> _activeEvents = new List<ActiveEvent>();
    
    private CityGenerator _city;
    private StreetGraph _graph;
    private TrafficManager _trafficManager;
    private Transform _eventRoot;
    private System.Random _rng;
    private float _timer;
    private float _nextEventTime;
    
    private bool _initialized;

    private struct ActiveEvent
    {
        public int edgeId;
        public float endTime;
        public string eventType;
        public GameObject visual;
    }
    
    public void Initialize(StreetGraph graph, TrafficManager trafficManager,
        CityConfig config, int seed, Transform parent, CityGenerator city)
    {
        _activeEvents.Clear();
        _timer = 0f;
        _city = city;
        _graph = graph;
        _trafficManager = trafficManager;
        
        _minInterval = config.minEventInterval;
        _maxInterval = config.maxEventInterval;
        _blockadeDuration = config.blockadeDuration;
        
        _rng = new System.Random(seed);
        
        GameObject rootObj = new GameObject("__CityEvents__");
        rootObj.transform.SetParent(parent);
        _eventRoot = rootObj.transform;
        
        _nextEventTime = GetRandomInterval();
        
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || _city == null || !_city.IsServer || !_city.ClosuresSynchronized) return;

        _timer += Time.deltaTime;
        
        // Trigger new event
        if (_timer >= _nextEventTime)
        {
            TriggerRandomEvent();
            _timer = 0f;
            _nextEventTime = GetRandomInterval();
        }
        
        // Check event expiration
        float currentTime = Time.time;
        for (int i = _activeEvents.Count - 1; i >= 0; i--)
        {
            if (currentTime >= _activeEvents[i].endTime)
            {
                RemoveEvent(i);
            }
        }
    }

    private float GetRandomInterval()
    {
        return _minInterval + (float)_rng.NextDouble() * (_maxInterval - _minInterval);
    }

    private void TriggerRandomEvent()
    {
        if (_graph == null || _graph.edges == null || _graph.edges.Count == 0) return;

        // Pick random edge
        int edgeIndex = _rng.Next(0, _graph.edges.Count);
        StreetEdge edge = _graph.edges[edgeIndex];
        
        // Both event ownership and the graph must agree before creating a closure.
        if (IsEdgeBlocked(edgeIndex) || edge.isBlocked) return;

        string[] eventTypes = { "accident", "construction", "monster_attack" };
        string eventType = eventTypes[_rng.Next(0, eventTypes.Length)];
        
        _city.SetRoadBlocked(edgeIndex, true);
        
        if (_trafficManager != null)
        {
            _trafficManager.OnEdgeBlocked(edgeIndex);
        }

        float endTime = Time.time + _blockadeDuration;
        
        // Visuals on server
        GameObject visual = CreateEventVisual(eventType, edge);
        
        _activeEvents.Add(new ActiveEvent
        {
            edgeId = edgeIndex,
            endTime = endTime,
            eventType = eventType,
            visual = visual
        });

        // CityGenerator replicates this closure; clients render from its snapshot.
    }

    private void RemoveEvent(int index)
    {
        ActiveEvent ev = _activeEvents[index];
        
        _city.SetRoadBlocked(ev.edgeId, false);
        
        if (_trafficManager != null)
        {
            _trafficManager.OnEdgeUnblocked(ev.edgeId);
        }

        if (ev.visual != null) Destroy(ev.visual);
        
        // CityGenerator propagates the unblocked edge.
        
        _activeEvents.RemoveAt(index);
    }

    public void SynchronizeVisuals(IReadOnlyCollection<int> blockedEdges)
    {
        if (_graph == null || _eventRoot == null) return;
        var blocked = new HashSet<int>(blockedEdges);
        for (int i = _activeEvents.Count - 1; i >= 0; i--)
        {
            if (blocked.Contains(_activeEvents[i].edgeId)) continue;
            if (_activeEvents[i].visual != null) Destroy(_activeEvents[i].visual);
            _activeEvents.RemoveAt(i);
        }
        foreach (int edgeId in blocked)
        {
            if (edgeId < 0 || edgeId >= _graph.edges.Count || IsEdgeBlocked(edgeId)) continue;
            _activeEvents.Add(new ActiveEvent
            {
                edgeId = edgeId,
                endTime = float.PositiveInfinity,
                eventType = "construction",
                visual = CreateEventVisual("construction", _graph.edges[edgeId])
            });
        }
    }

    private bool IsEdgeBlocked(int edgeId)
    {
        foreach (var ev in _activeEvents)
        {
            if (ev.edgeId == edgeId) return true;
        }
        return false;
    }

    private GameObject CreateEventVisual(string eventType, StreetEdge edge)
    {
        Vector3 startPos = _graph.nodes[edge.nodeA].worldPosition;
        Vector3 endPos = _graph.nodes[edge.nodeB].worldPosition;
        Vector3 midPoint = (startPos + endPos) / 2f;
        
        GameObject visual = new GameObject($"Event_{eventType}");
        visual.transform.SetParent(_eventRoot);
        visual.transform.position = midPoint;
        
        GameObject model = null;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        
        Color eventColor;
        switch (eventType)
        {
            case "accident":
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.localScale = new Vector3(4f, 2f, 4f);
                eventColor = Color.yellow;
                break;
            case "construction":
                model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                model.transform.localScale = new Vector3(3f, 2f, 3f);
                eventColor = new Color(1f, 0.5f, 0f); // Orange
                break;
            case "monster_attack":
                model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                model.transform.localScale = new Vector3(5f, 5f, 5f);
                eventColor = new Color(0.5f, 0f, 0.5f); // Purple
                break;
            default:
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                eventColor = Color.red;
                break;
        }

        if (model != null)
        {
            model.transform.SetParent(visual.transform);
            model.transform.localPosition = Vector3.zero;
            Renderer rend = model.GetComponent<Renderer>();
            if (rend != null)
            {
                Material eventMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                    rend.sharedMaterial,
                    ScaryParty.VisualStyles.VisualSemanticRole.Emission,
                    ScaryParty.VisualStyles.VisualCategoryIds.Effects);
                if (eventMat != null) rend.sharedMaterial = eventMat;
                ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, 0, eventColor, mpb);
            }
        }

        return visual;
    }

}

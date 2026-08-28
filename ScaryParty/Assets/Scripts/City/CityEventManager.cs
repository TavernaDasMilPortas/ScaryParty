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
    
    private CityGraphPathfinder _pathfinder;
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
    
    public void Initialize(CityGraphPathfinder pathfinder, StreetGraph graph, 
        TrafficManager trafficManager, CityConfig config, int seed, Transform parent)
    {
        _pathfinder = pathfinder;
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
        if (!_initialized || !IsServer) return;

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
        
        // Ensure not already blocked
        if (IsEdgeBlocked(edgeIndex)) return;

        string[] eventTypes = { "accident", "construction", "monster_attack" };
        string eventType = eventTypes[_rng.Next(0, eventTypes.Length)];
        
        // Block edge in pathfinder
        if (_pathfinder != null)
            _pathfinder.BlockEdge(edgeIndex);
        
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

        TriggerEventClientRpc(edgeIndex, eventType, _blockadeDuration);
    }

    private void RemoveEvent(int index)
    {
        ActiveEvent ev = _activeEvents[index];
        
        if (_pathfinder != null)
            _pathfinder.UnblockEdge(ev.edgeId);
        
        if (_trafficManager != null)
        {
            _trafficManager.OnEdgeUnblocked(ev.edgeId);
        }

        if (ev.visual != null) Destroy(ev.visual);
        
        RemoveEventClientRpc(ev.edgeId);
        
        _activeEvents.RemoveAt(index);
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
        
        switch (eventType)
        {
            case "accident":
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.localScale = new Vector3(4f, 2f, 4f);
                mpb.SetColor("_BaseColor", Color.yellow);
                break;
            case "construction":
                model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                model.transform.localScale = new Vector3(3f, 2f, 3f);
                mpb.SetColor("_BaseColor", new Color(1f, 0.5f, 0f)); // Orange
                break;
            case "monster_attack":
                model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                model.transform.localScale = new Vector3(5f, 5f, 5f);
                mpb.SetColor("_BaseColor", new Color(0.5f, 0f, 0.5f)); // Purple
                break;
            default:
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mpb.SetColor("_BaseColor", Color.red);
                break;
        }

        if (model != null)
        {
            model.transform.SetParent(visual.transform);
            model.transform.localPosition = Vector3.zero;
            Renderer rend = model.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.SetPropertyBlock(mpb);
            }
        }

        return visual;
    }

    [ClientRpc]
    private void TriggerEventClientRpc(int edgeId, string eventType, float duration)
    {
        if (IsServer) return; // Server already created it
        
        if (_graph == null || edgeId < 0 || edgeId >= _graph.edges.Count) return;
        
        StreetEdge edge = _graph.edges[edgeId];
        GameObject visual = CreateEventVisual(eventType, edge);
        
        _activeEvents.Add(new ActiveEvent
        {
            edgeId = edgeId,
            endTime = Time.time + duration,
            eventType = eventType,
            visual = visual
        });
    }

    [ClientRpc]
    private void RemoveEventClientRpc(int edgeId)
    {
        if (IsServer) return;
        
        for (int i = 0; i < _activeEvents.Count; i++)
        {
            if (_activeEvents[i].edgeId == edgeId)
            {
                if (_activeEvents[i].visual != null)
                {
                    Destroy(_activeEvents[i].visual);
                }
                _activeEvents.RemoveAt(i);
                break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All shared data types for the procedural city generation system.
/// This is the single source of truth — all other city scripts reference these types.
/// </summary>

public enum ZoneType
{
    Residential,
    Commercial,
    Industrial,
    MonsterZone
}

public enum StreetType
{
    Avenue,
    Street,
    Alley
}

[Serializable]
public struct StreetNode
{
    public int id;
    public Vector3 worldPosition;
    public List<int> connectedEdges;
}

[Serializable]
public struct StreetEdge
{
    public int id;
    public int nodeA;
    public int nodeB;
    public float length;
    public bool isBlocked;
    public StreetType streetType;
}

[Serializable]
public struct BlockInfo
{
    public Vector3 worldCenter;
    public Vector3 size;
    public ZoneType zoneType;
    public List<int> deliveryPointIndices;

    /// <summary>
    /// Vertices of the block polygon boundary.
    /// </summary>
    public Vector3[] polygon;

    /// <summary>
    /// Calculated area of the block polygon in square meters.
    /// </summary>
    public float area;

    /// <summary>
    /// Whether this block contains the Pizzaria.
    /// </summary>
    public bool hasPizzaria;
}

[Serializable]
public struct EnemySpawnPoint
{
    public Vector3 position;
    public int blockIndex;
    public ZoneType zone;
}

[Serializable]
public struct StreetEvent
{
    public int affectedEdgeId;
    public float startTime;
    public float duration;
    public string eventType; 
}

[Serializable]
public class StreetGraph
{
    public List<StreetNode> nodes = new List<StreetNode>();
    public List<StreetEdge> edges = new List<StreetEdge>();
}

/// <summary>
/// Runtime data representing the generated city state.
/// </summary>
[Serializable]
public class CityData : ScriptableObject
{
    public StreetGraph streetGraph = new StreetGraph();
    public List<BlockInfo> blocks = new List<BlockInfo>();
    public Vector3 pizzariaPosition;
    public Vector3 bancadaPosition;

    /// <summary>
    /// Index into the blocks list indicating which block contains the Pizzaria.
    /// -1 if no block has been assigned yet.
    /// </summary>
    public int pizzariaBlockIndex = -1;

    public List<EnemySpawnPoint> enemySpawnPoints = new List<EnemySpawnPoint>();

    [SerializeField] private int _gridWidth;
    [SerializeField] private int _gridHeight;
    [SerializeField] private float _blockWidth;
    [SerializeField] private float _blockDepth;
    [SerializeField] private float _streetWidth;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;

    public void Initialize(int width, int height, float blockWidth, float blockDepth, float streetWidth)
    {
        _gridWidth = width;
        _gridHeight = height;
        _blockWidth = blockWidth;
        _blockDepth = blockDepth;
        _streetWidth = streetWidth;
        blocks = new List<BlockInfo>();
        pizzariaBlockIndex = -1;
        enemySpawnPoints.Clear();
    }

    public StreetNode GetNearestIntersection(Vector3 pos)
    {
        if (streetGraph.nodes.Count == 0) return default;

        StreetNode nearest = streetGraph.nodes[0];
        float minDist = Vector3.SqrMagnitude(nearest.worldPosition - pos);

        for (int i = 1; i < streetGraph.nodes.Count; i++)
        {
            float dist = Vector3.SqrMagnitude(streetGraph.nodes[i].worldPosition - pos);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = streetGraph.nodes[i];
            }
        }
        return nearest;
    }

    public bool IsStreetAt(Vector3 pos)
    {
        float spacingX = _blockWidth + _streetWidth;
        float spacingZ = _blockDepth + _streetWidth;

        float localX = pos.x % spacingX;
        if (localX < 0) localX += spacingX;
        float distToX = Mathf.Min(localX, spacingX - localX);

        float localZ = pos.z % spacingZ;
        if (localZ < 0) localZ += spacingZ;
        float distToZ = Mathf.Min(localZ, spacingZ - localZ);

        float halfStreet = _streetWidth / 2f;
        return distToX <= halfStreet || distToZ <= halfStreet;
    }

    public int BlockCount => blocks?.Count ?? 0;

    public int StreetCount => streetGraph?.edges?.Count ?? 0;

    [SerializeField] private int _deliveryPointCount;
    public int DeliveryPointCount
    {
        get => _deliveryPointCount;
        set => _deliveryPointCount = value;
    }
}

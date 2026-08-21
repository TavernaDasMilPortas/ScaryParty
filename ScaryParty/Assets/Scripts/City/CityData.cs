using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All shared data types for the procedural city generation system.
/// This is the single source of truth — all other city scripts reference these types.
/// </summary>

// ─────────────────────────────────────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────────────────────────────────────

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

// ─────────────────────────────────────────────────────────────────────────────
// Structs
// ─────────────────────────────────────────────────────────────────────────────

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
    public int gridX;
    public int gridY;
    public Vector3 worldCenter;
    public Vector3 size;
    public ZoneType zoneType;
    public List<int> deliveryPointIndices;
}

// ─────────────────────────────────────────────────────────────────────────────
// StreetGraph
// ─────────────────────────────────────────────────────────────────────────────

[Serializable]
public class StreetGraph
{
    public List<StreetNode> nodes = new List<StreetNode>();
    public List<StreetEdge> edges = new List<StreetEdge>();
}

// ─────────────────────────────────────────────────────────────────────────────
// CityData — runtime data for the generated city
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Runtime data representing the generated city state.
/// Not a MonoBehaviour — instantiated by CityGenerator.
/// </summary>
[Serializable]
public class CityData : ScriptableObject
{
    public StreetGraph streetGraph = new StreetGraph();
    public BlockInfo[] blocks;
    public Vector3 pizzariaPosition;
    public Vector3 bancadaPosition;

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
        blocks = new BlockInfo[width * height];
    }

    /// <summary>
    /// Gets the block at the specified grid coordinates.
    /// </summary>
    public BlockInfo GetBlockAt(int x, int y)
    {
        if (x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight)
        {
            return blocks[x + y * _gridWidth];
        }
        throw new IndexOutOfRangeException($"Block coordinates ({x}, {y}) out of bounds.");
    }

    /// <summary>
    /// Sets the block at the specified grid coordinates.
    /// </summary>
    public void SetBlockAt(int x, int y, BlockInfo block)
    {
        if (x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight)
        {
            blocks[x + y * _gridWidth] = block;
        }
    }

    /// <summary>
    /// Finds the nearest street intersection node to a given world position.
    /// </summary>
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

    /// <summary>
    /// Checks if a world position is located on a street (approximate).
    /// </summary>
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

    /// <summary>
    /// Returns total number of blocks.
    /// </summary>
    public int BlockCount => blocks?.Length ?? 0;

    /// <summary>
    /// Returns total number of street segments.
    /// </summary>
    public int StreetCount => streetGraph?.edges?.Count ?? 0;

    [SerializeField] private int _deliveryPointCount;

    /// <summary>
    /// Returns total number of delivery points across all blocks.
    /// </summary>
    public int DeliveryPointCount
    {
        get => _deliveryPointCount;
        set => _deliveryPointCount = value;
    }
}

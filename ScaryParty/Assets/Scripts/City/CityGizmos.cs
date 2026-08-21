using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Debug visualization for the City Generator.
/// Hooks into CityGenerator to draw the generated map elements.
/// </summary>
public class CityGizmos : MonoBehaviour
{
    [Header("Gizmo Toggles")]
    [Tooltip("Show the street grid")]
    public bool showGridGizmos = true;
    
    [Tooltip("Show the generated zones and delivery points")]
    public bool showZones = true;
    
    [Tooltip("Show pathfinding debug paths")]
    public bool showPathDebug = true;

    private CityGenerator _generator;

    private void Awake()
    {
        _generator = GetComponent<CityGenerator>();
    }

    private void OnDrawGizmos()
    {
        if (_generator == null)
            _generator = GetComponent<CityGenerator>();

        if (_generator == null || _generator.CityData == null)
            return;

        if (showGridGizmos)
        {
            DrawGrid();
        }

        if (showZones)
        {
            DrawZones();
        }
    }

    private void DrawGrid()
    {
        var data = _generator.CityData;
        if (data.streetGraph == null || data.streetGraph.nodes == null) return;

        // Draw Nodes
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        foreach (var node in data.streetGraph.nodes)
        {
            Gizmos.DrawSphere(node.worldPosition, 1f);
        }

        // Draw Edges
        foreach (var edge in data.streetGraph.edges)
        {
            Gizmos.color = edge.isBlocked ? Color.red : Color.green;
            Vector3 pA = data.streetGraph.nodes[edge.nodeA].worldPosition;
            Vector3 pB = data.streetGraph.nodes[edge.nodeB].worldPosition;
            
            // Draw slightly elevated to avoid Z-fighting with geometry
            Gizmos.DrawLine(pA + Vector3.up * 0.2f, pB + Vector3.up * 0.2f);
        }
    }

    private void DrawZones()
    {
        var data = _generator.CityData;
        if (data.blocks == null) return;

        foreach (var block in data.blocks)
        {
            switch (block.zoneType)
            {
                case ZoneType.Residential: Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.3f); break;
                case ZoneType.Commercial: Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.3f); break;
                case ZoneType.Industrial: Gizmos.color = new Color(0.8f, 0.4f, 0.1f, 0.3f); break;
                case ZoneType.MonsterZone: Gizmos.color = new Color(0.6f, 0.1f, 0.6f, 0.3f); break;
            }

            // Draw block area
            Gizmos.DrawCube(block.worldCenter + Vector3.up * 0.5f, new Vector3(block.size.x, 1f, block.size.z));
            Gizmos.DrawWireCube(block.worldCenter + Vector3.up * 0.5f, new Vector3(block.size.x, 1f, block.size.z));

            // Draw Delivery Points (abstract representation based on count)
            if (block.deliveryPointIndices != null && block.deliveryPointIndices.Count > 0)
            {
                Gizmos.color = Color.magenta;
                for (int i = 0; i < block.deliveryPointIndices.Count; i++)
                {
                    Gizmos.DrawSphere(block.worldCenter + new Vector3(i * 2f - block.deliveryPointIndices.Count, 3f, i * 2f - block.deliveryPointIndices.Count), 1f);
                }
            }
        }
    }
}

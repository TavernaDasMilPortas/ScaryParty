using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Debug visualization for the City Generator.
/// Hooks into CityGenerator to draw the generated map elements
/// including organic block polygons, enemy spawn points, and active events.
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

    [Tooltip("Show block polygon outlines (organic boundaries)")]
    public bool showBlockPolygons = true;

    [Tooltip("Show enemy spawn points")]
    public bool showEnemySpawnPoints = true;

    [Tooltip("Show the pizzaria location")]
    public bool showPizzaria = true;

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

        if (showBlockPolygons)
        {
            DrawBlockPolygons();
        }

        if (showEnemySpawnPoints)
        {
            DrawEnemySpawnPoints();
        }

        if (showPizzaria)
        {
            DrawPizzaria();
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

            // Vary line thickness visually by street type
            Vector3 pA = data.streetGraph.nodes[edge.nodeA].worldPosition;
            Vector3 pB = data.streetGraph.nodes[edge.nodeB].worldPosition;
            
            // Draw slightly elevated to avoid Z-fighting with geometry
            Vector3 up = Vector3.up * 0.2f;
            Gizmos.DrawLine(pA + up, pB + up);

            // Draw thicker lines for avenues
            if (edge.streetType == StreetType.Avenue)
            {
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
                Vector3 offset = Vector3.Cross((pB - pA).normalized, Vector3.up) * 0.3f;
                Gizmos.DrawLine(pA + up + offset, pB + up + offset);
                Gizmos.DrawLine(pA + up - offset, pB + up - offset);
            }
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

    private void DrawBlockPolygons()
    {
        var data = _generator.CityData;
        if (data.blocks == null) return;

        foreach (var block in data.blocks)
        {
            if (block.polygon == null || block.polygon.Length < 3) continue;

            // Choose color by zone
            switch (block.zoneType)
            {
                case ZoneType.Residential: Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f); break;
                case ZoneType.Commercial: Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f); break;
                case ZoneType.Industrial: Gizmos.color = new Color(0.9f, 0.5f, 0.2f, 0.6f); break;
                case ZoneType.MonsterZone: Gizmos.color = new Color(0.7f, 0.2f, 0.8f, 0.6f); break;
            }

            // Draw polygon outline
            Vector3 up = Vector3.up * 0.4f;
            for (int i = 0; i < block.polygon.Length; i++)
            {
                int next = (i + 1) % block.polygon.Length;
                Gizmos.DrawLine(block.polygon[i] + up, block.polygon[next] + up);
            }

            // Mark pizzaria block specially
            if (block.hasPizzaria)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
                for (int i = 0; i < block.polygon.Length; i++)
                {
                    int next = (i + 1) % block.polygon.Length;
                    Gizmos.DrawLine(block.polygon[i] + up * 2, block.polygon[next] + up * 2);
                }
            }
        }
    }

    private void DrawEnemySpawnPoints()
    {
        var data = _generator.CityData;
        if (data.enemySpawnPoints == null) return;

        foreach (var spawnPoint in data.enemySpawnPoints)
        {
            // Purple for MonsterZone, dark orange for Industrial
            Gizmos.color = spawnPoint.zone == ZoneType.MonsterZone
                ? new Color(0.7f, 0.1f, 0.9f, 0.8f)
                : new Color(0.9f, 0.5f, 0.1f, 0.6f);

            Gizmos.DrawWireSphere(spawnPoint.position + Vector3.up * 1f, 1.5f);
            Gizmos.DrawIcon(spawnPoint.position + Vector3.up * 3f, "sv_icon_dot14_pix16_gizmo", true);
        }
    }

    private void DrawPizzaria()
    {
        var data = _generator.CityData;
        if (data.pizzariaPosition == Vector3.zero) return;

        // Draw a big red diamond at the pizzaria position
        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(data.pizzariaPosition + Vector3.up * 5f, 3f);
        Gizmos.DrawLine(data.pizzariaPosition, data.pizzariaPosition + Vector3.up * 8f);

        // Draw bancada position
        if (data.bancadaPosition != Vector3.zero)
        {
            Gizmos.color = new Color(0.6f, 0.3f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(data.bancadaPosition + Vector3.up * 0.5f, new Vector3(3f, 1f, 1.5f));
        }
    }
}

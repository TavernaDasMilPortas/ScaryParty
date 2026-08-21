using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the street grid geometry and navigation graph for the city.
/// Creates intersections as nodes, street segments as edges, and 3D geometry for roads/sidewalks.
/// </summary>
public class StreetGridGenerator : MonoBehaviour
{
    /// <summary>
    /// Generates the street grid graph and associated 3D geometry.
    /// </summary>
    /// <param name="gridW">Grid width (number of intersections horizontally).</param>
    /// <param name="gridH">Grid height (number of intersections vertically).</param>
    /// <param name="blockW">Width of a city block.</param>
    /// <param name="blockD">Depth of a city block.</param>
    /// <param name="streetW">Width of the streets.</param>
    /// <param name="sidewalkW">Width of the sidewalks.</param>
    /// <param name="removalChance">Chance to remove a street segment to create variety.</param>
    /// <param name="rng">Random number generator for deterministic generation.</param>
    /// <param name="parent">Parent transform for generated geometry.</param>
    /// <param name="streetMat">Material for road surfaces.</param>
    /// <param name="sidewalkMat">Material for sidewalks.</param>
    /// <returns>The generated StreetGraph containing nodes and edges.</returns>
    public StreetGraph Generate(int gridW, int gridH, float blockW, float blockD, float streetW,
        float sidewalkW, float removalChance, System.Random rng, Transform parent,
        Material streetMat, Material sidewalkMat)
    {
        // Fallback materials
        if (streetMat == null)
            streetMat = CreateFallbackMaterial(Color.gray);
        if (sidewalkMat == null)
            sidewalkMat = CreateFallbackMaterial(new Color(0.85f, 0.85f, 0.85f));

        StreetGraph graph = new StreetGraph();
        float spacingX = blockW + streetW;
        float spacingZ = blockD + streetW;

        // 1. Generate all intersection nodes
        for (int y = 0; y < gridH; y++)
        {
            for (int x = 0; x < gridW; x++)
            {
                StreetNode node = new StreetNode
                {
                    id = x + y * gridW,
                    worldPosition = new Vector3(x * spacingX, 0, y * spacingZ),
                    connectedEdges = new List<int>()
                };
                graph.nodes.Add(node);
            }
        }

        List<StreetEdge> potentialEdges = new List<StreetEdge>();
        int edgeIdCounter = 0;

        // 2. Generate horizontal edges
        for (int y = 0; y < gridH; y++)
        {
            for (int x = 0; x < gridW - 1; x++)
            {
                potentialEdges.Add(new StreetEdge
                {
                    id = edgeIdCounter++,
                    nodeA = x + y * gridW,
                    nodeB = (x + 1) + y * gridW,
                    length = spacingX,
                    isBlocked = false,
                    streetType = StreetType.Street
                });
            }
        }

        // 3. Generate vertical edges
        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH - 1; y++)
            {
                potentialEdges.Add(new StreetEdge
                {
                    id = edgeIdCounter++,
                    nodeA = x + y * gridW,
                    nodeB = x + (y + 1) * gridW,
                    length = spacingZ,
                    isBlocked = false,
                    streetType = StreetType.Street
                });
            }
        }

        // 4. Shuffle edges (Fisher-Yates) for randomized removal
        int n = potentialEdges.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var value = potentialEdges[k];
            potentialEdges[k] = potentialEdges[n];
            potentialEdges[n] = value;
        }

        int totalNodes = gridW * gridH;
        List<StreetEdge> activeEdges = new List<StreetEdge>(potentialEdges);

        // 5. Remove edges based on removalChance while maintaining full connectivity (BFS)
        for (int i = activeEdges.Count - 1; i >= 0; i--)
        {
            if (rng.NextDouble() < removalChance)
            {
                var edgeToRemove = activeEdges[i];
                activeEdges.RemoveAt(i);

                if (!IsGraphConnected(totalNodes, activeEdges))
                {
                    activeEdges.Add(edgeToRemove);
                }
            }
        }

        // 6. Build final graph structure
        graph.edges = activeEdges;
        for (int i = 0; i < graph.edges.Count; i++)
        {
            var edge = graph.edges[i];
            edge.id = i;
            graph.edges[i] = edge;

            graph.nodes[edge.nodeA].connectedEdges.Add(i);
            graph.nodes[edge.nodeB].connectedEdges.Add(i);
        }

        // 7. Generate 3D geometry
        foreach (var edge in graph.edges)
        {
            Vector3 posA = graph.nodes[edge.nodeA].worldPosition;
            Vector3 posB = graph.nodes[edge.nodeB].worldPosition;
            Vector3 center = (posA + posB) / 2f;
            bool isHorizontal = Mathf.Abs(posA.z - posB.z) < 0.01f;

            Vector2 roadSize = isHorizontal
                ? new Vector2(edge.length, streetW)
                : new Vector2(streetW, edge.length);

            string edgeName = $"Street_{(isHorizontal ? "H" : "V")}_{edge.nodeA}_{edge.nodeB}";
            GameObject segmentParent = new GameObject(edgeName);
            segmentParent.transform.position = center;
            segmentParent.transform.SetParent(parent);

            // Road surface
            GameObject road = CreateQuad("Road", Vector3.zero, roadSize, segmentParent.transform, streetMat);
            BoxCollider roadCollider = road.AddComponent<BoxCollider>();
            roadCollider.size = new Vector3(roadSize.x, 0.1f, roadSize.y);
            roadCollider.center = new Vector3(0, -0.05f, 0);

            // Sidewalks
            if (sidewalkW > 0)
            {
                float yOffset = 0.15f;
                if (isHorizontal)
                {
                    float zOffset = (streetW / 2f) + (sidewalkW / 2f);
                    CreateQuad("Sidewalk_N", new Vector3(0, yOffset, zOffset), new Vector2(edge.length, sidewalkW), segmentParent.transform, sidewalkMat);
                    CreateQuad("Sidewalk_S", new Vector3(0, yOffset, -zOffset), new Vector2(edge.length, sidewalkW), segmentParent.transform, sidewalkMat);
                }
                else
                {
                    float xOffset = (streetW / 2f) + (sidewalkW / 2f);
                    CreateQuad("Sidewalk_E", new Vector3(xOffset, yOffset, 0), new Vector2(sidewalkW, edge.length), segmentParent.transform, sidewalkMat);
                    CreateQuad("Sidewalk_W", new Vector3(-xOffset, yOffset, 0), new Vector2(sidewalkW, edge.length), segmentParent.transform, sidewalkMat);
                }
            }
        }

        return graph;
    }

    /// <summary>
    /// BFS to validate all nodes remain reachable.
    /// </summary>
    private bool IsGraphConnected(int totalNodes, List<StreetEdge> edges)
    {
        if (totalNodes <= 1) return true;

        List<int>[] adjacencyList = new List<int>[totalNodes];
        for (int i = 0; i < totalNodes; i++)
            adjacencyList[i] = new List<int>();

        foreach (var edge in edges)
        {
            adjacencyList[edge.nodeA].Add(edge.nodeB);
            adjacencyList[edge.nodeB].Add(edge.nodeA);
        }

        bool[] visited = new bool[totalNodes];
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(0);
        visited[0] = true;
        int visitedCount = 1;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int neighbor in adjacencyList[current])
            {
                if (!visited[neighbor])
                {
                    visited[neighbor] = true;
                    visitedCount++;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visitedCount == totalNodes;
    }

    /// <summary>
    /// Creates a flat quad mesh on the XZ plane at the given local position.
    /// </summary>
    private GameObject CreateQuad(string name, Vector3 localPosition, Vector2 size, Transform parent, Material mat)
    {
        GameObject quad = new GameObject(name);
        quad.transform.SetParent(parent);
        quad.transform.localPosition = localPosition;

        MeshFilter mf = quad.AddComponent<MeshFilter>();
        MeshRenderer mr = quad.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh { name = name + "_Mesh" };

        float halfX = size.x * 0.5f;
        float halfZ = size.y * 0.5f;

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-halfX, 0, -halfZ),
            new Vector3(halfX, 0, -halfZ),
            new Vector3(-halfX, 0, halfZ),
            new Vector3(halfX, 0, halfZ)
        };

        int[] triangles = new int[] { 0, 2, 1, 2, 3, 1 };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        mf.mesh = mesh;
        mr.sharedMaterial = mat;

        return quad;
    }

    private Material CreateFallbackMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }
}

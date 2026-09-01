using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the street grid using a Hierarchical Road Network approach.
/// Avenues form the macro-structure (Chunks), and secondary streets fill the blocks.
/// Dead-ends are aggressively snapped or pruned to ensure 100% block coverage.
/// </summary>
public class StreetGridGenerator : MonoBehaviour
{
    public StreetGraph Generate(CityConfig config, System.Random rng, Transform parent,
        Material streetMat, Material sidewalkMat, out List<Vector3[]> blockPolygons)
    {
        if (streetMat == null) streetMat = CreateFallbackMaterial(Color.gray);
        if (sidewalkMat == null) sidewalkMat = CreateFallbackMaterial(new Color(0.85f, 0.85f, 0.85f));

        StreetGraph graph = new StreetGraph();
        
        int targetNodes = config.gridWidth * config.gridHeight;
        if (targetNodes < 20) targetNodes = 100;
        int maxNodes = targetNodes;

        List<StreetNode> nodes = new List<StreetNode>();
        List<StreetEdge> edges = new List<StreetEdge>();

        nodes.Add(new StreetNode { id = 0, worldPosition = Vector3.zero, connectedEdges = new List<int>() });
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(0);

        int failCounter = 0;

        // Define two intersecting grids for the "São Paulo" fractured look
        float[] globalAngles = new float[] {
            0f,
            Mathf.PI * 0.5f,
            (float)(rng.NextDouble() * Mathf.PI * 0.5f), 
            0f
        };
        globalAngles[3] = globalAngles[2] + Mathf.PI * 0.5f; 

        // 1. Hierarchical Branching (Avenues vs Streets)
        while (nodes.Count < maxNodes && failCounter < 1000)
        {
            if (queue.Count == 0)
            {
                List<int> lowDegree = new List<int>();
                for (int j = 0; j < nodes.Count; j++) 
                    if (nodes[j].connectedEdges.Count < 3) lowDegree.Add(j);
                
                if (lowDegree.Count > 0)
                    queue.Enqueue(lowDegree[rng.Next(lowDegree.Count)]);
                else
                    queue.Enqueue(rng.Next(nodes.Count));
                
                failCounter++;
            }

            int uIdx = queue.Dequeue();
            StreetNode u = nodes[uIdx];

            int numBranches = rng.Next(1, 4); 
            bool addedAny = false;

            for (int i = 0; i < numBranches; i++)
            {
                if (nodes.Count >= maxNodes) break;

                // Avenues spawn mostly from the center or other avenues
                bool isAvenue = (uIdx == 0 || (rng.NextDouble() < 0.2f));

                float baseAngle = globalAngles[rng.Next(globalAngles.Length)];
                float angleDeviation = (float)((rng.NextDouble() - 0.5) * 0.2f); // slight deviation
                float angle = baseAngle + angleDeviation;
                
                // Avenidas são mais longas para delimitar os Chunks. Ruas locais são menores.
                float length = isAvenue 
                    ? config.maxStreetBranchLength * 1.5f 
                    : config.minStreetBranchLength + (float)(rng.NextDouble() * (config.maxStreetBranchLength - config.minStreetBranchLength));

                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * length;
                Vector3 newPos = u.worldPosition + offset;

                // Agressive Snapping: If a local street comes close to an avenue, force connection!
                int snapNodeIdx = -1;
                float snapDist = isAvenue ? 30f : 40f; 
                float minDist = float.MaxValue;
                
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (j == uIdx) continue;
                    float d = Vector3.Distance(newPos, nodes[j].worldPosition);
                    if (d < snapDist && d < minDist)
                    {
                        minDist = d;
                        snapNodeIdx = j;
                    }
                }

                int vIdx = -1;
                Vector3 finalPos = newPos;
                if (snapNodeIdx != -1)
                {
                    vIdx = snapNodeIdx;
                    finalPos = nodes[vIdx].worldPosition;
                    
                    bool edgeExists = false;
                    foreach (int eIdx in u.connectedEdges)
                    {
                        var edge = edges[eIdx];
                        if ((edge.nodeA == uIdx && edge.nodeB == vIdx) || (edge.nodeA == vIdx && edge.nodeB == uIdx))
                        {
                            edgeExists = true;
                            break;
                        }
                    }
                    if (edgeExists) continue;
                }

                // Intersection and proximity checks
                bool isValid = true;
                Vector2 pA = new Vector2(u.worldPosition.x, u.worldPosition.z);
                Vector2 pB = new Vector2(finalPos.x, finalPos.z);
                
                foreach (var edge in edges)
                {
                    Vector2 pC = new Vector2(nodes[edge.nodeA].worldPosition.x, nodes[edge.nodeA].worldPosition.z);
                    Vector2 pD = new Vector2(nodes[edge.nodeB].worldPosition.x, nodes[edge.nodeB].worldPosition.z);

                    if (SegmentsIntersect(pA, pB, pC, pD))
                    {
                        isValid = false; 
                        break;
                    }
                }

                if (!isValid) continue;

                foreach (var node in nodes)
                {
                    if (node.id == uIdx) continue;
                    if (vIdx != -1 && node.id == vIdx) continue;

                    Vector2 pP = new Vector2(node.worldPosition.x, node.worldPosition.z);
                    if (DistPointSegment(pP, pA, pB) < 10f) // Min clearance
                    {
                        isValid = false; 
                        break;
                    }
                }

                if (!isValid) continue;

                if (vIdx == -1)
                {
                    vIdx = nodes.Count;
                    nodes.Add(new StreetNode { id = vIdx, worldPosition = finalPos, connectedEdges = new List<int>() });
                    queue.Enqueue(vIdx);
                }

                int newEdgeId = edges.Count;
                edges.Add(new StreetEdge { 
                    id = newEdgeId, 
                    nodeA = uIdx, 
                    nodeB = vIdx, 
                    length = Vector3.Distance(u.worldPosition, finalPos), 
                    streetType = isAvenue ? StreetType.Avenue : StreetType.Street 
                });
                
                nodes[uIdx].connectedEdges.Add(newEdgeId);
                nodes[vIdx].connectedEdges.Add(newEdgeId);
                addedAny = true;
            }
            
            if (addedAny) failCounter = 0;
            else failCounter++;
        }

        // 2. Resolve Dead Ends (Aggressive Pruning to ensure cycles)
        bool prunedAny = true;
        while (prunedAny)
        {
            prunedAny = false;
            int[] nodeDegrees = new int[nodes.Count];
            foreach (var edge in edges)
            {
                if (edge.length < 0) continue; // Removed edge
                nodeDegrees[edge.nodeA]++;
                nodeDegrees[edge.nodeB]++;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge.length < 0) continue;

                if (nodeDegrees[edge.nodeA] == 1 || nodeDegrees[edge.nodeB] == 1)
                {
                    // Dead end found. Prune it to eliminate empty roads.
                    edge.length = -1f; // Mark as removed
                    edges[i] = edge;
                    prunedAny = true;
                }
            }
        }

        // Rebuild clean lists (removing dead ends)
        List<StreetEdge> cleanEdges = new List<StreetEdge>();
        foreach (var edge in edges)
        {
            if (edge.length >= 0) cleanEdges.Add(edge);
        }
        edges = cleanEdges;

        // Fase 3 (Perlin Noise) removida para manter quarteirões retos e poligonais (Estilo São Paulo)

        // Ensure IDs are strictly sequential and correctly mapped
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i]; 
            n.id = i; 
            n.connectedEdges.Clear(); // MUST CLEAR OLD IDS!
            nodes[i] = n;
        }
        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i]; 
            e.id = i; 
            edges[i] = e;
            nodes[e.nodeA].connectedEdges.Add(i); // Repopulate with correct mapped IDs
            nodes[e.nodeB].connectedEdges.Add(i);
        }

        graph.nodes = nodes;
        graph.edges = edges;

        // 4. Extract organic block faces using the planar graph
        blockPolygons = ExtractFaces(graph, out Vector3[] outerPerimeter);

        // 5. Generate Geometry
        foreach (var edge in graph.edges)
        {
            Vector3 posA = graph.nodes[edge.nodeA].worldPosition;
            Vector3 posB = graph.nodes[edge.nodeB].worldPosition;
            Vector3 center = (posA + posB) / 2f;
            Vector3 dir = posB - posA;
            float length = dir.magnitude;

            if (length < 0.001f) continue;
            dir /= length;

            float width = edge.streetType == StreetType.Avenue ? config.streetWidth * 1.5f : config.streetWidth;
            Vector2 roadSize = new Vector2(width, length);

            GameObject segmentParent = new GameObject($"Street_{edge.id}_{edge.nodeA}_{edge.nodeB}");
            segmentParent.transform.position = center;
            segmentParent.transform.rotation = Quaternion.LookRotation(dir);
            segmentParent.transform.SetParent(parent);

            GameObject road = CreateQuad("Road", Vector3.zero, roadSize, segmentParent.transform, streetMat);
            BoxCollider roadCollider = road.AddComponent<BoxCollider>();
            roadCollider.size = new Vector3(roadSize.x, 0.1f, roadSize.y);
            roadCollider.center = new Vector3(0, -0.05f, 0);

            if (config.sidewalkWidth > 0)
            {
                float yOffset = 0.15f;
                float xOffset = (width / 2f) + (config.sidewalkWidth / 2f);
                CreateQuad("Sidewalk_Right", new Vector3(xOffset, yOffset, 0), new Vector2(config.sidewalkWidth, length), segmentParent.transform, sidewalkMat);
                CreateQuad("Sidewalk_Left", new Vector3(-xOffset, yOffset, 0), new Vector2(config.sidewalkWidth, length), segmentParent.transform, sidewalkMat);
            }
        }

        if (config.generateBorderWalls && outerPerimeter != null && outerPerimeter.Length >= 3)
        {
            GameObject borderParent = new GameObject("CityBorders");
            borderParent.transform.SetParent(parent);
            Material wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            wallMat.color = new Color(0.15f, 0.15f, 0.15f);

            for (int i = 0; i < outerPerimeter.Length; i++)
            {
                Vector3 p1 = outerPerimeter[i];
                Vector3 p2 = outerPerimeter[(i + 1) % outerPerimeter.Length];
                
                Vector3 dir = p2 - p1;
                float length = dir.magnitude;
                if (length < 0.1f) continue;
                dir /= length;
                
                // Outer face is clockwise, so left of the vector is outward
                Vector3 normal = new Vector3(-dir.z, 0, dir.x); 
                
                float wallThickness = 10f;
                float wallHeight = config.maxBuildingHeight * 1.5f;
                
                // Espaço para os prédios da borda
                float buildingSpace = config.maxBuildingDepth + 5f; 
                float offsetOutward = (config.streetWidth / 2f) + config.sidewalkWidth + buildingSpace + (wallThickness / 2f);
                
                Vector3 center = (p1 + p2) / 2f + normal * offsetOutward;
                
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"BorderWall_{i}";
                wall.transform.SetParent(borderParent.transform);
                wall.transform.position = center + new Vector3(0, wallHeight / 2f, 0);
                wall.transform.rotation = Quaternion.LookRotation(dir);
                
                // Estender muito o tamanho da muralha para que os cantos se encontrem
                wall.transform.localScale = new Vector3(wallThickness, wallHeight, length + offsetOutward * 3f); 
                wall.GetComponent<Renderer>().sharedMaterial = wallMat;

                // --- GERAÇÃO DOS PRÉDIOS DO PAREDÃO ---
                float currentDist = config.blockCornerMargin;
                float maxDist = length - config.blockCornerMargin;
                float buildingOffset = (config.streetWidth / 2f) + config.sidewalkWidth;
                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                while (currentDist < maxDist)
                {
                    float bWidth = config.minBuildingWidth + (float)rng.NextDouble() * (config.maxBuildingWidth - config.minBuildingWidth);
                    if (currentDist + bWidth > maxDist)
                    {
                        bWidth = maxDist - currentDist;
                        if (bWidth < 2f) break;
                    }

                    float bDepth = config.minBuildingDepth + (float)rng.NextDouble() * (config.maxBuildingDepth - config.minBuildingDepth);
                    float bHeight = config.minBuildingHeight + (float)rng.NextDouble() * (config.maxBuildingHeight * 1.2f - config.minBuildingHeight);

                    // Casas apontam para a rua (-normal)
                    Vector3 bCenter = p1 + dir * (currentDist + bWidth * 0.5f) + normal * (buildingOffset + bDepth * 0.5f);
                    
                    GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bObj.name = $"BorderBuilding_{i}_{currentDist}";
                    bObj.transform.SetParent(borderParent.transform);
                    bObj.transform.position = bCenter + new Vector3(0, bHeight * 0.5f, 0);
                    bObj.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
                    bObj.transform.localScale = new Vector3(bWidth, bHeight, bDepth);

                    Renderer r = bObj.GetComponent<Renderer>();
                    r.sharedMaterial = wallMat; // Reaproveita o material base neutro
                    
                    // Cores orgânicas e sombrias para não chamar muita atenção
                    float hue = (float)rng.NextDouble();
                    float sat = (float)rng.NextDouble() * 0.2f;
                    float val = 0.2f + (float)rng.NextDouble() * 0.4f;
                    Color c = Color.HSVToRGB(hue, sat, val);
                    
                    propBlock.SetColor("_Color", c);
                    propBlock.SetColor("_BaseColor", c);
                    propBlock.SetColor("_MainColor", c);
                    r.SetPropertyBlock(propBlock);

                    currentDist += bWidth;
                }
            }
        }

        return graph;
    }

    private List<Vector3[]> ExtractFaces(StreetGraph graph, out Vector3[] outerPerimeter)
    {
        Dictionary<int, HashSet<int>> tempAdj = new Dictionary<int, HashSet<int>>();
        foreach (var node in graph.nodes)
        {
            tempAdj[node.id] = new HashSet<int>();
            foreach (var edgeId in node.connectedEdges)
            {
                var edge = graph.edges[edgeId];
                int other = (edge.nodeA == node.id) ? edge.nodeB : edge.nodeA;
                tempAdj[node.id].Add(other);
            }
        }

        Dictionary<int, List<int>> sortedAdj = new Dictionary<int, List<int>>();
        foreach (var kvp in tempAdj)
        {
            int u = kvp.Key;
            List<int> neighbors = new List<int>(kvp.Value);
            Vector3 center = graph.nodes[u].worldPosition;
            neighbors.Sort((a, b) => 
            {
                Vector3 dirA = graph.nodes[a].worldPosition - center;
                Vector3 dirB = graph.nodes[b].worldPosition - center;
                return Mathf.Atan2(dirA.z, dirA.x).CompareTo(Mathf.Atan2(dirB.z, dirB.x));
            });
            sortedAdj[u] = neighbors;
        }

        List<Vector3[]> faces = new List<Vector3[]>();
        HashSet<(int, int)> visited = new HashSet<(int, int)>();

        foreach (var kvp in sortedAdj)
        {
            int u = kvp.Key;
            foreach (int v in kvp.Value)
            {
                if (visited.Contains((u, v))) continue;
                
                List<int> faceNodes = new List<int>();
                int curr = u;
                int next = v;
                int loopGuard = 0;
                
                while (!visited.Contains((curr, next)) && loopGuard < graph.nodes.Count * 2)
                {
                    visited.Add((curr, next));
                    faceNodes.Add(curr);
                    
                    if (!sortedAdj.ContainsKey(next)) break;
                    List<int> adj = sortedAdj[next];
                    if (adj.Count == 0) break;

                    int idx = adj.IndexOf(curr);
                    if (idx == -1) break;

                    int nextIdx = (idx - 1 + adj.Count) % adj.Count;
                    int nextNext = adj[nextIdx];
                    
                    curr = next;
                    next = nextNext;
                    loopGuard++;
                }
                
                if (faceNodes.Count >= 3 && loopGuard < graph.nodes.Count * 2)
                {
                    Vector3[] poly = new Vector3[faceNodes.Count];
                    for (int i = 0; i < faceNodes.Count; i++) poly[i] = graph.nodes[faceNodes[i]].worldPosition;
                    faces.Add(poly);
                }
            }
        }
        
        outerPerimeter = null;
        if (faces.Count > 0)
        {
            int maxAreaIdx = 0;
            float maxArea = -1;
            for (int i = 0; i < faces.Count; i++)
            {
                float area = 0f;
                var poly = faces[i];
                for (int j = 0; j < poly.Length; j++)
                {
                    int k = (j + 1) % poly.Length;
                    area += poly[j].x * poly[k].z - poly[k].x * poly[j].z;
                }
                area = Mathf.Abs(area) * 0.5f;

                if (area > maxArea)
                {
                    maxArea = area;
                    maxAreaIdx = i;
                }
            }
            outerPerimeter = faces[maxAreaIdx];
            faces.RemoveAt(maxAreaIdx);
        }

        return faces;
    }

    private bool SegmentsIntersect(Vector2 A, Vector2 B, Vector2 C, Vector2 D)
    {
        float denominator = ((B.x - A.x) * (D.y - C.y)) - ((B.y - A.y) * (D.x - C.x));
        if (Mathf.Abs(denominator) < 1e-5f) return false;

        float r = (((A.y - C.y) * (D.x - C.x)) - ((A.x - C.x) * (D.y - C.y))) / denominator;
        float s = (((A.y - C.y) * (B.x - A.x)) - ((A.x - C.x) * (B.y - A.y))) / denominator;

        return (r > 0.01f && r < 0.99f) && (s > 0.01f && s < 0.99f);
    }

    private float DistPointSegment(Vector2 P, Vector2 A, Vector2 B)
    {
        Vector2 AB = B - A;
        float sqrLen = AB.sqrMagnitude;
        if (sqrLen < 1e-5f) return Vector2.Distance(P, A);
        
        float t = Vector2.Dot(P - A, AB) / sqrLen;
        t = Mathf.Clamp01(t);
        Vector2 proj = A + t * AB;
        return Vector2.Distance(P, proj);
    }

    private GameObject CreateQuad(string name, Vector3 localPosition, Vector2 size, Transform parent, Material mat)
    {
        GameObject quad = new GameObject(name);
        quad.transform.SetParent(parent);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;

        MeshFilter mf = quad.AddComponent<MeshFilter>();
        MeshRenderer mr = quad.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh { name = name + "_Mesh" };

        float halfX = size.x * 0.5f;
        float halfZ = size.y * 0.5f;
        Vector3[] vertices = new Vector3[] {
            new Vector3(-halfX, 0, -halfZ), new Vector3(halfX, 0, -halfZ),
            new Vector3(-halfX, 0, halfZ), new Vector3(halfX, 0, halfZ)
        };
        int[] triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        Vector2[] uvs = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };

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
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
        return mat;
    }
}

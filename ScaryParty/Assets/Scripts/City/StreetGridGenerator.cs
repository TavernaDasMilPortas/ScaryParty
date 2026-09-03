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
        Material streetMat, Material sidewalkMat, Material[] buildingMaterials, out List<Vector3[]> blockPolygons)
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

                // Enforce minimum edge length
                if (Vector3.Distance(u.worldPosition, finalPos) < 15f) continue;

                // BUG 1 FIX: Reject edges that form angles < 60° with existing edges at either endpoint.
                // This prevents "pizza slice" blocks that are too thin for buildings.
                float minAllowedAngle = 60f;
                Vector3 newEdgeDir = (finalPos - u.worldPosition).normalized;

                // Check angles at source node (uIdx)
                foreach (int existingEdgeIdx in u.connectedEdges)
                {
                    var existingEdge = edges[existingEdgeIdx];
                    int otherNode = (existingEdge.nodeA == uIdx) ? existingEdge.nodeB : existingEdge.nodeA;
                    Vector3 existingDir = (nodes[otherNode].worldPosition - u.worldPosition).normalized;
                    float angleDeg = Vector3.Angle(newEdgeDir, existingDir);
                    if (angleDeg < minAllowedAngle)
                    {
                        isValid = false;
                        break;
                    }
                }
                if (!isValid) continue;

                // Check angles at target node (vIdx) if snapping to existing node
                if (vIdx != -1)
                {
                    Vector3 reverseDir = -newEdgeDir;
                    StreetNode targetNode = nodes[vIdx];
                    foreach (int existingEdgeIdx in targetNode.connectedEdges)
                    {
                        var existingEdge = edges[existingEdgeIdx];
                        int otherNode = (existingEdge.nodeA == vIdx) ? existingEdge.nodeB : existingEdge.nodeA;
                        Vector3 existingDir = (nodes[otherNode].worldPosition - targetNode.worldPosition).normalized;
                        float angleDeg = Vector3.Angle(reverseDir, existingDir);
                        if (angleDeg < minAllowedAngle)
                        {
                            isValid = false;
                            break;
                        }
                    }
                    if (!isValid) continue;
                }

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

        // BUG 6 FIX: Remove orphan nodes and remap IDs
        bool[] nodeUsed = new bool[nodes.Count];
        foreach (var edge in edges)
        {
            nodeUsed[edge.nodeA] = true;
            nodeUsed[edge.nodeB] = true;
        }

        List<StreetNode> cleanNodes = new List<StreetNode>();
        int[] oldToNewNodeId = new int[nodes.Count];
        
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodeUsed[i])
            {
                oldToNewNodeId[i] = cleanNodes.Count;
                var n = nodes[i];
                n.id = cleanNodes.Count;
                n.connectedEdges.Clear(); // Clear old edge IDs
                cleanNodes.Add(n);
            }
            else
            {
                oldToNewNodeId[i] = -1; // Orphan
            }
        }

        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i]; 
            e.id = i;
            e.nodeA = oldToNewNodeId[e.nodeA];
            e.nodeB = oldToNewNodeId[e.nodeB];
            edges[i] = e;
            cleanNodes[e.nodeA].connectedEdges.Add(i); // Repopulate with correct mapped IDs
            cleanNodes[e.nodeB].connectedEdges.Add(i);
        }

        graph.nodes = cleanNodes;
        graph.edges = edges;

        // 4. Extract organic block faces using the planar graph
        blockPolygons = ExtractFaces(graph, out Vector3[] outerPerimeter);

        // 5. Generate Geometry using Unified Mesh
        UnifiedRoadMesher.BuildUnifiedMesh(graph, config, rng, parent, streetMat, sidewalkMat);

        if (config.generateBorderWalls && outerPerimeter != null && outerPerimeter.Length >= 3)
        {
            GameObject borderParent = new GameObject("CityBorders");
            borderParent.transform.SetParent(parent);
            Material wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            wallMat.color = new Color(0.15f, 0.15f, 0.15f);

            float wallThickness = 10f;
            float wallHeight = config.maxBuildingHeight * 1.5f;
            float buildingSpace = config.maxBuildingDepth + 5f; 
            float offsetOutward = (config.streetWidth / 2f) + config.sidewalkWidth + buildingSpace + (wallThickness / 2f);

            // BUG FIX: Clean outer perimeter to remove microscopic edges and collinear vertices
            List<Vector3> distanceCleaned = new List<Vector3>();
            if (outerPerimeter.Length > 0) distanceCleaned.Add(outerPerimeter[0]);
            for (int i = 1; i < outerPerimeter.Length; i++)
            {
                if (Vector3.Distance(distanceCleaned[distanceCleaned.Count - 1], outerPerimeter[i]) > 1f)
                    distanceCleaned.Add(outerPerimeter[i]);
            }
            if (distanceCleaned.Count > 2 && Vector3.Distance(distanceCleaned[distanceCleaned.Count - 1], distanceCleaned[0]) <= 1f)
                distanceCleaned.RemoveAt(distanceCleaned.Count - 1);
            
            List<Vector3> finalCleaned = new List<Vector3>();
            int dc = distanceCleaned.Count;
            for (int i = 0; i < dc; i++)
            {
                Vector3 prev = distanceCleaned[(i - 1 + dc) % dc];
                Vector3 curr = distanceCleaned[i];
                Vector3 next = distanceCleaned[(i + 1) % dc];
                float angle = Vector3.Angle((curr - prev).normalized, (next - curr).normalized);
                if (angle > 2f) finalCleaned.Add(curr);
            }
            outerPerimeter = finalCleaned.ToArray();

            // BUG 2 FIX: Calculate perfect mitered offset polygon for the border wall
            int n = outerPerimeter.Length;
            if (n < 3) return graph; // Prevent collapse if perimeter becomes too small
            Vector3[] miteredPerimeter = new Vector3[n];
            Vector3[] edgeDirs = new Vector3[n];
            Vector3[] edgeNormals = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 p1 = outerPerimeter[i];
                Vector3 p2 = outerPerimeter[(i + 1) % n];
                Vector3 dir = (p2 - p1).normalized;
                edgeDirs[i] = dir;
                // Outer face is extracted clockwise, left is outward
                edgeNormals[i] = new Vector3(-dir.z, 0, dir.x);
            }

            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                Vector3 n1 = edgeNormals[prev];
                Vector3 n2 = edgeNormals[i];
                Vector3 miter = (n1 + n2).normalized;
                float dot = Vector3.Dot(miter, n1);
                if (Mathf.Abs(dot) < 0.1f) dot = 0.1f * Mathf.Sign(dot);
                float miterLength = offsetOutward / dot;
                miterLength = Mathf.Min(miterLength, offsetOutward * 3f);
                miteredPerimeter[i] = outerPerimeter[i] + miter * miterLength;
            }

            for (int i = 0; i < n; i++)
            {
                // -- WALL GENERATION --
                // Remove this chaotic wall generation loop inside the border buildings loop.
                // We will generate the convex hull wall AFTER placing the border buildings.

                // --- BORDER BUILDING GENERATION ---
                Vector3 p1 = outerPerimeter[i];
                Vector3 p2 = outerPerimeter[(i + 1) % n];
                Vector3 dir = edgeDirs[i];
                Vector3 normal = edgeNormals[i];
                float length = Vector3.Distance(p1, p2);

                if (length < 10f) continue;

                // BUG 3 FIX: Trigonometric margins for border buildings to prevent corner overlaps
                Vector3 prevDir = edgeDirs[(i - 1 + n) % n];
                Vector3 nextDir = edgeDirs[(i + 1) % n];

                float angle1 = Vector3.Angle(-prevDir, dir);
                float angle2 = Vector3.Angle(-dir, nextDir);

                float margin1 = config.blockCornerMargin;
                if (angle1 > 5f && angle1 < 175f) margin1 = Mathf.Max(margin1, config.maxBuildingDepth / Mathf.Tan(angle1 * 0.5f * Mathf.Deg2Rad));

                float margin2 = config.blockCornerMargin;
                if (angle2 > 5f && angle2 < 175f) margin2 = Mathf.Max(margin2, config.maxBuildingDepth / Mathf.Tan(angle2 * 0.5f * Mathf.Deg2Rad));

                if (margin1 + margin2 >= length) continue;

                float currentDist = margin1;
                float maxDist = length - margin2;
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

                    Material baseMat = null;
                    if (buildingMaterials != null && buildingMaterials.Length > 0)
                    {
                        baseMat = buildingMaterials[rng.Next(buildingMaterials.Length)];
                    }
                    if (baseMat == null) baseMat = wallMat;
                    
                    Renderer r = bObj.GetComponent<Renderer>();
                    r.sharedMaterial = baseMat;
                    
                    // Zone-like colors for border (sombrias mas coloridas)
                    float hue = (float)rng.NextDouble();
                    float sat = 0.5f + (float)rng.NextDouble() * 0.5f; // Mais saturação para ficar colorido
                    float val = 0.3f + (float)rng.NextDouble() * 0.4f;
                    Color buildingColor = Color.HSVToRGB(hue, sat, val);
                    
                    propBlock.SetColor("_Color", buildingColor);
                    propBlock.SetColor("_BaseColor", buildingColor);
                    propBlock.SetColor("_MainColor", buildingColor);
                    r.SetPropertyBlock(propBlock);

                    currentDist += bWidth;
                }
            }
            // Generate clean enveloping walls using a Convex Hull
            List<Vector3> hullPoints = ComputeConvexHull(outerPerimeter);
            // The houses are pushed out by (streetWidth/2 + sidewalkWidth + maxBuildingDepth/2)
            // So we expand the hull by maxBuildingDepth + 10f to safely envelop all houses
            float hullExpansion = config.maxBuildingDepth + 10f; 
            
            // Expand the hull normals
            Vector3[] expandedHull = new Vector3[hullPoints.Count];
            for (int i = 0; i < hullPoints.Count; i++)
            {
                Vector3 pPrev = hullPoints[(i - 1 + hullPoints.Count) % hullPoints.Count];
                Vector3 pCurr = hullPoints[i];
                Vector3 pNext = hullPoints[(i + 1) % hullPoints.Count];
                
                Vector3 dir1 = (pCurr - pPrev).normalized;
                Vector3 dir2 = (pNext - pCurr).normalized;
                
                Vector3 n1 = new Vector3(-dir1.z, 0, dir1.x);
                Vector3 n2 = new Vector3(-dir2.z, 0, dir2.x);
                
                Vector3 miter = (n1 + n2).normalized;
                float dot = Vector3.Dot(miter, n1);
                float length = hullExpansion / Mathf.Max(dot, 0.1f);
                
                expandedHull[i] = pCurr + miter * length;
            }
            
            for (int i = 0; i < expandedHull.Length; i++)
            {
                Vector3 wp1 = expandedHull[i];
                Vector3 wp2 = expandedHull[(i + 1) % expandedHull.Length];
                Vector3 wallDir = wp2 - wp1;
                float wallLen = wallDir.magnitude;
                
                if (wallLen > 0.1f)
                {
                    wallDir /= wallLen;
                    Vector3 center = (wp1 + wp2) / 2f;
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = $"BorderWall_{i}";
                    wall.transform.SetParent(borderParent.transform);
                    wall.transform.position = center + new Vector3(0, wallHeight / 2f, 0);
                    wall.transform.rotation = Quaternion.LookRotation(wallDir);
                    wall.transform.localScale = new Vector3(wallThickness, wallHeight, wallLen); 
                    wall.GetComponent<Renderer>().sharedMaterial = wallMat;
                }
            }
        }

        return graph;
    }

    private List<Vector3> ComputeConvexHull(Vector3[] points)
    {
        if (points == null || points.Length <= 3) return new List<Vector3>(points);
        
        List<Vector3> pts = new List<Vector3>(points);
        pts.Sort((a, b) => a.x == b.x ? a.z.CompareTo(b.z) : a.x.CompareTo(b.x));
        
        List<Vector3> hull = new List<Vector3>();
        
        // Lower hull
        foreach (var pt in pts)
        {
            while (hull.Count >= 2 && CrossProduct(hull[hull.Count - 2], hull[hull.Count - 1], pt) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(pt);
        }
        
        // Upper hull
        int lowerCount = hull.Count;
        for (int i = pts.Count - 2; i >= 0; i--)
        {
            var pt = pts[i];
            while (hull.Count > lowerCount && CrossProduct(hull[hull.Count - 2], hull[hull.Count - 1], pt) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(pt);
        }
        
        hull.RemoveAt(hull.Count - 1);
        return hull;
    }
    
    private float CrossProduct(Vector3 o, Vector3 a, Vector3 b)
    {
        return (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
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

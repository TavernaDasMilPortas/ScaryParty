using UnityEngine;
using System.Collections.Generic;

public static class UnifiedRoadMesher
{
    private struct ConnectionData
    {
        public Vector3 leftPoint;
        public Vector3 rightPoint;
        public Vector3 outerLeftPoint;
        public Vector3 outerRightPoint;
    }

    public static void BuildUnifiedMesh(StreetGraph graph, CityConfig config, System.Random rng, Transform parent, Material streetMat, Material sidewalkMat)
    {
        if (graph == null || graph.nodes == null) return;

        GameObject roadNetworkObj = new GameObject("CityRoadNetwork");
        roadNetworkObj.transform.SetParent(parent);
        roadNetworkObj.transform.position = Vector3.zero;

        MeshFilter mf = roadNetworkObj.AddComponent<MeshFilter>();
        MeshRenderer mr = roadNetworkObj.AddComponent<MeshRenderer>();
        
        mr.sharedMaterials = new Material[] { streetMat, sidewalkMat };

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        
        List<int> asphaltTriangles = new List<int>(); // Submesh 0
        List<int> sidewalkTriangles = new List<int>(); // Submesh 1

        float halfWidth = config.streetWidth * 0.5f;
        float totalWidth = halfWidth + config.sidewalkWidth;

        // Dictionary to store the boundary points for each edge at each node
        // Key: (NodeID, EdgeID)
        Dictionary<(int, int), ConnectionData> edgeConnections = new Dictionary<(int, int), ConnectionData>();

        // 1. Build Intersections (and define edge connection points)
        foreach (var node in graph.nodes)
        {
            if (node.connectedEdges == null || node.connectedEdges.Count == 0) continue;

            // Sort edges CCW
            List<EdgeData> sortedEdges = new List<EdgeData>();
            foreach (int edgeId in node.connectedEdges)
            {
                var edge = graph.edges[edgeId];
                int otherId = (edge.nodeA == node.id) ? edge.nodeB : edge.nodeA;
                Vector3 dir = (graph.nodes[otherId].worldPosition - node.worldPosition).normalized;
                sortedEdges.Add(new EdgeData { id = edgeId, dir = dir, angle = Mathf.Atan2(dir.z, dir.x) });
            }
            sortedEdges.Sort((a, b) => a.angle.CompareTo(b.angle));

            int n = sortedEdges.Count;
            List<Vector3> corners = new List<Vector3>();
            List<Vector3> outerCorners = new List<Vector3>();
            
            for (int i = 0; i < n; i++)
            {
                var edgeA = sortedEdges[i];
                var edgeB = sortedEdges[(i + 1) % n];

                Vector3 leftNormalA = new Vector3(-edgeA.dir.z, 0, edgeA.dir.x);
                Vector3 rightNormalB = new Vector3(edgeB.dir.z, 0, -edgeB.dir.x);

                Vector3 p1 = node.worldPosition + leftNormalA * halfWidth;
                Vector3 p2 = node.worldPosition + rightNormalB * halfWidth;

                if (n == 1)
                {
                    corners.Add(p1);
                    corners.Add(node.worldPosition + new Vector3(edgeA.dir.z, 0, -edgeA.dir.x) * halfWidth);
                    outerCorners.Add(p1 + leftNormalA * config.sidewalkWidth);
                    outerCorners.Add(corners[1] + new Vector3(edgeA.dir.z, 0, -edgeA.dir.x) * config.sidewalkWidth);
                    
                    edgeConnections[(node.id, edgeA.id)] = new ConnectionData { 
                        leftPoint = corners[0], rightPoint = corners[1],
                        outerLeftPoint = outerCorners[0], outerRightPoint = outerCorners[1]
                    };
                    break;
                }
                
                if (LineLineIntersection(out Vector3 corner, p1, edgeA.dir, p2, edgeB.dir))
                {
                    // Clamp
                    if (Vector3.Distance(node.worldPosition, corner) > config.streetWidth * 3f)
                    {
                        corner = node.worldPosition + (corner - node.worldPosition).normalized * (config.streetWidth * 3f);
                    }
                    corners.Add(corner);
                    
                    Vector3 dirToCorner = corner - node.worldPosition;
                    if (dirToCorner.sqrMagnitude > 0.001f)
                    {
                        float scale = totalWidth / halfWidth;
                        outerCorners.Add(node.worldPosition + dirToCorner * scale);
                    }
                    else
                    {
                        outerCorners.Add(corner);
                    }
                }
                else
                {
                    corners.Add(p1);
                    outerCorners.Add(p1 + leftNormalA * config.sidewalkWidth);
                }
            }

            // Register edge connections from the corners
            if (n > 1)
            {
                for (int i = 0; i < n; i++)
                {
                    var edge = sortedEdges[i];
                    int prevCornerIdx = (i - 1 + n) % n;
                    int nextCornerIdx = i; // The corner formed with the next edge
                    
                    // For the current edge pointing OUTWARDS:
                    // Its LEFT corner is the corner formed with the NEXT edge (corner[i])
                    // Its RIGHT corner is the corner formed with the PREVIOUS edge (corner[i-1])
                    edgeConnections[(node.id, edge.id)] = new ConnectionData 
                    { 
                        leftPoint = corners[nextCornerIdx], 
                        rightPoint = corners[prevCornerIdx],
                        outerLeftPoint = outerCorners[nextCornerIdx],
                        outerRightPoint = outerCorners[prevCornerIdx]
                    };
                }
            }

            // Triangulate intersection polygon
            if (corners.Count >= 3)
            {
                int centerIdx = vertices.Count;
                vertices.Add(node.worldPosition + Vector3.up * 0.02f);
                uvs.Add(new Vector2(0.5f, 0.5f));

                int startIdx = vertices.Count;
                for (int i = 0; i < corners.Count; i++)
                {
                    vertices.Add(corners[i] + Vector3.up * 0.02f);
                    Vector3 localP = corners[i] - node.worldPosition;
                    uvs.Add(new Vector2(0.5f + localP.x / config.streetWidth, 0.5f + localP.z / config.streetWidth));
                }

                for (int i = 0; i < corners.Count; i++)
                {
                    asphaltTriangles.Add(centerIdx);
                    asphaltTriangles.Add(startIdx + ((i + 1) % corners.Count)); // Changed to CW for Unity
                    asphaltTriangles.Add(startIdx + i);
                }
            }
        }

        // 2. Build Street Segments
        foreach (var edge in graph.edges)
        {
            if (!edgeConnections.ContainsKey((edge.nodeA, edge.id)) || !edgeConnections.ContainsKey((edge.nodeB, edge.id)))
                continue;

            ConnectionData connA = edgeConnections[(edge.nodeA, edge.id)];
            ConnectionData connB = edgeConnections[(edge.nodeB, edge.id)];

            // From Node A pointing to Node B
            // connA.leftPoint is the left side of the street at Node A
            // connA.rightPoint is the right side of the street at Node A
            // At Node B, the edge points INWARDS to Node B. But the connection was built for the edge pointing OUTWARDS from Node B.
            // So connB.leftPoint is the left side when looking FROM B to A.
            // Thus, connB.leftPoint aligns with connA.rightPoint!
            
            Vector3 pALeft = connA.leftPoint + Vector3.up * 0.02f;
            Vector3 pARight = connA.rightPoint + Vector3.up * 0.02f;
            Vector3 pBLeft = connB.rightPoint + Vector3.up * 0.02f; // Notice the swap
            Vector3 pBRight = connB.leftPoint + Vector3.up * 0.02f; // Notice the swap

            Vector3 outerALeft = connA.outerLeftPoint + Vector3.up * 0.02f;
            Vector3 outerARight = connA.outerRightPoint + Vector3.up * 0.02f;
            Vector3 outerBLeft = connB.outerRightPoint + Vector3.up * 0.02f;
            Vector3 outerBRight = connB.outerLeftPoint + Vector3.up * 0.02f;

            // Add vertices for street
            int vBase = vertices.Count;
            vertices.Add(pALeft);   // 0
            vertices.Add(pARight);  // 1
            vertices.Add(pBLeft);   // 2
            vertices.Add(pBRight);  // 3

            float edgeLen = Vector3.Distance(graph.nodes[edge.nodeA].worldPosition, graph.nodes[edge.nodeB].worldPosition);
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, edgeLen / config.streetWidth));
            uvs.Add(new Vector2(1, edgeLen / config.streetWidth));

            // Triangles (0, 2, 1) and (1, 2, 3)
            asphaltTriangles.Add(vBase);
            asphaltTriangles.Add(vBase + 2);
            asphaltTriangles.Add(vBase + 1);
            asphaltTriangles.Add(vBase + 1);
            asphaltTriangles.Add(vBase + 2);
            asphaltTriangles.Add(vBase + 3);

            // Build Sidewalks if needed
            if (config.sidewalkWidth > 0)
            {
                float sw = config.sidewalkWidth;
                float yOff = config.sidewalkHeight; // Raised sidewalk
                
                // Left Sidewalk
                int swBase = vertices.Count;
                vertices.Add(outerALeft + Vector3.up * yOff);
                vertices.Add(pALeft + Vector3.up * yOff);
                vertices.Add(outerBLeft + Vector3.up * yOff);
                vertices.Add(pBLeft + Vector3.up * yOff);

                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, edgeLen / sw)); uvs.Add(new Vector2(1, edgeLen / sw));

                sidewalkTriangles.Add(swBase); sidewalkTriangles.Add(swBase + 2); sidewalkTriangles.Add(swBase + 1);
                sidewalkTriangles.Add(swBase + 1); sidewalkTriangles.Add(swBase + 2); sidewalkTriangles.Add(swBase + 3);

                // Right Sidewalk
                int swBaseR = vertices.Count;
                vertices.Add(pARight + Vector3.up * yOff);
                vertices.Add(outerARight + Vector3.up * yOff);
                vertices.Add(pBRight + Vector3.up * yOff);
                vertices.Add(outerBRight + Vector3.up * yOff);

                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, edgeLen / sw)); uvs.Add(new Vector2(1, edgeLen / sw));

                sidewalkTriangles.Add(swBaseR); sidewalkTriangles.Add(swBaseR + 2); sidewalkTriangles.Add(swBaseR + 1);
                sidewalkTriangles.Add(swBaseR + 1); sidewalkTriangles.Add(swBaseR + 2); sidewalkTriangles.Add(swBaseR + 3);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "UnifiedRoadMesh";
        // Use 32-bit indices to allow large city meshes (>65k vertices)
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        
        mesh.subMeshCount = 2;
        mesh.SetTriangles(asphaltTriangles, 0);
        mesh.SetTriangles(sidewalkTriangles, 1);
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        mf.mesh = mesh;

        MeshCollider col = roadNetworkObj.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
    }

    private struct EdgeData
    {
        public int id;
        public Vector3 dir;
        public float angle;
    }

    private static bool LineLineIntersection(out Vector3 intersection, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
    {
        Vector3 lineVec3 = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);
        float planarFactor = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
        
        if (Mathf.Abs(crossVec1and2.sqrMagnitude) < 0.0001f)
        {
            intersection = Vector3.zero;
            return false;
        }
        
        intersection = linePoint1 + (lineVec1 * planarFactor);
        return true;
    }
}

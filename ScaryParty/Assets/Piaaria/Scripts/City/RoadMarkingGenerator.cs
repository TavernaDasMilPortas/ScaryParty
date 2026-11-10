using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates procedural horizontal road markings:
/// - Yellow center lines (continuous for avenues, dashed for secondary streets)
/// - White stop lines before intersections
/// Meshes are combined for performance, double-sided, and elevated slightly above asphalt to avoid z-fighting.
/// Does not add colliders so NavMesh and physics are unaffected.
/// </summary>
public static class RoadMarkingGenerator
{
    public static GameObject GenerateMarkings(StreetGraph graph, CityConfig config, Transform parent,
        Material yellowMaterial = null, Material whiteMaterial = null)
    {
        if (graph == null || graph.edges == null || graph.nodes == null) return null;

        GameObject markingsRoot = new GameObject("RoadMarkings");
        markingsRoot.transform.SetParent(parent, false);
        markingsRoot.transform.localPosition = Vector3.zero;

        List<Vector3> yellowVerts = new List<Vector3>();
        List<int> yellowTris = new List<int>();
        List<Vector2> yellowUVs = new List<Vector2>();

        List<Vector3> whiteVerts = new List<Vector3>();
        List<int> whiteTris = new List<int>();
        List<Vector2> whiteUVs = new List<Vector2>();

        float roadHalfWidth = config.streetWidth * 0.5f;
        float yElevation = 0.05f; // Safely above asphalt (0.02f) to prevent any z-fighting
        float lineWidth = 0.35f;

        for (int eIdx = 0; eIdx < graph.edges.Count; eIdx++)
        {
            var edge = graph.edges[eIdx];
            if (edge.nodeA < 0 || edge.nodeA >= graph.nodes.Count ||
                edge.nodeB < 0 || edge.nodeB >= graph.nodes.Count) continue;

            Vector3 nodeAPos = graph.nodes[edge.nodeA].worldPosition;
            Vector3 nodeBPos = graph.nodes[edge.nodeB].worldPosition;

            Vector3 edgeVec = nodeBPos - nodeAPos;
            float fullLength = edgeVec.magnitude;
            if (fullLength < roadHalfWidth * 2.5f) continue;

            Vector3 edgeDir = edgeVec.normalized;
            Vector3 roadNormal = new Vector3(-edgeDir.z, 0, edgeDir.x); // Left normal

            // Intersect road markings before the intersection box
            Vector3 startPos = nodeAPos + edgeDir * (roadHalfWidth + 1.2f);
            Vector3 endPos = nodeBPos - edgeDir * (roadHalfWidth + 1.2f);
            float effectiveLength = Vector3.Distance(startPos, endPos);
            if (effectiveLength <= 2f) continue;

            bool isAvenue = edge.streetType == StreetType.Avenue;

            // 1. Yellow Center Lines
            if (isAvenue)
            {
                // Double solid yellow line
                float separation = 0.25f;
                AddLineQuad(yellowVerts, yellowTris, yellowUVs, startPos + roadNormal * separation, endPos + roadNormal * separation, roadNormal, lineWidth, yElevation);
                AddLineQuad(yellowVerts, yellowTris, yellowUVs, startPos - roadNormal * separation, endPos - roadNormal * separation, roadNormal, lineWidth, yElevation);
            }
            else
            {
                // Dashed yellow line: 3m dash, 3m gap
                float dashLen = 3.0f;
                float gapLen = 3.0f;
                float dist = 0f;

                while (dist < effectiveLength)
                {
                    float currentDash = Mathf.Min(dashLen, effectiveLength - dist);
                    Vector3 dStart = startPos + edgeDir * dist;
                    Vector3 dEnd = dStart + edgeDir * currentDash;

                    AddLineQuad(yellowVerts, yellowTris, yellowUVs, dStart, dEnd, roadNormal, lineWidth, yElevation);
                    dist += dashLen + gapLen;
                }
            }

            // 2. White Stop Lines before entering intersections (placed ONLY if approaching a real intersection >= 3 branches)
            float stopLineThickness = 0.6f;
            float stopLineWidth = roadHalfWidth * 0.85f;

            bool nodeAIsIntersection = graph.nodes[edge.nodeA].connectedEdges != null && graph.nodes[edge.nodeA].connectedEdges.Count >= 3;
            bool nodeBIsIntersection = graph.nodes[edge.nodeB].connectedEdges != null && graph.nodes[edge.nodeB].connectedEdges.Count >= 3;

            // Approaching node A (coming from B towards A): right side is -roadNormal
            if (nodeAIsIntersection)
            {
                Vector3 stopPosA = startPos;
                Vector3 rightLaneCenterA = stopPosA - roadNormal * (roadHalfWidth * 0.5f);
                AddBoxQuad(whiteVerts, whiteTris, whiteUVs, rightLaneCenterA, edgeDir, roadNormal, stopLineThickness, stopLineWidth, yElevation);
            }

            // Approaching node B (coming from A towards B): right side is +roadNormal
            if (nodeBIsIntersection)
            {
                Vector3 stopPosB = endPos;
                Vector3 rightLaneCenterB = stopPosB + roadNormal * (roadHalfWidth * 0.5f);
                AddBoxQuad(whiteVerts, whiteTris, whiteUVs, rightLaneCenterB, edgeDir, roadNormal, stopLineThickness, stopLineWidth, yElevation);
            }
        }

        // Build Yellow Mesh
        if (yellowVerts.Count > 0)
        {
            GameObject yellowObj = new GameObject("YellowLines");
            yellowObj.transform.SetParent(markingsRoot.transform, false);

            Mesh yellowMesh = new Mesh();
            yellowMesh.name = "YellowRoadMarkingsMesh";
            yellowMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            yellowMesh.SetVertices(yellowVerts);
            yellowMesh.SetTriangles(yellowTris, 0);
            yellowMesh.SetUVs(0, yellowUVs);
            yellowMesh.RecalculateNormals();

            MeshFilter mf = yellowObj.AddComponent<MeshFilter>();
            mf.sharedMesh = yellowMesh;

            MeshRenderer mr = yellowObj.AddComponent<MeshRenderer>();
            Material yMat = (yellowMaterial != null) ? yellowMaterial : CreateMarkingMaterial(new Color(1f, 0.84f, 0.05f));
            mr.sharedMaterial = yMat;
        }

        // Build White Mesh
        if (whiteVerts.Count > 0)
        {
            GameObject whiteObj = new GameObject("WhiteStopLines");
            whiteObj.transform.SetParent(markingsRoot.transform, false);

            Mesh whiteMesh = new Mesh();
            whiteMesh.name = "WhiteStopLinesMesh";
            whiteMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            whiteMesh.SetVertices(whiteVerts);
            whiteMesh.SetTriangles(whiteTris, 0);
            whiteMesh.SetUVs(0, whiteUVs);
            whiteMesh.RecalculateNormals();

            MeshFilter mf = whiteObj.AddComponent<MeshFilter>();
            mf.sharedMesh = whiteMesh;

            MeshRenderer mr = whiteObj.AddComponent<MeshRenderer>();
            Material wMat = (whiteMaterial != null) ? whiteMaterial : CreateMarkingMaterial(new Color(0.96f, 0.96f, 0.96f));
            mr.sharedMaterial = wMat;
        }

        return markingsRoot;
    }

    private static void AddLineQuad(List<Vector3> verts, List<int> tris, List<Vector2> uvs,
        Vector3 start, Vector3 end, Vector3 normal, float width, float yElevation)
    {
        Vector3 halfN = normal * (width * 0.5f);
        int vBase = verts.Count;

        float yStart = start.y + yElevation;
        float yEnd = end.y + yElevation;

        verts.Add(new Vector3(start.x - halfN.x, yStart, start.z - halfN.z)); // 0: bottom-left
        verts.Add(new Vector3(start.x + halfN.x, yStart, start.z + halfN.z)); // 1: bottom-right
        verts.Add(new Vector3(end.x - halfN.x, yEnd, end.z - halfN.z));       // 2: top-left
        verts.Add(new Vector3(end.x + halfN.x, yEnd, end.z + halfN.z));       // 3: top-right

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));

        // Front Face (+Y)
        tris.Add(vBase);
        tris.Add(vBase + 1);
        tris.Add(vBase + 2);

        tris.Add(vBase + 1);
        tris.Add(vBase + 3);
        tris.Add(vBase + 2);

        // Back Face (double-sided safety)
        tris.Add(vBase);
        tris.Add(vBase + 2);
        tris.Add(vBase + 1);

        tris.Add(vBase + 1);
        tris.Add(vBase + 2);
        tris.Add(vBase + 3);
    }

    private static void AddBoxQuad(List<Vector3> verts, List<int> tris, List<Vector2> uvs,
        Vector3 center, Vector3 lengthDir, Vector3 widthDir, float length, float width, float yElevation)
    {
        Vector3 halfL = lengthDir * (length * 0.5f);
        Vector3 halfW = widthDir * (width * 0.5f);
        int vBase = verts.Count;

        float yCenter = center.y + yElevation;

        verts.Add(new Vector3(center.x - halfL.x - halfW.x, yCenter, center.z - halfL.z - halfW.z)); // 0
        verts.Add(new Vector3(center.x - halfL.x + halfW.x, yCenter, center.z - halfL.z + halfW.z)); // 1
        verts.Add(new Vector3(center.x + halfL.x - halfW.x, yCenter, center.z + halfL.z - halfW.z)); // 2
        verts.Add(new Vector3(center.x + halfL.x + halfW.x, yCenter, center.z + halfL.z + halfW.z)); // 3

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));

        // Front Face (+Y)
        tris.Add(vBase);
        tris.Add(vBase + 1);
        tris.Add(vBase + 2);

        tris.Add(vBase + 1);
        tris.Add(vBase + 3);
        tris.Add(vBase + 2);

        // Back Face
        tris.Add(vBase);
        tris.Add(vBase + 2);
        tris.Add(vBase + 1);

        tris.Add(vBase + 1);
        tris.Add(vBase + 2);
        tris.Add(vBase + 3);
    }

    private static Material CreateMarkingMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
        // RenderQueue 2010 ensures it is drawn immediately after opaque road surface
        mat.renderQueue = 2010;
        return mat;
    }
}

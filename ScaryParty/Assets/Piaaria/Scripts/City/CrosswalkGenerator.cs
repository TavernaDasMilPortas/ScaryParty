using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates procedural zebra crosswalk meshes and creates logical PedestrianCrossing entities
/// at real intersections (nodes with >= 3 connected streets) across the city.
/// Computes exact corner projections for non-90-degree intersections so crosswalks never cut sidewalk corners.
/// Deduplicates crosswalks on short street segments to prevent visual clutter.
/// </summary>
public static class CrosswalkGenerator
{
    private struct BranchInfo
    {
        public int edgeId;
        public int otherNodeId;
        public Vector3 dir;
        public float angle;
        public float length;
    }

    public static List<PedestrianCrossing> GenerateCrosswalks(StreetGraph graph, CityConfig config,
        Transform parent, Material crosswalkMat, out GameObject crosswalksRootObj)
    {
        List<PedestrianCrossing> crossings = new List<PedestrianCrossing>();
        crosswalksRootObj = new GameObject("Crosswalks");
        crosswalksRootObj.transform.SetParent(parent, false);
        crosswalksRootObj.transform.localPosition = Vector3.zero;

        if (graph == null || graph.nodes == null) return crossings;

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float roadHalfWidth = config.streetWidth * 0.5f;
        float sidewalkHeight = config.sidewalkHeight > 0f ? config.sidewalkHeight : 0.15f;
        float yElevation = 0.052f; // Just above asphalt (0.02f) and yellow lines (0.050f)
        int crossingIdCounter = 0;

        // Track edges that already received a crosswalk to prevent clutter on short street segments
        HashSet<int> processedEdges = new HashSet<int>();

        foreach (var node in graph.nodes)
        {
            // Only generate crosswalks at real intersections (3 or more street branches)
            // Degree 1 = dead ends; Degree 2 = simple road curves/straights
            if (node.connectedEdges == null || node.connectedEdges.Count < 3) continue;

            // Find traffic controller if one exists on this intersection
            IntersectionTrafficController controller = null;
            Transform intersectionTransform = parent.parent != null ? parent.parent.Find($"Intersections/Intersection_{node.id}") : null;
            if (intersectionTransform == null && parent != null)
            {
                intersectionTransform = parent.Find($"Intersection_{node.id}");
            }
            if (intersectionTransform != null)
            {
                controller = intersectionTransform.GetComponent<IntersectionTrafficController>();
            }

            // Gather and validate branches around this node
            List<BranchInfo> branches = new List<BranchInfo>(node.connectedEdges.Count);
            foreach (int edgeId in node.connectedEdges)
            {
                if (edgeId < 0 || edgeId >= graph.edges.Count) continue;
                var edge = graph.edges[edgeId];
                int otherId = (edge.nodeA == node.id) ? edge.nodeB : edge.nodeA;
                if (otherId < 0 || otherId >= graph.nodes.Count) continue;

                Vector3 diff = graph.nodes[otherId].worldPosition - node.worldPosition;
                diff.y = 0f;
                float dist = diff.magnitude;
                if (dist < 1f) continue;

                Vector3 dir = diff.normalized;
                float angle = Mathf.Atan2(dir.z, dir.x);
                branches.Add(new BranchInfo
                {
                    edgeId = edgeId,
                    otherNodeId = otherId,
                    dir = dir,
                    angle = angle,
                    length = dist
                });
            }

            if (branches.Count < 3) continue;

            // Sort branches in counter-clockwise order around the intersection node
            branches.Sort((a, b) => a.angle.CompareTo(b.angle));
            int branchCount = branches.Count;

            for (int i = 0; i < branchCount; i++)
            {
                var branch = branches[i];
                var prevBranch = branches[(i - 1 + branchCount) % branchCount];
                var nextBranch = branches[(i + 1) % branchCount];

                // Calculate angles to adjacent streets
                float angleLeft = Vector3.Angle(branch.dir, prevBranch.dir);
                float angleRight = Vector3.Angle(branch.dir, nextBranch.dir);

                // Corner projection formula: roadHalfWidth / tan(halfAngle)
                // For 90 deg: halfAngle = 45 deg, tan = 1.0 => cornerOffset = roadHalfWidth
                // For 60 deg: halfAngle = 30 deg, tan = 0.577 => cornerOffset = 1.73 * roadHalfWidth
                float projLeft = roadHalfWidth / Mathf.Tan(Mathf.Clamp(angleLeft, 30f, 150f) * 0.5f * Mathf.Deg2Rad);
                float projRight = roadHalfWidth / Mathf.Tan(Mathf.Clamp(angleRight, 30f, 150f) * 0.5f * Mathf.Deg2Rad);
                float cornerOffset = Mathf.Max(roadHalfWidth, Mathf.Max(projLeft, projRight));

                // Safety clamp to avoid excessively large setbacks on extreme needle angles
                cornerOffset = Mathf.Min(cornerOffset, roadHalfWidth * 2.2f);

                float crosswalkDepth = 3.0f; // Crosswalk width along street direction
                float crosswalkDist = cornerOffset + 1.2f;

                // Check edge length: skip if street segment is too short to safely fit a crosswalk
                if (branch.length < crosswalkDist + crosswalkDepth + 2.0f) continue;

                // Edge deduplication: if this edge already received a crosswalk from its other end,
                // only allow a second crosswalk if the street is long enough (>= 45m).
                if (processedEdges.Contains(branch.edgeId))
                {
                    if (branch.length < 45f) continue;
                }
                processedEdges.Add(branch.edgeId);

                // Position the crosswalk cleanly on the street right after the sidewalk corner ends
                Vector3 crosswalkCenter = node.worldPosition + branch.dir * (crosswalkDist + crosswalkDepth * 0.5f);
                Vector3 normal = new Vector3(-branch.dir.z, 0, branch.dir.x); // Perpendicular across the street
                float crosswalkSpan = config.streetWidth * 0.88f;

                // Waiting points on the sidewalks
                Vector3 waitA = crosswalkCenter + normal * (roadHalfWidth + config.sidewalkWidth * 0.5f);
                waitA.y = sidewalkHeight;

                Vector3 waitB = crosswalkCenter - normal * (roadHalfWidth + config.sidewalkWidth * 0.5f);
                waitB.y = sidewalkHeight;

                // Create logical PedestrianCrossing entity
                GameObject crossingObj = new GameObject($"PedestrianCrossing_{crossingIdCounter}");
                crossingObj.transform.SetParent(crosswalksRootObj.transform, false);
                crossingObj.transform.position = crosswalkCenter;

                PedestrianCrossing pc = crossingObj.AddComponent<PedestrianCrossing>();
                pc.crossingId = crossingIdCounter++;
                pc.waitingPointA = waitA;
                pc.waitingPointB = waitB;
                pc.crossingDirection = normal;
                pc.crossingWidth = crosswalkDepth;

                if (controller != null)
                {
                    controller.RegisterCrossing(pc);
                }

                crossings.Add(pc);

                // Generate visual zebra stripes
                float stripeWidth = 0.5f;
                float stripeGap = 0.45f;
                float halfSpan = crosswalkSpan * 0.5f;

                for (float offset = -halfSpan + stripeWidth; offset <= halfSpan - stripeWidth; offset += stripeWidth + stripeGap)
                {
                    Vector3 stripeCenter = crosswalkCenter + normal * offset;
                    AddStripeQuad(verts, tris, uvs, stripeCenter, branch.dir, normal, crosswalkDepth, stripeWidth, yElevation);
                }
            }
        }

        // Build combined mesh for all crosswalk stripes
        if (verts.Count > 0)
        {
            Mesh mesh = new Mesh();
            mesh.name = "CrosswalkZebraMesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();

            MeshFilter mf = crosswalksRootObj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = crosswalksRootObj.AddComponent<MeshRenderer>();
            crosswalkMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                crosswalkMat,
                ScaryParty.VisualStyles.VisualSemanticRole.Crosswalk,
                ScaryParty.VisualStyles.VisualCategoryIds.Streets,
                () =>
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                 ?? Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Standard");
                    var mat = new Material(shader);
                    Color whiteColor = new Color(0.96f, 0.96f, 0.96f);
                    mat.color = whiteColor;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", whiteColor);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", whiteColor);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                    mat.renderQueue = 2010;
                    return mat;
                });
            mr.sharedMaterial = crosswalkMat;
        }

        return crossings;
    }

    private static void AddStripeQuad(List<Vector3> verts, List<int> tris, List<Vector2> uvs,
        Vector3 center, Vector3 lengthDir, Vector3 widthDir, float length, float width, float yElevation)
    {
        Vector3 halfL = lengthDir * (length * 0.5f);
        Vector3 halfW = widthDir * (width * 0.5f);
        int vBase = verts.Count;

        float y = center.y + yElevation;

        verts.Add(new Vector3(center.x - halfL.x - halfW.x, y, center.z - halfL.z - halfW.z)); // 0: bottom-left
        verts.Add(new Vector3(center.x - halfL.x + halfW.x, y, center.z - halfL.z + halfW.z)); // 1: bottom-right
        verts.Add(new Vector3(center.x + halfL.x - halfW.x, y, center.z + halfL.z - halfW.z)); // 2: top-left
        verts.Add(new Vector3(center.x + halfL.x + halfW.x, y, center.z + halfL.z + halfW.z)); // 3: top-right

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));

        // Front face (+Y)
        tris.Add(vBase);
        tris.Add(vBase + 1);
        tris.Add(vBase + 2);

        tris.Add(vBase + 1);
        tris.Add(vBase + 3);
        tris.Add(vBase + 2);

        // Back face (double-sided safety)
        tris.Add(vBase);
        tris.Add(vBase + 2);
        tris.Add(vBase + 1);

        tris.Add(vBase + 1);
        tris.Add(vBase + 2);
        tris.Add(vBase + 3);
    }
}

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds procedural intersection meshes that perfectly connect adjacent streets.
/// </summary>
public class IntersectionBuilder : MonoBehaviour
{
    public void BuildIntersections(StreetGraph graph, CityConfig config, System.Random rng,
        Transform parent, Material intersectionMaterial)
    {
        if (graph == null || graph.nodes == null) return;
        float halfWidth = config.streetWidth * 0.5f;

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
            
            // Calculate intersection corners
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
                    break;
                }
                
                if (LineLineIntersection(out Vector3 corner, p1, edgeA.dir, p2, edgeB.dir))
                {
                    // Clamp to prevent crazy spikes on very acute angles
                    if (Vector3.Distance(node.worldPosition, corner) > config.streetWidth * 3f)
                    {
                        corner = node.worldPosition + (corner - node.worldPosition).normalized * (config.streetWidth * 3f);
                    }
                    corners.Add(corner);
                }
                else
                {
                    // Parallel edges (180 deg)
                    corners.Add(p1);
                }
            }

            // Mesh generation has been moved to UnifiedRoadMesher.cs

            if (n >= 3 && rng.NextDouble() < config.trafficLightProbability)
            {
                GameObject interObj = new GameObject($"Intersection_{node.id}");
                interObj.transform.SetParent(parent, false);
                interObj.transform.position = node.worldPosition;
                var controller = interObj.AddComponent<IntersectionTrafficController>();

                List<int> northSouthEdges = new List<int>();
                List<int> eastWestEdges = new List<int>();
                for (int e = 0; e < sortedEdges.Count; e++)
                {
                    if (Mathf.Abs(sortedEdges[e].dir.z) >= Mathf.Abs(sortedEdges[e].dir.x))
                        northSouthEdges.Add(sortedEdges[e].id);
                    else
                        eastWestEdges.Add(sortedEdges[e].id);
                }

                float stopRadius = Mathf.Max(5.5f, config.streetWidth * 0.75f);
                var tlGroupA = PlaceTrafficLight(node.worldPosition, config.streetWidth, rng, interObj.transform, controller, 0, northSouthEdges.ToArray(), stopRadius, new Vector3(1f, 0f, 1f));
                var tlGroupB = PlaceTrafficLight(node.worldPosition, config.streetWidth, rng, interObj.transform, controller, 1, eastWestEdges.ToArray(), stopRadius, new Vector3(-1f, 0f, -1f));
                if (tlGroupA != null && tlGroupB != null)
                {
                    tlGroupA.pairedLight = tlGroupB;
                    tlGroupB.pairedLight = tlGroupA;
                }
            }
        }
    }

    private struct EdgeData
    {
        public int id;
        public Vector3 dir;
        public float angle;
    }

    private bool LineLineIntersection(out Vector3 intersection, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
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

    private TrafficLight PlaceTrafficLight(
        Vector3 center,
        float size,
        System.Random rng,
        Transform parent,
        IntersectionTrafficController controller,
        int groupIndex,
        int[] edgeIds,
        float stopRadius,
        Vector3 cornerSign)
    {
        GameObject tlObj = new GameObject(groupIndex == 0 ? "TrafficLight_GroupA" : "TrafficLight_GroupB");
        tlObj.transform.SetParent(parent);
        tlObj.transform.position = center + new Vector3(cornerSign.x * (size / 2.2f), 0f, cornerSign.z * (size / 2.2f));

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(tlObj.transform);
        pole.transform.localPosition = new Vector3(0, 2f, 0);
        pole.transform.localScale = new Vector3(0.2f, 2f, 0.2f);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(tlObj.transform);
        sphere.transform.localPosition = new Vector3(0, 4.2f, 0);
        sphere.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

        Renderer poleRend = pole.GetComponent<Renderer>();
        if (poleRend != null)
        {
            Material poleMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                poleRend.sharedMaterial, ScaryParty.VisualStyles.VisualSemanticRole.Metal, ScaryParty.VisualStyles.VisualCategoryIds.Props);
            if (poleMat != null) poleRend.sharedMaterial = poleMat;
        }

        Renderer sphereRend = sphere.GetComponent<Renderer>();
        if (sphereRend != null)
        {
            Material lensMat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                sphereRend.sharedMaterial, ScaryParty.VisualStyles.VisualSemanticRole.TrafficLightLens, ScaryParty.VisualStyles.VisualCategoryIds.Streets);
            if (lensMat != null) sphereRend.sharedMaterial = lensMat;
        }

        TrafficLight tlComp = tlObj.AddComponent<TrafficLight>();
        tlComp.controller = controller;
        controller.RegisterTrafficLight(tlComp);
        tlComp.Setup(sphereRend, (float)rng.NextDouble() * 10f, edgeIds, groupIndex, center, stopRadius);

        if (Application.isPlaying) { Destroy(pole.GetComponent<Collider>()); Destroy(sphere.GetComponent<Collider>()); }
        else { DestroyImmediate(pole.GetComponent<Collider>()); DestroyImmediate(sphere.GetComponent<Collider>()); }
        return tlComp;
    }
}

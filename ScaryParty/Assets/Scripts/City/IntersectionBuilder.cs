using UnityEngine;

/// <summary>
/// Builds intersection details at street crossings.
/// Places flat quads covering crossing areas and optional traffic lights.
/// </summary>
public class IntersectionBuilder : MonoBehaviour
{
    /// <summary>
    /// Builds intersections and traffic lights at street crossings.
    /// </summary>
    public void BuildIntersections(StreetGraph graph, CityConfig config, System.Random rng,
        Transform parent, Material intersectionMaterial)
    {
        if (graph == null || graph.nodes == null) return;

        float intersectionSize = config.streetWidth;

        foreach (var node in graph.nodes)
        {
            bool isMajorIntersection = node.connectedEdges != null && node.connectedEdges.Count >= 3;

            // Create intersection quad
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Intersection_{node.id}";
            quad.transform.SetParent(parent);
            quad.transform.position = node.worldPosition + Vector3.up * 0.02f;
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(intersectionSize, intersectionSize, 1f);

            if (intersectionMaterial != null)
            {
                quad.GetComponent<Renderer>().sharedMaterial = intersectionMaterial;
            }

            // Replace default collider with box
            var oldCollider = quad.GetComponent<Collider>();
            if (oldCollider != null)
            {
                if (Application.isPlaying) Destroy(oldCollider);
                else DestroyImmediate(oldCollider);
            }
            BoxCollider col = quad.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 1f, 0.1f);
            col.isTrigger = true;

            // Optionally place traffic lights
            if (isMajorIntersection && rng.NextDouble() < config.trafficLightProbability)
            {
                PlaceTrafficLight(node.worldPosition, intersectionSize, rng, parent);
            }
        }
    }

    private void PlaceTrafficLight(Vector3 center, float size, System.Random rng, Transform parent)
    {
        GameObject tlObj = new GameObject("TrafficLight");
        tlObj.transform.SetParent(parent);
        tlObj.transform.position = center + new Vector3(size / 2.5f, 0, size / 2.5f);

        // Pole
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(tlObj.transform);
        pole.transform.localPosition = new Vector3(0, 2f, 0);
        pole.transform.localScale = new Vector3(0.2f, 2f, 0.2f);

        // Light sphere
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(tlObj.transform);
        sphere.transform.localPosition = new Vector3(0, 4.2f, 0);
        sphere.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

        // TrafficLight component
        TrafficLight tlComp = tlObj.AddComponent<TrafficLight>();
        tlComp.Setup(sphere.GetComponent<Renderer>(), (float)rng.NextDouble() * 10f);

        // Clean up colliders on decorative parts
        var poleCol = pole.GetComponent<Collider>();
        var sphereCol = sphere.GetComponent<Collider>();
        if (Application.isPlaying) { Destroy(poleCol); Destroy(sphereCol); }
        else { DestroyImmediate(poleCol); DestroyImmediate(sphereCol); }

        BoxCollider col = tlObj.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 2f, 0);
        col.size = new Vector3(0.5f, 4.5f, 0.5f);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a continuous ground surface mesh for each city block polygon.
/// Positioned at the exact elevation of the sidewalk surface (config.sidewalkHeight),
/// ensuring seamless integration between block interiors, building bases, and sidewalks.
/// Generates double-sided triangles with upward normals to guarantee visibility from any view angle.
/// </summary>
public static class CityBlockGroundMesher
{
    /// <summary>
    /// Builds and attaches a continuous ground surface mesh to the given block GameObject.
    /// </summary>
    public static GameObject BuildBlockGround(BlockInfo block, CityConfig config, Transform parent, Material groundMat, int blockIndex)
    {
        if (block.polygon == null || block.polygon.Length < 3) return null;

        Vector3[] poly = PolygonUtils.CleanPolygon(block.polygon, 0.05f, 0.5f);
        if (poly.Length < 3) return null;

        float surfaceY = config != null && config.sidewalkHeight > 0f ? config.sidewalkHeight : 0.15f;

        GameObject groundObj = new GameObject($"BlockGround_{blockIndex}");
        groundObj.transform.SetParent(parent, false);
        groundObj.transform.localPosition = Vector3.zero;
        groundObj.transform.localRotation = Quaternion.identity;

        MeshFilter mf = groundObj.AddComponent<MeshFilter>();
        MeshRenderer mr = groundObj.AddComponent<MeshRenderer>();

        // Ensure a valid visible material with concrete/pavement tone
        if (groundMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                         ?? Shader.Find("Universal Render Pipeline/Unlit") 
                         ?? Shader.Find("Standard");
            groundMat = new Material(shader);
            Color concreteColor = new Color(0.78f, 0.78f, 0.76f); // Clean concrete sidewalk tone
            groundMat.color = concreteColor;
            if (groundMat.HasProperty("_BaseColor")) groundMat.SetColor("_BaseColor", concreteColor);
            if (groundMat.HasProperty("_Color")) groundMat.SetColor("_Color", concreteColor);
            if (groundMat.HasProperty("_Smoothness")) groundMat.SetFloat("_Smoothness", 0.05f);
        }
        mr.sharedMaterial = groundMat;

        // Convert world-space polygon coordinates to local space of the block parent
        Vector3[] localPoly = new Vector3[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            localPoly[i] = poly[i] - block.worldCenter;
        }

        Mesh mesh = CreatePolygonMesh(localPoly, surfaceY, $"BlockGroundMesh_{blockIndex}");
        if (mesh == null)
        {
            UnityEngine.Object.DestroyImmediate(groundObj);
            return null;
        }

        mf.sharedMesh = mesh;

        MeshCollider col = groundObj.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;

        return groundObj;
    }

    /// <summary>
    /// Triangulates a 2D polygon in the XZ plane at the specified Y elevation.
    /// Uses Ear Clipping with double-sided triangles to guarantee visibility regardless of face culling.
    /// </summary>
    public static Mesh CreatePolygonMesh(Vector3[] polygon, float elevationY, string meshName = "BlockGround")
    {
        int n = polygon.Length;
        if (n < 3) return null;

        List<Vector2> pts2D = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            pts2D.Add(new Vector2(polygon[i].x, polygon[i].z));
        }

        List<int> baseTriangles = PolygonUtils.TriangulateEarClipping(pts2D, clockwiseForUnityNormals: true);
        if (baseTriangles == null || baseTriangles.Count < 3)
        {
            return null;
        }

        // Build vertices, UVs, and normals
        List<Vector3> vertices = new List<Vector3>(pts2D.Count);
        List<Vector2> uvs = new List<Vector2>(pts2D.Count);
        List<Vector3> normals = new List<Vector3>(pts2D.Count);

        for (int i = 0; i < pts2D.Count; i++)
        {
            vertices.Add(new Vector3(pts2D[i].x, elevationY, pts2D[i].y));
            uvs.Add(new Vector2(pts2D[i].x * 0.25f, pts2D[i].y * 0.25f));
            normals.Add(Vector3.up);
        }

        // Generate double-sided triangles (both Clockwise and Counter-Clockwise)
        List<int> doubleTriangles = new List<int>(baseTriangles.Count * 2);
        for (int t = 0; t < baseTriangles.Count; t += 3)
        {
            int i0 = baseTriangles[t];
            int i1 = baseTriangles[t + 1];
            int i2 = baseTriangles[t + 2];

            // Side A (Clockwise viewed from above)
            doubleTriangles.Add(i0);
            doubleTriangles.Add(i2);
            doubleTriangles.Add(i1);

            // Side B (Counter-Clockwise)
            doubleTriangles.Add(i0);
            doubleTriangles.Add(i1);
            doubleTriangles.Add(i2);
        }

        Mesh mesh = new Mesh();
        mesh.name = meshName;
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(doubleTriangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }

    private static List<int> TriangulateEarClipping(List<Vector2> pts)
    {
        int n = pts.Count;
        if (n < 3) return new List<int>();

        List<int> indices = new List<int>(n);
        for (int i = 0; i < n; i++) indices.Add(i);

        List<int> result = new List<int>((n - 2) * 3);
        int safetyMax = n * n * 2;
        int safety = 0;

        while (indices.Count > 3 && safety++ < safetyMax)
        {
            bool earFound = false;
            int count = indices.Count;

            for (int i = 0; i < count; i++)
            {
                int prevIdx = indices[(i - 1 + count) % count];
                int currIdx = indices[i];
                int nextIdx = indices[(i + 1) % count];

                Vector2 a = pts[prevIdx];
                Vector2 b = pts[currIdx];
                Vector2 c = pts[nextIdx];

                if (!IsConvex(a, b, c)) continue;

                bool containsPoint = false;
                for (int j = 0; j < count; j++)
                {
                    int testIdx = indices[j];
                    if (testIdx == prevIdx || testIdx == currIdx || testIdx == nextIdx) continue;
                    if (IsPointInsideTriangle(pts[testIdx], a, b, c))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (!containsPoint)
                {
                    result.Add(prevIdx);
                    result.Add(currIdx);
                    result.Add(nextIdx);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound) break;
        }

        if (indices.Count == 3)
        {
            result.Add(indices[0]);
            result.Add(indices[1]);
            result.Add(indices[2]);
        }

        return result;
    }

    private static List<int> TriangulateFan(List<Vector2> pts)
    {
        List<int> result = new List<int>();
        for (int i = 1; i < pts.Count - 1; i++)
        {
            result.Add(0);
            result.Add(i);
            result.Add(i + 1);
        }
        return result;
    }

    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - a.x);
        return cross > 0.00001f;
    }

    private static bool IsPointInsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross2D(p - a, b - a);
        float d2 = Cross2D(p - b, c - b);
        float d3 = Cross2D(p - c, a - c);

        bool hasNeg = (d1 < -0.0001f) || (d2 < -0.0001f) || (d3 < -0.0001f);
        bool hasPos = (d1 > 0.0001f) || (d2 > 0.0001f) || (d3 > 0.0001f);

        return !(hasNeg && hasPos);
    }

    private static float Cross2D(Vector2 u, Vector2 v)
    {
        return u.x * v.y - u.y * v.x;
    }

    private static float ComputeSignedArea(List<Vector2> pts)
    {
        float area = 0f;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p1 = pts[i];
            Vector2 p2 = pts[(i + 1) % n];
            area += (p1.x * p2.y - p2.x * p1.y);
        }
        return area * 0.5f;
    }

    private static Vector3[] CleanPolygonPoints(Vector3[] raw)
    {
        List<Vector3> cleaned = new List<Vector3>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            Vector3 pt = raw[i];
            if (cleaned.Count == 0 || Vector3.Distance(cleaned[cleaned.Count - 1], pt) > 0.3f)
            {
                cleaned.Add(pt);
            }
        }
        if (cleaned.Count > 1 && Vector3.Distance(cleaned[0], cleaned[cleaned.Count - 1]) < 0.3f)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }
        return cleaned.ToArray();
    }
}

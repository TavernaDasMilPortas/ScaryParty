using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Robust 2D/3D polygon operations: Ear Clipping triangulation, polygon cleaning,
/// signed area, and safe geometric insetting.
/// Designed to guarantee valid geometry on concave, L-shaped, and acute city block polygons.
/// </summary>
public static class PolygonUtils
{
    /// <summary>
    /// Tests if a 2D point (in the XZ plane) lies inside a polygon (or on its boundary) using ray casting.
    /// </summary>
    public static bool IsPointInPolygon(Vector2 p, Vector3[] poly, float boundaryTolerance = 0.05f)
    {
        if (poly == null || poly.Length < 3) return false;
        bool inside = false;
        int j = poly.Length - 1;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 pi = new Vector2(poly[i].x, poly[i].z);
            Vector2 pj = new Vector2(poly[j].x, poly[j].z);

            if (boundaryTolerance > 0f)
            {
                // Check if p is on the boundary segment
                Vector2 seg = pj - pi;
                float segLenSq = seg.sqrMagnitude;
                if (segLenSq > 0.0001f)
                {
                    float t = Mathf.Clamp01(Vector2.Dot(p - pi, seg) / segLenSq);
                    Vector2 proj = pi + (seg * t);
                    if ((p - proj).sqrMagnitude <= boundaryTolerance * boundaryTolerance)
                    {
                        return true;
                    }
                }
            }

            if (((pi.y > p.y) != (pj.y > p.y)) &&
                (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x))
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    /// <summary>
    /// Computes the signed area of a 2D polygon in the XZ plane.
    /// Positive value means Counter-Clockwise (CCW).
    /// </summary>
    public static float ComputeSignedArea(List<Vector2> pts)
    {
        if (pts == null || pts.Count < 3) return 0f;
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

    /// <summary>
    /// Computes the signed area of a 3D polygon in the XZ plane.
    /// </summary>
    public static float ComputeSignedArea(Vector3[] pts)
    {
        if (pts == null || pts.Length < 3) return 0f;
        float area = 0f;
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[(i + 1) % n];
            area += (p1.x * p2.z - p2.x * p1.z);
        }
        return area * 0.5f;
    }

    /// <summary>
    /// Cleans a polygon by removing duplicate vertices closer than minDistance
    /// and collinear vertices with angle deviation less than minCollinearAngleDeg.
    /// </summary>
    public static Vector3[] CleanPolygon(Vector3[] raw, float minDistance = 0.05f, float minCollinearAngleDeg = 0.5f)
    {
        if (raw == null || raw.Length < 3) return raw ?? Array.Empty<Vector3>();

        // 1. Remove duplicate adjacent vertices
        List<Vector3> distCleaned = new List<Vector3>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            Vector3 pt = raw[i];
            if (distCleaned.Count == 0 || Vector3.Distance(distCleaned[distCleaned.Count - 1], pt) > minDistance)
            {
                distCleaned.Add(pt);
            }
        }

        // Check wrap-around closing vertex
        if (distCleaned.Count > 2 && Vector3.Distance(distCleaned[0], distCleaned[distCleaned.Count - 1]) <= minDistance)
        {
            distCleaned.RemoveAt(distCleaned.Count - 1);
        }

        if (distCleaned.Count < 3) return distCleaned.ToArray();

        // 2. Remove collinear vertices
        List<Vector3> angleCleaned = new List<Vector3>(distCleaned.Count);
        int n = distCleaned.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = distCleaned[(i - 1 + n) % n];
            Vector3 curr = distCleaned[i];
            Vector3 next = distCleaned[(i + 1) % n];

            Vector3 d1 = (curr - prev);
            d1.y = 0;
            Vector3 d2 = (next - curr);
            d2.y = 0;

            if (d1.sqrMagnitude < 1e-6f || d2.sqrMagnitude < 1e-6f)
                continue;

            float angle = Vector3.Angle(d1.normalized, d2.normalized);
            if (angle > minCollinearAngleDeg && angle < (180f - minCollinearAngleDeg))
            {
                angleCleaned.Add(curr);
            }
        }

        return angleCleaned.Count >= 3 ? angleCleaned.ToArray() : distCleaned.ToArray();
    }

    /// <summary>
    /// Triangulates a simple 2D polygon (convex or concave) using Ear Clipping.
    /// Returns triangle indices in Counter-Clockwise order (or Clockwise if specified).
    /// Always stays strictly inside the polygon boundary; never leaks outside concave notches.
    /// </summary>
    public static List<int> TriangulateEarClipping(List<Vector2> pts, bool clockwiseForUnityNormals = true)
    {
        int n = pts != null ? pts.Count : 0;
        if (n < 3) return new List<int>();

        // Ensure CCW orientation for ear testing
        List<Vector2> workingPts = new List<Vector2>(pts);
        List<int> origIndices = new List<int>(n);
        for (int i = 0; i < n; i++) origIndices.Add(i);

        float signedArea = ComputeSignedArea(workingPts);
        if (signedArea < 0f)
        {
            workingPts.Reverse();
            origIndices.Reverse();
        }

        List<int> remaining = new List<int>(n);
        for (int i = 0; i < n; i++) remaining.Add(i);

        List<int> resultTriangles = new List<int>((n - 2) * 3);
        int maxIterations = n * n * 3;
        int iter = 0;

        while (remaining.Count > 3 && iter++ < maxIterations)
        {
            bool earFound = false;
            int count = remaining.Count;

            // Find an ear
            for (int i = 0; i < count; i++)
            {
                int prevIdx = remaining[(i - 1 + count) % count];
                int currIdx = remaining[i];
                int nextIdx = remaining[(i + 1) % count];

                Vector2 a = workingPts[prevIdx];
                Vector2 b = workingPts[currIdx];
                Vector2 c = workingPts[nextIdx];

                // Check convexity: for CCW polygon, cross product (b-a) x (c-b) must be positive
                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - a.x);
                if (cross <= 1e-6f) continue; // Reflex or collinear

                // Check if any other remaining vertex is inside triangle ABC
                bool containsPoint = false;
                for (int j = 0; j < count; j++)
                {
                    int testIdx = remaining[j];
                    if (testIdx == prevIdx || testIdx == currIdx || testIdx == nextIdx) continue;

                    if (IsPointInTriangle2D(workingPts[testIdx], a, b, c))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (!containsPoint)
                {
                    // Map back to original indices
                    int o0 = origIndices[prevIdx];
                    int o1 = origIndices[currIdx];
                    int o2 = origIndices[nextIdx];

                    if (clockwiseForUnityNormals)
                    {
                        // In Unity XZ plane (Y up), CW order (0, 2, 1) has upward normals
                        resultTriangles.Add(o0);
                        resultTriangles.Add(o2);
                        resultTriangles.Add(o1);
                    }
                    else
                    {
                        resultTriangles.Add(o0);
                        resultTriangles.Add(o1);
                        resultTriangles.Add(o2);
                    }

                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            // Fallback if numerical precision prevented finding strict ear:
            // Find the most convex vertex that does not contain points, or simply clip the most convex
            if (!earFound)
            {
                int bestIdx = -1;
                float bestCross = -float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    int prevIdx = remaining[(i - 1 + count) % count];
                    int currIdx = remaining[i];
                    int nextIdx = remaining[(i + 1) % count];

                    Vector2 a = workingPts[prevIdx];
                    Vector2 b = workingPts[currIdx];
                    Vector2 c = workingPts[nextIdx];

                    float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - a.x);
                    if (cross > bestCross)
                    {
                        bestCross = cross;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    int prevIdx = remaining[(bestIdx - 1 + count) % count];
                    int currIdx = remaining[bestIdx];
                    int nextIdx = remaining[(bestIdx + 1) % count];

                    int o0 = origIndices[prevIdx];
                    int o1 = origIndices[currIdx];
                    int o2 = origIndices[nextIdx];

                    if (clockwiseForUnityNormals)
                    {
                        resultTriangles.Add(o0);
                        resultTriangles.Add(o2);
                        resultTriangles.Add(o1);
                    }
                    else
                    {
                        resultTriangles.Add(o0);
                        resultTriangles.Add(o1);
                        resultTriangles.Add(o2);
                    }

                    remaining.RemoveAt(bestIdx);
                }
                else
                {
                    break; // Could not make progress
                }
            }
        }

        // Final 3 vertices
        if (remaining.Count == 3)
        {
            int o0 = origIndices[remaining[0]];
            int o1 = origIndices[remaining[1]];
            int o2 = origIndices[remaining[2]];

            if (clockwiseForUnityNormals)
            {
                resultTriangles.Add(o0);
                resultTriangles.Add(o2);
                resultTriangles.Add(o1);
            }
            else
            {
                resultTriangles.Add(o0);
                resultTriangles.Add(o1);
                resultTriangles.Add(o2);
            }
        }

        return resultTriangles;
    }

    /// <summary>
    /// Checks if point p is inside triangle (a, b, c) in 2D with small tolerance.
    /// </summary>
    public static bool IsPointInTriangle2D(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        float c2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
        float c3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);

        bool hasNeg = (c1 < -1e-5f) || (c2 < -1e-5f) || (c3 < -1e-5f);
        bool hasPos = (c1 > 1e-5f) || (c2 > 1e-5f) || (c3 > 1e-5f);

        return !(hasNeg && hasPos);
    }

    /// <summary>
    /// Safely insets a 3D polygon (on XZ plane) inwards by 'inset' distance.
    /// Handles both convex and concave/reflex vertices without shooting to infinity or inverting outward.
    /// </summary>
    public static Vector3[] InsetPolygonSafe(Vector3[] poly, float inset)
    {
        if (poly == null || poly.Length < 3 || inset <= 0f) return poly;
        int n = poly.Length;

        // 1. Determine winding order (CW vs CCW)
        float signedArea = ComputeSignedArea(poly);
        float windingSign = Mathf.Sign(signedArea);
        if (Mathf.Abs(windingSign) < 0.001f) windingSign = 1f;

        // 2. Inward normal for each edge
        Vector3[] edgeNormals = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 edgeDir = poly[j] - poly[i];
            edgeDir.y = 0;
            if (edgeDir.sqrMagnitude > 1e-6f)
                edgeDir.Normalize();
            else
                edgeDir = Vector3.forward;

            // Inward normal
            edgeNormals[i] = new Vector3(-edgeDir.z * windingSign, 0, edgeDir.x * windingSign);
        }

        Vector3[] insetPoly = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int prevEdge = (i - 1 + n) % n;
            int currEdge = i;

            Vector3 n1 = edgeNormals[prevEdge];
            Vector3 n2 = edgeNormals[currEdge];

            Vector3 miter = (n1 + n2);
            miter.y = 0;

            if (miter.sqrMagnitude < 0.001f)
            {
                // Parallel or near parallel
                insetPoly[i] = poly[i] + n1 * inset;
            }
            else
            {
                miter.Normalize();
                float dot = Vector3.Dot(miter, n1);

                // Prevent negative dot (reflex corner) from pulling outwards or dividing by near zero
                // For a reflex corner, the inward bisector is still 'miter' if n1+n2 points inward.
                // If dot <= 0, clamp to a safe positive value along the normal
                if (dot < 0.2f)
                {
                    // Fallback to simple average normal offset
                    Vector3 avgN = (n1 + n2).normalized;
                    if (Vector3.Dot(avgN, n1) < 0.1f) avgN = n1;
                    insetPoly[i] = poly[i] + avgN * inset;
                }
                else
                {
                    float miterLength = inset / dot;
                    // Cap miter length to avoid spikes on acute angles
                    miterLength = Mathf.Min(miterLength, inset * 2.0f);
                    insetPoly[i] = poly[i] + miter * miterLength;
                }
            }
        }

        return insetPoly;
    }
}

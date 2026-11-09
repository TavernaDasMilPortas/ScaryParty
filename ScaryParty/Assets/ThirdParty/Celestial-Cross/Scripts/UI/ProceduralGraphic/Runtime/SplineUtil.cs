using System.Collections.Generic;
using UnityEngine;

namespace ProceduralGraphicSystem
{
    public static class SplineUtil
    {
        public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2.0f * p1) +
                (-p0 + p2) * t +
                (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
                (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
            );
        }

        public static void SubdivideSpline(IList<ShapePreset.ShapePoint> points, int subdivisions, bool closed, List<Vector2> result)
        {
            result.Clear();
            int count = points.Count;

            if (count == 0) return;
            if (count == 1)
            {
                result.Add(points[0].position);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (!closed && i == count - 1)
                {
                    result.Add(points[i].position);
                    continue;
                }

                int prev = closed ? (i - 1 + count) % count : Mathf.Max(i - 1, 0);
                int next = closed ? (i + 1) % count : Mathf.Min(i + 1, count - 1);
                int nextNext = closed ? (i + 2) % count : Mathf.Min(i + 2, count - 1);

                ShapePreset.ShapePoint p0 = points[prev];
                ShapePreset.ShapePoint p1 = points[i];
                ShapePreset.ShapePoint p2 = points[next];
                ShapePreset.ShapePoint p3 = points[nextNext];

                result.Add(p1.position);

                if (p1.isSharp || p2.isSharp)
                {
                    continue;
                }

                for (int j = 1; j <= subdivisions; j++)
                {
                    float t = j / (float)(subdivisions + 1);
                    result.Add(CatmullRom(p0.position, p1.position, p2.position, p3.position, t));
                }
            }

        }
        
        public static void SubdivideSplineWithPositions(IList<ShapePreset.ShapePoint> basePoints, IList<Vector2> positions, int subdivisions, bool closed, List<Vector2> result)
        {
            result.Clear();
            int count = positions.Count;
            if (count == 0) return;
            if (count == 1)
            {
                result.Add(positions[0]);
                return;
            }
            
            for (int i = 0; i < count; i++)
            {
                if (!closed && i == count - 1)
                {
                    result.Add(positions[i]);
                    continue;
                }

                int prev = closed ? (i - 1 + count) % count : Mathf.Max(i - 1, 0);
                int next = closed ? (i + 1) % count : Mathf.Min(i + 1, count - 1);
                int nextNext = closed ? (i + 2) % count : Mathf.Min(i + 2, count - 1);

                Vector2 p0 = positions[prev];
                Vector2 p1 = positions[i];
                Vector2 p2 = positions[next];
                Vector2 p3 = positions[nextNext];

                bool isSharp = false;
                if (i < basePoints.Count && next < basePoints.Count)
                {
                    isSharp = basePoints[i].isSharp || basePoints[next].isSharp;
                }

                result.Add(p1);

                if (isSharp) continue;

                for (int j = 1; j <= subdivisions; j++)
                {
                    float t = j / (float)(subdivisions + 1);
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

        }
    }
}

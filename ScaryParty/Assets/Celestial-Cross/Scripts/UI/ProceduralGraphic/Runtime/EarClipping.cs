using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralGraphicSystem
{
    public static class EarClipping
    {
        [ThreadStatic]
        private static List<int> _vList;

        public static void Triangulate(IList<Vector2> polygon, List<int> outIndices)
        {
            outIndices.Clear();
            int n = polygon.Count;
            if (n < 3) return;

            if (_vList == null) _vList = new List<int>();
            _vList.Clear();
            if (Area(polygon) > 0)
            {
                for (int v = 0; v < n; v++) _vList.Add(v);
            }
            else
            {
                for (int v = 0; v < n; v++) _vList.Add((n - 1) - v);
            }

            int nv = n;
            int count = 2 * nv;
            for (int v = nv - 1; nv > 2;)
            {
                if (count-- <= 0) return; // Polígono inválido

                int u = v;
                if (nv <= u) u = 0;
                v = u + 1;
                if (nv <= v) v = 0;
                int w = v + 1;
                if (nv <= w) w = 0;

                if (Snip(polygon, u, v, w, nv, _vList))
                {
                    int a, b, c, s, t;
                    a = _vList[u];
                    b = _vList[v];
                    c = _vList[w];
                    outIndices.Add(a);
                    outIndices.Add(b);
                    outIndices.Add(c);
                    for (s = v, t = v + 1; t < nv; s++, t++)
                    {
                        _vList[s] = _vList[t];
                    }
                    nv--;
                    count = 2 * nv;
                }
            }
        }

        private static float Area(IList<Vector2> contour)
        {
            int n = contour.Count;
            float A = 0.0f;
            for (int p = n - 1, q = 0; q < n; p = q++)
            {
                A += contour[p].x * contour[q].y - contour[q].x * contour[p].y;
            }
            return A * 0.5f;
        }

        private static bool Snip(IList<Vector2> contour, int u, int v, int w, int n, List<int> V)
        {
            Vector2 A = contour[V[u]];
            Vector2 B = contour[V[v]];
            Vector2 C = contour[V[w]];

            if (Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x)))) return false;

            for (int p = 0; p < n; p++)
            {
                if ((p == u) || (p == v) || (p == w)) continue;
                Vector2 P = contour[V[p]];
                if (InsideTriangle(A, B, C, P)) return false;
            }
            return true;
        }

        private static bool InsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
        {
            float ax, ay, bx, by, cx, cy, apx, apy, bpx, bpy, cpx, cpy;
            float cCROSSap, bCROSScp, aCROSSbp;

            ax = C.x - B.x; ay = C.y - B.y;
            bx = A.x - C.x; by = A.y - C.y;
            cx = B.x - A.x; cy = B.y - A.y;
            apx = P.x - A.x; apy = P.y - A.y;
            bpx = P.x - B.x; bpy = P.y - B.y;
            cpx = P.x - C.x; cpy = P.y - C.y;

            aCROSSbp = ax * bpy - ay * bpx;
            cCROSSap = cx * apy - cy * apx;
            bCROSScp = bx * cpy - by * cpx;

            return ((aCROSSbp >= 0.0f) && (bCROSScp >= 0.0f) && (cCROSSap >= 0.0f));
        }
    }
}

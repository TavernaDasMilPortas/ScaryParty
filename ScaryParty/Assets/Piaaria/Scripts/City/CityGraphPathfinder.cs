using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinding on the StreetGraph.
/// Finds paths between world positions and handles dynamic edge blockages.
/// </summary>
public class CityGraphPathfinder : MonoBehaviour
{
    private StreetGraph _graph;
    private List<Vector3> _lastDebugPath;
    public int Revision { get; private set; }
    public bool IsReady => _graph != null && _graph.nodes != null && _graph.edges != null && _graph.edges.Count > 0;
    public StreetGraph Graph => _graph;
    public bool RoutesReady { get; private set; } = true;
    public void SetRoutesReady(bool ready)
    {
        if (RoutesReady == ready) return;
        RoutesReady = ready;
        Revision++;
        if (!ready) _lastDebugPath = null;
    }
    public float MaxAccessDistance { get; set; } = 20f;

    public void Initialize(StreetGraph graph)
    {
        _graph = graph;
        RoutesReady = true;
        _lastDebugPath = null;
        Revision++;
    }

    public void BlockEdge(int edgeIndex) => SetBlocked(edgeIndex, true);
    public void UnblockEdge(int edgeIndex) => SetBlocked(edgeIndex, false);

    private void SetBlocked(int index, bool blocked)
    {
        if (_graph == null || index < 0 || index >= _graph.edges.Count) return;
        var edge = _graph.edges[index];
        if (edge.isBlocked == blocked) return;
        edge.isBlocked = blocked;
        _graph.edges[index] = edge;
        Revision++;
    }

    private struct Access
    {
        public int edgeIndex;
        public Vector3 point;
        public float fraction;
        public float distance;
    }

    private bool CrossesBlockedEdge(Vector3 start, Vector3 end)
    {
        foreach (var edge in _graph.edges)
        {
            if (!edge.isBlocked) continue;
            Vector3 a = _graph.nodes[edge.nodeA].worldPosition;
            Vector3 b = _graph.nodes[edge.nodeB].worldPosition;
            float x = end.x - start.x, z = end.z - start.z;
            float u = b.x - a.x, v = b.z - a.z;
            float cross = x * v - z * u;
            if (Mathf.Abs(cross) < 0.0001f) continue;
            float dx = a.x - start.x, dz = a.z - start.z;
            float along = (dx * v - dz * u) / cross;
            float onEdge = (dx * z - dz * x) / cross;
            if (along > 0.0001f && along < 0.9999f && onEdge > 0.0001f && onEdge < 0.9999f)
                return true;
        }
        return false;
    }

    private List<Access> GetAccesses(Vector3 point)
    {
        var accesses = new List<Access>();
        if (!IsReady || !RoutesReady) return accesses;
        float limit = MaxAccessDistance * MaxAccessDistance;
        // A destination on a closed segment cannot silently snap to a different street.
        for (int i = 0; i < _graph.edges.Count; i++)
        {
            var closed = _graph.edges[i];
            if (!closed.isBlocked) continue;
            var a = _graph.nodes[closed.nodeA].worldPosition;
            var b = _graph.nodes[closed.nodeB].worldPosition;
            var delta = new Vector2(b.x - a.x, b.z - a.z);
            var offset = new Vector2(point.x - a.x, point.z - a.z);
            float t = Mathf.Clamp01(Vector2.Dot(offset, delta) / Mathf.Max(delta.sqrMagnitude, 0.0001f));
            if (t > 0.0001f && t < 0.9999f && (offset - delta * t).sqrMagnitude < 0.0625f)
                return accesses;
        }
        for (int i = 0; i < _graph.edges.Count; i++)
        {
            var edge = _graph.edges[i];
            if (edge.isBlocked) continue;
            var a = _graph.nodes[edge.nodeA].worldPosition;
            var b = _graph.nodes[edge.nodeB].worldPosition;
            var delta = b - a;
            delta.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(new Vector3(point.x - a.x, 0, point.z - a.z), delta) /
                Mathf.Max(delta.sqrMagnitude, 0.0001f));
            var projected = Vector3.Lerp(a, b, t);
            float distance = new Vector2(point.x - projected.x, point.z - projected.z).sqrMagnitude;
            if (distance > limit || CrossesBlockedEdge(point, projected)) continue;
            accesses.Add(new Access { edgeIndex = i, point = projected, fraction = t, distance = Mathf.Sqrt(distance) });
        }
        return accesses;
    }

    public bool TryFindPath(Vector3 startWorldPos, Vector3 endWorldPos, out List<Vector3> path)
    {
        path = new List<Vector3>();
        var starts = GetAccesses(startWorldPos);
        var ends = GetAccesses(endWorldPos);
        if (starts.Count == 0 || ends.Count == 0) return false;

        int count = _graph.nodes.Count;
        var distance = new float[count];
        var previous = new int[count];
        var visited = new bool[count];
        var source = new Access[count];
        for (int i = 0; i < count; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
        foreach (var access in starts)
        {
            var edge = _graph.edges[access.edgeIndex];
            float a = access.distance + edge.length * access.fraction;
            float b = access.distance + edge.length * (1f - access.fraction);
            if (a < distance[edge.nodeA]) { distance[edge.nodeA] = a; source[edge.nodeA] = access; }
            if (b < distance[edge.nodeB]) { distance[edge.nodeB] = b; source[edge.nodeB] = access; }
        }

        // Direct partial-edge travel competes with routes through intersections.
        float best = float.PositiveInfinity;
        Access bestStart = default, bestEnd = default;
        int finish = -1;
        foreach (var start in starts)
            foreach (var end in ends)
                if (start.edgeIndex == end.edgeIndex)
                {
                    float cost = start.distance + end.distance +
                        _graph.edges[start.edgeIndex].length * Mathf.Abs(start.fraction - end.fraction);
                    if (cost >= best) continue;
                    best = cost;
                    bestStart = start;
                    bestEnd = end;
                }

        // Dijkstra remains correct even when authored costs are shorter than their geometry.
        for (int step = 0; step < count; step++)
        {
            int current = -1;
            for (int i = 0; i < count; i++)
                if (!visited[i] && (current < 0 || distance[i] < distance[current])) current = i;
            if (current < 0 || float.IsInfinity(distance[current]) || distance[current] >= best) break;
            visited[current] = true;
            var node = _graph.nodes[current];
            if (node.connectedEdges == null) continue;
            foreach (int index in node.connectedEdges)
            {
                var edge = _graph.edges[index];
                if (edge.isBlocked) continue;
                int next = edge.nodeA == current ? edge.nodeB : edge.nodeA;
                float cost = distance[current] + edge.length;
                if (cost >= distance[next]) continue;
                distance[next] = cost;
                previous[next] = current;
                source[next] = source[current];
            }
        }

        foreach (var end in ends)
        {
            var edge = _graph.edges[end.edgeIndex];
            int node = edge.nodeA;
            float cost = distance[node] + edge.length * end.fraction + end.distance;
            if (distance[edge.nodeB] + edge.length * (1f - end.fraction) + end.distance < cost)
            {
                node = edge.nodeB;
                cost = distance[node] + edge.length * (1f - end.fraction) + end.distance;
            }
            if (cost >= best) continue;
            best = cost;
            finish = node;
            bestStart = source[node];
            bestEnd = end;
        }
        if (float.IsInfinity(best)) return false;

        path.Add(startWorldPos);
        path.Add(bestStart.point);
        if (finish >= 0)
        {
            var nodes = new List<int>();
            for (int node = finish; node >= 0; node = previous[node]) nodes.Add(node);
            nodes.Reverse();
            foreach (int node in nodes) path.Add(_graph.nodes[node].worldPosition);
        }
        path.Add(bestEnd.point);
        path.Add(endWorldPos);
        _lastDebugPath = path;
        return true;
    }

    public List<Vector3> FindPath(Vector3 startWorldPos, Vector3 endWorldPos)
    {
        TryFindPath(startWorldPos, endWorldPos, out var path);
        return path;
    }
    private void OnDrawGizmos()
    {
        if (_lastDebugPath != null && _lastDebugPath.Count > 1)
        {
            Gizmos.color = Color.magenta;
            for (int i = 0; i < _lastDebugPath.Count - 1; i++)
            {
                Gizmos.DrawLine(_lastDebugPath[i] + Vector3.up, _lastDebugPath[i + 1] + Vector3.up);
                Gizmos.DrawSphere(_lastDebugPath[i] + Vector3.up, 0.5f);
            }
            Gizmos.DrawSphere(_lastDebugPath[_lastDebugPath.Count - 1] + Vector3.up, 0.5f);
        }
    }
}

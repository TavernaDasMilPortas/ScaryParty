using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinding on the StreetGraph.
/// Finds paths between world positions and handles dynamic edge blockages.
/// </summary>
public class CityGraphPathfinder : MonoBehaviour
{
    private StreetGraph _graph;
    private List<Vector3> _lastDebugPath = null;

    /// <summary>
    /// Initializes the pathfinder with the generated street graph.
    /// </summary>
    public void Initialize(StreetGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Blocks a specific edge to prevent pathfinding through it.
    /// </summary>
    public void BlockEdge(int edgeIndex)
    {
        if (_graph == null || edgeIndex < 0 || edgeIndex >= _graph.edges.Count) return;
        var edge = _graph.edges[edgeIndex];
        edge.isBlocked = true;
        _graph.edges[edgeIndex] = edge;
    }

    /// <summary>
    /// Unblocks a specific edge.
    /// </summary>
    public void UnblockEdge(int edgeIndex)
    {
        if (_graph == null || edgeIndex < 0 || edgeIndex >= _graph.edges.Count) return;
        var edge = _graph.edges[edgeIndex];
        edge.isBlocked = false;
        _graph.edges[edgeIndex] = edge;
    }

    /// <summary>
    /// Finds shortest path between two world positions along the street graph.
    /// </summary>
    public List<Vector3> FindPath(Vector3 startWorldPos, Vector3 endWorldPos)
    {
        if (_graph == null || _graph.nodes.Count == 0) return new List<Vector3>();

        int startNode = GetNearestNode(startWorldPos);
        int endNode = GetNearestNode(endWorldPos);

        if (startNode == -1 || endNode == -1) return new List<Vector3>();

        List<int> pathNodes = AStar(startNode, endNode);

        List<Vector3> path = new List<Vector3>();
        if (pathNodes != null && pathNodes.Count > 0)
        {
            // 90-degree start connection
            path.Add(startWorldPos);
            Vector3 startNodePos = _graph.nodes[pathNodes[0]].worldPosition;
            Vector3 startCorner = GetManhattanCorner(startWorldPos, startNodePos);
            if (startCorner != startWorldPos && startCorner != startNodePos) path.Add(startCorner);

            foreach (int nodeId in pathNodes)
            {
                path.Add(_graph.nodes[nodeId].worldPosition);
            }

            // 90-degree end connection
            Vector3 endNodePos = _graph.nodes[pathNodes[pathNodes.Count - 1]].worldPosition;
            Vector3 endCorner = GetManhattanCorner(endNodePos, endWorldPos); // Note: moving FROM node TO endWorldPos
            if (endCorner != endNodePos && endCorner != endWorldPos) path.Add(endCorner);

            path.Add(endWorldPos);
        }

        _lastDebugPath = path;
        return path;
    }

    private Vector3 GetManhattanCorner(Vector3 fromPos, Vector3 toPos)
    {
        // To make a 90-degree corner without cutting through blocks, we must project onto the street.
        // Assuming the "node" is at a street intersection, the street lies along the node's X or Z axis.
        // We pick the corner that minimizes the initial distance to the axis.
        float dx = Mathf.Abs(fromPos.x - toPos.x);
        float dz = Mathf.Abs(fromPos.z - toPos.z);
        
        if (dx < 0.1f || dz < 0.1f) return fromPos; // Already a straight line

        // If we move along X first, we get to (toPos.x, fromPos.z). This is shorter if dx < dz.
        // If we move along Z first, we get to (fromPos.x, toPos.z).
        if (dx < dz)
        {
            return new Vector3(toPos.x, fromPos.y, fromPos.z);
        }
        else
        {
            return new Vector3(fromPos.x, fromPos.y, toPos.z);
        }
    }

    private int GetNearestNode(Vector3 pos)
    {
        int bestNode = -1;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < _graph.nodes.Count; i++)
        {
            float distSq = (pos - _graph.nodes[i].worldPosition).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestNode = i;
            }
        }

        return bestNode;
    }

    private List<int> AStar(int startNode, int endNode)
    {
        SortedSet<AStarNode> openSet = new SortedSet<AStarNode>();
        Dictionary<int, float> gScore = new Dictionary<int, float>();
        Dictionary<int, int> cameFrom = new Dictionary<int, int>();

        gScore[startNode] = 0;
        openSet.Add(new AStarNode(startNode, Heuristic(startNode, endNode)));

        while (openSet.Count > 0)
        {
            AStarNode current = openSet.Min;
            openSet.Remove(current);

            if (current.id == endNode)
            {
                return ReconstructPath(cameFrom, current.id);
            }

            StreetNode node = _graph.nodes[current.id];
            if (node.connectedEdges == null) continue;

            foreach (int edgeIdx in node.connectedEdges)
            {
                StreetEdge edge = _graph.edges[edgeIdx];
                if (edge.isBlocked) continue;

                int neighborId = (edge.nodeA == current.id) ? edge.nodeB : edge.nodeA;
                float tentativeGScore = gScore[current.id] + edge.length;

                if (!gScore.ContainsKey(neighborId) || tentativeGScore < gScore[neighborId])
                {
                    cameFrom[neighborId] = current.id;
                    gScore[neighborId] = tentativeGScore;
                    float fScore = tentativeGScore + Heuristic(neighborId, endNode);

                    openSet.RemoveWhere(n => n.id == neighborId);
                    openSet.Add(new AStarNode(neighborId, fScore));
                }
            }
        }

        return null; // No path found
    }

    private float Heuristic(int nodeA, int nodeB)
    {
        Vector3 posA = _graph.nodes[nodeA].worldPosition;
        Vector3 posB = _graph.nodes[nodeB].worldPosition;
        return Mathf.Abs(posA.x - posB.x) + Mathf.Abs(posA.z - posB.z);
    }

    private List<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
    {
        List<int> path = new List<int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private struct AStarNode : System.IComparable<AStarNode>
    {
        public int id;
        public float fScore;

        public AStarNode(int id, float fScore)
        {
            this.id = id;
            this.fScore = fScore;
        }

        public int CompareTo(AStarNode other)
        {
            int cmp = fScore.CompareTo(other.fScore);
            if (cmp == 0) return id.CompareTo(other.id);
            return cmp;
        }
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

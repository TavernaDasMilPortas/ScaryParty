#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CityGraphPathfinderTests
{
    private GameObject _object;
    private CityGraphPathfinder _pathfinder;
    private StreetGraph _graph;

    [SetUp]
    public void SetUp()
    {
        _object = new GameObject("Pathfinder test");
        _pathfinder = _object.AddComponent<CityGraphPathfinder>();
        _graph = new StreetGraph();
        AddNode(0, 0);
        AddNode(10, 0);
        AddNode(20, 0);
        AddEdge(0, 1);
        AddEdge(1, 2);
        _pathfinder.Initialize(_graph);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_object);

    private void AddNode(float x, float z)
    {
        _graph.nodes.Add(new StreetNode {
            id = _graph.nodes.Count, worldPosition = new Vector3(x, 0, z), connectedEdges = new List<int>()
        });
    }

    private void AddEdge(int a, int b)
    {
        int index = _graph.edges.Count;
        _graph.edges.Add(new StreetEdge { id = index, nodeA = a, nodeB = b,
            length = Vector3.Distance(_graph.nodes[a].worldPosition, _graph.nodes[b].worldPosition) });
        _graph.nodes[a].connectedEdges.Add(index);
        _graph.nodes[b].connectedEdges.Add(index);
    }

    [Test]
    public void MidEdgeRouteUsesPartialEdgeInsteadOfIntersection()
    {
        Assert.IsTrue(_pathfinder.TryFindPath(new Vector3(2, 0, 1), new Vector3(8, 0, 1), out var path));
        Assert.AreEqual(new Vector3(2, 0, 0), path[1]);
        Assert.AreEqual(new Vector3(8, 0, 0), path[2]);
    }

    [Test]
    public void ClosureChangesRevisionAndRejectsDisconnectedRoute()
    {
        int revision = _pathfinder.Revision;
        _pathfinder.BlockEdge(1);
        Assert.Greater(_pathfinder.Revision, revision);
        Assert.IsFalse(_pathfinder.TryFindPath(new Vector3(1, 0, 0), new Vector3(19, 0, 0), out _));
        revision = _pathfinder.Revision;
        _pathfinder.BlockEdge(1);
        Assert.AreEqual(revision, _pathfinder.Revision);
        _pathfinder.UnblockEdge(1);
        Assert.IsTrue(_pathfinder.TryFindPath(new Vector3(1, 0, 0), new Vector3(19, 0, 0), out _));
    }

    [Test]
    public void AccessCannotCrossClosedStreet()
    {
        var isolated = new StreetGraph();
        isolated.nodes.Add(new StreetNode { id = 0, worldPosition = new Vector3(0, 0, -10), connectedEdges = new List<int> { 0 } });
        isolated.nodes.Add(new StreetNode { id = 1, worldPosition = new Vector3(0, 0, 10), connectedEdges = new List<int> { 0 } });
        isolated.nodes.Add(new StreetNode { id = 2, worldPosition = new Vector3(5, 0, -10), connectedEdges = new List<int> { 1 } });
        isolated.nodes.Add(new StreetNode { id = 3, worldPosition = new Vector3(5, 0, 10), connectedEdges = new List<int> { 1 } });
        isolated.edges.Add(new StreetEdge { id = 0, nodeA = 0, nodeB = 1, length = 20, isBlocked = true });
        isolated.edges.Add(new StreetEdge { id = 1, nodeA = 2, nodeB = 3, length = 20 });
        _pathfinder.Initialize(isolated);
        _pathfinder.MaxAccessDistance = 10;
        Assert.IsFalse(_pathfinder.TryFindPath(new Vector3(-1, 0, 0), new Vector3(5, 0, 5), out _));
    }

    [Test]
    public void PendingSnapshotCannotExposePreviousRoute()
    {
        _pathfinder.SetRoutesReady(false);
        Assert.IsFalse(_pathfinder.TryFindPath(Vector3.zero, new Vector3(10, 0, 0), out _));
        _pathfinder.SetRoutesReady(true);
        Assert.IsTrue(_pathfinder.TryFindPath(Vector3.zero, new Vector3(10, 0, 0), out _));
    }
}
#endif

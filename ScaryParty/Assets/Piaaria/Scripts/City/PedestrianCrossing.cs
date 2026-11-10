using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logical pedestrian crossing entity representing a zebra crossing.
/// Connects sidewalks on opposite sides of a street and communicates with
/// IntersectionTrafficController to coordinate pedestrian crossing safety.
/// </summary>
public class PedestrianCrossing : MonoBehaviour
{
    public int crossingId;
    public Vector3 waitingPointA;
    public Vector3 waitingPointB;
    public Vector3 crossingDirection;
    public float crossingWidth = 3.5f;

    [Header("Controller")]
    public IntersectionTrafficController controller;

    private readonly HashSet<object> _activeCrossers = new HashSet<object>();

    public bool IsControlledBySignal => controller != null;

    /// <summary>
    /// Checks whether an agent is allowed to start crossing now.
    /// If traffic light is green for pedestrians or if unsignalized, returns true.
    /// </summary>
    public bool CanBeginCrossing(bool isPedestrian = true)
    {
        if (controller == null) return true; // Unsignalized crosswalk
        return controller.IsPedestrianCrossingPermitted(this);
    }

    /// <summary>
    /// Called when an NPC starts crossing the street.
    /// Once crossing has begun, the pedestrian is committed to reach the other side
    /// even if the signal changes during the crossing.
    /// </summary>
    public void NotifyCrossingStarted(object agent)
    {
        if (agent != null) _activeCrossers.Add(agent);
    }

    /// <summary>
    /// Called when an NPC finishes crossing and reaches the opposite sidewalk.
    /// </summary>
    public void NotifyCrossingCompleted(object agent)
    {
        if (agent != null) _activeCrossers.Remove(agent);
    }

    public Vector3 GetOppositeWaitingPoint(Vector3 currentPos)
    {
        float distA = Vector3.Distance(currentPos, waitingPointA);
        float distB = Vector3.Distance(currentPos, waitingPointB);
        return distA < distB ? waitingPointB : waitingPointA;
    }

    public Vector3 GetNearestWaitingPoint(Vector3 currentPos)
    {
        float distA = Vector3.Distance(currentPos, waitingPointA);
        float distB = Vector3.Distance(currentPos, waitingPointB);
        return distA < distB ? waitingPointA : waitingPointB;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = CanBeginCrossing() ? Color.green : Color.red;
        Gizmos.DrawSphere(waitingPointA, 0.35f);
        Gizmos.DrawSphere(waitingPointB, 0.35f);
        Gizmos.DrawLine(waitingPointA, waitingPointB);
    }
}

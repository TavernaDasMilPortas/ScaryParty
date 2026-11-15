using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Individual NPC traffic vehicle that follows waypoints along streets.
/// Controlled by TrafficManager.
/// </summary>
public class TrafficVehicle : MonoBehaviour
{
    public int VehicleId;
    public int CurrentStartNode = -1;
    public int CurrentEndNode = -1;
    public int CurrentEdgeId = -1;
    public List<Vector3> waypoints;
    public int currentWaypointIndex;
    public float speed;
    public bool isWaitingAtLight;
    public bool isActive;
    public Color vehicleColor;

    private float _waypointTolerance = 1.0f;
    private float _raycastDist = 8f;
    private float _currentSpeed;

    private void Start()
    {
        _currentSpeed = speed;
    }

    /// <summary>
    /// Move along path: MoveTowards limitado ao waypoint para evitar overshoot em queda de FPS.
    /// Returns true when path is complete.
    /// </summary>
    public bool UpdateMovement(float deltaTime)
    {
        if (waypoints == null || currentWaypointIndex >= waypoints.Count)
        {
            return true;
        }

        Vector3 target = waypoints[currentWaypointIndex];
        target.y = transform.position.y;
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        while (toTarget.magnitude < _waypointTolerance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                return true;
            }
            target = waypoints[currentWaypointIndex];
            target.y = transform.position.y;
            toTarget = target - transform.position;
            toTarget.y = 0f;
        }

        if (toTarget.sqrMagnitude > 0.0025f)
        {
            Vector3 dir = toTarget.normalized;
            bool stoppedByLight = CheckTrafficLight();
            isWaitingAtLight = stoppedByLight;
            bool shouldStop = stoppedByLight || CheckVehicleAhead();

            if (shouldStop)
            {
                _currentSpeed = Mathf.Max(0f, _currentSpeed - (speed * 2.5f * deltaTime));
            }
            else
            {
                _currentSpeed = Mathf.Min(speed, _currentSpeed + (speed * deltaTime));
            }

            if (_currentSpeed > 0.01f)
            {
                float maxStep = _currentSpeed * deltaTime;
                Vector3 nextPos = Vector3.MoveTowards(transform.position, target, maxStep);
                transform.position = nextPos;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), deltaTime * 6f);
            }
        }

        return false;
    }

    private bool CheckTrafficLight()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) return false;
        forward.Normalize();

        int approachGroup = Mathf.Abs(forward.z) >= Mathf.Abs(forward.x) ? 0 : 1;
        float brakingBuffer = Mathf.Max(3.5f, _currentSpeed * 0.9f);

        for (int i = 0; i < TrafficLight.ActiveLights.Count; i++)
        {
            var light = TrafficLight.ActiveLights[i];
            if (light == null || light.IsGreen) continue;

            if (CurrentEdgeId >= 0 && light.controlledEdgeIds != null && light.controlledEdgeIds.Length > 0)
            {
                if (!light.ControlsEdge(CurrentEdgeId)) continue;
            }
            else if (light.signalGroupIndex != approachGroup)
            {
                continue;
            }

            Vector3 center = light.intersectionCenter != default
                ? light.intersectionCenter
                : (light.controller != null ? light.controller.transform.position : light.transform.position);
            Vector3 toIntersection = center - transform.position;
            toIntersection.y = 0f;

            float dist = toIntersection.magnitude;
            if (dist < light.stopLineRadius * 0.65f || dist > light.stopLineRadius + brakingBuffer)
                continue;

            if (Vector3.Dot(toIntersection / Mathf.Max(0.01f, dist), forward) > 0.45f)
            {
                return light.IsRed || light.IsYellow;
            }
        }

        return false;
    }

    private bool CheckVehicleAhead()
    {
        // Short forward raycast to avoid collisions with other vehicles
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out hit, _raycastDist))
        {
            TrafficVehicle otherVehicle = hit.collider.GetComponent<TrafficVehicle>();
            if (otherVehicle != null && otherVehicle != this) return true;
        }
        return false;
    }
}

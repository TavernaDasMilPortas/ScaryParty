using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Individual NPC traffic vehicle that follows waypoints along streets.
/// Controlled by TrafficManager.
/// </summary>
public class TrafficVehicle : MonoBehaviour
{
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
    /// Move along path: interpolate towards current waypoint, advance when close.
    /// Returns true when path is complete.
    /// </summary>
    public bool UpdateMovement(float deltaTime)
    {
        if (waypoints == null || currentWaypointIndex >= waypoints.Count)
        {
            return true; // Reached end
        }

        Vector3 target = waypoints[currentWaypointIndex];
        Vector3 dir = (target - transform.position);
        dir.y = 0; // Keep on flat ground for simple movement

        if (dir.magnitude < _waypointTolerance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                return true;
            }
            target = waypoints[currentWaypointIndex];
            dir = (target - transform.position);
            dir.y = 0;
        }

        if (dir.sqrMagnitude > 0.01f)
        {
            dir.Normalize();
            
            // Check traffic light and vehicles ahead
            bool shouldStop = CheckTrafficLight() || CheckVehicleAhead();
            
            if (shouldStop)
            {
                _currentSpeed = Mathf.Max(0, _currentSpeed - (speed * 2f * deltaTime)); // Brake
            }
            else
            {
                _currentSpeed = Mathf.Min(speed, _currentSpeed + (speed * deltaTime)); // Accelerate
            }

            if (_currentSpeed > 0.01f)
            {
                transform.position += dir * _currentSpeed * deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), deltaTime * 5f);
            }
        }

        return false;
    }

    private bool CheckTrafficLight()
    {
        // Raycast forward to detect a TrafficLight component at intersections
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, _raycastDist))
        {
            TrafficLight light = hit.collider.GetComponentInParent<TrafficLight>();
            if (light != null && light.IsRed) return true;
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

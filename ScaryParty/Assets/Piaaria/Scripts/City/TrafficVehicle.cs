using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ScaryParty.Combat;

public enum TrafficStopReason
{
    None = 0,
    NoPath = 1,
    TrafficLight = 2,
    VehicleAhead = 3,
    PedestrianOrPlayerAhead = 4
}

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

    [Header("Collision")]
    public int collisionDamage = 15;
    public float minCollisionSpeed = 3.5f;

    public TrafficStopReason CurrentStopReason { get; private set; }
    public float CurrentSpeed => _currentSpeed;

    private float _waypointTolerance = 1.0f;
    private float _raycastDist = 8f;
    private float _currentSpeed;
    private readonly Dictionary<int, float> _collisionCooldowns = new Dictionary<int, float>();
    private static readonly RaycastHit[] _rayHits = new RaycastHit[16];

    private void Start()
    {
        if (speed <= 0.01f) speed = 8f;
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
            CurrentStopReason = TrafficStopReason.NoPath;
            return true;
        }

        if (speed <= 0.01f) speed = 8f;

        Vector3 target = waypoints[currentWaypointIndex];
        target.y = transform.position.y;
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        while (toTarget.magnitude < _waypointTolerance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                CurrentStopReason = TrafficStopReason.NoPath;
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

            TrafficStopReason obstacleReason = TrafficStopReason.None;
            bool blockedAhead = !stoppedByLight && CheckObstacleAhead(dir, out obstacleReason);

            if (stoppedByLight)
            {
                CurrentStopReason = TrafficStopReason.TrafficLight;
            }
            else if (blockedAhead)
            {
                CurrentStopReason = obstacleReason;
            }
            else
            {
                CurrentStopReason = TrafficStopReason.None;
            }

            if (stoppedByLight || blockedAhead)
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

    private bool CheckObstacleAhead(Vector3 forwardDir, out TrafficStopReason reason)
    {
        reason = TrafficStopReason.None;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        int hitCount = Physics.RaycastNonAlloc(origin, forwardDir, _rayHits, _raycastDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        float closestDist = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _rayHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance >= closestDist) continue;

            // A11: suportar colliders filhos de veículos
            TrafficVehicle otherVehicle = hit.collider.GetComponentInParent<TrafficVehicle>();
            if (otherVehicle != null && otherVehicle != this && otherVehicle.isActive)
            {
                // Ignorar veículo em sentido oposto fora da nossa faixa
                if (Vector3.Dot(otherVehicle.transform.forward, forwardDir) > -0.35f)
                {
                    closestDist = hit.distance;
                    reason = TrafficStopReason.VehicleAhead;
                }
                continue;
            }

            if (hit.distance <= 6.5f)
            {
                var ped = hit.collider.GetComponentInParent<PedestrianNpc>();
                var player = hit.collider.GetComponentInParent<PlayerState>();
                var moto = hit.collider.GetComponentInParent<ScaryParty.City.DeliveryMotorcycleController>();
                if (ped != null || player != null || moto != null)
                {
                    closestDist = hit.distance;
                    reason = TrafficStopReason.PedestrianOrPlayerAhead;
                }
            }
        }

        return reason != TrafficStopReason.None;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || _currentSpeed < minCollisionSpeed || other == null) return;
        if (other.transform.IsChildOf(transform)) return;

        bool isServerOrLocal = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServerOrLocal) return;

        var ped = other.GetComponentInParent<PedestrianNpc>();
        if (ped != null)
        {
            int key = ped.GetInstanceID();
            if (!_collisionCooldowns.TryGetValue(key, out float nextAllowed) || Time.time >= nextAllowed)
            {
                _collisionCooldowns[key] = Time.time + 2.0f;
                ped.ReactToHorn(transform.position);
            }
            return;
        }

        var dmgTarget = other.GetComponentInParent<IDamageable>();
        if (dmgTarget != null && dmgTarget.IsAlive)
        {
            int key = other.transform.root.GetInstanceID();
            if (!_collisionCooldowns.TryGetValue(key, out float nextAllowed) || Time.time >= nextAllowed)
            {
                _collisionCooldowns[key] = Time.time + 2.0f;
                dmgTarget.ApplyDamageServer(new DamageEvent
                {
                    Amount = Mathf.Max(1, collisionDamage),
                    Source = DamageSourceType.Collision,
                    InstigatorClientId = 0UL,
                    HitPoint = other.ClosestPoint(transform.position),
                    HitNormal = (other.transform.position - transform.position).normalized
                });
            }
        }
    }
}

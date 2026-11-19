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

    private float _waypointTolerance = 1.6f;
    private float _raycastDist = 8.5f;
    private float _currentSpeed;
    private Rigidbody _rb;
    private readonly Dictionary<int, float> _collisionCooldowns = new Dictionary<int, float>();
    private static readonly RaycastHit[] _rayHits = new RaycastHit[16];

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (speed <= 0.01f) speed = 8f;
        _currentSpeed = speed;
    }

    /// <summary>
    /// Simulação física em passo fixo (FixedUpdate) controlada exclusivamente pelo servidor.
    /// </summary>
    public bool UpdateMovement(float deltaTime)
    {
        if (waypoints == null || currentWaypointIndex >= waypoints.Count)
        {
            CurrentStopReason = TrafficStopReason.NoPath;
            ApplyZeroHorizontalVelocity();
            return true;
        }

        if (speed <= 0.01f) speed = 8f;

        Vector3 currentPos = _rb != null ? _rb.position : transform.position;
        Vector3 target = waypoints[currentWaypointIndex];
        target.y = currentPos.y;
        Vector3 toTarget = target - currentPos;
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
            target.y = currentPos.y;
            toTarget = target - currentPos;
            toTarget.y = 0f;
        }

        if (toTarget.sqrMagnitude > 0.0025f)
        {
            Vector3 dir = toTarget.normalized;
            bool severeDrift = false;
            // Recuperação se empurrado para fora do corredor da faixa
            if (currentWaypointIndex > 0 && currentWaypointIndex < waypoints.Count)
            {
                Vector3 prevWp = waypoints[currentWaypointIndex - 1];
                prevWp.y = currentPos.y;
                Vector3 seg = target - prevWp;
                seg.y = 0f;
                float segLenSq = seg.sqrMagnitude;
                if (segLenSq > 0.01f)
                {
                    float projT = Mathf.Clamp01(Vector3.Dot(currentPos - prevWp, seg) / segLenSq);
                    Vector3 onSeg = prevWp + seg * projT;
                    Vector3 drift = onSeg - currentPos;
                    drift.y = 0f;
                    severeDrift = drift.magnitude > 3.5f;
                    if (drift.magnitude > 1.8f)
                    {
                        dir = (dir + drift.normalized * 0.85f).normalized;
                    }
                }
            }
            float turnAngle = Vector3.Angle(transform.forward, dir);
            float curveSpeedFactor = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(turnAngle / 60f));
            float effectiveTargetSpeed = speed * curveSpeedFactor * (severeDrift ? 0.65f : 1f);

            bool stoppedByLight = CheckTrafficLight();
            isWaitingAtLight = stoppedByLight;

            TrafficStopReason obstacleReason = TrafficStopReason.None;
            bool blockedAhead = !stoppedByLight && CheckObstacleAhead(dir, out obstacleReason);

            if (stoppedByLight)
                CurrentStopReason = TrafficStopReason.TrafficLight;
            else if (blockedAhead)
                CurrentStopReason = obstacleReason;
            else
                CurrentStopReason = TrafficStopReason.None;

            if (stoppedByLight || blockedAhead)
                _currentSpeed = Mathf.Max(0f, _currentSpeed - (speed * 2.8f * deltaTime));
            else if (_currentSpeed > effectiveTargetSpeed)
                _currentSpeed = Mathf.Max(effectiveTargetSpeed, _currentSpeed - (speed * 2.0f * deltaTime));
            else
                _currentSpeed = Mathf.Min(effectiveTargetSpeed, _currentSpeed + (speed * 1.2f * deltaTime));

            if (_rb != null && !_rb.isKinematic)
            {
                Vector3 horizVel = dir * _currentSpeed;
                _rb.linearVelocity = new Vector3(horizVel.x, _rb.linearVelocity.y, horizVel.z);
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, deltaTime * 6f));
            }
            else if (_currentSpeed > 0.01f)
            {
                float maxStep = _currentSpeed * deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, target, maxStep);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), deltaTime * 6f);
            }
        }

        return false;
    }

    private void ApplyZeroHorizontalVelocity()
    {
        if (_rb != null && !_rb.isKinematic)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
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
        Vector3 origin = transform.position + Vector3.up * 0.65f + forwardDir * 2.1f;
        Vector3 halfExtents = new Vector3(TrafficManager.VehicleDimensions.x * 0.46f, 0.42f, 0.35f);
        Quaternion boxRot = Quaternion.LookRotation(forwardDir, Vector3.up);
        int hitCount = Physics.BoxCastNonAlloc(origin, halfExtents, forwardDir, _rayHits, boxRot, _raycastDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

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
        if (!isActive || other == null) return;
        if (other.transform.IsChildOf(transform)) return;

        bool isServerOrLocal = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServerOrLocal) return;

        var ped = other.GetComponentInParent<PedestrianNpc>();
        if (ped != null)
        {
            ped.ReactToHorn(transform.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive || collision == null || collision.collider == null) return;
        if (collision.collider.transform.IsChildOf(transform)) return;

        bool isServerOrLocal = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServerOrLocal) return;

        float impactSpeed = Mathf.Max(_currentSpeed, collision.relativeVelocity.magnitude);
        if (impactSpeed < minCollisionSpeed) return;

        var victimRoot = collision.collider.transform.root;
        int episodeKey = victimRoot.GetInstanceID();
        if (_collisionCooldowns.TryGetValue(episodeKey, out float nextAllowed) && Time.time < nextAllowed)
            return;

        _collisionCooldowns[episodeKey] = Time.time + 1.6f;

        Vector3 pushDir = Vector3.ProjectOnPlane(victimRoot.position - transform.position, Vector3.up);
        if (pushDir.sqrMagnitude < 0.01f) pushDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        pushDir.Normalize();

        Vector3 vehVel = _rb != null ? Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up) : transform.forward * _currentSpeed;
        if (vehVel.sqrMagnitude > 0.25f)
        {
            pushDir = (pushDir * 0.65f + vehVel.normalized * 0.35f).normalized;
        }

        float knockbackSpeed = Mathf.Clamp(impactSpeed * 1.25f, 5.5f, 14f);
        Vector3 knockbackVel = pushDir * knockbackSpeed;

        var ps = collision.collider.GetComponentInParent<PlayerState>();
        if (ps != null)
        {
            ps.ApplyVehicleKnockbackServer(knockbackVel, 0.36f);
        }
        else
        {
            var ped = collision.collider.GetComponentInParent<PedestrianNpc>();
            if (ped != null)
            {
                ped.ApplyExternalKnockback(knockbackVel, 0.38f);
            }
        }

        var dmgTarget = collision.collider.GetComponentInParent<IDamageable>();
        if (dmgTarget != null && dmgTarget.IsAlive)
        {
            int scaledDamage = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(8f, Mathf.Max(12, collisionDamage * 1.5f), Mathf.InverseLerp(minCollisionSpeed, 14f, impactSpeed))), 5, 35);
            Vector3 hitPt = collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.ClosestPoint(transform.position);
            dmgTarget.ApplyDamageServer(new DamageEvent
            {
                Amount = scaledDamage,
                Source = DamageSourceType.Collision,
                InstigatorClientId = 0UL,
                HitPoint = hitPt,
                HitNormal = pushDir
            });

            if (ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance != null &&
                ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance.IsServer)
            {
                ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance.BroadcastTransversalFeelClientRpc(
                    (ushort)ScaryParty.Pizzeria.Presentation.Feedback.GameFeelEventId.VehicleImpact,
                    hitPt,
                    (ulong)VehicleId,
                    Mathf.Clamp01(impactSpeed / 12f),
                    (byte)ScaryParty.Pizzeria.Presentation.Feedback.GameFeelMaterialFamily.RigidMetal);
            }
            else
            {
                ScaryParty.Pizzeria.Presentation.Feedback.GameFeelPresenter.Instance?.TriggerAtPosition(
                    ScaryParty.Pizzeria.Presentation.Feedback.GameFeelEventId.VehicleImpact,
                    hitPt,
                    transform,
                    (ulong)VehicleId,
                    0,
                    0,
                    ScaryParty.Pizzeria.Presentation.Feedback.GameFeelMaterialFamily.RigidMetal,
                    Mathf.Clamp01(impactSpeed / 12f),
                    isLocal: false);
            }
        }
    }
}

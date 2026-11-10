using System;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Navigation.Agents
{
    public enum NavigationMotorState
    {
        Idle,
        Moving,
        WaitingAtSignal,
        Arrived,
        Stuck,
        Failed
    }

    /// <summary>
    /// Unified navigation motor for all NPCs (pedestrians, customers, monsters).
    /// Decouples pathfinding and locomotion control from character logic.
    /// Includes throttling, stuck detection, and safe NavMesh recovery.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcNavigationMotor : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _arrivalDistance = 0.35f;
        [SerializeField] private float _pathUpdateInterval = 0.25f;
        [SerializeField] private float _stuckTimeout = 3.0f;

        [Header("State")]
        [SerializeField] private NavigationMotorState _state = NavigationMotorState.Idle;
        public NavigationMotorState State => _state;

        public Vector3 Destination { get; private set; }
        public Transform TargetTransform { get; private set; }
        public float CurrentSpeed => Agent != null && Agent.enabled && Agent.isOnNavMesh ? Agent.velocity.magnitude : 0f;

        public event Action OnDestinationReached;
        public event Action OnNavigationFailed;
        public event Action OnStuckDetected;

        private float _lastPathUpdateTime;
        private float _stuckTimer;
        private Vector3 _lastSampledPos;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            _lastSampledPos = transform.position;
        }

        public void Configure(float speed, float angularSpeed = 360f, float acceleration = 12f, float stoppingDist = 0.25f)
        {
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            if (Agent != null)
            {
                Agent.speed = speed;
                Agent.angularSpeed = angularSpeed;
                Agent.acceleration = acceleration;
                Agent.stoppingDistance = stoppingDist;
                _arrivalDistance = Mathf.Max(stoppingDist, 0.3f);
            }
        }

        public bool MoveTo(Vector3 destination)
        {
            TargetTransform = null;
            Destination = destination;

            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            if (!Agent.enabled) Agent.enabled = true;

            if (!Agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.5f, Agent.areaMask))
                {
                    Agent.Warp(hit.position);
                }
                else
                {
                    _state = NavigationMotorState.Failed;
                    OnNavigationFailed?.Invoke();
                    return false;
                }
            }

            if (NavMesh.SamplePosition(destination, out NavMeshHit destHit, 3.0f, Agent.areaMask))
            {
                Destination = destHit.position;
                Agent.isStopped = false;
                bool success = Agent.SetDestination(Destination);
                if (success)
                {
                    _state = NavigationMotorState.Moving;
                    _stuckTimer = 0f;
                    _lastPathUpdateTime = Time.time;
                    return true;
                }
            }

            _state = NavigationMotorState.Failed;
            OnNavigationFailed?.Invoke();
            return false;
        }

        public bool FollowTarget(Transform target)
        {
            if (target == null) return false;
            TargetTransform = target;
            return MoveTo(target.position);
        }

        public void Stop()
        {
            TargetTransform = null;
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.isStopped = true;
                Agent.ResetPath();
            }
            _state = NavigationMotorState.Idle;
        }

        public bool HasReachedDestination()
        {
            if (_state == NavigationMotorState.Arrived) return true;
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return false;

            if (!Agent.pathPending)
            {
                if (Agent.remainingDistance <= _arrivalDistance)
                {
                    return true;
                }
            }

            return false;
        }

        public NavMeshPathStatus GetPathStatus()
        {
            if (Agent == null || !Agent.enabled || !Agent.hasPath) return NavMeshPathStatus.PathInvalid;
            return Agent.pathStatus;
        }

        private void Update()
        {
            if (_state != NavigationMotorState.Moving) return;

            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return;

            // Follow dynamic target update
            if (TargetTransform != null && Time.time - _lastPathUpdateTime > _pathUpdateInterval)
            {
                _lastPathUpdateTime = Time.time;
                if (Vector3.Distance(Destination, TargetTransform.position) > 0.5f)
                {
                    Destination = TargetTransform.position;
                    Agent.SetDestination(Destination);
                }
            }

            // Arrival check
            if (!Agent.pathPending && Agent.remainingDistance <= _arrivalDistance)
            {
                _state = NavigationMotorState.Arrived;
                Agent.isStopped = true;
                OnDestinationReached?.Invoke();
                return;
            }

            // Stuck detection: agent is supposed to be moving, but hasn't covered distance
            float movedDist = Vector3.Distance(transform.position, _lastSampledPos);
            _lastSampledPos = transform.position;

            if (movedDist < 0.05f * Time.deltaTime && Agent.remainingDistance > _arrivalDistance)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= _stuckTimeout)
                {
                    _state = NavigationMotorState.Stuck;
                    _stuckTimer = 0f;
                    OnStuckDetected?.Invoke();
                    RecoverFromStuck();
                }
            }
            else
            {
                _stuckTimer = Mathf.Max(0f, _stuckTimer - Time.deltaTime * 0.5f);
            }
        }

        public void RecoverFromStuck()
        {
            if (Agent == null || !Agent.enabled) return;

            // Sample nearest valid NavMesh location
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, Agent.areaMask))
            {
                Agent.Warp(hit.position);
                if (Destination != Vector3.zero)
                {
                    MoveTo(Destination);
                }
            }
            else
            {
                Stop();
                OnNavigationFailed?.Invoke();
            }
        }
    }
}

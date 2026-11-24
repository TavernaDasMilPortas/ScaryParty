using System;
using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Navigation.Agents;
using ScaryParty.Navigation.Urban;
using ScaryParty.Player;

public enum PedestrianState
{
    InsideBuilding,
    ExitingBuilding,
    Travelling,
    WaitingAtCrosswalk,
    CrossingStreet,
    EnteringBuilding,
    VisitingPizzeria
}

/// <summary>
/// Individual pedestrian NPC living in the procedural city.
/// Operates on a state machine: exits buildings, travels along sidewalks, waits at traffic lights,
/// crosses zebra crossings, visits other buildings, or transitions to visiting the pizzeria.
/// </summary>
[RequireComponent(typeof(NpcNavigationMotor))]
[RequireComponent(typeof(NpcLocomotionAdapter))]
public class PedestrianNpc : MonoBehaviour
{
    public string PedestrianId { get; private set; }
    public PedestrianState State { get; private set; } = PedestrianState.Travelling;

    public NpcNavigationMotor Motor { get; private set; }
    public NpcLocomotionAdapter Locomotion { get; private set; }
    public CharacterVisualController VisualController { get; private set; }
    public CharacterAppearance Appearance { get; private set; }

    [Header("Current Goal")]
    public CityBuilding TargetBuilding;
    public PedestrianCrossing CurrentCrossing;
    public Vector3 TargetPosition;

    [Header("Timers")]
    private float _stateTimer;
    private float _dwellDuration = 8f;
    private float _walkSpeed = 2.2f;
    private int _navFailRetries;
    private float _hornYieldTimer;
    private bool _isYieldingToHorn;
    private PedestrianState _preCrosswalkState = PedestrianState.Travelling;
    private float _crosswalkCheckTimer;
    private PedestrianCrossing _lastCompletedCrossing;
    private static PedestrianCrossing[] _cachedCrossings;
    private static float _cachedCrossingsTime = -10f;

    public void ReactToHorn(Vector3 hornOrigin)
    {
        if (!gameObject.activeInHierarchy || Motor == null) return;
        if (State == PedestrianState.InsideBuilding || State == PedestrianState.EnteringBuilding) return;
        if (_hornYieldTimer > 0f) return;
        _hornYieldTimer = 2.5f;
        _isYieldingToHorn = true;

        Vector3 away = transform.position - hornOrigin;
        away.y = 0f;
        if (away.sqrMagnitude < 0.05f) away = transform.right;
        away.Normalize();

        Vector3 sideStep = transform.position + away * 2.2f;
        if (NavMesh.SamplePosition(sideStep, out var navHit, 2.5f, NavMesh.AllAreas))
            sideStep = navHit.position;
        Motor.MoveTo(sideStep);
    }

    private Coroutine _knockbackCoroutine;

    public void ApplyExternalKnockback(Vector3 horizontalVelocity, float duration = 0.35f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_knockbackCoroutine != null) StopCoroutine(_knockbackCoroutine);
        _knockbackCoroutine = StartCoroutine(RoutineExternalKnockback(horizontalVelocity, Mathf.Clamp(duration, 0.1f, 0.7f)));
    }

    private System.Collections.IEnumerator RoutineExternalKnockback(Vector3 initialVelocity, float duration)
    {
        var agent = GetComponent<NavMeshAgent>();
        bool hadAgent = agent != null && agent.enabled;
        if (hadAgent)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
        }

        Vector3 vel = new Vector3(initialVelocity.x, 0f, initialVelocity.z);
        if (vel.sqrMagnitude > 196f) vel = vel.normalized * 14f;

        float elapsed = 0f;
        while (elapsed < duration && vel.sqrMagnitude > 0.05f)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float decay = 1f - Mathf.Clamp01(elapsed / duration);
            Vector3 delta = vel * (decay * dt);
            float dist = delta.magnitude;
            if (dist > 0.001f)
            {
                Vector3 origin = transform.position + Vector3.up * 0.6f;
                if (Physics.SphereCast(origin, 0.35f, delta / dist, out RaycastHit hit, dist + 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    float safeDist = Mathf.Max(0f, hit.distance - 0.15f);
                    transform.position += (delta / dist) * safeDist;
                    break;
                }
                transform.position += delta;
            }
            yield return null;
        }

        if (hadAgent && agent != null && agent.enabled)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 3.5f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
            }
            agent.updatePosition = true;
            agent.isStopped = false;
        }
        _knockbackCoroutine = null;
    }

    public int ColorIndex { get; private set; }

    public event Action<PedestrianNpc> OnReachedPizzeria;
    public event Action<PedestrianNpc, CityBuilding> OnEnteredBuilding;
    public event Action<PedestrianNpc> OnNavigationAbandoned;

    private static readonly Color[] PedestrianPalette = new[]
    {
        new Color(0.25f, 0.58f, 0.85f),
        new Color(0.85f, 0.32f, 0.28f),
        new Color(0.32f, 0.75f, 0.42f),
        new Color(0.88f, 0.68f, 0.22f),
        new Color(0.68f, 0.38f, 0.82f),
        new Color(0.22f, 0.75f, 0.72f),
        new Color(0.90f, 0.48f, 0.25f),
        new Color(0.75f, 0.35f, 0.58f)
    };

    private void Awake()
    {
        Motor = GetComponent<NpcNavigationMotor>();
        Locomotion = GetComponent<NpcLocomotionAdapter>();
    }

    public void Initialize(string id, GameObject visualInstance, float speed = 2.2f, int colorIndex = 0)
    {
        PedestrianId = id;
        _walkSpeed = speed;
        ColorIndex = colorIndex;
        _navFailRetries = 0;

        if (Motor == null) Motor = GetComponent<NpcNavigationMotor>() ?? gameObject.AddComponent<NpcNavigationMotor>();
        if (Locomotion == null) Locomotion = GetComponent<NpcLocomotionAdapter>() ?? gameObject.AddComponent<NpcLocomotionAdapter>();

        if (visualInstance != null)
        {
            visualInstance.transform.SetParent(transform, false);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;

            VisualController = visualInstance.GetComponent<CharacterVisualController>()
                            ?? visualInstance.AddComponent<CharacterVisualController>();
            Appearance = visualInstance.GetComponent<CharacterAppearance>()
                      ?? visualInstance.AddComponent<CharacterAppearance>();

            VisualController.ResolveReferences();
            Locomotion.BindVisual(VisualController);
            VisualController.SetPreparing(false);
            VisualController.SetSitting(false, false);
            VisualController.SetLocomotion(0f, 1f, true);

            if (Appearance != null)
            {
                Color c = PedestrianPalette[Mathf.Abs(colorIndex) % PedestrianPalette.Length];
                Appearance.ApplyPrimaryColor(c);
            }
        }

        if (Motor != null)
        {
            Motor.OnDestinationReached -= HandleDestinationReached;
            Motor.OnNavigationFailed -= HandleNavigationFailed;
            Motor.Configure(_walkSpeed, 360f, 12f, 0.35f);
            Motor.OnDestinationReached += HandleDestinationReached;
            Motor.OnNavigationFailed += HandleNavigationFailed;
        }
    }

    public void ReconfigureFromPool(string id, float speed, int colorIndex)
    {
        PedestrianId = id;
        _walkSpeed = speed;
        ColorIndex = colorIndex;
        ResetStateForPool();
        Motor?.Configure(_walkSpeed, 360f, 12f, 0.35f);
        if (Appearance != null)
        {
            Color c = PedestrianPalette[Mathf.Abs(colorIndex) % PedestrianPalette.Length];
            Appearance.ApplyPrimaryColor(c);
        }
    }

    public void ResetStateForPool()
    {
        if (CurrentCrossing != null)
        {
            CurrentCrossing.NotifyCrossingCompleted(this);
            CurrentCrossing = null;
        }
        TargetBuilding = null;
        TargetPosition = Vector3.zero;
        _navFailRetries = 0;
        _hornYieldTimer = 0f;
        _isYieldingToHorn = false;
        _stateTimer = 0f;
        _crosswalkCheckTimer = 0f;
        _lastCompletedCrossing = null;
        _preCrosswalkState = PedestrianState.Travelling;
        State = PedestrianState.InsideBuilding;
    }

    private void OnDisable()
    {
        if (CurrentCrossing != null)
        {
            CurrentCrossing.NotifyCrossingCompleted(this);
            CurrentCrossing = null;
        }
        _isYieldingToHorn = false;
        _hornYieldTimer = 0f;
    }

    private void OnDestroy()
    {
        if (Motor != null)
        {
            Motor.OnDestinationReached -= HandleDestinationReached;
            Motor.OnNavigationFailed -= HandleNavigationFailed;
        }
    }

    public void SpawnAt(Vector3 position, Quaternion rotation)
    {
        if (NavMesh.SamplePosition(position, out var navHit, 4.0f, NavMesh.AllAreas))
            position = navHit.position;
        transform.position = position;
        transform.rotation = rotation;
        if (Motor != null && Motor.Agent != null)
        {
            Motor.Agent.Warp(position);
        }
        gameObject.SetActive(true);
    }

    private bool TryResolveReachableNavMeshPoint(Vector3 desired, out Vector3 safeDest)
    {
        safeDest = desired;
        if (!NavMesh.SamplePosition(desired, out var hit, 4.0f, NavMesh.AllAreas))
            return false;

        if (Motor != null && Motor.Agent != null && Motor.Agent.isOnNavMesh)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }
        }
        safeDest = hit.position;
        return true;
    }

    public void AssignTravelToBuilding(CityBuilding target, float dwellTime = 8f)
    {
        if (target == null) return;
        TargetBuilding = target;
        _dwellDuration = dwellTime;
        _preCrosswalkState = PedestrianState.Travelling;
        _lastCompletedCrossing = null;
        State = PedestrianState.Travelling;
        if (TryResolveReachableNavMeshPoint(target.entrancePosition, out var safeDest))
        {
            TargetPosition = safeDest;
            Motor.MoveTo(TargetPosition);
        }
        else
        {
            HandleNavigationFailed();
        }
    }

    public void AssignVisitPizzeria(Vector3 pizzeriaEntrance)
    {
        TargetBuilding = null;
        _preCrosswalkState = PedestrianState.VisitingPizzeria;
        _lastCompletedCrossing = null;
        State = PedestrianState.VisitingPizzeria;
        if (TryResolveReachableNavMeshPoint(pizzeriaEntrance, out var safeDest))
        {
            TargetPosition = safeDest;
            Motor.MoveTo(TargetPosition);
        }
        else
        {
            HandleNavigationFailed();
        }
    }

    private void ResumeAfterHornYield()
    {
        _isYieldingToHorn = false;
        _hornYieldTimer = 0f;
        if (Motor == null) return;
        if (State == PedestrianState.CrossingStreet && CurrentCrossing != null)
        {
            Motor.MoveTo(CurrentCrossing.GetOppositeWaitingPoint(transform.position));
        }
        else if (TargetPosition != Vector3.zero)
        {
            Motor.MoveTo(TargetPosition);
        }
    }

    private static PedestrianCrossing[] GetSceneCrossingsCached()
    {
        if (_cachedCrossings == null || Time.unscaledTime - _cachedCrossingsTime > 5f)
        {
            _cachedCrossings = FindObjectsByType<PedestrianCrossing>(FindObjectsSortMode.None);
            _cachedCrossingsTime = Time.unscaledTime;
        }
        return _cachedCrossings;
    }

    private void Update()
    {
        if (_hornYieldTimer > 0f)
        {
            _hornYieldTimer -= Time.deltaTime;
            if (_hornYieldTimer <= 0f && _isYieldingToHorn)
            {
                ResumeAfterHornYield();
            }
        }

        if (State == PedestrianState.WaitingAtCrosswalk)
        {
            _stateTimer += Time.deltaTime;
            if (CurrentCrossing == null || CurrentCrossing.CanBeginCrossing(true) || _stateTimer > 12f)
            {
                // Signal turned green (or safety timeout) -> begin crossing!
                State = PedestrianState.CrossingStreet;
                if (CurrentCrossing != null)
                {
                    CurrentCrossing.NotifyCrossingStarted(this);
                    Vector3 oppositeSide = CurrentCrossing.GetOppositeWaitingPoint(transform.position);
                    Motor.MoveTo(oppositeSide);
                }
                else if (TargetPosition != Vector3.zero)
                {
                    State = _preCrosswalkState;
                    Motor.MoveTo(TargetPosition);
                }
            }
            return;
        }

        if (!_isYieldingToHorn && CurrentCrossing == null && (State == PedestrianState.Travelling || State == PedestrianState.VisitingPizzeria) && TargetPosition != Vector3.zero)
        {
            _crosswalkCheckTimer -= Time.deltaTime;
            if (_crosswalkCheckTimer <= 0f)
            {
                _crosswalkCheckTimer = 0.25f;
                var crossings = GetSceneCrossingsCached();
                var nearCrossing = PedestrianRoutePlanner.FindNearestCrosswalk(transform.position, crossings, 3.2f);
                if (nearCrossing != null && nearCrossing != _lastCompletedCrossing && nearCrossing.IsControlledBySignal && !nearCrossing.CanBeginCrossing(true))
                {
                    Vector3 waitNear = nearCrossing.GetNearestWaitingPoint(transform.position);
                    Vector3 waitOpp = nearCrossing.GetOppositeWaitingPoint(transform.position);
                    Vector3 crossDir = waitOpp - waitNear;
                    Vector3 toGoal = TargetPosition - transform.position;
                    crossDir.y = 0f;
                    toGoal.y = 0f;
                    if (crossDir.sqrMagnitude > 0.2f && toGoal.sqrMagnitude > 16f &&
                        Vector3.Dot(crossDir.normalized, toGoal.normalized) > 0.3f &&
                        (waitNear - transform.position).sqrMagnitude <= 3.0f * 3.0f)
                    {
                        _preCrosswalkState = State;
                        CurrentCrossing = nearCrossing;
                        State = PedestrianState.WaitingAtCrosswalk;
                        _stateTimer = 0f;
                        Motor?.Stop();
                    }
                }
            }
        }
    }

    private void HandleDestinationReached()
    {
        _navFailRetries = 0;
        if (_isYieldingToHorn)
        {
            ResumeAfterHornYield();
            return;
        }

        switch (State)
        {
            case PedestrianState.Travelling:
                if (TargetBuilding != null && TargetBuilding.portal != null)
                {
                    State = PedestrianState.EnteringBuilding;
                    TargetBuilding.portal.RegisterOccupant(PedestrianId);
                    OnEnteredBuilding?.Invoke(this, TargetBuilding);
                }
                break;

            case PedestrianState.CrossingStreet:
                if (CurrentCrossing != null)
                {
                    _lastCompletedCrossing = CurrentCrossing;
                    CurrentCrossing.NotifyCrossingCompleted(this);
                    CurrentCrossing = null;
                }
                // Resume travelling to target building or pizzeria
                if (TargetPosition != Vector3.zero)
                {
                    State = _preCrosswalkState == PedestrianState.VisitingPizzeria ? PedestrianState.VisitingPizzeria : PedestrianState.Travelling;
                    Motor.MoveTo(TargetPosition);
                }
                break;

            case PedestrianState.VisitingPizzeria:
                OnReachedPizzeria?.Invoke(this);
                break;
        }
    }

    private void HandleNavigationFailed()
    {
        if (_isYieldingToHorn)
        {
            ResumeAfterHornYield();
            return;
        }

        if (CurrentCrossing != null)
        {
            CurrentCrossing.NotifyCrossingCompleted(this);
            CurrentCrossing = null;
        }

        _navFailRetries++;
        if (_navFailRetries > 3)
        {
            AbandonAndReturnToPool();
            return;
        }

        // If path failed, attempt to find a nearby reachable building or recovery
        var nearest = CityBuildingRegistry.Instance?.GetRandomBuilding(null, pedestrianEligibleOnly: true);
        if (nearest != null)
        {
            AssignTravelToBuilding(nearest, 5f);
        }
        else
        {
            AbandonAndReturnToPool();
        }
    }

    private void AbandonAndReturnToPool()
    {
        _navFailRetries = 0;
        TargetBuilding = null;
        TargetPosition = Vector3.zero;
        if (OnNavigationAbandoned != null)
        {
            OnNavigationAbandoned.Invoke(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
